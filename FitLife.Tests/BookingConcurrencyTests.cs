using System.Data.Common;
using FitLife.Core.Interfaces;
using FitLife.Core.Models;
using FitLife.Core.Services;
using FitLife.Infrastructure.Data;
using FitLife.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FitLife.Tests;

public class BookingConcurrencyTests
{
    [SqlServerFact]
    public async Task ConcurrentLastSeatRequests_CreateExactlyOneBooking()
    {
        var baseConnectionString =
            Environment.GetEnvironmentVariable("FITLIFE_SQLSERVER_TEST_CONNECTION")!;

        var databaseName = $"FitLifeBookingTests_{Guid.NewGuid():N}";
        var connectionBuilder = new SqlConnectionStringBuilder(baseConnectionString)
        {
            InitialCatalog = databaseName
        };
        var connectionString = connectionBuilder.ConnectionString;
        var options = new DbContextOptionsBuilder<FitLifeDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        try
        {
            await using (var setup = new FitLifeDbContext(options))
            {
                await setup.Database.EnsureCreatedAsync();
                setup.Users.AddRange(
                    NewUser("user-1"),
                    NewUser("user-2"));
                setup.Classes.Add(new Class
                {
                    Id = "last-seat",
                    Name = "Last seat",
                    Type = "HIIT",
                    StartTime = DateTime.UtcNow.AddDays(1),
                    Capacity = 1
                });
                await setup.SaveChangesAsync();
            }

            var gate = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var first = BookAfterGateAsync(options, "user-1", gate.Task);
            var second = BookAfterGateAsync(options, "user-2", gate.Task);
            gate.SetResult();

            var results = await Task.WhenAll(first, second);

            results.Should().ContainSingle(result =>
                result == BookingOutcome.Booked);
            results.Should().ContainSingle(result =>
                result == BookingOutcome.ClassFull
                || result == BookingOutcome.Conflict);

            await using var verification = new FitLifeDbContext(options);
            (await verification.Bookings.CountAsync()).Should().Be(1);
            (await verification.Interactions.CountAsync(interaction =>
                interaction.EventType == "Book")).Should().Be(1);
            (await verification.Classes.SingleAsync())
                .CurrentEnrollment.Should().Be(1);
        }
        finally
        {
            await using var cleanup = new FitLifeDbContext(options);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }

    [SqlServerFact]
    public async Task DatabaseFailure_RollsBackBookingEnrollmentAndInteraction()
    {
        var baseConnectionString =
            Environment.GetEnvironmentVariable("FITLIFE_SQLSERVER_TEST_CONNECTION")!;
        var databaseName = $"FitLifeBookingRollbackTests_{Guid.NewGuid():N}";
        var connectionString = new SqlConnectionStringBuilder(baseConnectionString)
        {
            InitialCatalog = databaseName
        }.ConnectionString;
        var verificationOptions = new DbContextOptionsBuilder<FitLifeDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        try
        {
            await using (var setup = new FitLifeDbContext(verificationOptions))
            {
                await setup.Database.EnsureCreatedAsync();
                setup.Users.Add(NewUser("user-1"));
                setup.Classes.Add(new Class
                {
                    Id = "rollback-class",
                    Name = "Rollback test",
                    Type = "HIIT",
                    StartTime = DateTime.UtcNow.AddDays(1),
                    Capacity = 2
                });
                await setup.SaveChangesAsync();
            }

            var failingOptions = new DbContextOptionsBuilder<FitLifeDbContext>()
                .UseSqlServer(connectionString)
                .AddInterceptors(new ForcedBookingWriteFailureInterceptor())
                .Options;
            await using (var context = new FitLifeDbContext(failingOptions))
            {
                var cache = new Mock<ICacheService>();
                var service = new BookingService(
                    context,
                    cache.Object,
                    NullLogger<BookingService>.Instance);

                var action = () => service.BookAsync("user-1", "rollback-class");

                var exception = await action.Should()
                    .ThrowAsync<DbUpdateException>();
                exception.Which.InnerException.Should()
                    .BeOfType<InvalidOperationException>()
                    .Which.Message.Should().Be("Forced booking write failure");
                cache.Verify(
                    cacheService => cacheService.DeleteAsync(It.IsAny<string>()),
                    Times.Never);
            }

            await using var verification =
                new FitLifeDbContext(verificationOptions);
            (await verification.Bookings.CountAsync()).Should().Be(0);
            (await verification.Interactions.CountAsync()).Should().Be(0);
            (await verification.Classes.SingleAsync())
                .CurrentEnrollment.Should().Be(0);
        }
        finally
        {
            await using var cleanup = new FitLifeDbContext(verificationOptions);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }

    private static async Task<BookingOutcome> BookAfterGateAsync(
        DbContextOptions<FitLifeDbContext> options,
        string userId,
        Task gate)
    {
        await using var context = new FitLifeDbContext(options);
        var cache = new Mock<ICacheService>();
        cache.Setup(service => service.DeleteAsync($"rec:{userId}"))
            .ReturnsAsync(true);
        var service = new BookingService(
            context,
            cache.Object,
            NullLogger<BookingService>.Instance);

        await gate;
        return (await service.BookAsync(userId, "last-seat")).Outcome;
    }

    private static User NewUser(string id) => new()
    {
        Id = id,
        Email = $"{id}@example.com",
        PasswordHash = "not-used"
    };

    private sealed class ForcedBookingWriteFailureInterceptor
        : DbCommandInterceptor
    {
        public override InterceptionResult<int> NonQueryExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result)
        {
            ThrowForBookingWrite(command);
            return result;
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            ThrowForBookingWrite(command);
            return ValueTask.FromResult(result);
        }

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            ThrowForBookingWrite(command);
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>>
            ReaderExecutingAsync(
                DbCommand command,
                CommandEventData eventData,
                InterceptionResult<DbDataReader> result,
                CancellationToken cancellationToken = default)
        {
            ThrowForBookingWrite(command);
            return ValueTask.FromResult(result);
        }

        private static void ThrowForBookingWrite(DbCommand command)
        {
            if (command.CommandText.Contains(
                    "INSERT INTO [Bookings]",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Forced booking write failure");
            }
        }
    }
}

public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable("FITLIFE_SQLSERVER_TEST_CONNECTION")))
        {
            Skip =
                "Set FITLIFE_SQLSERVER_TEST_CONNECTION to run SQL Server concurrency tests.";
        }
    }
}
