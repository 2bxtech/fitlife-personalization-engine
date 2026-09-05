using System.Text.Json;
using Confluent.Kafka;
using FitLife.Api.BackgroundServices;
using FitLife.Core.Interfaces;
using FitLife.Core.Models;
using FitLife.Infrastructure.Data;
using FitLife.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FitLife.Tests;

public class EventConsumerServiceTests
{
    [Fact]
    public async Task MalformedEvent_IsDeadLetteredWithoutRetrying()
    {
        var repository = new Mock<IInteractionRepository>();
        var deadLetterPublisher = new RecordingDeadLetterPublisher();
        await using var provider = BuildProvider(
            repository.Object,
            deadLetterPublisher);
        var service = CreateService(provider);
        var consumeResult = ConsumeResultFor("{not-json");

        await service.ProcessWithRetryAsync(
            consumeResult,
            CancellationToken.None);

        deadLetterPublisher.Published.Should().ContainSingle();
        deadLetterPublisher.Published[0].Disposition.Should().Be("malformed-json");
        deadLetterPublisher.Published[0].Attempts.Should().Be(1);
        deadLetterPublisher.Published[0].SourceOffset.Should().Be(42);
        repository.Verify(
            item => item.SaveChangesAsync(),
            Times.Never);
    }

    [Fact]
    public async Task UnsupportedSchema_IsDeadLetteredWithoutPersistence()
    {
        var repository = new Mock<IInteractionRepository>();
        var deadLetterPublisher = new RecordingDeadLetterPublisher();
        await using var provider = BuildProvider(
            repository.Object,
            deadLetterPublisher);
        var service = CreateService(provider);
        var userEvent = ValidEvent();
        userEvent.SchemaVersion = 2;

        await service.ProcessWithRetryAsync(
            ConsumeResultFor(JsonSerializer.Serialize(userEvent)),
            CancellationToken.None);

        deadLetterPublisher.Published.Should().ContainSingle();
        deadLetterPublisher.Published[0].Disposition
            .Should().Be("unsupported-schema");
        deadLetterPublisher.Published[0].EventId.Should().Be(userEvent.EventId);
        repository.Verify(
            item => item.SaveChangesAsync(),
            Times.Never);
    }

    [Fact]
    public async Task TransientFailure_IsRetriedThreeTimesThenDeadLettered()
    {
        var repository = new Mock<IInteractionRepository>();
        repository
            .Setup(item => item.ExistsByEventIdAsync(It.IsAny<string>()))
            .ReturnsAsync(false);
        repository
            .Setup(item => item.AddAsync(It.IsAny<Interaction>()))
            .ReturnsAsync((Interaction interaction) => interaction);
        repository
            .Setup(item => item.SaveChangesAsync())
            .ThrowsAsync(new InvalidOperationException("transient database failure"));
        var deadLetterPublisher = new RecordingDeadLetterPublisher();
        await using var provider = BuildProvider(
            repository.Object,
            deadLetterPublisher);
        var service = CreateService(provider);

        await service.ProcessWithRetryAsync(
            ConsumeResultFor(JsonSerializer.Serialize(ValidEvent())),
            CancellationToken.None);

        repository.Verify(
            item => item.SaveChangesAsync(),
            Times.Exactly(3));
        deadLetterPublisher.Published.Should().ContainSingle();
        deadLetterPublisher.Published[0].Disposition
            .Should().Be("retries-exhausted");
        deadLetterPublisher.Published[0].Attempts.Should().Be(3);
    }

