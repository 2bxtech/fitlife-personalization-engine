using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FitLife.Core.DTOs;
using FitLife.Core.Models;
using FitLife.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace FitLife.Tests.Controllers;

internal static class AuthenticationTestTokens
{
    public static string WithoutSubject()
    {
        const string secret = "your-256-bit-secret-key-here-change-in-production-minimum-32-characters";
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "FitLife.Api",
            audience: "FitLife.Client",
            claims: new[] { new Claim(ClaimTypes.Role, "Member") },
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

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

    private async Task<string> GetOperatorTokenAsync()
    {
        const string email = "operator@example.com";
        const string password = "OperatorPass123!";

        await _client.PostAsJsonAsync("/api/auth/register", new RegisterUserDto
        {
            Email = email,
            Password = password,
            FirstName = "Demo",
            LastName = "Operator",
            FitnessLevel = "Intermediate"
        });

        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginDto
        {
            Email = email,
            Password = password
        });
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>(JsonOptions);
        return body!.Data!.Token;
    }

    private static CreateClassDto CreateClassRequest() => new()
    {
        Name = "Operator-created class",
        Type = "Yoga",
        Description = "Authorization integration test",
        InstructorId = "operator-test-instructor",
        InstructorName = "Test Instructor",
        Level = "Beginner",
        StartTime = DateTime.UtcNow.AddDays(2),
        DurationMinutes = 45,
        Capacity = 20
    };

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
    public async Task BookClass_SequentialRetry_ReturnsStableResultWithoutDuplicateState()
    {
        var classId = $"retry_{Guid.NewGuid():N}";
        await SeedClassAsync(classId, capacity: 30, enrollment: 5);

        using var client = _factory.CreateClient();
        var token = await GetAuthTokenAsync();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var first = await client.PostAsync($"/api/classes/{classId}/book", null);
        var retry = await client.PostAsync($"/api/classes/{classId}/book", null);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        retry.StatusCode.Should().Be(HttpStatusCode.OK);
        var retryBody =
            await retry.Content.ReadFromJsonAsync<ApiResponse<ClassDto>>(JsonOptions);
        retryBody!.Message.Should().Be("Class was already booked");
        retryBody.Data!.CurrentEnrollment.Should().Be(6);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<FitLifeDbContext>();
        context.Bookings.Count(booking =>
            booking.ClassId == classId
            && booking.Status == BookingStatuses.Active).Should().Be(1);
        context.Interactions.Count(interaction =>
            interaction.ItemId == classId
            && interaction.EventType == "Book").Should().Be(1);
    }

