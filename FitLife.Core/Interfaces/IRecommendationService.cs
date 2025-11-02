using FitLife.Core.DTOs;

namespace FitLife.Core.Interfaces;

/// <summary>
/// Service for generating and managing personalized class recommendations
/// </summary>
public interface IRecommendationService
{
    /// <summary>
    /// Gets personalized recommendations for a user
    /// Uses cache-aside pattern: Redis → Database → Generate fresh
    /// </summary>
    /// <param name="userId">User ID requesting recommendations</param>
    /// <param name="limit">Maximum number of recommendations to return (default: 10)</param>
    /// <returns>List of personalized class recommendations</returns>
    Task<List<RecommendationDto>> GetRecommendationsAsync(string userId, int limit = 10);

    /// <summary>
    /// Force regenerates recommendations for a user and invalidates cache
    /// </summary>
    /// <param name="userId">User ID to refresh recommendations for</param>
    /// <param name="limit">Maximum number of recommendations to generate</param>
    Task<List<RecommendationDto>> RefreshRecommendationsAsync(string userId, int limit = 10);

    /// <summary>
    /// Invalidates the cached recommendations for a user
    /// Called when user books a class or updates preferences
    /// </summary>
    /// <param name="userId">User ID to invalidate cache for</param>
    Task InvalidateCacheAsync(string userId);

    /// <summary>
    /// Generates fresh recommendations without checking cache
    /// Used by background workers for batch processing
    /// </summary>
    /// <param name="userId">User ID to generate recommendations for</param>
    /// <param name="limit">Maximum number of recommendations to generate</param>
    Task<List<RecommendationDto>> GenerateRecommendationsAsync(string userId, int limit = 10);
}
