using System.ComponentModel.DataAnnotations;

namespace FitLife.Core.DTOs;

// ============================================================
// Standard API Response Wrapper
// ============================================================

/// <summary>
/// Standard API response wrapper for consistent response shape
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Message { get; set; }
    public List<string>? Errors { get; set; }
}

// ============================================================
// Class DTOs
// ============================================================

/// <summary>
/// Data transfer object for class information
/// </summary>
public class ClassDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string InstructorId { get; set; } = string.Empty;
    public string InstructorName { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public int DurationMinutes { get; set; }
    public int Capacity { get; set; }
    public int CurrentEnrollment { get; set; }
    public int AvailableSpots => Capacity - CurrentEnrollment;
    public decimal AverageRating { get; set; }
    public int TotalRatings { get; set; }
    public int WeeklyBookings { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// DTO for creating a new class
/// </summary>
public class CreateClassDto
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string InstructorId { get; set; } = string.Empty;
    public string InstructorName { get; set; } = string.Empty;
    public string Level { get; set; } = "Beginner";
    public DateTime StartTime { get; set; }
    public int DurationMinutes { get; set; } = 60;
    public int Capacity { get; set; } = 30;
}

/// <summary>
/// DTO for updating a class
/// </summary>
public class UpdateClassDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public DateTime? StartTime { get; set; }
    public int? Capacity { get; set; }
    public bool? IsActive { get; set; }
}

// ============================================================
// Recommendation DTOs
// ============================================================

/// <summary>
/// Recommendation data transfer object with personalized score and explanation
/// </summary>
public class RecommendationDto
{
    public int Rank { get; set; }
    public double Score { get; set; }
    public string Reason { get; set; } = string.Empty;
    public ClassDto Class { get; set; } = null!;
    public DateTime GeneratedAt { get; set; }
}

// ============================================================
// User DTOs
// ============================================================

/// <summary>
/// User data transfer object for API responses
/// </summary>
public class UserDto
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FitnessLevel { get; set; } = string.Empty;
    public List<string> Goals { get; set; } = new();
    public List<string> PreferredClassTypes { get; set; } = new();
    public string? Segment { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// DTO for user registration
/// </summary>
public class RegisterUserDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FitnessLevel { get; set; } = "Beginner";
    public List<string>? Goals { get; set; }
    public List<string>? PreferredClassTypes { get; set; }
}

/// <summary>
/// DTO for user login
/// </summary>
public class LoginDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// DTO for updating user preferences
/// </summary>
public class UpdateUserPreferencesDto
{
    public string? FitnessLevel { get; set; }
    public List<string>? Goals { get; set; }
    public List<string>? PreferredClassTypes { get; set; }
}

// ============================================================
// Auth DTOs
// ============================================================

/// <summary>
/// DTO for authentication response (login/register)
/// </summary>
public class AuthResponseDto
{
    public string Token { get; set; } = string.Empty;
    public UserDto User { get; set; } = null!;
}

// ============================================================
// Event DTOs
// ============================================================

/// <summary>
/// DTO for tracking user interaction events
/// </summary>
public class EventDto
{
    [Required(ErrorMessage = "UserId is required")]
    public string UserId { get; set; } = string.Empty;

    [Required(ErrorMessage = "ItemId is required")]
    public string ItemId { get; set; } = string.Empty;

    public string ItemType { get; set; } = "Class";

    /// <summary>
    /// Must be one of: View, Click, Book, Complete, Cancel, Rate
    /// </summary>
    [Required(ErrorMessage = "EventType is required")]
    public string EventType { get; set; } = string.Empty;

    public Dictionary<string, object>? Metadata { get; set; }
}
