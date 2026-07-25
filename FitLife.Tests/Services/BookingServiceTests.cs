using FitLife.Core.Interfaces;
using FitLife.Core.Models;
using FitLife.Core.Services;
using FitLife.Infrastructure.Data;
using FitLife.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FitLife.Tests.Services;

public class BookingServiceTests
{
    [Fact]
    public async Task BookAsync_InvalidatesCacheOnlyForCommittedFirstBooking()
    {
        var options = new DbContextOptionsBuilder<FitLifeDbContext>()
            .UseInMemoryDatabase($"booking-service-{Guid.NewGuid():N}")
            .Options;
        await using var context = new FitLifeDbContext(options);
        var user = new User
        {
            Id = "user-1",
            Email = "booking@example.com",
            PasswordHash = "not-used"
        };
        var classEntity = new Class
        {
            Id = "class-1",
            Name = "Booking test",
            Type = "Yoga",
            StartTime = DateTime.UtcNow.AddDays(1),
            Capacity = 2
        };
        context.AddRange(user, classEntity);
        await context.SaveChangesAsync();

        var cache = new Mock<ICacheService>();
        cache.Setup(service => service.DeleteAsync("rec:user-1"))
            .ReturnsAsync(true);
        var service = new BookingService(
            context,
            cache.Object,
            NullLogger<BookingService>.Instance);

        var first = await service.BookAsync("user-1", "class-1");
        var retry = await service.BookAsync("user-1", "class-1");

        first.Outcome.Should().Be(BookingOutcome.Booked);
        retry.Outcome.Should().Be(BookingOutcome.AlreadyBooked);
        context.Classes.Single().CurrentEnrollment.Should().Be(1);
        context.Bookings.Should().ContainSingle();
        context.Interactions.Should().ContainSingle(interaction =>
            interaction.EventType == "Book");
        cache.Verify(
            service => service.DeleteAsync("rec:user-1"),
            Times.Once);
    }
}
