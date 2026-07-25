using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using FitLife.Core.Interfaces;
using FitLife.Core.Models;

namespace FitLife.Infrastructure.Kafka;

/// <summary>
/// Kafka producer service for publishing user events to the event stream
/// </summary>
public class KafkaProducer : IEventPublisher, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaProducer> _logger;
    private readonly string _bootstrapServers;
    private bool _disposed = false;

    public KafkaProducer(IConfiguration configuration, ILogger<KafkaProducer> logger)
    {
        _logger = logger;
        _bootstrapServers = configuration["Kafka:BootstrapServers"] 
            ?? throw new InvalidOperationException("Kafka:BootstrapServers not configured");

        var config = new ProducerConfig
        {
            BootstrapServers = _bootstrapServers,
            ClientId = "fitlife-api-producer",
            Acks = Acks.All, // Required when EnableIdempotence = true
            MessageTimeoutMs = 30000, // 30 seconds timeout
            RequestTimeoutMs = 30000,
            EnableIdempotence = true, // Prevent duplicate messages
            MaxInFlight = 5,
            CompressionType = CompressionType.Snappy,
            LingerMs = 10, // Small batching for better throughput
            BatchSize = 16384
        };

        _producer = new ProducerBuilder<string, string>(config)
            .SetErrorHandler((_, error) =>
            {
                _logger.LogError("Kafka error: Code={Code}, Reason={Reason}, IsFatal={IsFatal}",
                    error.Code, error.Reason, error.IsFatal);
            })
            .SetLogHandler((_, log) =>
            {
                if (log.Level <= SyslogLevel.Warning)
                {
                    _logger.LogWarning("Kafka log: {Message}", log.Message);
                }
            })
            .Build();

        _logger.LogInformation("Kafka producer initialized with bootstrap servers: {Servers}", _bootstrapServers);
    }

    /// <summary>
    /// Publishes an event to the specified Kafka topic
    /// Fire-and-forget pattern for API performance
    /// </summary>
    /// <param name="topic">Topic name (e.g., "user-events")</param>
    /// <param name="key">Partition key - use UserId for ordered processing per user</param>
    /// <param name="userEvent">Versioned event envelope to serialize.</param>
    public async Task PublishAsync(
        string topic,
        string key,
        UserEvent userEvent,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(userEvent);
            var message = new Message<string, string>
            {
                Key = key,
                Value = json,
                Timestamp = new Timestamp(DateTime.UtcNow)
            };

            var result = await _producer.ProduceAsync(
                topic,
                message,
                cancellationToken);
            _logger.LogDebug(
                "Message published to {Topic} [partition {Partition}, offset {Offset}]",
                result.Topic,
                result.Partition.Value,
                result.Offset.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error producing message to topic {Topic}", topic);
            throw;
        }
    }

    /// <summary>
    /// Synchronous produce for background workers where blocking is acceptable
    /// </summary>
    public DeliveryResult<string, string> ProduceSync(string topic, string key, object value)
    {
        try
        {
            var json = JsonSerializer.Serialize(value);
            var message = new Message<string, string>
            {
                Key = key,
                Value = json,
                Timestamp = new Timestamp(DateTime.UtcNow)
            };

            var result = _producer.ProduceAsync(topic, message).GetAwaiter().GetResult();
            
            _logger.LogInformation(
                "Message delivered to {Topic} [partition {Partition}, offset {Offset}]",
                result.Topic, result.Partition.Value, result.Offset.Value);

            return result;
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(ex, 
                "Failed to deliver message to topic {Topic}: {Error}", 
                topic, ex.Error.Reason);
            throw;
        }
    }

    /// <summary>
    /// Flushes any pending messages - call before shutdown
    /// </summary>
    public void Flush(TimeSpan timeout)
    {
        try
        {
            var remaining = _producer.Flush(timeout);
            if (remaining > 0)
            {
                _logger.LogWarning("{Count} messages were not flushed", remaining);
            }
            else
            {
                _logger.LogInformation("All messages flushed successfully");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error flushing producer");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        try
        {
            // Flush pending messages with 30 second timeout
            Flush(TimeSpan.FromSeconds(30));
            _producer?.Dispose();
            _logger.LogInformation("Kafka producer disposed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing Kafka producer");
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
