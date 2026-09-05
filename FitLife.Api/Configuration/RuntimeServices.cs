using FitLife.Core.Interfaces;
using FitLife.Core.Services;
using FitLife.Infrastructure.Cache;
using FitLife.Infrastructure.Data;
using FitLife.Infrastructure.Kafka;
using FitLife.Infrastructure.Repositories;
using FitLife.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace FitLife.Api.Configuration;

public static class RuntimeServices
{
    public static void AddFitLifeRuntime(this IServiceCollection services,
        IConfiguration configuration, IHostEnvironment environment, ProcessRole role)
    {
        // Configure Entity Framework Core with SQL Server
        // Skip registration in Testing environment — tests register InMemory provider instead
        if (!environment.IsEnvironment("Testing"))
        {
            services.AddDbContext<FitLifeDbContext>(options =>
                options.UseSqlServer(
                    configuration.GetConnectionString("DefaultConnection"),
                    sqlOptions => sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorNumbersToAdd: null
                    )
                )
            );
        }

        // Register repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IClassRepository, ClassRepository>();
        services.AddScoped<IInteractionRepository, InteractionRepository>();
        services.AddScoped<IRecommendationRepository, RecommendationRepository>();
        services.AddScoped<IBookingService, BookingService>();

        // Register core services
        services.AddScoped<IScoringEngine, ScoringEngine>();
        services.AddScoped<IRecommendationService, RecommendationService>();

        if (role != ProcessRole.Scheduler)
        {
            // Register Kafka producer (singleton - connection pooling)
            services.AddSingleton<KafkaProducer>();
            services.AddSingleton<IEventPublisher>(sp =>
                sp.GetRequiredService<KafkaProducer>());
            services.AddSingleton<IDeadLetterPublisher>(sp =>
                sp.GetRequiredService<KafkaProducer>());
        }

        // Register Redis cache service (singleton - connection pooling)
        services.AddSingleton<RedisCacheService>();
        services.AddSingleton<ICacheService>(sp => sp.GetRequiredService<RedisCacheService>());
        services.AddSingleton<IRedisHealthProbe>(sp => sp.GetRequiredService<RedisCacheService>());
    }
}