    [Fact]
    public async Task DeadLetterPublishFailure_IsRethrownSoOffsetIsNotCommitted()
    {
        var repository = new Mock<IInteractionRepository>();
        await using var provider = BuildProvider(
            repository.Object,
            new FailingDeadLetterPublisher());
        var service = CreateService(provider);

        var act = () => service.ProcessWithRetryAsync(
            ConsumeResultFor("{not-json"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("dead-letter unavailable");
    }

    [Fact]
    public async Task CacheFailure_RetriesInvalidationWithoutDuplicatingInteraction()
    {
        var repository = new Mock<IInteractionRepository>();
        repository
            .SetupSequence(item => item.ExistsByEventIdAsync(It.IsAny<string>()))
            .ReturnsAsync(false)
            .ReturnsAsync(true);
        repository
            .Setup(item => item.AddAsync(It.IsAny<Interaction>()))
            .ReturnsAsync((Interaction interaction) => interaction);
        repository
            .Setup(item => item.SaveChangesAsync())
            .ReturnsAsync(1);
        var recommendations = new Mock<IRecommendationService>();
        recommendations
            .SetupSequence(service =>
                service.InvalidateCacheAsync("user-1"))
            .ThrowsAsync(new InvalidOperationException("cache unavailable"))
            .Returns(Task.CompletedTask);
        var deadLetterPublisher = new RecordingDeadLetterPublisher();
        await using var provider = BuildProvider(
            repository.Object,
            deadLetterPublisher,
            recommendations.Object);
        var service = CreateService(provider);
        var userEvent = ValidEvent();
        userEvent.EventType = EventTypes.Book;

        await service.ProcessWithRetryAsync(
            ConsumeResultFor(JsonSerializer.Serialize(userEvent)),
            CancellationToken.None);

        repository.Verify(
            item => item.SaveChangesAsync(),
            Times.Once);
        recommendations.Verify(
            item => item.InvalidateCacheAsync("user-1"),
            Times.Exactly(2));
        deadLetterPublisher.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task WorkerLoop_DeadLetterFailureStopsBeforePollingOrCommittingLaterRecords()
    {
        await using var provider = BuildProvider(new Mock<IInteractionRepository>().Object,
            new FailingDeadLetterPublisher());
        var consumer = new Mock<IConsumer<string, string>>();
        consumer.Setup(c => c.Consume(It.IsAny<TimeSpan>())).Returns(ConsumeResultFor("{not-json"));
        using var service = CreateLoopService(provider, consumer.Object);
        await service.StartAsync(CancellationToken.None);
        var execute = () => service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));
        await execute.Should().ThrowAsync<InvalidOperationException>().WithMessage("dead-letter unavailable");
        consumer.Verify(c => c.Consume(It.IsAny<TimeSpan>()), Times.Once);
        consumer.Verify(c => c.Commit(It.IsAny<ConsumeResult<string, string>>()), Times.Never);
        consumer.Verify(c => c.Close(), Times.Once);
        consumer.Verify(c => c.Dispose(), Times.Once);
    }

    [Fact]
    public async Task WorkerLoop_StopWaitsForInFlightProcessingThenClosesConsumer()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new Mock<IInteractionRepository>();
        repository.Setup(r => r.ExistsByEventIdAsync(It.IsAny<string>()))
            .Returns(async () => { entered.SetResult(); await release.Task; return true; });
        await using var provider = BuildProvider(repository.Object, new RecordingDeadLetterPublisher());
        var consumer = new Mock<IConsumer<string, string>>();
        consumer.Setup(c => c.Consume(It.IsAny<TimeSpan>()))
            .Returns(ConsumeResultFor(JsonSerializer.Serialize(ValidEvent())));
        using var service = CreateLoopService(provider, consumer.Object);
        await service.StartAsync(CancellationToken.None);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var stop = service.StopAsync(CancellationToken.None);
        consumer.Verify(c => c.Close(), Times.Never);
        release.SetResult();
        await stop.WaitAsync(TimeSpan.FromSeconds(10));
        consumer.Verify(c => c.Commit(It.IsAny<ConsumeResult<string, string>>()), Times.Once);
        consumer.Verify(c => c.Consume(It.IsAny<TimeSpan>()), Times.Once);
        consumer.Verify(c => c.Close(), Times.Once);
        consumer.Verify(c => c.Dispose(), Times.Once);
        service.ExecuteTask!.IsCompletedSuccessfully.Should().BeTrue();
    }

