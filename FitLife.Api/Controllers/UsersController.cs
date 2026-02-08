using FitLife.Core.DTOs;
using FitLife.Core.Interfaces;
using FitLife.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;

namespace FitLife.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly ICacheService _cacheService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        IUserRepository userRepository,
        ICacheService cacheService,
        ILogger<UsersController> logger)
    {
        _userRepository = userRepository;
        _cacheService = cacheService;
        _logger = logger;
    }

    /// <summary>
    /// Get user by ID (must match authenticated user)
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<UserDto>>> GetUser(string id)
    {
        try
        {
            var tokenUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                              ?? User.FindFirst("sub")?.Value;
            if (tokenUserId != id)
                return Forbid();

            var user = await _userRepository.GetByIdAsync(id);
            
            if (user == null)
            {
                return NotFound(new ApiResponse<UserDto>
                {
                    Success = false,
                    Message = "User not found"
                });
            }

            return Ok(new ApiResponse<UserDto>
            {
                Success = true,
                Data = DtoMappers.MapToUserDto(user)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user {UserId}", id);
            return StatusCode(500, new ApiResponse<UserDto>
            {
                Success = false,
                Message = "Failed to retrieve user"
            });
        }
    }

    /// <summary>
    /// Update user preferences (must match authenticated user)
    /// </summary>
    [HttpPut("{id}/preferences")]
    public async Task<ActionResult<ApiResponse<UserDto>>> UpdatePreferences(
        string id, 
        [FromBody] UpdateUserPreferencesDto dto)
    {
        try
        {
            var tokenUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                              ?? User.FindFirst("sub")?.Value;
            if (tokenUserId != id)
                return Forbid();

            var user = await _userRepository.GetByIdAsync(id);
            
            if (user == null)
            {
                return NotFound(new ApiResponse<UserDto>
                {
                    Success = false,
                    Message = "User not found"
                });
            }

            // Update preferences
            if (dto.FitnessLevel != null)
                user.FitnessLevel = dto.FitnessLevel;
            
            if (dto.Goals != null)
                user.Goals = JsonSerializer.Serialize(dto.Goals);
            
            if (dto.PreferredClassTypes != null)
                user.PreferredClassTypes = JsonSerializer.Serialize(dto.PreferredClassTypes);

            user.UpdatedAt = DateTime.UtcNow;

            await _userRepository.UpdateAsync(user);
            await _userRepository.SaveChangesAsync();

            // Invalidate recommendation cache after preference update
            await _cacheService.DeleteAsync($"rec:{id}");

            _logger.LogInformation("Updated preferences for user {UserId}", id);

            return Ok(new ApiResponse<UserDto>
            {
                Success = true,
                Data = DtoMappers.MapToUserDto(user),
                Message = "Preferences updated successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating preferences for user {UserId}", id);
            return StatusCode(500, new ApiResponse<UserDto>
            {
                Success = false,
                Message = "Failed to update preferences"
            });
        }
    }

    /// <summary>
    /// Delete user (must match authenticated user)
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteUser(string id)
    {
        try
        {
            var tokenUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                              ?? User.FindFirst("sub")?.Value;
            if (tokenUserId != id)
                return Forbid();

            var user = await _userRepository.GetByIdAsync(id);
            
            if (user == null)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = "User not found"
                });
            }

            await _userRepository.DeleteAsync(user);
            await _userRepository.SaveChangesAsync();

            // Clean up cache
            await _cacheService.DeleteAsync($"rec:{id}");

            _logger.LogInformation("Deleted user {UserId}", id);

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "User deleted successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user {UserId}", id);
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = "Failed to delete user"
            });
        }
    }
}
