using FitLife.Core.Interfaces;
using FitLife.Core.Models;

namespace FitLife.Api.Events;

/// <summary>
/// Kafka-free transport for the minimal demo: records the event in-process before
/// the request returns. Acceptance therefore means "committed to SQL", and a failure
/// surfaces to the caller instead of being retried or dead-lettered by a consumer.
/// </summary>
public sealed class DirectEventPublisher : IEventPublisher
{
    private readonly InteractionEventRecorder _recorder;

    public DirectEventPublisher(InteractionEventRecorder recorder) => _recorder = recorder;

    public async Task PublishAsync(
        string topic,
        string key,
        UserEvent userEvent,
        CancellationToken cancellationToken = default)
    {
        // Do not abandon a half-finished write when the client disconnects; the
        // write is short and idempotent, so finishing it is the safer outcome.
        await _recorder.RecordAsync(userEvent);
    }
}