    private static EventConsumerService CreateLoopService(IServiceProvider provider,
        IConsumer<string, string> consumer) => new(
            NullLogger<EventConsumerService>.Instance,
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kafka:BootstrapServers"] = "unused:9092",
                ["BackgroundWorkers:EventConsumer:RetryDelayMilliseconds"] = "0"
            }).Build(), provider, consumer);

    [SqlServerFact]
    public async Task ConcurrentDuplicateDelivery_DoesNotThrowAndStoresExactlyOneInteraction()
    {
        var baseConnectionString =
            Environment.GetEnvironmentVariable("FITLIFE_SQLSERVER_TEST_CONNECTION")!;
        var databaseName = $"FitLifeEventDedupTests_{Guid.NewGuid():N}";
        var connectionString = new SqlConnectionStringBuilder(baseConnectionString)
        {
            InitialCatalog = databaseName
        }.ConnectionString;
        var options = new DbContextOptionsBuilder<FitLifeDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        try
        {
            await using (var setup = new FitLifeDbContext(options))
            {
                await setup.Database.EnsureCreatedAsync();
                setup.Users.Add(new User
                {
                    Id = "user-1",
                    Email = "dedup@example.com",
                    PasswordHash = "not-used"
                });
                await setup.SaveChangesAsync();
            }

            var eventId = Guid.NewGuid().ToString();
            var userEvent = new UserEvent
            {
                EventId = eventId,
                UserId = "user-1",
                ItemId = "class-1",
                ItemType = "Class",
                EventType = "View",
                OccurredAt = DateTime.UtcNow,
                Timestamp = DateTime.UtcNow
            };
            var message = new Message<string, string>
            {
                Key = userEvent.UserId,
                Value = JsonSerializer.Serialize(userEvent)
            };

            var gate = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var first = ProcessAfterGateAsync(connectionString, message, gate.Task);
            var second = ProcessAfterGateAsync(connectionString, message, gate.Task);
            gate.SetResult();

            // Neither call should throw: a duplicate delivery must be
            // ignored, not treated as a failure that blocks the consumer
            // from committing its offset.
            await Task.WhenAll(first, second);

            await using var verification = new FitLifeDbContext(options);
            (await verification.Interactions.CountAsync(interaction =>
                interaction.EventId == eventId)).Should().Be(1);
        }
        finally
        {
            await using var cleanup = new FitLifeDbContext(options);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }

    private static async Task ProcessAfterGateAsync(
        string connectionString,
        Message<string, string> message,
        Task gate)
    {
        var services = new ServiceCollection();
        services.AddDbContext<FitLifeDbContext>(builder =>
            builder.UseSqlServer(connectionString));
        services.AddScoped<IInteractionRepository, InteractionRepository>();
        await using var provider = services.BuildServiceProvider();

        var consumerService = new EventConsumerService(
            NullLogger<EventConsumerService>.Instance,
            new ConfigurationBuilder().Build(),
            provider);

        await gate;
        await consumerService.ProcessEventAsync(message, CancellationToken.None);
    }

    private static ServiceProvider BuildProvider(
        IInteractionRepository repository,
        IDeadLetterPublisher deadLetterPublisher,
        IRecommendationService? recommendationService = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(repository);
        services.AddSingleton(deadLetterPublisher);
        if (recommendationService != null)
            services.AddSingleton(recommendationService);
        return services.BuildServiceProvider();
    }

    private static EventConsumerService CreateService(
        IServiceProvider provider) =>
        new(
            NullLogger<EventConsumerService>.Instance,
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["BackgroundWorkers:EventConsumer:MaxAttempts"] = "3",
                    ["BackgroundWorkers:EventConsumer:RetryDelayMilliseconds"] = "0"
                })
                .Build(),
            provider);

    private static ConsumeResult<string, string> ConsumeResultFor(string value) =>
        new()
        {
            Topic = "user-events",
            Partition = new Partition(2),
            Offset = new Offset(42),
            Message = new Message<string, string>
            {
                Key = "user-1",
                Value = value
            }
        };

    private static UserEvent ValidEvent() => new()
    {
        EventId = Guid.NewGuid().ToString(),
        UserId = "user-1",
        ItemId = "class-1",
        ItemType = "Class",
        EventType = EventTypes.View,
        OccurredAt = DateTime.UtcNow,
        Timestamp = DateTime.UtcNow
    };

    private sealed class RecordingDeadLetterPublisher
        : IDeadLetterPublisher
    {
        public List<DeadLetterEvent> Published { get; } = new();

        public Task PublishDeadLetterAsync(
            string topic,
            string key,
            DeadLetterEvent deadLetterEvent,
            CancellationToken cancellationToken = default)
        {
            Published.Add(deadLetterEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class FailingDeadLetterPublisher
        : IDeadLetterPublisher
    {
        public Task PublishDeadLetterAsync(
            string topic,
            string key,
            DeadLetterEvent deadLetterEvent,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("dead-letter unavailable");
    }
}
