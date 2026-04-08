using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace ApiGateway.Controllers;

[ApiController]
[Route("api/v1/feedback")]
[Authorize]
public class FeedbackController : ControllerBase
{
    private readonly ILogger<FeedbackController> _logger;

    // In-memory store for feedback and rate tracking (use a database-backed repository in production)
    private static readonly ConcurrentBag<FeedbackEntry> _feedbackStore = new();
    private static readonly ConcurrentDictionary<string, List<DateTime>> _rateLedger = new();

    // PII patterns: credit card numbers (13-19 digits with optional separators) and NZ bank account format (XX-XXXX-XXXXXXX-XXX)
    private static readonly Regex CreditCardPattern = new(
        @"\b(?:\d[ -]*?){13,19}\b",
        RegexOptions.Compiled);

    private static readonly Regex BankAccountPattern = new(
        @"\b\d{2}[- ]?\d{4}[- ]?\d{7}[- ]?\d{2,3}\b",
        RegexOptions.Compiled);

    public FeedbackController(ILogger<FeedbackController> logger)
    {
        _logger = logger;
    }

    [HttpPost]
    [EnableRateLimiting("feedback")]
    public IActionResult Submit([FromBody] FeedbackRequest request)
    {
        // --- Validation ---
        if (request == null || string.IsNullOrWhiteSpace(request.Message))
        {
            return Problem(
                detail: "Message is required.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var message = request.Message.Trim();

        if (message.Length < 10)
        {
            return Problem(
                detail: "Message must be at least 10 characters.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (message.Length > 2000)
        {
            return Problem(
                detail: "Message must not exceed 2000 characters.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        // --- User identity ---
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                       ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        // --- Per-user daily rate limit (5 per day) ---
        var today = DateTime.UtcNow.Date;
        var rateKey = $"{userId}:{today:yyyyMMdd}";
        var timestamps = _rateLedger.GetOrAdd(rateKey, _ => new List<DateTime>());
        lock (timestamps)
        {
            // Prune entries from previous days (defensive)
            timestamps.RemoveAll(t => t.Date != today);
            if (timestamps.Count >= 5)
            {
                return Problem(
                    detail: "You have reached the daily feedback limit (5). Please try again tomorrow.",
                    statusCode: StatusCodes.Status429TooManyRequests);
            }
            timestamps.Add(DateTime.UtcNow);
        }

        // --- PII sanitisation ---
        var sanitised = CreditCardPattern.Replace(message, "[REDACTED]");
        sanitised = BankAccountPattern.Replace(sanitised, "[REDACTED]");

        // --- Persist ---
        var feedbackId = Guid.NewGuid();
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        _feedbackStore.Add(new FeedbackEntry
        {
            Id = feedbackId,
            UserId = userId,
            Message = sanitised,
            CreatedAt = DateTime.UtcNow,
            ClientIp = clientIp
        });

        // --- Audit trail ---
        _logger.LogInformation(
            "[Feedback][Audit] FeedbackId={FeedbackId} UserId={UserId} Timestamp={Timestamp} ClientIp={ClientIp}",
            feedbackId, userId, DateTime.UtcNow, clientIp);

        return Ok(new { feedbackId });
    }
}

public class FeedbackRequest
{
    public string? Message { get; set; }
}

public class FeedbackEntry
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Message { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public string ClientIp { get; set; } = null!;
}