    [Fact]
    public async Task BookClass_IdempotencyKeyReusedForDifferentClass_ReturnsConflict()
    {
        var firstClassId = $"idem_first_{Guid.NewGuid():N}";
        var secondClassId = $"idem_second_{Guid.NewGuid():N}";
        await SeedClassAsync(firstClassId);
        await SeedClassAsync(secondClassId);

        using var client = _factory.CreateClient();
        var token = await GetAuthTokenAsync();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));

        var first = await client.PostAsync($"/api/classes/{firstClassId}/book", null);
        var conflictingRetry =
            await client.PostAsync($"/api/classes/{secondClassId}/book", null);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        conflictingRetry.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<FitLifeDbContext>();
        context.Classes.Single(entity => entity.Id == secondClassId)
            .CurrentEnrollment.Should().Be(5);
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

    [Fact]
    public async Task BookClass_MissingSubject_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            AuthenticationTestTokens.WithoutSubject());

        var response = await client.PostAsync("/api/classes/some-id/book", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CatalogMutations_NoAuth_ReturnUnauthorized()
    {
        using var client = _factory.CreateClient();

        var create = await client.PostAsJsonAsync("/api/classes", CreateClassRequest());
        var update = await client.PutAsJsonAsync("/api/classes/missing", new UpdateClassDto
        {
            Name = "Unauthorized update"
        });
        var delete = await client.DeleteAsync("/api/classes/missing");

        create.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        update.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        delete.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CatalogMutations_Member_ReturnForbidden()
    {
        using var client = _factory.CreateClient();
        var token = await GetAuthTokenAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var create = await client.PostAsJsonAsync("/api/classes", CreateClassRequest());
        var update = await client.PutAsJsonAsync("/api/classes/missing", new UpdateClassDto
        {
            Name = "Forbidden update"
        });
        var delete = await client.DeleteAsync("/api/classes/missing");

        create.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        update.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        delete.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CatalogMutations_Operator_ReturnSuccess()
    {
        using var client = _factory.CreateClient();
        var token = await GetOperatorTokenAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var create = await client.PostAsJsonAsync("/api/classes", CreateClassRequest());
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var createBody = await create.Content.ReadFromJsonAsync<ApiResponse<ClassDto>>(JsonOptions);
        var classId = createBody!.Data!.Id;

        var update = await client.PutAsJsonAsync($"/api/classes/{classId}", new UpdateClassDto
        {
            Name = "Operator-updated class"
        });
        update.StatusCode.Should().Be(HttpStatusCode.OK);

        var delete = await client.DeleteAsync($"/api/classes/{classId}");
        delete.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Register_ClientSuppliedOperatorRole_RemainsMember()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            Email = $"self_elevate_{Guid.NewGuid():N}@example.com",
            Password = "TestPass123!",
            FirstName = "Self",
            LastName = "Elevate",
            FitnessLevel = "Beginner",
            Role = "Operator"
        });
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>(JsonOptions);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body!.Data!.Token);

        var mutation = await client.PostAsJsonAsync("/api/classes", CreateClassRequest());

        mutation.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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

    [Fact]
    public async Task UpdatePreferences_OtherProfile_ReturnsForbidden()
    {
        var (token, _) = await RegisterAndGetAuthAsync();
        var (_, otherUser) = await RegisterAndGetAuthAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PutAsJsonAsync(
            $"/api/users/{otherUser.Id}/preferences",
            new UpdateUserPreferencesDto { FitnessLevel = "Advanced" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteUser_OtherProfile_ReturnsForbidden()
    {
        var (token, _) = await RegisterAndGetAuthAsync();
        var (_, otherUser) = await RegisterAndGetAuthAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.DeleteAsync($"/api/users/{otherUser.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetUser_MissingSubject_ReturnsUnauthorized()
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            AuthenticationTestTokens.WithoutSubject());

        var response = await _client.GetAsync("/api/users/some-id");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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

    [Fact]
    public async Task GetRecommendations_OtherUser_ReturnsForbidden()
    {
        var (token, _) = await RegisterAndGetAuthAsync();
        var (_, otherUser) = await RegisterAndGetAuthAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync($"/api/recommendations/{otherUser.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RefreshRecommendations_OtherUser_ReturnsForbidden()
    {
        var (token, _) = await RegisterAndGetAuthAsync();
        var (_, otherUser) = await RegisterAndGetAuthAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsync(
            $"/api/recommendations/{otherUser.Id}/refresh",
            null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetRecommendations_MissingSubject_ReturnsUnauthorized()
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            AuthenticationTestTokens.WithoutSubject());

        var response = await _client.GetAsync("/api/recommendations/some-user");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

public class EventsControllerTests : IClassFixture<FitLifeWebApplicationFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public EventsControllerTests(FitLifeWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<(string Token, UserDto User)> RegisterAndGetAuthAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterUserDto
        {
            Email = $"event_{Guid.NewGuid():N}@example.com",
            Password = "TestPass123!",
            FirstName = "Event",
            LastName = "Tester",
            FitnessLevel = "Beginner"
        });
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>(JsonOptions);
        return (body!.Data!.Token, body.Data.User!);
    }

    private static EventDto CreateEvent(string userId) => new()
    {
        UserId = userId,
        ItemId = "class_001",
        ItemType = "Class",
        EventType = "View"
    };

    [Fact]
    public async Task TrackEvent_OtherUser_ReturnsForbidden()
    {
        var (token, _) = await RegisterAndGetAuthAsync();
        var (_, otherUser) = await RegisterAndGetAuthAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsJsonAsync("/api/events", CreateEvent(otherUser.Id));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TrackEventBatch_OtherUser_ReturnsForbidden()
    {
        var (token, _) = await RegisterAndGetAuthAsync();
        var (_, otherUser) = await RegisterAndGetAuthAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsJsonAsync(
            "/api/events/batch",
            new[] { CreateEvent(otherUser.Id) });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TrackEvent_MissingSubject_ReturnsUnauthorized()
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            AuthenticationTestTokens.WithoutSubject());

        var response = await _client.PostAsJsonAsync("/api/events", CreateEvent("some-user"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
