using FitLife.Core.Services;

namespace FitLife.Core.Interfaces;

public interface IBookingService
{
    Task<BookingResult> BookAsync(
        string userId,
        string classId,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);

    Task<BookingResult> CancelAsync(
        string userId,
        string classId,
        CancellationToken cancellationToken = default);

    Task<HashSet<string>> GetActiveClassIdsAsync(
        string userId,
        IEnumerable<string> classIds,
        CancellationToken cancellationToken = default);

    Task<HashSet<string>> GetActiveClassIdsAsync(
        string userId,
        CancellationToken cancellationToken = default);
}
