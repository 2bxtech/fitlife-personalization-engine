namespace FitLife.Api.Configuration;

/// <summary>How accepted user events reach the Interactions table.</summary>
public enum EventTransportMode
{
    /// <summary>Publish to Kafka; a separate Consumer process persists events.</summary>
    Kafka,

    /// <summary>Persist in the API request. No broker, consumer, or DLQ.</summary>
    Direct
}

public static class EventTransport
{
    public static EventTransportMode Read(IConfiguration configuration) =>
        configuration["Events:Transport"] switch
        {
            null or "Kafka" => EventTransportMode.Kafka,
            "Direct" => EventTransportMode.Direct,
            _ => throw new InvalidOperationException("Events:Transport must be Kafka or Direct.")
        };
}
