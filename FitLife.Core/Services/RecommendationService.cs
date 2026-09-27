using FitLife.Core.DTOs;
using FitLife.Core.Interfaces;
using FitLife.Core.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace FitLife.Core.Services;

/// <summary>
/// Service for generating and managing personalized class recommendations
/// Implements cache-aside pattern with Redis and database persistence
/// </summary>
public class RecommendationService : IRecommendationService
{
    private readonly IUserRepository _userRepository;
    private readonly IClassRepository _classRepository;
    private readonly IInteractionRepository _interactionRepository;
    private readonly IRecommendationRepository _recommendationRepository;
    private readonly ICacheService _cacheService;
    private readonly IScoringEngine _scoringEngine;
    private readonly IBookingService _bookingService;
    private readonly ILogger<RecommendationService> _logger;

    public RecommendationService(
        IUserRepository userRepository,
        IClassRepository classRepository,
        IInteractionRepository interactionRepository,
        IRecommendationRepository recommendationRepository,
        ICacheService cacheService,
        IScoringEngine scoringEngine,
        IBookingService bookingService,
        ILogger<RecommendationService> logger)
    {
        _userRepository = userRepository;
        _classRepository = classRepository;
        _interactionRepository = interactionRepository;
        _recommendationRepository = recommendationRepository;
        _cacheService = cacheService;
        _scoringEngine = scoringEngine;
        _bookingService = bookingService;
        _logger = logger;
    }

    /// <summary>
    /// Gets personalized recommendations for a user
    /// Uses cache-aside pattern: Redis → Database → Generate fresh
    /// </summary>
    public async Task<List<RecommendationDto>> GetRecommendationsAsync(string userId, int limit = 10)
    {
        // Check Redis cache first
        var cacheKey = $"rec:{userId}";
        var cached = await _cacheService.GetAsync<List<RecommendationDto>>(cacheKey);
        
        if (cached != null && cached.Any())
        {
            _logger.LogInformation("Cache hit for user {UserId} - returning {Count} recommendations", 
                userId, cached.Count);
            return cached.Take(limit).ToList();
        }

        _logger.LogDebug("Cache miss for user {UserId}", userId);

        // Check database for recently generated recommendations (< 10 min old)
        var recentRecs = await _recommendationRepository.GetRecentByUserIdAsync(userId, withinMinutes: 10, limit);
        
        if (recentRecs.Any())
        {
            _logger.LogInformation("Found {Count} recent recommendations in database for user {UserId}", 
                recentRecs.Count, userId);

            var recentDtos = await ConvertToDtos(userId, recentRecs);
            
            // Cache for next request
            await _cacheService.SetAsync(cacheKey, recentDtos, TimeSpan.FromMinutes(10));
            
            return recentDtos;
        }

        // Generate fresh recommendations
        _logger.LogInformation("Generating fresh recommendations for user {UserId}", userId);
        return await GenerateRecommendationsAsync(userId, limit);
    }

    /// <summary>
    /// Force regenerates recommendations for a user and invalidates cache
    /// </summary>
    public async Task<List<RecommendationDto>> RefreshRecommendationsAsync(string userId, int limit = 10)
    {
        _logger.LogInformation("Force refreshing recommendations for user {UserId}", userId);
        
        // Invalidate cache
        await InvalidateCacheAsync(userId);
        
        // Generate fresh recommendations
        return await GenerateRecommendationsAsync(userId, limit);
    }

    /// <summary>
    /// Invalidates the cached recommendations for a user
    /// Called when user books a class or updates preferences
    /// </summary>
    public async Task InvalidateCacheAsync(string userId)
    {
        var cacheKey = $"rec:{userId}";
        await _cacheService.DeleteAsync(cacheKey);
        _logger.LogDebug("Invalidated cache for user {UserId}", userId);
    }

