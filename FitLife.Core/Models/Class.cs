namespace FitLife.Core.Models;

/// <summary>
/// Represents a fitness class offering
/// </summary>
public class Class
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Class type: Yoga, HIIT, Strength, Cardio, Cycling, Pilates, etc.
    /// </summary>
    public string Type { get; set; } = string.Empty;
    
    public string Description { get; set; } = string.Empty;
    public string InstructorId { get; set; } = string.Empty;
    public string InstructorName { get; set; } = string.Empty;
    
    /// <summary>
    /// Difficulty level: Beginner, Intermediate, Advanced
    /// </summary>
    public string Level { get; set; } = "Beginner";
    
    public DateTime StartTime { get; set; }
    public int DurationMinutes { get; set; } = 60;
    
    public int Capacity { get; set; } = 30;
    public int CurrentEnrollment { get; set; } = 0;
    
    /// <summary>
    /// Average rating from user feedback (0-5 scale)
    /// </summary>
    public decimal AverageRating { get; set; } = 0m;
    
    public int TotalRatings { get; set; } = 0;
    
    /// <summary>
    /// Total bookings per week for popularity scoring
    /// </summary>
    public int WeeklyBookings { get; set; } = 0;
    
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
