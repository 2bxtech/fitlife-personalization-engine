using FitLife.Core.Interfaces;
using FitLife.Infrastructure.Cache;
using FitLife.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FitLife.Tests;

/// <summary>
/// Custom WebApplicationFactory that replaces external dependencies
/// (SQL Server, Redis, Kafka) with in-memory/fake implementations.
/// KafkaProducer is left as-is (uses app config, connects lazily, won't fail in tests).
/// </summary>
public class FitLifeWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove ALL EF Core and SqlServer related descriptors to prevent
            // the "multiple database providers registered" error.
            // Must check both ServiceType and ImplementationType since some
            // provider services are registered with factory delegates.
            var descriptorsToRemove = services.Where(d =>
            {
                var serviceTypeName = d.ServiceType.FullName ?? string.Empty;
                var implTypeName = d.ImplementationType?.FullName ?? string.Empty;
                return serviceTypeName.Contains("EntityFrameworkCore") ||
                       serviceTypeName.Contains("SqlServer") ||
                       implTypeName.Contains("EntityFrameworkCore") ||
                       implTypeName.Contains("SqlServer") ||
                       d.ServiceType == typeof(FitLifeDbContext) ||
                       d.ServiceType == typeof(DbContextOptions<FitLifeDbContext>);
            }).ToList();

            foreach (var descriptor in descriptorsToRemove)
                services.Remove(descriptor);

            // Add in-memory database for testing.
            // IMPORTANT: DB name must be captured outside the lambda so all
            // DbContext instances within this factory share the same database.
            var dbName = "FitLifeTestDb_" + Guid.NewGuid().ToString("N");
            services.AddDbContext<FitLifeDbContext>(options =>
            {
                options.UseInMemoryDatabase(dbName);
            });

            // Replace real Redis cache with in-memory fake
            services.RemoveAll<RedisCacheService>();
            services.RemoveAll<ICacheService>();
            services.AddSingleton<ICacheService, FakeCacheService>();

            // Remove hosted services (background workers) — not needed in tests
            services.RemoveAll<Microsoft.Extensions.Hosting.IHostedService>();
        });
    }
}

/// <summary>
/// In-memory cache service for testing
/// </summary>
public class FakeCacheService : ICacheService
{
    private readonly Dictionary<string, object> _cache = new();

    public bool IsConnected => true;

    public Task<T?> GetAsync<T>(string key) where T : class
    {
        if (_cache.TryGetValue(key, out var value))
            return Task.FromResult(value as T);
        return Task.FromResult<T?>(null);
    }

    public Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
    {
        _cache[key] = value;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(string key)
    {
        _cache.Remove(key);
        return Task.FromResult(true);
    }
}
