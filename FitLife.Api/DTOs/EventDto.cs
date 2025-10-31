namespace FitLife.Api.DTOs;

/// <summary>
/// DTO for tracking user interaction events
/// </summary>
public class EventDto
{
    public string UserId { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public string ItemType { get; set; } = "Class";
    
    /// <summary>
    /// Must be one of: View, Click, Book, Complete, Cancel, Rate
    /// </summary>
    public string EventType { get; set; } = string.Empty;
    
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Standard API response wrapper
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Message { get; set; }
    public List<string>? Errors { get; set; }
}
