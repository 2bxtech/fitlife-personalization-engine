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

    [Fact]
    public async Task CancelAsync_RepeatedRequest_RestoresCapacityAndRecordsInteractionExactlyOnce()
    {
        var options = new DbContextOptionsBuilder<FitLifeDbContext>()
            .UseInMemoryDatabase($"booking-cancellation-{Guid.NewGuid():N}")
            .Options;
        await using var context = new FitLifeDbContext(options);
        var user = new User
        {
            Id = "user-1",
            Email = "cancellation@example.com",
            PasswordHash = "not-used"
        };
        var classEntity = new Class
        {
            Id = "class-1",
            Name = "Cancellation test",
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

        (await service.BookAsync("user-1", "class-1")).Outcome
            .Should().Be(BookingOutcome.Booked);
        var firstCancellation = await service.CancelAsync("user-1", "class-1");
        var retry = await service.CancelAsync("user-1", "class-1");

        firstCancellation.Outcome.Should().Be(BookingOutcome.Cancelled);
        retry.Outcome.Should().Be(BookingOutcome.AlreadyCancelled);
        context.Classes.Single().CurrentEnrollment.Should().Be(0);
        context.Bookings.Single().Status.Should().Be(BookingStatuses.Cancelled);
        context.Interactions.Count(interaction =>
            interaction.EventType == "Cancel").Should().Be(1);
        cache.Verify(
            cacheService => cacheService.DeleteAsync("rec:user-1"),
            Times.Exactly(2));
    }

    [Fact]
    public async Task CancelAsync_OtherUsersBooking_DoesNotChangeState()
    {
        var options = new DbContextOptionsBuilder<FitLifeDbContext>()
            .UseInMemoryDatabase($"booking-ownership-{Guid.NewGuid():N}")
            .Options;
        await using var context = new FitLifeDbContext(options);
        var owner = new User
        {
            Id = "owner",
            Email = "owner@example.com",
            PasswordHash = "not-used"
        };
        var other = new User
        {
            Id = "other",
            Email = "other@example.com",
            PasswordHash = "not-used"
        };
        var classEntity = new Class
        {
            Id = "class-1",
            Name = "Ownership test",
            Type = "Yoga",
            StartTime = DateTime.UtcNow.AddDays(1),
            Capacity = 2,
            CurrentEnrollment = 1
        };
        var booking = new Booking
        {
            UserId = owner.Id,
            ClassId = classEntity.Id
        };
        context.AddRange(owner, other, classEntity, booking);
        await context.SaveChangesAsync();

        var cache = new Mock<ICacheService>();
        var service = new BookingService(
            context,
            cache.Object,
            NullLogger<BookingService>.Instance);

        var result = await service.CancelAsync(other.Id, classEntity.Id);

        result.Outcome.Should().Be(BookingOutcome.BookingNotFound);
        booking.Status.Should().Be(BookingStatuses.Active);
        classEntity.CurrentEnrollment.Should().Be(1);
        context.Interactions.Should().BeEmpty();
        cache.Verify(
            cacheService => cacheService.DeleteAsync(It.IsAny<string>()),
            Times.Never);
    }
}
