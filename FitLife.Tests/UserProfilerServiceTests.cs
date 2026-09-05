using FitLife.Api.BackgroundServices;
using FitLife.Core.Interfaces;
using FitLife.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FitLife.Tests;

public class UserProfilerServiceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ChangedSegment_IsSavedBeforeInvalidation_AndSaveFailureDoesNotInvalidate(bool failSave)
    {
        var user = new User { Id = "persona", Segment = "General" };
        var users = new Mock<IUserRepository>();
        users.Setup(r => r.GetAllAsync()).ReturnsAsync(new[] { user });
        var history = new Mock<IInteractionRepository>();
        history.Setup(r => r.GetRecentByUserIdAsync(user.Id, 30)).ReturnsAsync(new List<Interaction>());
        var recommendations = new Mock<IRecommendationService>(MockBehavior.Strict);
        var saved = false;
        users.Setup(r => r.SaveChangesAsync()).Returns(() =>
        {
            if (failSave) throw new InvalidOperationException("forced save failure");
            saved = true;
            return Task.FromResult(1);
        });
        recommendations.Setup(r => r.InvalidateCacheAsync(user.Id)).Returns(() =>
        {
            Assert.True(saved);
            Assert.Equal("Beginner", user.Segment);
            return Task.CompletedTask;
        });
        var services = new ServiceCollection();
        services.AddSingleton(users.Object);
        services.AddSingleton(history.Object);
        services.AddSingleton(Mock.Of<IClassRepository>());
        services.AddSingleton(recommendations.Object);
        await using var provider = services.BuildServiceProvider();
        using var worker = new UserProfilerService(NullLogger<UserProfilerService>.Instance,
            new ConfigurationBuilder().Build(), provider);
        await worker.ProfileUsersBatchAsync(30, CancellationToken.None);
        users.Verify(r => r.SaveChangesAsync(), Times.Once);
        recommendations.Verify(r => r.InvalidateCacheAsync(user.Id), failSave ? Times.Never() : Times.Once());
    }
}
