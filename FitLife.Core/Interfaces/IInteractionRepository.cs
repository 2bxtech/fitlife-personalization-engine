using FitLife.Core.Models;

namespace FitLife.Core.Interfaces;

/// <summary>
/// Repository interface for managing user interactions
/// </summary>
public interface IInteractionRepository : IRepository<Interaction>
{
    /// <summary>
    /// Gets all interactions for a user
    /// </summary>
    Task<List<Interaction>> GetByUserIdAsync(string userId);

    /// <summary>
    /// Gets recent interactions for a user within specified days
    /// </summary>
    Task<List<Interaction>> GetRecentByUserIdAsync(string userId, int days = 30);

    /// <summary>
    /// Gets interactions of a specific type for a user
    /// </summary>
    Task<List<Interaction>> GetByUserAndTypeAsync(string userId, string eventType, int days = 30);
}