    /// <summary>
    /// Generates fresh recommendations without checking cache
    /// Used by background workers for batch processing
    /// </summary>
    public async Task<List<RecommendationDto>> GenerateRecommendationsAsync(string userId, int limit = 10)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            // Fetch user profile
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found", userId);
                return new List<RecommendationDto>();
            }

            // Get user's interaction history and the classes it refers to, so history
            // facts (instructor, start time) come from the classes themselves.
            var userInteractions = await _interactionRepository.GetRecentByUserIdAsync(userId, days: 90);
            // Only Book and Complete events feed history facts; skip the lookup without them.
            var historyClassIds = userInteractions
                .Where(interaction => interaction.EventType is EventTypes.Book or EventTypes.Complete)
                .Select(interaction => interaction.ItemId)
                .Distinct()
                .ToList();
            var historyClasses = historyClassIds.Count == 0
                ? new Dictionary<string, Class>()
                : (await _classRepository.GetByIdsAsync(historyClassIds)).ToDictionary(classItem => classItem.Id);
            var history = ScoringHistory.From(userInteractions, historyClasses);

            // Get candidate classes (upcoming, active, not full)
            var candidates = (await _classRepository.GetUpcomingClassesAsync(limit: 100)).ToList();
            
            if (!candidates.Any())
            {
                _logger.LogWarning("No candidate classes available for recommendations");
                return new List<RecommendationDto>();
            }

            _logger.LogDebug("Scoring {Count} candidate classes for user {UserId}", candidates.Count, userId);

            // Score each candidate class
            var scoredClasses = candidates
                .Select(classItem => (Class: classItem, Breakdown: _scoringEngine.Explain(user, classItem, history)))
                .ToList();

            // Sort by score (ties broken by start time, then id, for stable demo ordering)
            var topRecommendations = scoredClasses
                .OrderByDescending(x => x.Breakdown.Total)
                .ThenBy(x => x.Class.StartTime)
                .ThenBy(x => x.Class.Id, StringComparer.Ordinal)
                .Take(limit)
                .ToList();

            // Generate recommendation DTOs with explanations
            var recommendations = new List<RecommendationDto>();
            var activeClassIds = await _bookingService.GetActiveClassIdsAsync(
                userId,
                topRecommendations.Select(recommendation =>
                    recommendation.Class.Id));
            for (int i = 0; i < topRecommendations.Count; i++)
            {
                var (classItem, breakdown) = topRecommendations[i];

                recommendations.Add(new RecommendationDto
                {
                    Rank = i + 1,
                    Score = Math.Round(breakdown.Total, 2),
                    Reason = RecommendationReasons.Compose(breakdown.Factors),
                    Factors = breakdown.Factors.ToList(),
                    Class = DtoMappers.MapToClassDto(
                        classItem,
                        activeClassIds.Contains(classItem.Id)),
                    GeneratedAt = DateTime.UtcNow
                });
            }

            // Save to database for persistence
            await SaveRecommendationsToDatabaseAsync(userId, recommendations);

            // Cache for 10 minutes
            var cacheKey = $"rec:{userId}";
            await _cacheService.SetAsync(cacheKey, recommendations, TimeSpan.FromMinutes(10));

            var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
            _logger.LogInformation(
                "Generated {Count} recommendations for user {UserId} in {Duration}ms",
                recommendations.Count, userId, elapsed);

            return recommendations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating recommendations for user {UserId}", userId);
            
            // Fallback: Return popular classes
            return await GetPopularClassesFallback(userId, limit);
        }
    }

    /// <summary>
    /// Saves recommendations to database for persistence
    /// </summary>
    private async Task SaveRecommendationsToDatabaseAsync(string userId, List<RecommendationDto> recommendationDtos)
    {
        var recommendations = recommendationDtos.Select(dto => new Recommendation
        {
            UserId = userId,
            ItemId = dto.Class.Id,
            Score = (decimal)dto.Score,
            Rank = dto.Rank,
            Reason = dto.Reason,
            FactorsJson = ScoreBreakdown.Serialize(dto.Factors),
            GeneratedAt = dto.GeneratedAt
        }).ToList();

        await _recommendationRepository.SaveRecommendationsAsync(userId, recommendations);
        _logger.LogDebug("Saved {Count} recommendations to database for user {UserId}", 
            recommendations.Count, userId);
    }

    /// <summary>
    /// Converts database recommendation entities to DTOs with class details
    /// Uses batch loading to avoid N+1 queries
    /// </summary>
    private async Task<List<RecommendationDto>> ConvertToDtos(
        string userId,
        List<Recommendation> recommendations)
    {
        var classIds = recommendations.Select(r => r.ItemId).Distinct().ToList();
        var classes = await _classRepository.GetByIdsAsync(classIds);
        var classLookup = classes.ToDictionary(c => c.Id);
        var activeClassIds = await _bookingService.GetActiveClassIdsAsync(
            userId,
            classIds);

        var dtos = new List<RecommendationDto>();

        foreach (var rec in recommendations)
        {
            if (classLookup.TryGetValue(rec.ItemId, out var classItem))
            {
                dtos.Add(new RecommendationDto
                {
                    Rank = rec.Rank,
                    Score = (double)rec.Score,
                    Reason = rec.Reason,
                    Factors = ScoreBreakdown.Deserialize(rec.FactorsJson).ToList(),
                    Class = DtoMappers.MapToClassDto(
                        classItem,
                        activeClassIds.Contains(classItem.Id)),
                    GeneratedAt = rec.GeneratedAt
                });
            }
        }

        return dtos;
    }

    /// <summary>
    /// Fallback when recommendation generation fails
    /// Returns popular classes instead
    /// </summary>
    private async Task<List<RecommendationDto>> GetPopularClassesFallback(
        string userId,
        int limit)
    {
        _logger.LogWarning("Using popular classes fallback for recommendations");

        var popularClasses = (await _classRepository.GetPopularClassesAsync(limit)).ToList();
        var activeClassIds = await _bookingService.GetActiveClassIdsAsync(
            userId,
            popularClasses.Select(classEntity => classEntity.Id));
        
        return popularClasses.Select((c, index) => new RecommendationDto
        {
            Rank = index + 1,
            Score = 50.0, // Default score for fallback
            Reason = "Popular this week. Personalized scoring was unavailable.",
            Class = DtoMappers.MapToClassDto(
                c,
                activeClassIds.Contains(c.Id)),
            GeneratedAt = DateTime.UtcNow
        }).ToList();
    }
}
