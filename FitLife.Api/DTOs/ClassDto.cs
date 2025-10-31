namespace FitLife.Api.DTOs;

/// <summary>
/// Class data transfer object for API responses
/// </summary>
public class ClassDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string InstructorId { get; set; } = string.Empty;
    public string InstructorName { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public int DurationMinutes { get; set; }
    public int Capacity { get; set; }
    public int CurrentEnrollment { get; set; }
    public int AvailableSpots => Capacity - CurrentEnrollment;
    public decimal AverageRating { get; set; }
    public int TotalRatings { get; set; }
    public int WeeklyBookings { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// DTO for creating a new class
/// </summary>
public class CreateClassDto
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string InstructorId { get; set; } = string.Empty;
    public string InstructorName { get; set; } = string.Empty;
    public string Level { get; set; } = "Beginner";
    public DateTime StartTime { get; set; }
    public int DurationMinutes { get; set; } = 60;
    public int Capacity { get; set; } = 30;
}

/// <summary>
/// DTO for updating a class
/// </summary>
public class UpdateClassDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public DateTime? StartTime { get; set; }
    public int? Capacity { get; set; }
    public bool? IsActive { get; set; }
}
