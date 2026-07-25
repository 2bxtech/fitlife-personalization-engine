using System.Text.Json;
using FitLife.Core.Interfaces;
using FitLife.Core.Models;
using FitLife.Core.Services;
using FitLife.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FitLife.Infrastructure.Services;

public sealed class BookingService : IBookingService
{
    private const int MaxConcurrencyAttempts = 3;
    private readonly FitLifeDbContext _context;
    private readonly ICacheService _cacheService;
    private readonly ILogger<BookingService> _logger;

    public BookingService(
        FitLifeDbContext context,
        ICacheService cacheService,
        ILogger<BookingService> logger)
    {
        _context = context;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<BookingResult> BookAsync(
        string userId,
        string classId,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        idempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey)
            ? null
            : idempotencyKey.Trim();

        for (var attempt = 1; attempt <= MaxConcurrencyAttempts; attempt++)
        {
            var existingResult = await FindExistingResultAsync(
                userId, classId, idempotencyKey, cancellationToken);
            if (existingResult != null)
                return existingResult;

            var classEntity = await _context.Classes
                .SingleOrDefaultAsync(entity => entity.Id == classId, cancellationToken);
            if (classEntity == null)
                return new BookingResult(BookingOutcome.ClassNotFound);

            if (classEntity.CurrentEnrollment >= classEntity.Capacity)
                return new BookingResult(BookingOutcome.ClassFull, classEntity);

            var now = DateTime.UtcNow;
            var booking = new Booking
            {
                UserId = userId,
                ClassId = classId,
                IdempotencyKey = idempotencyKey,
                CreatedAt = now,
                UpdatedAt = now
            };
            var interaction = new Interaction
            {
                UserId = userId,
                ItemId = classId,
                ItemType = "Class",
                EventType = "Book",
                Timestamp = now,
                Metadata = JsonSerializer.Serialize(new
                {
                    source = "web",
                    className = classEntity.Name,
                    bookingId = booking.Id
                })
            };

            classEntity.CurrentEnrollment++;
            classEntity.UpdatedAt = now;
            _context.Bookings.Add(booking);
            _context.Interactions.Add(interaction);

            try
            {
                await SaveAtomicallyAsync(cancellationToken);
                await _cacheService.DeleteAsync($"rec:{userId}");

                _logger.LogInformation(
                    "User {UserId} booked class {ClassId} with booking {BookingId}",
                    userId, classId, booking.Id);
                return new BookingResult(BookingOutcome.Booked, classEntity, booking);
            }
            catch (DbUpdateConcurrencyException)
            {
                _context.ChangeTracker.Clear();
                if (attempt == MaxConcurrencyAttempts)
                    return new BookingResult(BookingOutcome.Conflict);
            }
            catch (DbUpdateException)
            {
                _context.ChangeTracker.Clear();
                var racedResult = await FindExistingResultAsync(
                    userId, classId, idempotencyKey, cancellationToken);
                if (racedResult != null)
                    return racedResult;

                throw;
            }
        }

        return new BookingResult(BookingOutcome.Conflict);
    }

