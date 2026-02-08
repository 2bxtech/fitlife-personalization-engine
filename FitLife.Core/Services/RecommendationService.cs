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
    private readonly ILogger<RecommendationService> _logger;

    public RecommendationService(
        IUserRepository userRepository,
        IClassRepository classRepository,
        IInteractionRepository interactionRepository,
        IRecommendationRepository recommendationRepository,
        ICacheService cacheService,
        IScoringEngine scoringEngine,
        ILogger<RecommendationService> logger)
    {
        _userRepository = userRepository;
        _classRepository = classRepository;
        _interactionRepository = interactionRepository;
        _recommendationRepository = recommendationRepository;
        _cacheService = cacheService;
        _scoringEngine = scoringEngine;
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

            var recentDtos = await ConvertToDtos(recentRecs);
            
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

            // Get user's interaction history
            var userInteractions = await _interactionRepository.GetRecentByUserIdAsync(userId, days: 90);

            // Get candidate classes (upcoming, active, not full)
            var candidates = (await _classRepository.GetUpcomingClassesAsync(limit: 100)).ToList();
            
            if (!candidates.Any())
            {
                _logger.LogWarning("No candidate classes available for recommendations");
                return new List<RecommendationDto>();
            }

            _logger.LogDebug("Scoring {Count} candidate classes for user {UserId}", candidates.Count, userId);

            // Score each candidate class
            var scoredClasses = new List<(Class Class, double Score)>();
            foreach (var classItem in candidates)
            {
                var score = _scoringEngine.CalculateScore(user, classItem, userInteractions);
                scoredClasses.Add((classItem, score));
            }

            // Sort by score and take top N
            var topRecommendations = scoredClasses
                .OrderByDescending(x => x.Score)
                .Take(limit)
                .ToList();

            // Generate recommendation DTOs with explanations
            var recommendations = new List<RecommendationDto>();
            for (int i = 0; i < topRecommendations.Count; i++)
            {
                var (classItem, score) = topRecommendations[i];
                var reason = GenerateExplanation(user, classItem, score, userInteractions);

                recommendations.Add(new RecommendationDto
                {
                    Rank = i + 1,
                    Score = Math.Round(score, 2),
                    Reason = reason,
                    Class = DtoMappers.MapToClassDto(classItem),
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
            return await GetPopularClassesFallback(limit);
        }
    }

    /// <summary>
    /// Generates a human-readable explanation for why a class was recommended
    /// </summary>
    private string GenerateExplanation(User user, Class classItem, double score, List<Interaction> userInteractions)
    {
        var reasons = new List<string>();

        // Check preferred class types
        try
        {
            var preferredTypes = JsonSerializer.Deserialize<List<string>>(user.PreferredClassTypes ?? "[]");
            if (preferredTypes?.Contains(classItem.Type) == true)
            {
                reasons.Add($"you love {classItem.Type} classes");
            }
        }
        catch { }

        // Check favorite instructors (has completed classes with this instructor)
        var completedWithInstructor = userInteractions
            .Count(i => i.EventType == "Complete" && 
                       i.Metadata.Contains($"\"instructorId\":\"{classItem.InstructorId}\""));
        
        if (completedWithInstructor >= 2)
        {
            reasons.Add($"you enjoy classes with {classItem.InstructorName}");
        }

        // High rating
        if (classItem.AverageRating >= 4.7m)
        {
            reasons.Add("this class has excellent reviews");
        }

        // Segment-based
        if (!string.IsNullOrEmpty(user.Segment) && user.Segment != "General")
        {
            reasons.Add($"popular among {user.Segment} members like you");
        }

        // Trending
        if (classItem.WeeklyBookings > 50)
        {
            reasons.Add("trending this week");
        }

        // Time preference
        var bookingHours = userInteractions
            .Where(i => i.EventType == "Book")
            .Select(i => i.Timestamp.Hour)
            .Distinct()
            .ToList();
        
        if (bookingHours.Contains(classItem.StartTime.Hour))
        {
            reasons.Add("at your preferred time");
        }

        if (!reasons.Any())
        {
            return "Recommended based on your activity";
        }

        return $"Because {string.Join(" and ", reasons)}";
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
    private async Task<List<RecommendationDto>> ConvertToDtos(List<Recommendation> recommendations)
    {
        var classIds = recommendations.Select(r => r.ItemId).Distinct().ToList();
        var classes = await _classRepository.GetByIdsAsync(classIds);
        var classLookup = classes.ToDictionary(c => c.Id);

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
                    Class = DtoMappers.MapToClassDto(classItem),
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
    private async Task<List<RecommendationDto>> GetPopularClassesFallback(int limit)
    {
        _logger.LogWarning("Using popular classes fallback for recommendations");

        var popularClasses = (await _classRepository.GetPopularClassesAsync(limit)).ToList();
        
        return popularClasses.Select((c, index) => new RecommendationDto
        {
            Rank = index + 1,
            Score = 50.0, // Default score for fallback
            Reason = "Popular class this week",
            Class = DtoMappers.MapToClassDto(c),
            GeneratedAt = DateTime.UtcNow
        }).ToList();
    }
}
