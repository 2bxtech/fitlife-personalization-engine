using FitLife.Api.DTOs;
using FitLife.Core.Models;
using FitLife.Infrastructure.Kafka;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitLife.Api.Controllers;

/// <summary>
/// Event tracking endpoint for user interactions
/// Publishes events to Kafka for downstream processing
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EventsController : ControllerBase
{
    private readonly KafkaProducer _kafkaProducer;
    private readonly ILogger<EventsController> _logger;
    private const string UserEventsTopic = "user-events";

    public EventsController(
        KafkaProducer kafkaProducer,
        ILogger<EventsController> logger)
    {
        _kafkaProducer = kafkaProducer;
        _logger = logger;
    }

    /// <summary>
    /// Track a user interaction event
    /// </summary>
    /// <remarks>
    /// Event types:
    /// - View: User viewed a class
    /// - Click: User clicked on a class for details
    /// - Book: User booked a class
    /// - Complete: User completed a class
    /// - Cancel: User cancelled a booking
    /// - Rate: User rated a class
    /// 
    /// Example metadata:
    /// - { "source": "browse", "page": 2 }
    /// - { "rating": 5, "comment": "Great class!" }
    /// - { "duration": 45, "calories": 350 }
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> TrackEvent([FromBody] EventDto eventDto)
    {
        try
        {
            // Validate EventType
            if (!EventTypes.IsValid(eventDto.EventType))
            {
                _logger.LogWarning(
                    "Invalid event type received: {EventType} from user {UserId}",
                    eventDto.EventType, eventDto.UserId);

                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Invalid event type",
                    Errors = new List<string>
                    {
                        $"EventType must be one of: {string.Join(", ", EventTypes.ValidTypes)}"
                    }
                });
            }

            // Validate user from JWT token matches UserId in request
            // JwtRegisteredClaimNames.Sub becomes ClaimTypes.NameIdentifier in ASP.NET Core
            var tokenUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value 
                           ?? User.FindFirst("sub")?.Value;
            
            if (string.IsNullOrEmpty(tokenUserId))
            {
                _logger.LogError("Unable to extract user ID from JWT token. Claims: {Claims}", 
                    string.Join(", ", User.Claims.Select(c => $"{c.Type}={c.Value}")));
                return Unauthorized("Invalid token");
            }
            
            if (tokenUserId != eventDto.UserId)
            {
                _logger.LogWarning(
                    "User {TokenUserId} attempted to track event for different user {EventUserId}",
                    tokenUserId, eventDto.UserId);

                return Forbid();
            }

            // Create event object
            var userEvent = new UserEvent
            {
                UserId = eventDto.UserId,
                ItemId = eventDto.ItemId,
                ItemType = eventDto.ItemType,
                EventType = eventDto.EventType,
                Timestamp = DateTime.UtcNow,
                Metadata = eventDto.Metadata
            };

            // Publish to Kafka (fire-and-forget for performance)
            // Partition key = UserId ensures events for same user are processed in order
            await _kafkaProducer.ProduceAsync(
                topic: UserEventsTopic,
                key: userEvent.UserId,
                value: userEvent
            );

            _logger.LogInformation(
                "Event tracked: User {UserId} performed {EventType} on {ItemType} {ItemId}",
                userEvent.UserId, userEvent.EventType, userEvent.ItemType, userEvent.ItemId);

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "Event tracked successfully",
                Data = new
                {
                    eventId = Guid.NewGuid().ToString(),
                    timestamp = userEvent.Timestamp
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to track event for user {UserId}",
                eventDto.UserId);

            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = "Failed to track event",
                Errors = new List<string> { "An error occurred while processing your request" }
            });
        }
    }

    /// <summary>
    /// Batch track multiple events at once (for efficiency)
    /// </summary>
    [HttpPost("batch")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> TrackEventBatch([FromBody] List<EventDto> events)
    {
        try
        {
            if (events == null || events.Count == 0)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "No events provided"
                });
            }

            if (events.Count > 100)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Maximum 100 events per batch"
                });
            }

            var tokenUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value 
                           ?? User.FindFirst("sub")?.Value;
            var publishedCount = 0;
            var errors = new List<string>();

            foreach (var eventDto in events)
            {
                // Validate each event
                if (!EventTypes.IsValid(eventDto.EventType))
                {
                    errors.Add($"Invalid event type: {eventDto.EventType}");
                    continue;
                }

                if (tokenUserId != eventDto.UserId)
                {
                    errors.Add($"User mismatch for event: {eventDto.ItemId}");
                    continue;
                }

                var userEvent = new UserEvent
                {
                    UserId = eventDto.UserId,
                    ItemId = eventDto.ItemId,
                    ItemType = eventDto.ItemType,
                    EventType = eventDto.EventType,
                    Timestamp = DateTime.UtcNow,
                    Metadata = eventDto.Metadata
                };

                await _kafkaProducer.ProduceAsync(UserEventsTopic, userEvent.UserId, userEvent);
                publishedCount++;
            }

            _logger.LogInformation(
                "Batch tracked {Published}/{Total} events for user {UserId}",
                publishedCount, events.Count, tokenUserId);

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = $"Published {publishedCount} of {events.Count} events",
                Data = new
                {
                    published = publishedCount,
                    total = events.Count,
                    errors = errors.Count > 0 ? errors : null
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process event batch");
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = "Failed to process event batch"
            });
        }
    }
}
