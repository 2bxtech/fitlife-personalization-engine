using FitLife.Core.Models;

namespace FitLife.Core.Interfaces;

/// <summary>
/// Repository interface for User entity with specific operations
/// </summary>
public interface IUserRepository : IRepository<User>
{
    /// <summary>
    /// Find user by email address
    /// </summary>
    Task<User?> GetByEmailAsync(string email);
    
    /// <summary>
    /// Get users by segment
    /// </summary>
    Task<IEnumerable<User>> GetBySegmentAsync(string segment);
    
    /// <summary>
    /// Get active users (users with interactions in last N days)
    /// </summary>
    Task<IEnumerable<User>> GetActiveUsersAsync(int daysSinceLastInteraction = 30);
}
