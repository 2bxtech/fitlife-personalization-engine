namespace FitLife.Core.Models;

/// <summary>
/// Represents a user interaction event (view, click, book, complete, cancel, rate)
/// Event store for building recommendation history
/// </summary>
public class Interaction
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    public string UserId { get; set; } = string.Empty;
    
    /// <summary>
    /// The item being interacted with (typically a Class ID)
    /// </summary>
    public string ItemId { get; set; } = string.Empty;
    
    /// <summary>
    /// Type of item: Class, Instructor, etc.
    /// </summary>
    public string ItemType { get; set; } = "Class";
    
    /// <summary>
    /// Event type: View, Click, Book, Complete, Cancel, Rate
    /// MUST be one of these exact values for scoring algorithm
    /// </summary>
    public string EventType { get; set; } = string.Empty;
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Additional event data stored as JSON
    /// For Rate events: { "rating": 5 }
    /// For View events: { "source": "browse", "durationSeconds": 15 }
    /// </summary>
    public string Metadata { get; set; } = "{}";
    
    // Navigation property
    public virtual User? User { get; set; }
}
