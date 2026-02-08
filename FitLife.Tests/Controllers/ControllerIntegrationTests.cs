using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FitLife.Core.DTOs;
using FitLife.Core.Models;
using FitLife.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FitLife.Tests.Controllers;

/// <summary>
/// Integration tests for AuthController (register + login)
/// </summary>
public class AuthControllerTests : IClassFixture<FitLifeWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly FitLifeWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public AuthControllerTests(FitLifeWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_ValidData_ReturnsTokenAndUser()
    {
        var dto = new RegisterUserDto
        {
            Email = $"test_{Guid.NewGuid():N}@example.com",
            Password = "TestPass123!",
            FirstName = "Test",
            LastName = "User",
            FitnessLevel = "Beginner",
            PreferredClassTypes = new List<string> { "Yoga", "HIIT" }
        };

        var response = await _client.PostAsJsonAsync("/api/auth/register", dto);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>(JsonOptions);
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
        body.Data!.Token.Should().NotBeNullOrEmpty();
        body.Data.User.Should().NotBeNull();
        body.Data.User!.Email.Should().Be(dto.Email);
        body.Data.User.FirstName.Should().Be("Test");
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsConflict()
    {
        var email = $"dup_{Guid.NewGuid():N}@example.com";
        var dto = new RegisterUserDto
        {
            Email = email,
            Password = "TestPass123!",
            FirstName = "First",
            LastName = "User",
            FitnessLevel = "Beginner"
        };

        // First registration
        var first = await _client.PostAsJsonAsync("/api/auth/register", dto);
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        // Duplicate registration
        var second = await _client.PostAsJsonAsync("/api/auth/register", dto);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Register_MissingEmail_ReturnsBadRequest()
    {
        var dto = new RegisterUserDto
        {
            Email = "",
            Password = "TestPass123!",
            FirstName = "Test",
            LastName = "User",
            FitnessLevel = "Beginner"
        };

        var response = await _client.PostAsJsonAsync("/api/auth/register", dto);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsToken()
    {
        var email = $"login_{Guid.NewGuid():N}@example.com";
        var password = "TestPass123!";

        // Register first
        await _client.PostAsJsonAsync("/api/auth/register", new RegisterUserDto
        {
            Email = email,
            Password = password,
            FirstName = "Login",
            LastName = "Test",
            FitnessLevel = "Intermediate"
        });

        // Login
        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginDto
        {
            Email = email,
            Password = password
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>(JsonOptions);
        body!.Success.Should().BeTrue();
        body.Data!.Token.Should().NotBeNullOrEmpty();
        body.Data.User!.Email.Should().Be(email);
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsUnauthorized()
    {
        var email = $"wrong_{Guid.NewGuid():N}@example.com";

        // Register
        await _client.PostAsJsonAsync("/api/auth/register", new RegisterUserDto
        {
            Email = email,
            Password = "CorrectPass123!",
            FirstName = "Wrong",
            LastName = "Pass",
            FitnessLevel = "Beginner"
        });

        // Login with wrong password
        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginDto
        {
            Email = email,
            Password = "WrongPass999!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_NonExistentUser_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginDto
        {
            Email = "nonexistent@example.com",
            Password = "Whatever123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

/// <summary>
/// Integration tests for ClassesController
/// </summary>
public class ClassesControllerTests : IClassFixture<FitLifeWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly FitLifeWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public ClassesControllerTests(FitLifeWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<string> GetAuthTokenAsync()
    {
        var email = $"class_{Guid.NewGuid():N}@example.com";
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterUserDto
        {
            Email = email,
            Password = "TestPass123!",
            FirstName = "Class",
            LastName = "Tester",
            FitnessLevel = "Intermediate"
        });
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>(JsonOptions);
        return body!.Data!.Token;
    }

    private async Task SeedClassAsync(string id, string name = "Yoga Flow", string type = "Yoga",
        string level = "Intermediate", int capacity = 30, int enrollment = 5)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<FitLifeDbContext>();
        if (!context.Classes.Any(c => c.Id == id))
        {
            context.Classes.Add(new Class
            {
                Id = id,
                Name = name,
                Type = type,
                Level = level,
                InstructorId = "inst_1",
                InstructorName = "Sarah",
                Description = "Test class",
                StartTime = DateTime.UtcNow.AddDays(1),
                DurationMinutes = 60,
                Capacity = capacity,
                CurrentEnrollment = enrollment,
                AverageRating = 4.5m,
                IsActive = true
            });
            await context.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task GetClasses_NoAuth_ReturnsOk()
    {
        // GET /api/classes should be [AllowAnonymous]
        var response = await _client.GetAsync("/api/classes");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetClasses_WithTypeFilter_ReturnsFilteredResults()
    {
        var classId = Guid.NewGuid().ToString();
        await SeedClassAsync(classId, "HIIT Blast", "HIIT", "Advanced");

        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/classes?type=HIIT");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<ClassDto>>>(JsonOptions);
        body!.Data.Should().NotBeNull();
        body.Data!.Should().AllSatisfy(c => c.Type.Should().Be("HIIT"));
    }

    [Fact]
    public async Task BookClass_Authenticated_ReturnsOk()
    {
        var classId = $"book_{Guid.NewGuid():N}";
        await SeedClassAsync(classId, capacity: 30, enrollment: 5);

        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsync($"/api/classes/{classId}/book", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ClassDto>>(JsonOptions);
        body!.Success.Should().BeTrue();
        body.Data!.CurrentEnrollment.Should().Be(6); // Was 5, now 6
    }

    [Fact]
    public async Task BookClass_NoAuth_ReturnsUnauthorized()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.PostAsync("/api/classes/some-id/book", null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task BookClass_FullClass_ReturnsBadRequest()
    {
        var classId = $"full_{Guid.NewGuid():N}";
        await SeedClassAsync(classId, capacity: 10, enrollment: 10);

        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsync($"/api/classes/{classId}/book", null);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task BookClass_NonExistentClass_ReturnsNotFound()
    {
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsync("/api/classes/nonexistent/book", null);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

/// <summary>
/// Integration tests for UsersController (auth + ownership)
/// </summary>
public class UsersControllerTests : IClassFixture<FitLifeWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly FitLifeWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public UsersControllerTests(FitLifeWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(string Token, UserDto User)> RegisterAndGetAuthAsync()
    {
        var email = $"user_{Guid.NewGuid():N}@example.com";
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterUserDto
        {
            Email = email,
            Password = "TestPass123!",
            FirstName = "User",
            LastName = "Tester",
            FitnessLevel = "Beginner",
            PreferredClassTypes = new List<string> { "Yoga" }
        });
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>(JsonOptions);
        return (body!.Data!.Token, body.Data.User!);
    }

    [Fact]
    public async Task GetUser_NoAuth_ReturnsUnauthorized()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.GetAsync("/api/users/some-id");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUser_OwnProfile_ReturnsOk()
    {
        var (token, user) = await RegisterAndGetAuthAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync($"/api/users/{user.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<UserDto>>(JsonOptions);
        body!.Data!.Email.Should().Be(user.Email);
    }

    [Fact]
    public async Task GetUser_OtherProfile_ReturnsForbidden()
    {
        var (token, _) = await RegisterAndGetAuthAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/users/some-other-user-id");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdatePreferences_OwnProfile_ReturnsOk()
    {
        var (token, user) = await RegisterAndGetAuthAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var prefs = new UpdateUserPreferencesDto
        {
            FitnessLevel = "Advanced",
            PreferredClassTypes = new List<string> { "HIIT", "Strength" },
            Goals = new List<string> { "Build muscle" }
        };

        var response = await _client.PutAsJsonAsync($"/api/users/{user.Id}/preferences", prefs);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<UserDto>>(JsonOptions);
        body!.Data!.FitnessLevel.Should().Be("Advanced");
        body.Data.PreferredClassTypes.Should().Contain("HIIT");
    }
}

/// <summary>
/// Integration tests for RecommendationsController
/// </summary>
public class RecommendationsControllerTests : IClassFixture<FitLifeWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly FitLifeWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public RecommendationsControllerTests(FitLifeWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(string Token, UserDto User)> RegisterAndGetAuthAsync()
    {
        var email = $"rec_{Guid.NewGuid():N}@example.com";
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterUserDto
        {
            Email = email,
            Password = "TestPass123!",
            FirstName = "Rec",
            LastName = "Tester",
            FitnessLevel = "Intermediate",
            PreferredClassTypes = new List<string> { "Yoga" }
        });
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>(JsonOptions);
        return (body!.Data!.Token, body.Data.User!);
    }

    [Fact]
    public async Task GetRecommendations_NoAuth_ReturnsUnauthorized()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.GetAsync("/api/recommendations/some-user");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetRecommendations_Authenticated_ReturnsOk()
    {
        var (token, user) = await RegisterAndGetAuthAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync($"/api/recommendations/{user.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<RecommendationDto>>>(JsonOptions);
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task RefreshRecommendations_Authenticated_ReturnsOk()
    {
        var (token, user) = await RegisterAndGetAuthAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Seed some classes for recommendations
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<FitLifeDbContext>();
        for (int i = 0; i < 3; i++)
        {
            context.Classes.Add(new Class
            {
                Id = $"rec_class_{Guid.NewGuid():N}",
                Name = $"Yoga Session {i}",
                Type = "Yoga",
                Level = "Intermediate",
                InstructorId = "inst_1",
                InstructorName = "Sarah",
                Description = "A relaxing yoga class",
                StartTime = DateTime.UtcNow.AddDays(i + 1),
                DurationMinutes = 60,
                Capacity = 30,
                CurrentEnrollment = 10,
                AverageRating = 4.5m,
                IsActive = true
            });
        }
        await context.SaveChangesAsync();

        var response = await _client.PostAsync($"/api/recommendations/{user.Id}/refresh", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
