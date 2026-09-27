using FitLife.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FitLife.Infrastructure.Data;

/// <summary>
/// Seeds the database with sample data for demo purposes
/// </summary>
public class DbSeeder
{
    private readonly FitLifeDbContext _context;
    private readonly ILogger<DbSeeder> _logger;

    public DbSeeder(FitLifeDbContext context, ILogger<DbSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Idempotently seeds the synthetic catalog. On SQL Server the whole operation runs
    /// in one transaction under an application lock, so replicas starting together
    /// cannot both pass the "no history yet" check and insert duplicate histories.
    /// </summary>
    public async Task SeedAsync()
    {
        if (!_context.Database.IsRelational())
        {
            await SeedCoreAsync();
            return;
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            _context.ChangeTracker.Clear();
            await using var transaction = await _context.Database.BeginTransactionAsync();
            await SqlAppLock.AcquireAsync(_context, "fitlife-demo-seed");
            await SeedCoreAsync();
            await transaction.CommitAsync();
        });
    }

    private async Task SeedCoreAsync()
    {
        try
        {
            _logger.LogInformation("Starting database seeding...");

            var now = DateTime.UtcNow;
            var users = DemoCatalog.Users(now);
            var classes = DemoCatalog.Classes(now);
            var sampleUserIds = users.Select(user => user.Id).ToList();
            var sampleEmails = users.Select(user => user.Email).ToList();
            var existingUsers = await _context.Users
                .Where(user =>
                    sampleUserIds.Contains(user.Id)
                    || sampleEmails.Contains(user.Email))
                .Select(user => new { user.Id, user.Email })
                .ToListAsync();
            var missingUsers = users
                .Where(user =>
                    !existingUsers.Any(existing =>
                        existing.Id == user.Id
                        || existing.Email == user.Email))
                .ToList();
            if (missingUsers.Count > 0)
            {
                await _context.Users.AddRangeAsync(missingUsers);
                await _context.SaveChangesAsync();
            }
            _logger.LogInformation("Seeded {Count} missing demo users", missingUsers.Count);

            var sampleClassIds = classes.Select(gymClass => gymClass.Id).ToList();
            var existingClasses = await _context.Classes
                .Where(gymClass => sampleClassIds.Contains(gymClass.Id))
                .ToListAsync();
            var missingClasses = classes
                .Where(gymClass =>
                    !existingClasses.Any(existing => existing.Id == gymClass.Id))
                .ToList();
            if (missingClasses.Count > 0)
            {
                await _context.Classes.AddRangeAsync(missingClasses);
            }
            _logger.LogInformation("Seeded {Count} missing demo classes", missingClasses.Count);

            var refreshedClassCount = 0;
            foreach (var existingClass in existingClasses
                         .Where(gymClass => gymClass.StartTime <= DateTime.UtcNow))
            {
                var currentSchedule = classes.Single(
                    gymClass => gymClass.Id == existingClass.Id);
                existingClass.StartTime = currentSchedule.StartTime;
                existingClass.UpdatedAt = DateTime.UtcNow;
                refreshedClassCount++;
            }
            if (missingClasses.Count > 0 || refreshedClassCount > 0)
                await _context.SaveChangesAsync();
            _logger.LogInformation(
                "Refreshed {Count} stale demo class start times",
                refreshedClassCount);

            var persistedSampleUserIds = await _context.Users
                .Where(user => sampleUserIds.Contains(user.Id))
                .Select(user => user.Id)
                .ToListAsync();
            var hasDemoInteractions = await _context.Interactions.AnyAsync(
                interaction => persistedSampleUserIds.Contains(interaction.UserId));
            if (!hasDemoInteractions)
            {
                var interactions = persistedSampleUserIds
                    .SelectMany(userId => DemoCatalog.History(userId, now))
                    .ToList();
                await _context.Interactions.AddRangeAsync(interactions);
                await _context.SaveChangesAsync();
                _logger.LogInformation(
                    "Seeded {Count} demo interactions",
                    interactions.Count);
            }
            else
            {
                _logger.LogInformation("Demo interactions already exist, skipping...");
            }

            _logger.LogInformation("Database seeding completed successfully!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding database");
            throw;
        }
    }
}
