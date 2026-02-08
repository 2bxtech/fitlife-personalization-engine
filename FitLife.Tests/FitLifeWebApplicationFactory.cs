using FitLife.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
        });
    }
}