using FitLife.Infrastructure.Data;
using FitLife.Core.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection.Extensions;
using FitLife.Core.Models;

namespace FitLife.Tests;

/// <summary>
/// Custom WebApplicationFactory for integration tests.
/// Configures the test environment with an in-memory database.
/// Program.cs skips SQL Server registration when environment is "Testing",
/// so we only need to register the InMemory provider here.
/// </summary>
public class FitLifeWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DemoAuthorization:OperatorEmails:0"] = "operator@example.com"
            });
        });
        
        builder.ConfigureServices(services =>
        {
            // Remove hosted services (background workers) to avoid interference during tests
            var hostedServices = services
                .Where(d => d.ServiceType == typeof(IHostedService))
                .ToList();
            foreach (var descriptor in hostedServices)
            {
                services.Remove(descriptor);
            }

            // Register InMemory database for testing
            // SQL Server is not registered because Program.cs checks for Testing environment
            services.AddDbContext<FitLifeDbContext>(options =>
            {
                options.UseInMemoryDatabase("FitLifeTestDb");
            });

            services.RemoveAll<IRedisHealthProbe>();
            services.AddSingleton<IRedisHealthProbe, HealthyRedisHealthProbe>();
            services.RemoveAll<IEventPublisher>();
            services.AddSingleton<RecordingEventPublisher>();
            services.AddSingleton<IEventPublisher>(serviceProvider =>
                serviceProvider.GetRequiredService<RecordingEventPublisher>());
        });
    }

    private sealed class HealthyRedisHealthProbe : IRedisHealthProbe
    {
        public Task<TimeSpan> PingAsync() => Task.FromResult(TimeSpan.Zero);
    }
}

public sealed class RecordingEventPublisher : IEventPublisher
{
    public List<UserEvent> PublishedEvents { get; } = new();
    public bool FailNextPublish { get; set; }

    public Task PublishAsync(
        string topic,
        string key,
        UserEvent userEvent,
        CancellationToken cancellationToken = default)
    {
        if (FailNextPublish)
        {
            FailNextPublish = false;
            throw new InvalidOperationException("Forced publisher failure");
        }

        PublishedEvents.Add(userEvent);
        return Task.CompletedTask;
    }

    public void Reset()
    {
        PublishedEvents.Clear();
        FailNextPublish = false;
    }
}
