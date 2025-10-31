using FitLife.Api.DTOs;
using FitLife.Core.Interfaces;
using FitLife.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace FitLife.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClassesController : ControllerBase
{
    private readonly IClassRepository _classRepository;
    private readonly ILogger<ClassesController> _logger;

    public ClassesController(IClassRepository classRepository, ILogger<ClassesController> logger)
    {
        _classRepository = classRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get all upcoming classes
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IEnumerable<ClassDto>>>> GetClasses(
        [FromQuery] string? type = null,
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

            var classDtos = classes.Select(MapToDto);

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

            var classDto = MapToDto(classEntity);
            return Ok(new ApiResponse<ClassDto>
            {
                Success = true,
                Data = classDto
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
    public async Task<ActionResult<ApiResponse<IEnumerable<ClassDto>>>> GetPopularClasses(
        [FromQuery] int limit = 20)
    {
        try
        {
            var classes = await _classRepository.GetPopularClassesAsync(limit);
            var classDtos = classes.Select(MapToDto);

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
    /// Create a new class
    /// </summary>
    [HttpPost]
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

            var classDto = MapToDto(classEntity);
            return CreatedAtAction(nameof(GetClass), new { id = classEntity.Id }, new ApiResponse<ClassDto>
            {
                Success = true,
                Data = classDto,
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
    /// Update a class
    /// </summary>
    [HttpPut("{id}")]
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

            var classDto = MapToDto(classEntity);
            return Ok(new ApiResponse<ClassDto>
            {
                Success = true,
                Data = classDto,
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
    /// Delete a class
    /// </summary>
    [HttpDelete("{id}")]
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

    private static ClassDto MapToDto(Class classEntity)
    {
        return new ClassDto
        {
            Id = classEntity.Id,
            Name = classEntity.Name,
            Type = classEntity.Type,
            Description = classEntity.Description,
            InstructorId = classEntity.InstructorId,
            InstructorName = classEntity.InstructorName,
            Level = classEntity.Level,
            StartTime = classEntity.StartTime,
            DurationMinutes = classEntity.DurationMinutes,
            Capacity = classEntity.Capacity,
            CurrentEnrollment = classEntity.CurrentEnrollment,
            AverageRating = classEntity.AverageRating,
            TotalRatings = classEntity.TotalRatings,
            WeeklyBookings = classEntity.WeeklyBookings,
            IsActive = classEntity.IsActive
        };
    }
}
