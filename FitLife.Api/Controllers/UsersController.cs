using FitLife.Api.DTOs;
using FitLife.Core.Interfaces;
using FitLife.Core.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace FitLife.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IUserRepository userRepository, ILogger<UsersController> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get user by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<UserDto>>> GetUser(string id)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(id);
            
            if (user == null)
            {
                return NotFound(new ApiResponse<UserDto>
                {
                    Success = false,
                    Message = "User not found"
                });
            }

            var userDto = MapToDto(user);
            return Ok(new ApiResponse<UserDto>
            {
                Success = true,
                Data = userDto
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
    /// Get user by email
    /// </summary>
    [HttpGet("by-email/{email}")]
    public async Task<ActionResult<ApiResponse<UserDto>>> GetUserByEmail(string email)
    {
        try
        {
            var user = await _userRepository.GetByEmailAsync(email);
            
            if (user == null)
            {
                return NotFound(new ApiResponse<UserDto>
                {
                    Success = false,
                    Message = "User not found"
                });
            }

            var userDto = MapToDto(user);
            return Ok(new ApiResponse<UserDto>
            {
                Success = true,
                Data = userDto
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user by email {Email}", email);
            return StatusCode(500, new ApiResponse<UserDto>
            {
                Success = false,
                Message = "Failed to retrieve user"
            });
        }
    }

    /// <summary>
    /// Update user preferences
    /// </summary>
    [HttpPut("{id}/preferences")]
    public async Task<ActionResult<ApiResponse<UserDto>>> UpdatePreferences(
        string id, 
        [FromBody] UpdateUserPreferencesDto dto)
    {
        try
        {
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

            _logger.LogInformation("Updated preferences for user {UserId}", id);

            var userDto = MapToDto(user);
            return Ok(new ApiResponse<UserDto>
            {
                Success = true,
                Data = userDto,
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
    /// Delete user
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteUser(string id)
    {
        try
        {
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

    private static UserDto MapToDto(User user)
    {
        return new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            FitnessLevel = user.FitnessLevel,
            Goals = JsonSerializer.Deserialize<List<string>>(user.Goals) ?? new(),
            PreferredClassTypes = JsonSerializer.Deserialize<List<string>>(user.PreferredClassTypes) ?? new(),
            Segment = user.Segment,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }
}
