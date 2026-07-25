using FitLife.Core.Models;

namespace FitLife.Core.Interfaces;

public interface IEventPublisher
{
    Task PublishAsync(
        string topic,
        string key,
        UserEvent userEvent,
        CancellationToken cancellationToken = default);
}
