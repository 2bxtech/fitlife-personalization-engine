namespace FitLife.Api.DTOs;

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
