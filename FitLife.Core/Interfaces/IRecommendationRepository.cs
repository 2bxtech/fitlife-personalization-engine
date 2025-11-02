using FitLife.Core.Models;

namespace FitLife.Core.Interfaces;

/// <summary>
/// Repository interface for managing recommendations
/// </summary>
public interface IRecommendationRepository : IRepository<Recommendation>
{
    /// <summary>
    /// Gets recommendations for a user, ordered by rank
    /// </summary>
    Task<List<Recommendation>> GetByUserIdAsync(string userId, int limit = 10);

    /// <summary>
    /// Gets recent recommendations for a user (generated within specified minutes)
    /// </summary>
    Task<List<Recommendation>> GetRecentByUserIdAsync(string userId, int withinMinutes = 10, int limit = 10);

    /// <summary>
    /// Deletes all recommendations for a user
    /// Used before regenerating fresh recommendations
    /// </summary>
    Task DeleteByUserIdAsync(string userId);

    /// <summary>
    /// Saves a batch of recommendations for a user
    /// </summary>
    Task SaveRecommendationsAsync(string userId, List<Recommendation> recommendations);
}