    public async Task<BookingResult> CancelAsync(
        string userId,
        string classId,
        CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; attempt <= MaxConcurrencyAttempts; attempt++)
        {
            var booking = await _context.Bookings
                .Include(entity => entity.Class)
                .SingleOrDefaultAsync(
                    entity => entity.UserId == userId
                        && entity.ClassId == classId
                        && entity.Status == BookingStatuses.Active,
                    cancellationToken);

            if (booking == null)
            {
                var cancelledBooking = await _context.Bookings
                    .AsNoTracking()
                    .Include(entity => entity.Class)
                    .Where(entity => entity.UserId == userId
                        && entity.ClassId == classId
                        && entity.Status == BookingStatuses.Cancelled)
                    .OrderByDescending(entity => entity.CancelledAt)
                    .FirstOrDefaultAsync(cancellationToken);

                return cancelledBooking == null
                    ? new BookingResult(BookingOutcome.BookingNotFound)
                    : new BookingResult(
                        BookingOutcome.AlreadyCancelled,
                        cancelledBooking.Class,
                        cancelledBooking);
            }

            var now = DateTime.UtcNow;
            booking.Status = BookingStatuses.Cancelled;
            booking.CancelledAt = now;
            booking.UpdatedAt = now;
            booking.Class.CurrentEnrollment--;
            booking.Class.UpdatedAt = now;
            _context.Interactions.Add(new Interaction
            {
                UserId = userId,
                ItemId = classId,
                ItemType = "Class",
                EventType = "Cancel",
                Timestamp = now,
                Metadata = JsonSerializer.Serialize(new
                {
                    source = "web",
                    className = booking.Class.Name,
                    bookingId = booking.Id
                })
            });

            try
            {
                await SaveAtomicallyAsync(cancellationToken);
                await _cacheService.DeleteAsync($"rec:{userId}");

                _logger.LogInformation(
                    "User {UserId} cancelled booking {BookingId} for class {ClassId}",
                    userId, booking.Id, classId);
                return new BookingResult(
                    BookingOutcome.Cancelled,
                    booking.Class,
                    booking);
            }
            catch (DbUpdateConcurrencyException)
            {
                _context.ChangeTracker.Clear();
                if (attempt == MaxConcurrencyAttempts)
                    return new BookingResult(BookingOutcome.Conflict);
            }
        }

        return new BookingResult(BookingOutcome.Conflict);
    }

    public async Task<HashSet<string>> GetActiveClassIdsAsync(
        string userId,
        IEnumerable<string> classIds,
        CancellationToken cancellationToken = default)
    {
        var distinctClassIds = classIds.Distinct().ToList();
        if (distinctClassIds.Count == 0)
            return new HashSet<string>();

        var activeClassIds = await _context.Bookings
            .AsNoTracking()
            .Where(booking => booking.UserId == userId
                && booking.Status == BookingStatuses.Active
                && distinctClassIds.Contains(booking.ClassId))
            .Select(booking => booking.ClassId)
            .ToListAsync(cancellationToken);

        return activeClassIds.ToHashSet();
    }

    public async Task<HashSet<string>> GetActiveClassIdsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var activeClassIds = await _context.Bookings
            .AsNoTracking()
            .Where(booking => booking.UserId == userId
                && booking.Status == BookingStatuses.Active)
            .Select(booking => booking.ClassId)
            .ToListAsync(cancellationToken);

        return activeClassIds.ToHashSet();
    }

    private async Task<BookingResult?> FindExistingResultAsync(
        string userId,
        string classId,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (idempotencyKey != null)
        {
            var idempotentBooking = await _context.Bookings
                .AsNoTracking()
                .Include(booking => booking.Class)
                .SingleOrDefaultAsync(
                    booking => booking.IdempotencyKey == idempotencyKey,
                    cancellationToken);

            if (idempotentBooking != null)
            {
                return idempotentBooking.UserId == userId
                       && idempotentBooking.ClassId == classId
                       && idempotentBooking.Status == BookingStatuses.Active
                    ? new BookingResult(
                        BookingOutcome.AlreadyBooked,
                        idempotentBooking.Class,
                        idempotentBooking)
                    : new BookingResult(BookingOutcome.Conflict);
            }
        }

        var activeBooking = await _context.Bookings
            .AsNoTracking()
            .Include(booking => booking.Class)
            .SingleOrDefaultAsync(
                booking => booking.UserId == userId
                    && booking.ClassId == classId
                    && booking.Status == BookingStatuses.Active,
                cancellationToken);

        return activeBooking == null
            ? null
            : new BookingResult(
                BookingOutcome.AlreadyBooked,
                activeBooking.Class,
                activeBooking);
    }

    private async Task SaveAtomicallyAsync(CancellationToken cancellationToken)
    {
        if (!_context.Database.IsRelational())
        {
            await _context.SaveChangesAsync(cancellationToken);
            return;
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction =
                await _context.Database.BeginTransactionAsync(cancellationToken);
            await _context.SaveChangesAsync(
                acceptAllChangesOnSuccess: false,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
        _context.ChangeTracker.AcceptAllChanges();
    }
}
