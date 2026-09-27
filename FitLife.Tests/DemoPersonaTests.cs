using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FitLife.Api.BackgroundServices;
using FitLife.Core.DTOs;
using FitLife.Core.Interfaces;
using FitLife.Core.Models;
using FitLife.Infrastructure.Data;
using FitLife.Infrastructure.Repositories;
using FitLife.Infrastructure.Services;
using Microsoft.Data.SqlClient;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;

namespace FitLife.Tests;

public class DemoPersonaDisabledTests : IClassFixture<FitLifeWebApplicationFactory>
{
    private readonly FitLifeWebApplicationFactory _factory;

    public DemoPersonaDisabledTests(FitLifeWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task DemoRoutes_AreNotFound_UnlessDemoModeIsEnabled()
    {
        var client = _factory.CreateClient();

        (await client.GetAsync("/api/demo/personas")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.PostAsync("/api/demo/personas/sarah/session", null)).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
    }
}

/// <summary>Demo tests run sequentially against one isolated seeded database.</summary>
public class DemoPersonaTests : IClassFixture<DemoPersonaTests.DemoFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly DemoFactory _factory;

    public DemoPersonaTests(DemoFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await new DbSeeder(scope.ServiceProvider.GetRequiredService<FitLifeDbContext>(),
            NullLogger<DbSeeder>.Instance).SeedAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Personas_AreListedWithoutAuthentication()
    {
        var response = await _factory.CreateClient().GetFromJsonAsync<ApiResponse<List<DemoPersonaDto>>>(
            "/api/demo/personas", Json);

        response!.Data!.Select(p => p.Id).Should().Equal("sarah", "mike", "emily");
    }

    [Fact]
    public async Task UnknownPersona_IsNotFound()
    {
        (await _factory.CreateClient().PostAsync("/api/demo/personas/user_004/session", null))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Session_IssuesAMemberToken_ThatCannotManageTheCatalog()
    {
        var (client, _) = await SessionAsync("sarah");

        var response = await client.PostAsJsonAsync("/api/classes", new CreateClassDto
        {
            Name = "Should not be created", Type = "Yoga", InstructorId = "x", InstructorName = "X",
            Level = "Beginner", StartTime = DateTime.UtcNow.AddDays(2), DurationMinutes = 30, Capacity = 10
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Personas_GetStableAndMateriallyDifferentTopRecommendations()
    {
        var sarah = await TopAsync("sarah");
        var mike = await TopAsync("mike");
        var sarahAgain = await TopAsync("sarah");

        sarah.Select(r => r.Class.Type).Should().OnlyContain(type => type == "Yoga" || type == "Pilates");
        mike.Select(r => r.Class.Type).Should().OnlyContain(type => type == "HIIT" || type == "Strength" || type == "Spin");
        sarah.Select(r => r.Class.Id).Should().NotIntersectWith(mike.Select(r => r.Class.Id));
        sarahAgain.Select(r => r.Class.Id).Should().Equal(sarah.Select(r => r.Class.Id));
        sarah[0].Reason.Should().Contain("preferred class types");
        sarah[0].Factors.Should().Contain(f => f.Key == ScoreFactorKeys.Instructor && f.Points == 20);
    }

    [Fact]
    public async Task NewSession_UndoesThePreviousVisitorsBookingAndRestoresEnrollment()
    {
        var (client, _) = await SessionAsync("mike");
        var baseline = await EnrollmentAsync("class_012");
        (await client.PostAsync("/api/classes/class_012/book", null)).EnsureSuccessStatusCode();
        (await EnrollmentAsync("class_012")).Should().Be(baseline + 1);

        await SessionAsync("mike");

        (await EnrollmentAsync("class_012")).Should().Be(baseline);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FitLifeDbContext>();
        (await db.Bookings.CountAsync(b => b.UserId == "user_002" && b.Status == BookingStatuses.Active))
            .Should().Be(0);
        (await db.Interactions.CountAsync(i => i.UserId == "user_002"))
            .Should().Be(DemoCatalog.History("user_002", DateTime.UtcNow).Count);
    }

    [Fact]
    public async Task NewSession_ReleasesSeatsOnClassesOutsideTheDemoCatalog()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FitLifeDbContext>();
            if (!await db.Classes.AnyAsync(c => c.Id == "operator_class"))
            {
                db.Classes.Add(new Class
                {
                    Id = "operator_class", Name = "Operator class", Type = "Yoga", Level = "All Levels",
                    InstructorId = "inst_x", InstructorName = "X", StartTime = DateTime.UtcNow.AddDays(3),
                    Capacity = 10, CurrentEnrollment = 0, IsActive = true
                });
                await db.SaveChangesAsync();
            }
        }
        var (client, _) = await SessionAsync("emily");
        var before = await EnrollmentAsync("operator_class");
        (await client.PostAsync("/api/classes/operator_class/book", null)).EnsureSuccessStatusCode();

        await SessionAsync("emily");

        (await EnrollmentAsync("operator_class")).Should().Be(before);
    }

    [SqlServerFact]
    public async Task ConcurrentSessions_OnSqlServer_AllSucceedAndLeaveEnrollmentConsistent()
    {
        var connectionString = new SqlConnectionStringBuilder(
            Environment.GetEnvironmentVariable("FITLIFE_SQLSERVER_TEST_CONNECTION")!)
        {
            InitialCatalog = $"FitLifeDemoReset_{Guid.NewGuid():N}"
        }.ConnectionString;
        var options = new DbContextOptionsBuilder<FitLifeDbContext>()
            .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()).Options;
        try
        {
            await using (var setup = new FitLifeDbContext(options))
            {
                await setup.Database.EnsureCreatedAsync();
                await new DbSeeder(setup, NullLogger<DbSeeder>.Instance).SeedAsync();
                // Make every catalog row differ from its canonical enrollment so each
                // reset writes to the shared rows and contends on their row versions.
                await setup.Classes.ExecuteUpdateAsync(c => c.SetProperty(x => x.CurrentEnrollment, 0));
            }

            var resets = Enumerable.Range(0, 8).Select(async i =>
            {
                await using var context = new FitLifeDbContext(options);
                var persona = DemoCatalog.Personas[i % DemoCatalog.Personas.Count];
                await new DemoPersonaService(context, NullLogger<DemoPersonaService>.Instance).ResetAsync(persona);
            });
            await Task.WhenAll(resets);

            await using var verify = new FitLifeDbContext(options);
            foreach (var classItem in await verify.Classes.ToListAsync())
                classItem.CurrentEnrollment.Should().Be(DemoCatalog.BaselineEnrollment(classItem.Id));
            // Same-persona resets must serialize, or history is duplicated.
            foreach (var persona in DemoCatalog.Personas)
                (await verify.Interactions.CountAsync(i => i.UserId == persona.UserId))
                    .Should().Be(DemoCatalog.History(persona.UserId, DateTime.UtcNow).Count);
        }
        finally
        {
            await using var cleanup = new FitLifeDbContext(options);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task ScheduledProfiler_AssignsEverySeededUserTheSegmentTheyAreSeededWith()
    {
        var seeded = DemoCatalog.Users(DateTime.UtcNow).ToDictionary(u => u.Id, u => u.Segment);
        var options = new DbContextOptionsBuilder<FitLifeDbContext>()
            .UseInMemoryDatabase($"ProfilerAgreement_{Guid.NewGuid():N}").Options;
        await using (var context = new FitLifeDbContext(options))
            await new DbSeeder(context, NullLogger<DbSeeder>.Instance).SeedAsync();

        var services = new ServiceCollection();
        services.AddDbContext<FitLifeDbContext>(o => o.UseInMemoryDatabase(
            options.FindExtension<Microsoft.EntityFrameworkCore.InMemory.Infrastructure.Internal.InMemoryOptionsExtension>()!.StoreName));
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IInteractionRepository, InteractionRepository>();
        services.AddScoped<IClassRepository, ClassRepository>();
        services.AddSingleton(Moq.Mock.Of<IRecommendationService>());
        await using var provider = services.BuildServiceProvider();
        var profiler = new UserProfilerService(NullLogger<UserProfilerService>.Instance,
            new ConfigurationBuilder().Build(), provider);

        await profiler.ProfileUsersBatchAsync(lookbackDays: 30, CancellationToken.None);

        await using var verify = new FitLifeDbContext(options);
        (await verify.Users.ToDictionaryAsync(u => u.Id, u => u.Segment)).Should().Equal(seeded);
    }

    private async Task<List<RecommendationDto>> TopAsync(string persona)
    {
        var (client, userId) = await SessionAsync(persona);
        var response = await client.GetFromJsonAsync<ApiResponse<List<RecommendationDto>>>(
            $"/api/recommendations/{userId}?limit=3", Json);
        return response!.Data!;
    }

    private async Task<(HttpClient Client, string UserId)> SessionAsync(string persona)
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsync($"/api/demo/personas/{persona}/session", null);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.Data!.Token);
        return (client, body.Data.User.Id);
    }

    private async Task<int> EnrollmentAsync(string classId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FitLifeDbContext>();
        return (await db.Classes.AsNoTracking().SingleAsync(c => c.Id == classId)).CurrentEnrollment;
    }

    public sealed class DemoFactory : FitLifeWebApplicationFactory
    {
        protected override IReadOnlyDictionary<string, string> StartupSettings =>
            new Dictionary<string, string> { ["Demo:Enabled"] = "true" };

        protected override string DatabaseName => "FitLifeDemoPersonaTests";

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                // Keep these tests independent of a local Redis instance.
                services.RemoveAll<ICacheService>();
                services.AddSingleton<ICacheService, InMemoryCache>();
            });
        }
    }

    private sealed class InMemoryCache : ICacheService
    {
        private readonly ConcurrentDictionary<string, string> _values = new();

        public Task<T?> GetAsync<T>(string key) where T : class =>
            Task.FromResult(_values.TryGetValue(key, out var json) ? JsonSerializer.Deserialize<T>(json) : null);

        public Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
        {
            _values[key] = JsonSerializer.Serialize(value);
            return Task.FromResult(true);
        }

        public Task<bool> DeleteAsync(string key) => Task.FromResult(_values.TryRemove(key, out _));

        public bool IsConnected => true;
    }
}
