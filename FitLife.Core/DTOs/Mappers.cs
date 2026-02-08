using FitLife.Core.Models;
using System.Text.Json;

namespace FitLife.Core.DTOs;

/// <summary>
/// Centralized DTO mapping methods to avoid duplication across controllers
/// </summary>
public static class DtoMappers
{
    /// <summary>
    /// Maps a User entity to UserDto
    /// </summary>
    public static UserDto MapToUserDto(User user)
    {
        return new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            FitnessLevel = user.FitnessLevel,
            Goals = JsonSerializer.Deserialize<List<string>>(user.Goals ?? "[]") ?? new(),
            PreferredClassTypes = JsonSerializer.Deserialize<List<string>>(user.PreferredClassTypes ?? "[]") ?? new(),
            Segment = user.Segment,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }

    /// <summary>
    /// Maps a Class entity to ClassDto
    /// </summary>
    public static ClassDto MapToClassDto(Class classEntity)
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
