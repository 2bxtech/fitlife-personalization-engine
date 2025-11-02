using FitLife.Core.DTOs;
using FitLife.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FitLife.Api.Controllers;

/// <summary>
/// Controller for personalized class recommendations
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RecommendationsController : ControllerBase
{
    private readonly IRecommendationService _recommendationService;
    private readonly ILogger<RecommendationsController> _logger;

    public RecommendationsController(
        IRecommendationService recommendationService,
        ILogger<RecommendationsController> logger)
    {
        _recommendationService = recommendationService;
        _logger = logger;
    }

    /// <summary>
    /// Gets personalized class recommendations for the authenticated user
    /// </summary>
    /// <param name="userId">User ID (must match authenticated user)</param>
    /// <param name="limit">Maximum number of recommendations to return (default: 10)</param>
    /// <returns>List of personalized class recommendations</returns>
    [HttpGet("{userId}")]
    public async Task<IActionResult> GetRecommendations(string userId, [FromQuery] int limit = 10)
    {
        try
        {
            // Validate user authorization
            var authenticatedUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (authenticatedUserId != userId)
            {
                _logger.LogWarning(
                    "User {AuthUserId} attempted to access recommendations for {RequestedUserId}",
                    authenticatedUserId, userId);
                return Forbid();
            }

            if (limit < 1 || limit > 50)
            {
                return BadRequest(new
                {
                    success = false,
                    error = "Limit must be between 1 and 50"
                });
            }

            var recommendations = await _recommendationService.GetRecommendationsAsync(userId, limit);

            return Ok(new
            {
                success = true,
                data = recommendations,
                count = recommendations.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recommendations for user {UserId}", userId);
            return StatusCode(500, new
            {
                success = false,
                error = "Failed to retrieve recommendations"
            });
        }
    }

    /// <summary>
    /// Forces regeneration of recommendations for the authenticated user
    /// Invalidates cache and generates fresh recommendations
    /// </summary>
    /// <param name="userId">User ID (must match authenticated user)</param>
    /// <param name="limit">Maximum number of recommendations to generate (default: 10)</param>
    /// <returns>Newly generated recommendations</returns>
    [HttpPost("{userId}/refresh")]
    public async Task<IActionResult> RefreshRecommendations(string userId, [FromQuery] int limit = 10)
    {
        try
        {
            // Validate user authorization
            var authenticatedUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (authenticatedUserId != userId)
            {
                _logger.LogWarning(
                    "User {AuthUserId} attempted to refresh recommendations for {RequestedUserId}",
                    authenticatedUserId, userId);
                return Forbid();
            }

            if (limit < 1 || limit > 50)
            {
                return BadRequest(new
                {
                    success = false,
                    error = "Limit must be between 1 and 50"
                });
            }

            _logger.LogInformation("User {UserId} requested recommendation refresh", userId);

            var recommendations = await _recommendationService.RefreshRecommendationsAsync(userId, limit);

            return Ok(new
            {
                success = true,
                data = recommendations,
                count = recommendations.Count,
                message = "Recommendations refreshed successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing recommendations for user {UserId}", userId);
            return StatusCode(500, new
            {
                success = false,
                error = "Failed to refresh recommendations"
            });
        }
    }
}
