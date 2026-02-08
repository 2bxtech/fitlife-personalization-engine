using FitLife.Api.BackgroundServices;
using FitLife.Core.Interfaces;
using FitLife.Core.Services;
using FitLife.Infrastructure.Auth;
using FitLife.Infrastructure.Cache;
using FitLife.Infrastructure.Data;
using FitLife.Infrastructure.Kafka;
using FitLife.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using AspNetCoreRateLimit;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configure Swagger with JWT authentication
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "FitLife Personalization Engine API",
        Version = "v1",
        Description = "AI-powered gym class recommendation system with real-time personalization",
        Contact = new OpenApiContact
        {
            Name = "FitLife Team",
            Email = "support@fitlife.com"
        }
    });

    // Add JWT authentication to Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Configure Entity Framework Core with SQL Server
builder.Services.AddDbContext<FitLifeDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorNumbersToAdd: null
        )
    )
);

// Register repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IClassRepository, ClassRepository>();
builder.Services.AddScoped<IInteractionRepository, InteractionRepository>();
builder.Services.AddScoped<IRecommendationRepository, RecommendationRepository>();

// Register core services
builder.Services.AddScoped<IScoringEngine, ScoringEngine>();
builder.Services.AddScoped<IRecommendationService, RecommendationService>();

// Register Kafka producer (singleton - connection pooling)
builder.Services.AddSingleton<KafkaProducer>();

// Register Redis cache service (singleton - connection pooling)
builder.Services.AddSingleton<RedisCacheService>();
builder.Services.AddSingleton<ICacheService>(sp => sp.GetRequiredService<RedisCacheService>());

// Register JWT service
builder.Services.AddSingleton<IJwtService, JwtService>();

// Register background workers
builder.Services.AddHostedService<EventConsumerService>();
builder.Services.AddHostedService<RecommendationGeneratorService>();
builder.Services.AddHostedService<UserProfilerService>();

// Configure JWT Authentication
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? throw new InvalidOperationException("JWT Secret not configured");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "FitLife.Api";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "FitLife.Client";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// Configure rate limiting
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(options =>
{
    options.EnableEndpointRateLimiting = true;
    options.StackBlockedRequests = false;
    options.HttpStatusCode = 429;
    options.RealIpHeader = "X-Real-IP";
    options.ClientIdHeader = "X-ClientId";
    options.GeneralRules = new List<RateLimitRule>
    {
        new RateLimitRule
        {
            Endpoint = "*",
            Period = "1s",
            Limit = 10 // 10 requests per second per IP
        },
        new RateLimitRule
        {
            Endpoint = "*",
            Period = "1m",
            Limit = 100 // 100 requests per minute per IP
        }
    };
});

builder.Services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
builder.Services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
builder.Services.AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>();

// Configure CORS for production
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() 
    ?? new[] { "http://localhost:3000", "http://localhost:5173" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials()
              .WithExposedHeaders("X-Correlation-ID");
    });
});

// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck("database", () =>
    {
        try
        {
            using var scope = builder.Services.BuildServiceProvider().CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<FitLifeDbContext>();
            context.Database.CanConnect();
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Database connection is healthy");
        }
        catch (Exception ex)
        {
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("Database connection failed", ex);
        }
    })
    .AddCheck("redis", () =>
    {
        try
        {
            var redis = builder.Services.BuildServiceProvider().GetRequiredService<RedisCacheService>();
            return redis.IsConnected
                ? Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Redis connection is healthy")
                : Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("Redis is not connected");
        }
        catch (Exception ex)
        {
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("Redis check failed", ex);
        }
    });

var app = builder.Build();

// Auto-apply pending migrations at startup (skip in Testing environment)
if (!app.Environment.IsEnvironment("Testing"))
{
    using (var migrationScope = app.Services.CreateScope())
    {
        var db = migrationScope.ServiceProvider.GetRequiredService<FitLifeDbContext>();
        var migrationLogger = migrationScope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        try
        {
            migrationLogger.LogInformation("Applying pending database migrations...");
            await db.Database.MigrateAsync();
            migrationLogger.LogInformation("Database migrations applied successfully");
        }
        catch (Exception ex)
        {
            migrationLogger.LogError(ex, "Error applying database migrations");
            throw;
        }
    }
}

// Seed database if --seed argument is provided
if (args.Contains("--seed"))
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    
    try
    {
        logger.LogInformation("Seeding database...");
        var context = services.GetRequiredService<FitLifeDbContext>();
        var seeder = new FitLife.Infrastructure.Data.DbSeeder(context, services.GetRequiredService<ILogger<FitLife.Infrastructure.Data.DbSeeder>>());
        await seeder.SeedAsync();
        logger.LogInformation("Database seeded successfully!");
        
        // Exit after seeding
        return;
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while seeding the database");
        throw;
    }
}

// Add correlation ID middleware (before other middleware)
app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
        ?? Guid.NewGuid().ToString();
    
    context.Items["CorrelationId"] = correlationId;
    context.Response.Headers.Append("X-Correlation-ID", correlationId);
    
    await next();
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "FitLife API V1");
        c.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();

app.UseCors("AllowFrontend");

// Rate limiting middleware
app.UseIpRateLimiting();

app.UseAuthentication();
app.UseAuthorization();

// Health check endpoints for Kubernetes probes
app.MapHealthChecks("/health");

app.MapControllers();

// Graceful shutdown: Flush Kafka producer
// Capture references BEFORE app.Run() to avoid ObjectDisposedException in the callback
var kafkaProducerForShutdown = app.Services.GetRequiredService<KafkaProducer>();
var shutdownLogger = app.Services.GetRequiredService<ILogger<Program>>();
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() =>
{
    shutdownLogger.LogInformation("Application shutting down - flushing Kafka producer");
    
    try
    {
        kafkaProducerForShutdown.Flush(TimeSpan.FromSeconds(30));
        shutdownLogger.LogInformation("Kafka producer flushed successfully");
    }
    catch (Exception ex)
    {
        shutdownLogger.LogError(ex, "Error flushing Kafka producer during shutdown");
    }
});

app.Run();

// Make Program class accessible for WebApplicationFactory in integration tests
public partial class Program { }