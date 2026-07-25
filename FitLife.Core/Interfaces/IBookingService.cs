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
}
