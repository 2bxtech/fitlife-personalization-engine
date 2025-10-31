namespace FitLife.Core.Interfaces;

/// <summary>
/// Interface for JWT token generation and validation
/// </summary>
public interface IJwtService
{
    /// <summary>
    /// Generate JWT token for authenticated user
    /// </summary>
    string GenerateToken(string userId, string email, string? segment = null);
    
    /// <summary>
    /// Validate JWT token and extract user ID
    /// </summary>
    string? ValidateToken(string token);
}
