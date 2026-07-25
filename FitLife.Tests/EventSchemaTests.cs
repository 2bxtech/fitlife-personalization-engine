using FitLife.Core.Models;
using FitLife.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace FitLife.Tests;

public class EventSchemaTests
{
    [Fact]
    public void Model_EnforcesUniqueNonNullEventIds()
    {
        using var context = CreateSqlServerContext();
        var interaction = context.GetService<IDesignTimeModel>()
            .Model
            .FindEntityType(typeof(Interaction));

        interaction.Should().NotBeNull();
        interaction!.GetIndexes().Should().Contain(index =>
            index.IsUnique
            && index.Properties.Single().Name == nameof(Interaction.EventId)
            && index.GetFilter() == "[EventId] IS NOT NULL");
    }

    [Fact]
    public void Model_MatchesLatestMigrationSnapshot()
    {
        using var context = CreateSqlServerContext();

        context.Database.HasPendingModelChanges().Should().BeFalse();
    }

    [SqlServerFact]
    public async Task DuplicateEventId_IsStoredOnlyOnce()
    {
        var databaseName = $"FitLifeEventDedupTests_{Guid.NewGuid():N}";
        var connectionString = new SqlConnectionStringBuilder(
            Environment.GetEnvironmentVariable("FITLIFE_SQLSERVER_TEST_CONNECTION")!)
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
                    Id = "event-user",
                    Email = "event-user@example.com",
                    PasswordHash = "not-used"
                });
                await setup.SaveChangesAsync();
            }

            const string eventId = "61401d53-a261-4db0-b5da-2113a32fc6dd";
            await using (var firstWrite = new FitLifeDbContext(options))
            {
                firstWrite.Interactions.Add(NewInteraction(eventId));
                await firstWrite.SaveChangesAsync();
            }

            await using (var duplicateWrite = new FitLifeDbContext(options))
            {
                duplicateWrite.Interactions.Add(NewInteraction(eventId));
                var write = () => duplicateWrite.SaveChangesAsync();
                await write.Should().ThrowAsync<DbUpdateException>();
            }

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

    private static FitLifeDbContext CreateSqlServerContext()
    {
        var options = new DbContextOptionsBuilder<FitLifeDbContext>()
            .UseSqlServer(
                "Server=localhost;Database=FitLifeEventSchemaTests;User Id=test;Password=test;TrustServerCertificate=True")
            .Options;
        return new FitLifeDbContext(options);
    }

    private static Interaction NewInteraction(string eventId) => new()
    {
        EventId = eventId,
        UserId = "event-user",
        ItemId = "class-1",
        ItemType = "Class",
        EventType = EventTypes.View
    };
}
