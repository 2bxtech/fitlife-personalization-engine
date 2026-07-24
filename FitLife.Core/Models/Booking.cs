namespace FitLife.Core.Models;

public static class BookingStatuses
{
    public const string Active = "Active";
    public const string Cancelled = "Cancelled";
}

/// <summary>
/// Durable record of a member's class reservation.
/// </summary>
public class Booking
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string ClassId { get; set; } = string.Empty;
    public string Status { get; set; } = BookingStatuses.Active;
    public string? IdempotencyKey { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CancelledAt { get; set; }

    public virtual User User { get; set; } = null!;
    public virtual Class Class { get; set; } = null!;
}
