namespace FitLife.Core.Models;

/// <summary>
/// Represents a user/member in the FitLife system
/// </summary>
public class User
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    
    /// <summary>
    /// Fitness level: Beginner, Intermediate, Advanced
    /// </summary>
    public string FitnessLevel { get; set; } = "Beginner";
    
    /// <summary>
    /// User goals stored as JSON array: ["Weight Loss", "Muscle Gain", "Flexibility"]
    /// </summary>
    public string Goals { get; set; } = "[]";
    
    /// <summary>
    /// Preferred class types stored as JSON array: ["Yoga", "HIIT", "Strength"]
    /// </summary>
    public string PreferredClassTypes { get; set; } = "[]";
    
    /// <summary>
    /// User segment: Beginner, HighlyActive, YogaEnthusiast, StrengthTrainer, CardioLover, WeekendWarrior, General
    /// </summary>
    public string? Segment { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual ICollection<Interaction> Interactions { get; set; } = new List<Interaction>();
    public virtual ICollection<Recommendation> Recommendations { get; set; } = new List<Recommendation>();
}
