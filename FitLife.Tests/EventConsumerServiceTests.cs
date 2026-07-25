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

namespace FitLife.Tests;

public class EventConsumerServiceTests
{
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
}
