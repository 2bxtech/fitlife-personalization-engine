namespace FitLife.Core.Models;

/// <summary>
/// Valid event types for user interactions
/// </summary>
public static class EventTypes
{
    public const string View = "View";
    public const string Click = "Click";
    public const string Book = "Book";
    public const string Complete = "Complete";
    public const string Cancel = "Cancel";
    public const string Rate = "Rate";

    public static readonly string[] ValidTypes = 
    {
        View, Click, Book, Complete, Cancel, Rate
    };

    public static bool IsValid(string eventType)
    {
        return ValidTypes.Contains(eventType, StringComparer.OrdinalIgnoreCase);
    }
}

/// <summary>
/// User event for Kafka streaming
/// Represents all user interactions with classes
/// </summary>
public class UserEvent
{
    /// <summary>
    /// User who performed the action
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Item being interacted with (usually ClassId)
    /// </summary>
    public string ItemId { get; set; } = string.Empty;

    /// <summary>
    /// Type of item (Class, Instructor, etc.)
    /// </summary>
    public string ItemType { get; set; } = "Class";

    /// <summary>
    /// Event type: View, Click, Book, Complete, Cancel, Rate
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp of the event
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Additional event metadata (JSON serializable)
    /// Example: { "source": "browse", "rating": 5, "duration": 45 }
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}
