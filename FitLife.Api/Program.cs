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
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

// Configure CORS for development
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
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

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

// Health check endpoints for Kubernetes probes
app.MapHealthChecks("/health");

app.MapControllers();

// Graceful shutdown: Flush Kafka producer
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() =>
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Application shutting down - flushing Kafka producer");
    
    try
    {
        var kafkaProducer = app.Services.GetRequiredService<KafkaProducer>();
        kafkaProducer.Flush(TimeSpan.FromSeconds(30));
        logger.LogInformation("Kafka producer flushed successfully");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error flushing Kafka producer during shutdown");
    }
});

app.Run();
