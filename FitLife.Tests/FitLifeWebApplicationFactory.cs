using FitLife.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace FitLife.Tests;

/// <summary>
/// Custom WebApplicationFactory for integration tests.
/// Configures the test environment with an in-memory database.
/// </summary>
public class FitLifeWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        
        builder.ConfigureServices(services =>
        {
            // Remove the existing DbContext registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<FitLifeDbContext>));
            
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Add DbContext using in-memory database for testing
            services.AddDbContext<FitLifeDbContext>(options =>
            {
                options.UseInMemoryDatabase("FitLifeTestDb");
            });

            // Build service provider and ensure database is created
            var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FitLifeDbContext>();
            db.Database.EnsureCreated();
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Suppress background services during testing to avoid interference
        builder.ConfigureServices(services =>
        {
            // Remove hosted services (background workers)
            var hostedServices = services
                .Where(descriptor => descriptor.ServiceType == typeof(IHostedService))
                .ToList();
            
            foreach (var service in hostedServices)
            {
                services.Remove(service);
            }
        });

        return base.CreateHost(builder);
    }
}