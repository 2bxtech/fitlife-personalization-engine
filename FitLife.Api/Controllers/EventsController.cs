using FitLife.Core.DTOs;
using FitLife.Core.Models;
using FitLife.Core.Interfaces;
using FitLife.Api.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

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
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<EventsController> _logger;
    private const string UserEventsTopic = "user-events";

    public EventsController(
        IEventPublisher eventPublisher,
        ILogger<EventsController> logger)
    {
        _eventPublisher = eventPublisher;
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
            // Validate user from JWT token matches UserId in request
            // JwtRegisteredClaimNames.Sub becomes ClaimTypes.NameIdentifier in ASP.NET Core
            var tokenUserId = User.GetSubjectId();
            
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

            var validationError = ValidateEvent(eventDto);
            if (validationError != null)
                return BadRequest(EventContractError(validationError));

            var userEvent = CreateUserEvent(eventDto);

            // Wait for broker acknowledgement before accepting the event.
            // Partition key = UserId preserves per-user ordering.
            await _eventPublisher.PublishAsync(
                topic: UserEventsTopic,
                key: userEvent.UserId,
                userEvent: userEvent,
                cancellationToken: HttpContext.RequestAborted
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
                    eventId = userEvent.EventId,
                    schemaVersion = userEvent.SchemaVersion,
                    occurredAt = userEvent.OccurredAt
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

            var tokenUserId = User.GetSubjectId();
            if (tokenUserId == null)
            {
                return Unauthorized("Invalid token");
            }

            if (events.Any(eventDto => eventDto.UserId != tokenUserId))
            {
                return Forbid();
            }

            var validationErrors = events
                .Select((eventDto, index) => new
                {
                    Index = index,
                    Error = ValidateEvent(eventDto)
                })
                .Where(result => result.Error != null)
                .Select(result => $"events[{result.Index}]: {result.Error}")
                .ToList();
            if (validationErrors.Count > 0)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Event batch validation failed",
                    Errors = validationErrors
                });
            }

            var publishedCount = 0;
            var publishedEventIds = new List<string>();

            foreach (var eventDto in events)
            {
                var userEvent = CreateUserEvent(eventDto);
                await _eventPublisher.PublishAsync(
                    UserEventsTopic,
                    userEvent.UserId,
                    userEvent,
                    HttpContext.RequestAborted);
                publishedCount++;
                publishedEventIds.Add(userEvent.EventId);
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
                    eventIds = publishedEventIds
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

    private UserEvent CreateUserEvent(EventDto eventDto)
    {
        var occurredAt = eventDto.OccurredAt ?? DateTime.UtcNow;
        return new UserEvent
        {
            EventId = eventDto.EventId ?? Guid.NewGuid().ToString(),
            SchemaVersion = eventDto.SchemaVersion ?? 1,
            OccurredAt = occurredAt,
            CorrelationId =
                HttpContext.Items["CorrelationId"]?.ToString() ?? string.Empty,
            CausationId = eventDto.CausationId,
            UserId = eventDto.UserId,
            ItemId = eventDto.ItemId,
            ItemType = eventDto.ItemType,
            EventType = eventDto.EventType,
            Timestamp = occurredAt,
            Metadata = eventDto.Metadata
        };
    }

    private static string? ValidateEvent(EventDto eventDto)
    {
        if (!EventTypes.IsValid(eventDto.EventType))
            return $"EventType must be one of: {string.Join(", ", EventTypes.ValidTypes)}";
        if (string.IsNullOrWhiteSpace(eventDto.ItemId)
            || eventDto.ItemId.Length > 200)
            return "ItemId is required and must be 200 characters or fewer";
        if (string.IsNullOrWhiteSpace(eventDto.ItemType)
            || eventDto.ItemType.Length > 50)
            return "ItemType is required and must be 50 characters or fewer";
        if (eventDto.EventId != null
            && (!Guid.TryParse(eventDto.EventId, out _)
                || eventDto.EventId.Length > 100))
            return "EventId must be a valid GUID";
        if (eventDto.SchemaVersion is not null and not 1)
            return "SchemaVersion must be 1";

        var occurredAt = eventDto.OccurredAt ?? DateTime.UtcNow;
        if (occurredAt < DateTime.UtcNow.AddHours(-24)
            || occurredAt > DateTime.UtcNow.AddMinutes(5))
            return "OccurredAt must be within the last 24 hours and no more than 5 minutes in the future";

        if (eventDto.Metadata != null
            && JsonSerializer.SerializeToUtf8Bytes(eventDto.Metadata).Length
            > 8 * 1024)
            return "Metadata must be 8 KiB or smaller";

        return null;
    }

    private static ApiResponse<object> EventContractError(string error) => new()
    {
        Success = false,
        Message = "Event validation failed",
        Errors = new List<string> { error }
    };
}
