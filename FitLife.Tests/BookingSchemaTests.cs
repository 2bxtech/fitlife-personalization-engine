using FitLife.Core.Models;
using FitLife.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace FitLife.Tests;

public class BookingSchemaTests
{
    [Fact]
    public void Model_EnforcesActiveBookingAndIdempotencyUniqueness()
    {
        using var context = CreateSqlServerContext();
        var booking = DesignTimeModel(context).FindEntityType(typeof(Booking));

        booking.Should().NotBeNull();
        booking!.GetIndexes().Should().Contain(index =>
            index.IsUnique
            && index.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { nameof(Booking.UserId), nameof(Booking.ClassId) })
            && index.GetFilter() == "[Status] = 'Active'");
        booking.GetIndexes().Should().Contain(index =>
            index.IsUnique
            && index.Properties.Single().Name == nameof(Booking.IdempotencyKey)
            && index.GetFilter() == "[IdempotencyKey] IS NOT NULL");
    }

    [Fact]
    public void Model_EnforcesBookingRelationshipsAndStatusValues()
    {
        using var context = CreateSqlServerContext();
        var booking = DesignTimeModel(context).FindEntityType(typeof(Booking))!;

        booking.GetForeignKeys().Should().Contain(foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(User)
            && foreignKey.DeleteBehavior == DeleteBehavior.Cascade);
        booking.GetForeignKeys().Should().Contain(foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Class)
            && foreignKey.DeleteBehavior == DeleteBehavior.Restrict);
        booking.GetCheckConstraints().Should().Contain(constraint =>
            constraint.Name == "CK_Bookings_Status"
            && constraint.Sql == "[Status] IN ('Active', 'Cancelled')");
    }

    [Fact]
    public void Model_EnforcesClassCapacityAndOptimisticConcurrency()
    {
        using var context = CreateSqlServerContext();
        var classEntity = DesignTimeModel(context).FindEntityType(typeof(Class))!;
        var rowVersion = classEntity.FindProperty(nameof(Class.RowVersion));

        rowVersion.Should().NotBeNull();
        rowVersion!.IsConcurrencyToken.Should().BeTrue();
        rowVersion.ValueGenerated.Should().Be(ValueGenerated.OnAddOrUpdate);
        classEntity.GetCheckConstraints().Should().Contain(constraint =>
            constraint.Name == "CK_Classes_Enrollment_WithinCapacity"
            && constraint.Sql ==
            "[CurrentEnrollment] >= 0 AND [CurrentEnrollment] <= [Capacity]");
    }

    [Fact]
    public void Model_MatchesLatestMigrationSnapshot()
    {
        using var context = CreateSqlServerContext();

        context.Database.HasPendingModelChanges().Should().BeFalse();
    }

    private static FitLifeDbContext CreateSqlServerContext()
    {
        var options = new DbContextOptionsBuilder<FitLifeDbContext>()
            .UseSqlServer(
                "Server=localhost;Database=FitLifeSchemaTests;User Id=test;Password=test;TrustServerCertificate=True")
            .Options;

        return new FitLifeDbContext(options);
    }

    private static IModel DesignTimeModel(FitLifeDbContext context) =>
        context.GetService<IDesignTimeModel>().Model;
}
