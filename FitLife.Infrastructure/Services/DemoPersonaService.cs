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
        // Overlapping sessions can race on catalog row versions (or with a booking).
        // The execution strategy retries transient faults only, so retry conflicts here.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await ResetOnceAsync(persona, cancellationToken);
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxConcurrencyAttempts)
            {
                _logger.LogInformation(
                    "Demo reset for {PersonaId} hit a concurrency conflict; retrying ({Attempt}/{Max})",
                    persona.Id, attempt, MaxConcurrencyAttempts);
            }
        }
    }

    private const int MaxConcurrencyAttempts = 3;

    private async Task<User> ResetOnceAsync(DemoPersona persona, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _context.ChangeTracker.Clear();
            await using var transaction = _context.Database.IsRelational()
                ? await _context.Database.BeginTransactionAsync(cancellationToken)
                : null;
            if (transaction != null)
                await LockPersonaAsync(persona.Id, cancellationToken);

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

    /// <summary>
    /// Serializes resets of one persona. Without it, two concurrent sessions both
    /// delete and re-insert the same history, duplicating it. Held until commit.
    /// </summary>
    private Task LockPersonaAsync(string personaId, CancellationToken cancellationToken)
    {
        var resource = $"fitlife-demo-reset:{personaId}";
        return _context.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @result int;
            EXEC @result = sp_getapplock @Resource = {resource}, @LockMode = 'Exclusive',
                @LockOwner = 'Transaction', @LockTimeout = 15000;
            IF @result < 0 THROW 50001, 'Timed out waiting for a demo persona reset lock.', 1;
            """, cancellationToken);
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

        // Catalog enrollment is recomputed afterwards; release seats on any other
        // (operator-created) class directly, as a normal cancellation would.
        var catalogIds = DemoCatalog.Classes(now).Select(c => c.Id).ToHashSet();
        var released = active
            .Where(b => !catalogIds.Contains(b.ClassId))
            .GroupBy(b => b.ClassId)
            .ToDictionary(g => g.Key, g => g.Count());
        if (released.Count == 0)
            return;
        var ids = released.Keys.ToList();
        foreach (var classItem in await _context.Classes.Where(c => ids.Contains(c.Id)).ToListAsync(cancellationToken))
        {
            classItem.CurrentEnrollment = Math.Max(0, classItem.CurrentEnrollment - released[classItem.Id]);
            classItem.UpdatedAt = now;
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
            // Enrollment is authoritative: synthetic baseline plus real active bookings.
            var expected = DemoCatalog.BaselineEnrollment(classItem.Id) + activeCounts.GetValueOrDefault(classItem.Id);
            var enrollment = Math.Min(classItem.Capacity, expected);
            if (enrollment < expected)
                _logger.LogWarning(
                    "Class {ClassId} capacity {Capacity} is below its expected enrollment {Expected}",
                    classItem.Id, classItem.Capacity, expected);
            var stale = classItem.StartTime <= now;
            // Only touch rows that change, so concurrent resets rarely contend.
            if (!stale && classItem.CurrentEnrollment == enrollment)
                continue;
            if (stale)
                classItem.StartTime = canonical[classItem.Id].StartTime;
            classItem.CurrentEnrollment = enrollment;
            classItem.UpdatedAt = now;
        }
    }
}
