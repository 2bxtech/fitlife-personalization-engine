using FitLife.Core.Models;

namespace FitLife.Core.Services;

public enum BookingOutcome
{
    Booked,
    AlreadyBooked,
    ClassNotFound,
    ClassFull,
    Conflict
}

public sealed record BookingResult(
    BookingOutcome Outcome,
    Class? Class = null,
    Booking? Booking = null);
