using FitLife.Api.Configuration;
using FitLife.Api.Health;
using FitLife.Core.Interfaces;
using FitLife.Core.Auth;
using FitLife.Infrastructure.Auth;
using FitLife.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text;
using AspNetCoreRateLimit;

var builder = WebApplication.CreateBuilder(args);

var processRole = ProcessTopology.ReadRole(builder.Configuration);
ProductionConfigurationValidator.Validate(builder.Configuration, builder.Environment, processRole);
ProcessTopology.Validate(builder.Configuration, processRole);

if (processRole != ProcessRole.Api)
{
    if (args.Contains("--seed"))
        throw new InvalidOperationException("Seeding must run in the Api role.");

    // A generic host starts no HTTP listener and never runs API migrations/seeding.
    var workerBuilder = WorkerApplication.CreateBuilder(
        builder.Configuration, builder.Environment, processRole);
    using var workerHost = workerBuilder.Build();
    var workers = workerHost.Services.GetServices<IHostedService>().OfType<BackgroundService>().ToArray();
    await workerHost.RunAsync();
    if (workers.Any(worker => worker.ExecuteTask?.IsFaulted == true))
        Environment.ExitCode = 1;
    return;
}

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

builder.Services.AddFitLifeRuntime(builder.Configuration, builder.Environment, processRole);

// Register JWT service
builder.Services.AddSingleton<IJwtService, JwtService>();

// The API role deliberately registers no background workers.

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

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(FitLifePolicies.ManageCatalog, policy =>
        policy.RequireRole(FitLifeRoles.Operator));
});

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

// Add dependency-independent liveness and dependency-aware readiness checks
builder.Services.AddHealthChecks()
    .AddCheck(
        "self",
        () => HealthCheckResult.Healthy("Process is running"),
        tags: new[] { "live" })
    .AddCheck<DatabaseHealthCheck>("database", tags: new[] { "ready" })
    .AddCheck<RedisHealthCheck>("redis", tags: new[] { "ready" });

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

// Local Vite development proxies to the HTTP launch profile. Redirecting its
// CORS preflight to HTTPS makes browsers reject the request before CORS runs.
if (!app.Environment.IsDevelopment()
    && !app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpsRedirection();
}

app.UseCors("AllowFrontend");

// Rate limiting middleware
app.UseIpRateLimiting();

app.UseAuthentication();
app.UseAuthorization();

// /health remains a compatibility alias for dependency-aware readiness.
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
});
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("live")
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
});

app.MapControllers();

// DI disposes and flushes an instantiated Kafka producer after hosted work stops.
app.Run();

// Make Program class accessible for WebApplicationFactory in integration tests
public partial class Program { }
