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
public class ClassesController : ControllerBase
{
    private readonly IClassRepository _classRepository;
    private readonly IInteractionRepository _interactionRepository;
    private readonly ICacheService _cacheService;
    private readonly ILogger<ClassesController> _logger;

    public ClassesController(
        IClassRepository classRepository,
        IInteractionRepository interactionRepository,
        ICacheService cacheService,
        ILogger<ClassesController> logger)
    {
        _classRepository = classRepository;
        _interactionRepository = interactionRepository;
        _cacheService = cacheService;
        _logger = logger;
    }

    /// <summary>
    /// Get all upcoming classes with optional filters
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<IEnumerable<ClassDto>>>> GetClasses(
        [FromQuery] string? type = null,
        [FromQuery] string? level = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] int limit = 50)
    {
        try
        {
            IEnumerable<Class> classes;

            if (!string.IsNullOrEmpty(type))
            {
                classes = await _classRepository.GetByTypeAsync(type);
            }
            else
            {
                classes = await _classRepository.GetUpcomingClassesAsync(limit);
            }

            // Apply additional filters
            if (!string.IsNullOrEmpty(level))
                classes = classes.Where(c => c.Level.Equals(level, StringComparison.OrdinalIgnoreCase));

            if (startDate.HasValue)
                classes = classes.Where(c => c.StartTime.Date >= startDate.Value.Date);

            var classDtos = classes.Take(limit).Select(DtoMappers.MapToClassDto);

            return Ok(new ApiResponse<IEnumerable<ClassDto>>
            {
                Success = true,
                Data = classDtos
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting classes");
            return StatusCode(500, new ApiResponse<IEnumerable<ClassDto>>
            {
                Success = false,
                Message = "Failed to retrieve classes"
            });
        }
    }

    /// <summary>
    /// Get class by ID
    /// </summary>
    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<ClassDto>>> GetClass(string id)
    {
        try
        {
            var classEntity = await _classRepository.GetByIdAsync(id);
            
            if (classEntity == null)
            {
                return NotFound(new ApiResponse<ClassDto>
                {
                    Success = false,
                    Message = "Class not found"
                });
            }

            return Ok(new ApiResponse<ClassDto>
            {
                Success = true,
                Data = DtoMappers.MapToClassDto(classEntity)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting class {ClassId}", id);
            return StatusCode(500, new ApiResponse<ClassDto>
            {
                Success = false,
                Message = "Failed to retrieve class"
            });
        }
    }

    /// <summary>
    /// Get popular classes
    /// </summary>
    [HttpGet("popular")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<IEnumerable<ClassDto>>>> GetPopularClasses(
        [FromQuery] int limit = 20)
    {
        try
        {
            var classes = await _classRepository.GetPopularClassesAsync(limit);
            var classDtos = classes.Select(DtoMappers.MapToClassDto);

            return Ok(new ApiResponse<IEnumerable<ClassDto>>
            {
                Success = true,
                Data = classDtos
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting popular classes");
            return StatusCode(500, new ApiResponse<IEnumerable<ClassDto>>
            {
                Success = false,
                Message = "Failed to retrieve popular classes"
            });
        }
    }

    /// <summary>
    /// Book a class for the authenticated user
    /// </summary>
    [HttpPost("{id}/book")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<ClassDto>>> BookClass(string id)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                         ?? User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse<ClassDto> { Success = false, Message = "User not authenticated" });

            var classEntity = await _classRepository.GetByIdAsync(id);
            if (classEntity == null)
                return NotFound(new ApiResponse<ClassDto> { Success = false, Message = "Class not found" });

            if (classEntity.CurrentEnrollment >= classEntity.Capacity)
                return BadRequest(new ApiResponse<ClassDto> { Success = false, Message = "Class is full" });

            // Increment enrollment
            classEntity.CurrentEnrollment++;
            classEntity.UpdatedAt = DateTime.UtcNow;
            await _classRepository.UpdateAsync(classEntity);
            await _classRepository.SaveChangesAsync();

            // Create booking interaction record
            var interaction = new Interaction
            {
                UserId = userId,
                ItemId = id,
                ItemType = "Class",
                EventType = "Book",
                Timestamp = DateTime.UtcNow,
                Metadata = JsonSerializer.Serialize(new { source = "web", className = classEntity.Name })
            };
            await _interactionRepository.AddAsync(interaction);
            await _interactionRepository.SaveChangesAsync();

            // Invalidate recommendation cache
            await _cacheService.DeleteAsync($"rec:{userId}");

            _logger.LogInformation("User {UserId} booked class {ClassId}: {ClassName}",
                userId, id, classEntity.Name);

            return Ok(new ApiResponse<ClassDto>
            {
                Success = true,
                Data = DtoMappers.MapToClassDto(classEntity),
                Message = "Class booked successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error booking class {ClassId}", id);
            return StatusCode(500, new ApiResponse<ClassDto>
            {
                Success = false,
                Message = "Failed to book class"
            });
        }
    }

    /// <summary>
    /// Create a new class (requires authentication)
    /// </summary>
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<ApiResponse<ClassDto>>> CreateClass([FromBody] CreateClassDto dto)
    {
        try
        {
            var classEntity = new Class
            {
                Name = dto.Name,
                Type = dto.Type,
                Description = dto.Description,
                InstructorId = dto.InstructorId,
                InstructorName = dto.InstructorName,
                Level = dto.Level,
                StartTime = dto.StartTime,
                DurationMinutes = dto.DurationMinutes,
                Capacity = dto.Capacity
            };

            await _classRepository.AddAsync(classEntity);
            await _classRepository.SaveChangesAsync();

            _logger.LogInformation("Created class {ClassId}: {ClassName}", classEntity.Id, classEntity.Name);

            return CreatedAtAction(nameof(GetClass), new { id = classEntity.Id }, new ApiResponse<ClassDto>
            {
                Success = true,
                Data = DtoMappers.MapToClassDto(classEntity),
                Message = "Class created successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating class");
            return StatusCode(500, new ApiResponse<ClassDto>
            {
                Success = false,
                Message = "Failed to create class"
            });
        }
    }

    /// <summary>
    /// Update a class (requires authentication)
    /// </summary>
    [HttpPut("{id}")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<ClassDto>>> UpdateClass(
        string id,
        [FromBody] UpdateClassDto dto)
    {
        try
        {
            var classEntity = await _classRepository.GetByIdAsync(id);
            
            if (classEntity == null)
            {
                return NotFound(new ApiResponse<ClassDto>
                {
                    Success = false,
                    Message = "Class not found"
                });
            }

            // Update fields
            if (dto.Name != null)
                classEntity.Name = dto.Name;
            
            if (dto.Description != null)
                classEntity.Description = dto.Description;
            
            if (dto.StartTime.HasValue)
                classEntity.StartTime = dto.StartTime.Value;
            
            if (dto.Capacity.HasValue)
                classEntity.Capacity = dto.Capacity.Value;
            
            if (dto.IsActive.HasValue)
                classEntity.IsActive = dto.IsActive.Value;

            classEntity.UpdatedAt = DateTime.UtcNow;

            await _classRepository.UpdateAsync(classEntity);
            await _classRepository.SaveChangesAsync();

            _logger.LogInformation("Updated class {ClassId}", id);

            return Ok(new ApiResponse<ClassDto>
            {
                Success = true,
                Data = DtoMappers.MapToClassDto(classEntity),
                Message = "Class updated successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating class {ClassId}", id);
            return StatusCode(500, new ApiResponse<ClassDto>
            {
                Success = false,
                Message = "Failed to update class"
            });
        }
    }

    /// <summary>
    /// Delete a class (requires authentication)
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> DeleteClass(string id)
    {
        try
        {
            var classEntity = await _classRepository.GetByIdAsync(id);
            
            if (classEntity == null)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Class not found"
                });
            }

            await _classRepository.DeleteAsync(classEntity);
            await _classRepository.SaveChangesAsync();

            _logger.LogInformation("Deleted class {ClassId}", id);

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "Class deleted successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting class {ClassId}", id);
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = "Failed to delete class"
            });
        }
    }
}
