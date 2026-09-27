using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Confluent.Kafka;
using FitLife.Api.BackgroundServices;
using FitLife.Api.Events;
using FitLife.Api.Observability;
using FitLife.Core.DTOs;
using FitLife.Core.Interfaces;
using FitLife.Core.Models;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FitLife.Tests;

/// <summary>
/// Each test observes a meter created from its own IMeterFactory, so parallel
/// test classes cannot contribute measurements.
/// </summary>
public class OperationalSignalsTests : IClassFixture<FitLifeWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly FitLifeWebApplicationFactory _factory;

    public OperationalSignalsTests(FitLifeWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Consumer_CountsRetriesAndDeadLetterDisposition()
    {
        var repository = new Mock<IInteractionRepository>();
        repository.Setup(r => r.ExistsByEventIdAsync(It.IsAny<string>())).ReturnsAsync(false);
        repository.Setup(r => r.AddAsync(It.IsAny<Interaction>()))
            .ReturnsAsync((Interaction interaction) => interaction);
        repository.Setup(r => r.SaveChangesAsync())
            .ThrowsAsync(new InvalidOperationException("transient database failure"));
        await using var provider = Provider(services =>
        {
            services.AddSingleton(repository.Object);
            services.AddSingleton(Mock.Of<IDeadLetterPublisher>());
        });
        var meter = provider.GetRequiredService<FitLifeMetrics>().Meter;
        using var retries = new MetricCollector<long>(meter, "fitlife.events.retries");
        using var deadLettered = new MetricCollector<long>(meter, "fitlife.events.dead_lettered");

        await Consumer(provider).ProcessWithRetryAsync(
            ConsumeResultFor(JsonSerializer.Serialize(ValidEvent())), CancellationToken.None);
        await Consumer(provider).ProcessWithRetryAsync(
            ConsumeResultFor("{not-json"), CancellationToken.None);

        retries.GetMeasurementSnapshot().EvaluateAsCounter().Should().Be(2);
        deadLettered.GetMeasurementSnapshot().Select(m => m.Tags["disposition"])
            .Should().BeEquivalentTo(new[] { "retries-exhausted", "malformed-json" });
    }

    [Fact]
    public async Task Recorder_CountsStoredAndDuplicateOutcomes()
    {
        var repository = new Mock<IInteractionRepository>();
        repository.SetupSequence(r => r.ExistsByEventIdAsync(It.IsAny<string>()))
            .ReturnsAsync(false)
            .ReturnsAsync(true);
        repository.Setup(r => r.AddAsync(It.IsAny<Interaction>()))
            .ReturnsAsync((Interaction interaction) => interaction);
        repository.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);
        await using var provider = Provider(services => services.AddSingleton(repository.Object));
        using var recorded = new MetricCollector<long>(
            provider.GetRequiredService<FitLifeMetrics>().Meter, "fitlife.events.recorded");
        var recorder = new InteractionEventRecorder(
            provider, NullLogger<InteractionEventRecorder>.Instance);

        var userEvent = ValidEvent();
        await recorder.RecordAsync(userEvent);
        await recorder.RecordAsync(userEvent);

        recorded.GetMeasurementSnapshot().Select(m => m.Tags["outcome"])
            .Should().Equal("stored", "duplicate");
    }

    [Fact]
    public async Task EventsApi_CountsPublishSuccessAndFailureByTransport()
    {
        var recording = _factory.Services.GetRequiredService<RecordingEventPublisher>();
        using var published = new MetricCollector<long>(
            _factory.Services.GetRequiredService<FitLifeMetrics>().Meter, "fitlife.events.published");
        var (client, userId) = await AuthenticatedClientAsync();
        var dto = new EventDto
        {
            UserId = userId, ItemId = "class-1", ItemType = "Class", EventType = EventTypes.View
        };

        (await client.PostAsJsonAsync("/api/events", dto)).EnsureSuccessStatusCode();
        recording.FailNextPublish = true;
        (await client.PostAsJsonAsync("/api/events", dto)).IsSuccessStatusCode.Should().BeFalse();

        var outcomes = published.GetMeasurementSnapshot()
            .Select(m => $"{m.Tags["transport"]}:{m.Tags["outcome"]}");
        outcomes.Should().Equal("kafka:success", "kafka:failure");
    }

    [Fact]
    public async Task SchedulerRun_RecordsOutcomeDurationAndPerUserCounts()
    {
        var users = new Mock<IUserRepository>();
        users.Setup(r => r.GetAllAsync()).ReturnsAsync(new[]
        {
            new User { Id = "ok-user" }, new User { Id = "failing-user" }
        });
        var recommendations = new Mock<IRecommendationService>();
        recommendations.Setup(s => s.GenerateRecommendationsAsync("ok-user", 10))
            .ReturnsAsync(new List<RecommendationDto>());
        recommendations.Setup(s => s.GenerateRecommendationsAsync("failing-user", 10))
            .ThrowsAsync(new InvalidOperationException("scoring failed"));
        await using var provider = Provider(services =>
        {
            services.AddSingleton(users.Object);
            services.AddSingleton(Mock.Of<IInteractionRepository>());
            services.AddSingleton(recommendations.Object);
        });
        var meter = provider.GetRequiredService<FitLifeMetrics>().Meter;
        using var runs = new MetricCollector<long>(meter, "fitlife.worker.runs");
        using var duration = new MetricCollector<double>(meter, "fitlife.worker.run.duration");
        using var processed = new MetricCollector<long>(meter, "fitlife.worker.users");
        var generator = new RecommendationGeneratorService(
            NullLogger<RecommendationGeneratorService>.Instance, EmptyConfig(), provider);

        await generator.RunBatchAsync(batchSize: 10, processActiveOnly: false, CancellationToken.None);

        runs.GetMeasurementSnapshot().Should().ContainSingle()
            .Which.Tags["outcome"].Should().Be("success");
        duration.GetMeasurementSnapshot().Should().ContainSingle()
            .Which.Value.Should().BeGreaterThanOrEqualTo(0);
        processed.GetMeasurementSnapshot()
            .ToDictionary(m => (string)m.Tags["outcome"]!, m => m.Value)
            .Should().Equal(new Dictionary<string, long> { ["success"] = 1, ["failure"] = 1 });
    }

    [Fact]
    public async Task SchedulerRun_FailureIsCountedAndRethrown()
    {
        var users = new Mock<IUserRepository>();
        users.Setup(r => r.GetAllAsync()).ThrowsAsync(new InvalidOperationException("database down"));
        await using var provider = Provider(services =>
        {
            services.AddSingleton(users.Object);
            services.AddSingleton(Mock.Of<IInteractionRepository>());
            services.AddSingleton(Mock.Of<IRecommendationService>());
        });
        using var runs = new MetricCollector<long>(
            provider.GetRequiredService<FitLifeMetrics>().Meter, "fitlife.worker.runs");
        var generator = new RecommendationGeneratorService(
            NullLogger<RecommendationGeneratorService>.Instance, EmptyConfig(), provider);

        var run = () => generator.RunBatchAsync(10, false, CancellationToken.None);

        await run.Should().ThrowAsync<InvalidOperationException>();
        runs.GetMeasurementSnapshot().Should().ContainSingle()
            .Which.Tags["outcome"].Should().Be("failure");
    }

    private async Task<(HttpClient Client, string UserId)> AuthenticatedClientAsync()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterUserDto
        {
            Email = $"signals_{Guid.NewGuid():N}@example.com",
            Password = $"Pw-{Guid.NewGuid():N}-A1",
            FirstName = "Signal",
            LastName = "Test",
            FitnessLevel = "Beginner"
        });
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>(JsonOptions);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body!.Data!.Token);
        return (client, body.Data.User!.Id);
    }

    private static ServiceProvider Provider(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.AddMetrics();
        services.AddSingleton(EmptyConfig());
        services.AddSingleton<FitLifeMetrics>();
        configure(services);
        return services.BuildServiceProvider();
    }

    private static IConfiguration EmptyConfig() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BackgroundWorkers:EventConsumer:RetryDelayMilliseconds"] = "0"
        }).Build();

    private static EventConsumerService Consumer(IServiceProvider provider) =>
        new(NullLogger<EventConsumerService>.Instance, EmptyConfig(), provider);

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

    private static ConsumeResult<string, string> ConsumeResultFor(string value) => new()
    {
        Topic = "user-events",
        Partition = new Partition(0),
        Offset = new Offset(7),
        Message = new Message<string, string> { Key = "user-1", Value = value }
    };
}
