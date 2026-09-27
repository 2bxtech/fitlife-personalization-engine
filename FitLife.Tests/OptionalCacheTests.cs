using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FitLife.Api.Configuration;
using FitLife.Core.DTOs;
using FitLife.Core.Interfaces;
using FitLife.Infrastructure.Cache;
using FitLife.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace FitLife.Tests;

/// <summary>Cache:Provider=None runs the demo without Redis.</summary>
public class OptionalCacheTests : IClassFixture<OptionalCacheTests.NoCacheFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly NoCacheFactory _factory;

    public OptionalCacheTests(NoCacheFactory factory) => _factory = factory;

    [Fact]
    public void WithoutACache_NoRedisClientOrReadinessCheckIsRegistered()
    {
        _factory.Services.GetRequiredService<ICacheService>().Should().BeOfType<NoOpCacheService>();
        _factory.Services.GetService<RedisCacheService>().Should().BeNull();
        var checks = _factory.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;
        checks.Select(c => c.Name).Should().BeEquivalentTo("self", "database");
    }

    [Fact]
    public async Task WithoutACache_ReadinessIsHealthy_AndAPersonaGetsRecommendations()
    {
        var client = _factory.CreateClient();
        (await client.GetAsync("/health/ready")).StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = _factory.Services.CreateScope())
        {
            await new DbSeeder(scope.ServiceProvider.GetRequiredService<FitLifeDbContext>(),
                NullLogger<DbSeeder>.Instance).SeedAsync();
        }
        var session = await client.PostAsync("/api/demo/personas/mike/session", null);
        session.EnsureSuccessStatusCode();
        var auth = (await session.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>(Json))!.Data!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);

        var first = await client.GetFromJsonAsync<ApiResponse<List<RecommendationDto>>>(
            $"/api/recommendations/{auth.User.Id}?limit=3", Json);
        var second = await client.GetFromJsonAsync<ApiResponse<List<RecommendationDto>>>(
            $"/api/recommendations/{auth.User.Id}?limit=3", Json);

        first!.Data.Should().HaveCount(3);
        second!.Data!.Select(r => r.Class.Id).Should().Equal(first.Data!.Select(r => r.Class.Id));
    }

    [Fact]
    public void ProductionWithoutACache_DoesNotRequireRedis()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Secret"] = new string('k', 48),
            ["ConnectionStrings:DefaultConnection"] = "Server=tcp:fitlife.database.windows.net;Database=FitLifeDb",
            ["Events:Transport"] = "Direct",
            ["Cache:Provider"] = "None"
        }).Build();
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns("Production");

        var validate = () => ProductionConfigurationValidator.Validate(config, environment.Object, ProcessRole.Api);

        validate.Should().NotThrow();
    }

    [Fact]
    public void SchedulerRole_WithoutACache_RegistersNoRedisClient()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Cache:Provider"] = "None"
        }).Build();
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns("Testing");
        environment.SetupGet(e => e.ContentRootPath).Returns(AppContext.BaseDirectory);

        var services = WorkerApplication.CreateBuilder(config, environment.Object, ProcessRole.Scheduler).Services;

        services.Should().Contain(d => d.ServiceType == typeof(ICacheService) && d.ImplementationType == typeof(NoOpCacheService));
        services.Should().NotContain(d => d.ServiceType == typeof(RedisCacheService) || d.ServiceType == typeof(IRedisHealthProbe));
    }

    [Theory]
    [InlineData("none")]
    [InlineData("Memory")]
    [InlineData("")]
    public void UnknownCacheProvider_FailsClosed(string value)
    {
        var read = () => CacheMode.Read(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Cache:Provider"] = value }).Build());
        read.Should().Throw<InvalidOperationException>();
    }

    public sealed class NoCacheFactory : FitLifeWebApplicationFactory
    {
        protected override IReadOnlyDictionary<string, string> StartupSettings => new Dictionary<string, string>
        {
            ["Cache:Provider"] = "None",
            ["Demo:Enabled"] = "true"
        };

        protected override string DatabaseName => "FitLifeOptionalCacheTests";
    }
}
