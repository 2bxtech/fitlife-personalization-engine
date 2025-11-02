namespace FitLife.Core.DTOs;

/// <summary>
/// Data transfer object for class information
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
/// Recommendation data transfer object with personalized score and explanation
/// </summary>
public class RecommendationDto
{
    public int Rank { get; set; }
    public double Score { get; set; }
    public string Reason { get; set; } = string.Empty;
    public ClassDto Class { get; set; } = null!;
    public DateTime GeneratedAt { get; set; }
}
