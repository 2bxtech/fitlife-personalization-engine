using FitLife.Core.Models;

namespace FitLife.Core.Interfaces;

public interface IDeadLetterPublisher
{
    Task PublishDeadLetterAsync(
        string topic,
        string key,
        DeadLetterEvent deadLetterEvent,
        CancellationToken cancellationToken = default);
}
