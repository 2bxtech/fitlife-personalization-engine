using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using FluentAssertions;

namespace FitLife.Tests;

/// <summary>
/// Rate-limit identity must come from the connection (or a trusted proxy), never
/// from headers the client controls.
/// </summary>
public class RateLimitTests
{
    [Fact]
    public async Task RotatingSpoofedIpHeaders_DoesNotEvadeThePerIpLimit()
    {
        using var factory = new FixedClientIpFactory(trustForwardedHeaders: false);
        var client = factory.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 15; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
            request.Headers.Add("X-Real-IP", $"198.51.100.{i}");
            request.Headers.Add("X-ClientId", $"client-{i}");
            request.Headers.Add("X-Forwarded-For", $"192.0.2.{i}");
            statuses.Add((await client.SendAsync(request)).StatusCode);
        }

        statuses.Should().Contain(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task BehindATrustedProxy_ClientPrependedForwardedValuesAreIgnored()
    {
        using var factory = new FixedClientIpFactory(trustForwardedHeaders: true);
        var client = factory.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 15; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
            // The client prepends a rotating value; the proxy appends the real address.
            request.Headers.Add("X-Forwarded-For", $"192.0.2.{i}, 203.0.113.50");
            statuses.Add((await client.SendAsync(request)).StatusCode);
        }

        statuses.Should().Contain(HttpStatusCode.TooManyRequests);
    }

    private sealed class FixedClientIpFactory : FitLifeWebApplicationFactory
    {
        private readonly bool _trustForwardedHeaders;

        public FixedClientIpFactory(bool trustForwardedHeaders) => _trustForwardedHeaders = trustForwardedHeaders;

        protected override IReadOnlyDictionary<string, string> StartupSettings =>
            new Dictionary<string, string>
            {
                ["ReverseProxy:TrustForwardedHeaders"] = _trustForwardedHeaders ? "true" : "false"
            };

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            // TestServer has no socket; give every request the same connection address.
            builder.ConfigureServices(services =>
                services.AddSingleton<IStartupFilter>(new FixedRemoteIpStartupFilter()));
        }
    }

    private sealed class FixedRemoteIpStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse("10.1.2.3");
                return nextMiddleware(context);
            });
            next(app);
        };
    }
}
