using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using MassTransit;
using UserService.Data;
using Contracts.Events;

namespace UserService.Controllers;

[ApiController]
[Route("api/kyc")]
[Authorize]
public class KycController : ControllerBase
{
    private readonly UserDbContext _context;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<KycController> _logger;

    public KycController(
        UserDbContext context,
        IPublishEndpoint publishEndpoint,
        ILogger<KycController> logger)
    {
        _context = context;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetKycStatus()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                       ?? User.FindFirst("sub")?.Value
                       ?? User.FindFirst("id")?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var user = await _context.Users.FindAsync(userId);
        if (user is null)
            return NotFound();

        return Ok(new { userId = user.Id, status = user.KycStatus });
    }

    [HttpPatch("status")]
    [Authorize(Roles = "ComplianceOfficer,Admin")]
    public async Task<IActionResult> UpdateKycStatus([FromBody] UpdateKycStatusDto dto)
    {
        var validStatuses = new[] { "Pending", "Verified", "Rejected", "UnderReview" };
        if (!validStatuses.Contains(dto.Status, StringComparer.OrdinalIgnoreCase))
            return BadRequest(new { error = "Invalid KYC status. Allowed: Pending, Verified, Rejected, UnderReview" });

        var user = await _context.Users.FindAsync(dto.UserId);
        if (user is null)
            return NotFound();

        user.KycStatus = dto.Status;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await _publishEndpoint.Publish(new KycStatusChanged(
            user.Id,
            user.KycStatus,
            dto.Notes,
            DateTime.UtcNow
        ));

        _logger.LogInformation("KYC status updated for user {UserId} to {Status}", user.Id, user.KycStatus);

        return Ok(new { userId = user.Id, status = user.KycStatus });
    }
}

public class UpdateKycStatusDto
{
    public Guid UserId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}
