using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace ApiGateway.Controllers;

/// <summary>
/// Stub endpoints at /api/notifications consumed by the frontend SPA.
/// These return valid JSON so the UI does not crash while persistence is not yet implemented.
/// </summary>
[ApiController]
[Route("api/notifications")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class NotificationsStubController : ControllerBase
{
    /// <summary>Returns an empty list of recent notifications for the authenticated user.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult GetNotifications() => Ok(Array.Empty<object>());

    /// <summary>Marks all notifications as read for the authenticated user.</summary>
    [HttpPost("read-all")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult MarkAllRead() => Ok(new { });

    /// <summary>Returns default notification preferences for the authenticated user.</summary>
    [HttpGet("preferences")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult GetPreferences()
    {
        var defaults = new[]
        {
            new { eventType = "TransactionCreated",                  emailEnabled = true, smsEnabled = true },
            new { eventType = "PaymentApproved",                     emailEnabled = true, smsEnabled = true },
            new { eventType = "PaymentBatchSubmittedForApproval",    emailEnabled = true, smsEnabled = true },
            new { eventType = "RepaymentCompleted",                  emailEnabled = true, smsEnabled = true },
            new { eventType = "KycStatusChanged",                    emailEnabled = true, smsEnabled = true },
            new { eventType = "SuspiciousActivityFlagged",           emailEnabled = true, smsEnabled = true },
        };
        return Ok(defaults);
    }

    /// <summary>Accepts updated notification preferences. Persistence is not yet implemented.</summary>
    [HttpPut("preferences")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult SavePreferences([FromBody] JsonElement body) => Ok(new { });
}
