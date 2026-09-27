using FitLife.Core.Models;
using FitLife.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FitLife.Infrastructure.Services;

/// <summary>
/// Restores a demo persona to its canonical state so every visitor starts from the
/// same, reproducible recommendations. Only allowlisted synthetic users can be reset.
/// </summary>
public sealed class DemoPersonaService
{
    private readonly FitLifeDbContext _context;
    private readonly ILogger<DemoPersonaService> _logger;

    public DemoPersonaService(FitLifeDbContext context, ILogger<DemoPersonaService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public static DemoPersona? Find(string personaId) =>
        DemoCatalog.Personas.SingleOrDefault(persona =>
            string.Equals(persona.Id, personaId, StringComparison.Ordinal));

    /// <summary>
    /// Resets the persona's profile, bookings, history, and cached recommendation rows,
    /// rolls stale class schedules forward, and recomputes catalog enrollment from
    /// active bookings. Concurrent visitors on the same persona share one member, so
    /// the most recent reset wins.
    /// </summary>
    public async Task<User> ResetAsync(DemoPersona persona, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _context.ChangeTracker.Clear();
            await using var transaction = _context.Database.IsRelational()
                ? await _context.Database.BeginTransactionAsync(cancellationToken)
                : null;

            var user = await RestoreUserAsync(persona.UserId, now, cancellationToken);
            await ReplaceHistoryAsync(persona.UserId, now, cancellationToken);
            await CancelActiveBookingsAsync(persona.UserId, now, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await RestoreCatalogAsync(now, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            if (transaction != null)
                await transaction.CommitAsync(cancellationToken);
            _logger.LogInformation("Reset demo persona {PersonaId}", persona.Id);
            return user;
        });
    }

    private async Task<User> RestoreUserAsync(string userId, DateTime now, CancellationToken cancellationToken)
    {
        var canonical = DemoCatalog.User(userId, now);
        var user = await _context.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user == null)
        {
            _context.Users.Add(canonical);
            return canonical;
        }

        user.FirstName = canonical.FirstName;
        user.LastName = canonical.LastName;
        user.FitnessLevel = canonical.FitnessLevel;
        user.Goals = canonical.Goals;
        user.PreferredClassTypes = canonical.PreferredClassTypes;
        user.Segment = canonical.Segment;
        user.UpdatedAt = now;
        return user;
    }

    private async Task ReplaceHistoryAsync(string userId, DateTime now, CancellationToken cancellationToken)
    {
        _context.Interactions.RemoveRange(
            await _context.Interactions.Where(i => i.UserId == userId).ToListAsync(cancellationToken));
        _context.Recommendations.RemoveRange(
            await _context.Recommendations.Where(r => r.UserId == userId).ToListAsync(cancellationToken));
        _context.Interactions.AddRange(DemoCatalog.History(userId, now));
    }

    private async Task CancelActiveBookingsAsync(string userId, DateTime now, CancellationToken cancellationToken)
    {
        var active = await _context.Bookings
            .Where(b => b.UserId == userId && b.Status == BookingStatuses.Active)
            .ToListAsync(cancellationToken);
        foreach (var booking in active)
        {
            booking.Status = BookingStatuses.Cancelled;
            booking.CancelledAt = now;
            booking.UpdatedAt = now;
        }
    }

    private async Task RestoreCatalogAsync(DateTime now, CancellationToken cancellationToken)
    {
        var canonical = DemoCatalog.Classes(now).ToDictionary(c => c.Id);
        var ids = canonical.Keys.ToList();
        var classes = await _context.Classes.Where(c => ids.Contains(c.Id)).ToListAsync(cancellationToken);
        var activeCounts = await _context.Bookings
            .Where(b => ids.Contains(b.ClassId) && b.Status == BookingStatuses.Active)
            .GroupBy(b => b.ClassId)
            .Select(g => new { ClassId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.ClassId, g => g.Count, cancellationToken);

        foreach (var missing in canonical.Values.Where(c => classes.All(existing => existing.Id != c.Id)))
            _context.Classes.Add(missing);

        foreach (var classItem in classes)
        {
            if (classItem.StartTime <= now)
                classItem.StartTime = canonical[classItem.Id].StartTime;
            // Enrollment is authoritative: synthetic baseline plus real active bookings.
            classItem.CurrentEnrollment = Math.Min(
                classItem.Capacity,
                DemoCatalog.BaselineEnrollment(classItem.Id) + activeCounts.GetValueOrDefault(classItem.Id));
            classItem.UpdatedAt = now;
        }
    }
}
