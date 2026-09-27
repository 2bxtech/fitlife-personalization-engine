using FitLife.Api.Observability;
using FitLife.Core.Interfaces;
using FitLife.Core.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace FitLife.Api.Events;

/// <summary>
/// Idempotently persists a validated user event and invalidates affected caches.
/// Shared by the Kafka consumer and the Kafka-free Direct transport so both paths
/// keep the same duplicate-handling boundary (EventId lookup + unique index).
/// </summary>
public sealed class InteractionEventRecorder
{
    private static readonly string[] CacheInvalidatingEvents =
        { EventTypes.Book, EventTypes.Cancel, EventTypes.Complete, EventTypes.Rate };

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;
    private readonly FitLifeMetrics? _metrics;

    public InteractionEventRecorder(IServiceProvider serviceProvider, ILogger<InteractionEventRecorder> logger)
        : this(serviceProvider, (ILogger)logger)
    {
    }

    internal InteractionEventRecorder(IServiceProvider serviceProvider, ILogger logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _metrics = serviceProvider.GetService<FitLifeMetrics>();
    }

    /// <summary>Returns false when the event was already stored (duplicate delivery).</summary>
    public async Task<bool> RecordAsync(UserEvent userEvent)
    {
        var stored = false;
        using (var scope = _serviceProvider.CreateScope())
        {
            var interactionRepository = scope.ServiceProvider.GetRequiredService<IInteractionRepository>();
            if (await interactionRepository.ExistsByEventIdAsync(userEvent.EventId))
            {
                _logger.LogInformation("Ignoring duplicate event {EventId}", userEvent.EventId);
            }
            else
            {
                var interaction = new Interaction
                {
                    EventId = userEvent.EventId,
                    UserId = userEvent.UserId,
                    ItemId = userEvent.ItemId,
                    ItemType = userEvent.ItemType,
                    EventType = userEvent.EventType,
                    Timestamp = userEvent.OccurredAt,
                    Metadata = userEvent.Metadata != null
                        ? JsonSerializer.Serialize(userEvent.Metadata)
                        : "{}"
                };
                await interactionRepository.AddAsync(interaction);

                try
                {
                    await interactionRepository.SaveChangesAsync();
                    stored = true;
                    _logger.LogInformation(
                        "Stored interaction: {InteractionId} - User={UserId}, Event={EventType}",
                        interaction.Id, interaction.UserId, interaction.EventType);
                }
                catch (DbUpdateException ex) when (IsDuplicateEventId(ex))
                {
                    // The unique EventId index is the final concurrency boundary.
                    // Continue to idempotent cache invalidation so a prior
                    // post-write failure can recover.
                    _logger.LogInformation(
                        "Ignoring duplicate event {EventId} (unique constraint)", userEvent.EventId);
                }
            }
        }

        _metrics?.EventRecorded(stored);

        if (CacheInvalidatingEvents.Contains(userEvent.EventType))
        {
            using var scope = _serviceProvider.CreateScope();
            var recommendationService = scope.ServiceProvider.GetRequiredService<IRecommendationService>();
            await recommendationService.InvalidateCacheAsync(userEvent.UserId);
            _logger.LogDebug("Invalidated recommendation cache for user {UserId} after {EventType}",
                userEvent.UserId, userEvent.EventType);
        }

        return stored;
    }

    private static bool IsDuplicateEventId(DbUpdateException ex) =>
        ex.InnerException is SqlException sqlException
        && (sqlException.Number == 2601 || sqlException.Number == 2627);
}
