using Confluent.Kafka;
using FitLife.Core.Interfaces;
using FitLife.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace FitLife.Api.BackgroundServices;

/// <summary>
/// Background service that consumes Kafka events and stores them in the Interactions table
/// Processes user interaction events (View, Click, Book, Complete, Cancel, Rate)
/// </summary>
public class EventConsumerService : BackgroundService
{
    private readonly ILogger<EventConsumerService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;
    private IConsumer<string, string>? _consumer;

    public EventConsumerService(
        ILogger<EventConsumerService> logger,
        IConfiguration configuration,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _configuration = configuration;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Check if worker is enabled
        var enabled = _configuration.GetValue<bool>("BackgroundWorkers:EventConsumer:Enabled", true);
        if (!enabled)
        {
            _logger.LogInformation("EventConsumerService is disabled in configuration");
            return;
        }

        var bootstrapServers = _configuration["Kafka:BootstrapServers"]
            ?? throw new InvalidOperationException("Kafka:BootstrapServers not configured");

        var groupId = _configuration["Kafka:GroupId"] ?? "fitlife-event-consumers";
        var topic = "user-events";

        var config = new ConsumerConfig
        {
            GroupId = groupId,
            BootstrapServers = bootstrapServers,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false, // Manual commit after processing
            MaxPollIntervalMs = 300000, // 5 minutes
            SessionTimeoutMs = 45000,
            EnablePartitionEof = false
        };

        _consumer = new ConsumerBuilder<string, string>(config)
            .SetErrorHandler((_, e) => _logger.LogError("Kafka consumer error: {Reason}", e.Reason))
            .SetPartitionsAssignedHandler((c, partitions) =>
            {
                _logger.LogInformation("Partitions assigned: {Partitions}",
                    string.Join(", ", partitions.Select(p => $"{p.Topic}[{p.Partition}]")));
            })
            .Build();

        _consumer.Subscribe(topic);

        _logger.LogInformation(
            "EventConsumerService started - Consuming from topic '{Topic}' with group '{GroupId}'",
            topic, groupId);

        var batchSize = _configuration.GetValue<int>("BackgroundWorkers:EventConsumer:BatchSize", 100);
        var pollIntervalMs = _configuration.GetValue<int>("BackgroundWorkers:EventConsumer:PollIntervalMs", 1000);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var consumeResult = _consumer.Consume(TimeSpan.FromMilliseconds(pollIntervalMs));

                if (consumeResult != null)
                {
                    await ProcessEventAsync(consumeResult.Message, stoppingToken);

                    // Manual commit after successful processing
                    try
                    {
                        _consumer.Commit(consumeResult);
                    }
                    catch (KafkaException ex)
                    {
                        _logger.LogError(ex, "Failed to commit offset for message at {Partition}:{Offset}",
                            consumeResult.Partition.Value, consumeResult.Offset.Value);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("EventConsumerService is shutting down");
                break;
            }
            catch (ConsumeException ex)
            {
                _logger.LogError(ex, "Error consuming message: {Error}", ex.Error.Reason);
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); // Back off on errors
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in EventConsumerService");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); // Back off on errors
            }
        }

        _logger.LogInformation("EventConsumerService stopped");
    }

    /// <summary>
    /// Processes a single Kafka event message and stores it in the database
    /// </summary>
    private async Task ProcessEventAsync(Message<string, string> message, CancellationToken cancellationToken)
    {
        try
        {
            // Deserialize the event
            var userEvent = JsonSerializer.Deserialize<UserEvent>(message.Value);
            if (userEvent == null)
            {
                _logger.LogWarning("Failed to deserialize event message: {Message}", message.Value);
                return;
            }

            _logger.LogDebug(
                "Processing event: User={UserId}, Item={ItemId}, Type={EventType}",
                userEvent.UserId, userEvent.ItemId, userEvent.EventType);

            // Create interaction entity
            var metadataJson = userEvent.Metadata != null 
                ? JsonSerializer.Serialize(userEvent.Metadata) 
                : "{}";

            var interaction = new Interaction
            {
                UserId = userEvent.UserId,
                ItemId = userEvent.ItemId,
                ItemType = userEvent.ItemType,
                EventType = userEvent.EventType,
                Timestamp = userEvent.Timestamp,
                Metadata = metadataJson
            };

            // Store in database using scoped service
            using (var scope = _serviceProvider.CreateScope())
            {
                var interactionRepository = scope.ServiceProvider.GetRequiredService<IInteractionRepository>();
                await interactionRepository.AddAsync(interaction);

                _logger.LogInformation(
                    "Stored interaction: {InteractionId} - User={UserId}, Event={EventType}",
                    interaction.Id, interaction.UserId, interaction.EventType);
            }

            // Check if cache invalidation is needed (for Book events)
            if (userEvent.EventType == "Book")
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var recommendationService = scope.ServiceProvider.GetRequiredService<IRecommendationService>();
                    await recommendationService.InvalidateCacheAsync(userEvent.UserId);

                    _logger.LogDebug("Invalidated recommendation cache for user {UserId} after booking", 
                        userEvent.UserId);
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize event JSON: {Message}", message.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing event: {Message}", message.Value);
            throw; // Re-throw to prevent commit (will be retried)
        }
    }

    public override void Dispose()
    {
        try
        {
            _consumer?.Close();
            _consumer?.Dispose();
            _logger.LogInformation("EventConsumerService disposed gracefully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing EventConsumerService");
        }

        base.Dispose();
    }
}
