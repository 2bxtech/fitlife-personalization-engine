using FitLife.Core.Models;
using FitLife.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FitLife.Tests;

public class DbSeederTests
{
    [Fact]
    public async Task SeedAsync_WithExistingRegisteredUser_AddsDemoCatalogAndIsIdempotent()
    {
        var options = new DbContextOptionsBuilder<FitLifeDbContext>()
            .UseInMemoryDatabase($"FitLifeSeederTests_{Guid.NewGuid():N}")
            .Options;
        await using var context = new FitLifeDbContext(options);
        context.Users.Add(new User
        {
            Id = "registered-user",
            Email = "registered@example.com",
            PasswordHash = "not-used"
        });
        await context.SaveChangesAsync();
        var seeder = new DbSeeder(
            context,
            NullLogger<DbSeeder>.Instance);

        await seeder.SeedAsync();

        (await context.Users.CountAsync()).Should().Be(6);
        (await context.Classes.CountAsync()).Should().Be(10);
        (await context.Interactions.CountAsync()).Should().BeGreaterThan(0);

        var interactionCount = await context.Interactions.CountAsync();
        var staleClass = await context.Classes.FirstAsync();
        staleClass.StartTime = DateTime.UtcNow.AddDays(-1);
        await context.SaveChangesAsync();
        await seeder.SeedAsync();

        (await context.Users.CountAsync()).Should().Be(6);
        (await context.Classes.CountAsync()).Should().Be(10);
        (await context.Interactions.CountAsync()).Should().Be(interactionCount);
        (await context.Classes.FindAsync(staleClass.Id))!
            .StartTime.Should().BeAfter(DateTime.UtcNow);
    }
}
