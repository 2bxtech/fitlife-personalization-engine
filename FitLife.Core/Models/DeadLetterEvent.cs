namespace FitLife.Core.Models;

public class DeadLetterEvent
{
    public string DeadLetterId { get; set; } = Guid.NewGuid().ToString();
    public int SchemaVersion { get; set; } = 1;
    public DateTime FailedAt { get; set; } = DateTime.UtcNow;
    public string SourceTopic { get; set; } = string.Empty;
    public int SourcePartition { get; set; }
    public long SourceOffset { get; set; }
    public string MessageKey { get; set; } = string.Empty;
    public string? EventId { get; set; }
    public string Disposition { get; set; } = string.Empty;
    public int Attempts { get; set; }
}
