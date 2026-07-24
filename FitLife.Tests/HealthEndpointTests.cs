using System.Net;
using FitLife.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FitLife.Tests;

public class HealthEndpointTests : IClassFixture<FitLifeWebApplicationFactory>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(FitLifeWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/health/ready")]
    public async Task Readiness_HealthyDependencies_ReturnsOk(string path)
    {
        var response = await _client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Liveness_HealthyProcess_ReturnsOk()
    {
        var response = await _client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DependencyFailure_AffectsReadinessButNotLiveness()
    {
        await using var factory = new UnhealthyRedisWebApplicationFactory();
        using var client = factory.CreateClient();

        var readiness = await client.GetAsync("/health/ready");
        var liveness = await client.GetAsync("/health/live");

        readiness.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        liveness.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private sealed class UnhealthyRedisWebApplicationFactory
        : FitLifeWebApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IRedisHealthProbe>();
                services.AddSingleton<IRedisHealthProbe, UnhealthyRedisHealthProbe>();
            });
        }
    }

    private sealed class UnhealthyRedisHealthProbe : IRedisHealthProbe
    {
        public Task<TimeSpan> PingAsync() =>
            throw new InvalidOperationException("Redis unavailable for test");
    }
}
