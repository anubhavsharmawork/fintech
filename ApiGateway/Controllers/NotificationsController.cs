using ApiGateway.Services;
using ApiGateway.Stores;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.Controllers;

[ApiController]
[Route("api/v1/notifications")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class NotificationsController : ControllerBase
{
    private readonly NotificationPreferenceService _preferenceService;
    private readonly RecentNotificationStore _store;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(
        NotificationPreferenceService preferenceService,
        RecentNotificationStore store,
        ILogger<NotificationsController> logger)
    {
        _preferenceService = preferenceService;
        _store = store;
        _logger = logger;
    }

    /// <summary>
    /// Returns the authenticated user's notification preferences.
    /// </summary>
    [HttpGet("preferences")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetPreferences()
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var prefs = await _preferenceService.GetPreferences(userId);
        return Ok(prefs.Select(p => new
        {
            eventType = p.EventType,
            emailEnabled = p.EmailEnabled,
            smsEnabled = p.SmsEnabled
        }));
    }

    /// <summary>
    /// Updates a notification preference for the authenticated user.
    /// </summary>
    [HttpPut("preferences/{eventType}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdatePreference(string eventType, [FromBody] UpdatePreferenceRequest request)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(eventType))
            return BadRequest(new { message = "eventType is required." });

        await _preferenceService.UpdatePreference(userId, eventType, request.EmailEnabled, request.SmsEnabled);

        _logger.LogInformation(
            "Notification preference updated: UserId={UserId} EventType={EventType} Email={Email} Sms={Sms}",
            userId, eventType, request.EmailEnabled, request.SmsEnabled);

        return NoContent();
    }

    /// <summary>
    /// Returns the 5 most recent notification events for the authenticated user.
    /// </summary>
    [HttpGet("recent")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult GetRecent()
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var recent = _store.GetRecent(userId, 5);
        return Ok(recent.Select(n => new
        {
            id = n.Id,
            eventType = n.EventType,
            message = n.Message,
            timestamp = n.Timestamp,
            read = n.Read
        }));
    }

    private bool TryGetUserId(out Guid userId)
    {
        var claim = User.FindFirst("sub")?.Value ?? User.FindFirst("id")?.Value;
        return Guid.TryParse(claim, out userId);
    }
}

public record UpdatePreferenceRequest(bool EmailEnabled, bool SmsEnabled);
