using FitLife.Core.Services;

namespace FitLife.Core.Interfaces;

public interface IBookingService
{
    Task<BookingResult> BookAsync(
        string userId,
        string classId,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);
}
