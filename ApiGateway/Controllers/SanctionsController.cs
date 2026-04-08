using ApiGateway.Models;
using ApiGateway.Models.Dtos;
using ApiGateway.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ApiGateway.Controllers;

[ApiController]
[Route("api/v1/ftk/sanctions")]
[Authorize]
public class SanctionsController : ControllerBase
{
    private readonly ISanctioningService _sanctioningService;
    private readonly ILogger<SanctionsController> _logger;

    public SanctionsController(ISanctioningService sanctioningService, ILogger<SanctionsController> logger)
    {
        _sanctioningService = sanctioningService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new sanction request and synchronously runs screening and underwriting.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(SanctionRequestDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateSanctionRequestDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.ExternalProjectId) ||
            string.IsNullOrWhiteSpace(dto.ExternalTenantId) ||
            string.IsNullOrWhiteSpace(dto.Purpose) ||
            string.IsNullOrWhiteSpace(dto.IdempotencyKey) ||
            dto.RequestedAmount <= 0)
        {
            return BadRequest(new { message = "ExternalProjectId, ExternalTenantId, Purpose, IdempotencyKey are required and RequestedAmount must be > 0." });
        }

        var caller = GetCallerEmail();

        var request = await _sanctioningService.CreateSanctionRequestAsync(dto, caller, ct);

        // Run screening synchronously
        request = await _sanctioningService.RunScreeningAsync(request.Id, caller, ct);

        // If screening auto-advanced to underwriting, run underwriting too
        if (request.Status == SanctionStatus.Underwriting)
        {
            request = await _sanctioningService.RunUnderwritingAsync(request.Id, caller, ct);
        }

        _logger.LogInformation(
            "Sanction workflow completed for {SanctionId}: FinalStatus={Status}",
            request.Id, request.Status);

        return CreatedAtAction(nameof(GetById), new { id = request.Id }, MapToDto(request));
    }

    /// <summary>
    /// Retrieves a sanction request by its unique identifier.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SanctionRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var request = await _sanctioningService.GetByIdAsync(id, ct);
        if (request is null)
            return NotFound(new { message = $"Sanction request {id} not found." });

        return Ok(MapToDto(request));
    }

    /// <summary>
    /// Lists sanction requests, optionally filtered by projectId and/or userId.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<SanctionRequestDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFiltered(
        [FromQuery] string? projectId,
        [FromQuery] Guid? userId,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(projectId) && userId.HasValue)
        {
            var result = await _sanctioningService.GetSanctionStatusAsync(projectId, userId.Value, ct);
            return Ok(result is null ? Array.Empty<SanctionRequestDto>() : new[] { MapToDto(result) });
        }

        var all = await _sanctioningService.GetAllAsync(ct);
        return Ok(all.Select(MapToDto).ToList());
    }

    /// <summary>
    /// Disburses an approved sanction request to the FTK ledger.
    /// </summary>
    [HttpPost("{id:guid}/disburse")]
    [Authorize(Roles = "CreditOfficer,Admin")]
    [ProducesResponseType(typeof(SanctionRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Disburse(Guid id, CancellationToken ct)
    {
        try
        {
            var request = await _sanctioningService.DisburseToFtkAsync(id, GetCallerEmail(), ct);
            return Ok(MapToDto(request));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = $"Sanction request {id} not found." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Rejects a sanction request with a mandatory reason.
    /// </summary>
    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = "CreditOfficer,Admin")]
    [ProducesResponseType(typeof(SanctionRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectCancelRequestDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Reason))
            return BadRequest(new { message = "Reason is required." });

        try
        {
            var request = await _sanctioningService.RejectRequestAsync(id, dto.Reason, GetCallerEmail(), ct);
            return Ok(MapToDto(request));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = $"Sanction request {id} not found." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Cancels a sanction request (user-initiated).
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(SanctionRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] RejectCancelRequestDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Reason))
            return BadRequest(new { message = "Reason is required." });

        try
        {
            var request = await _sanctioningService.CancelRequestAsync(id, dto.Reason, GetCallerEmail(), ct);
            return Ok(MapToDto(request));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = $"Sanction request {id} not found." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Returns the audit trail for a specific sanction request.
    /// </summary>
    [HttpGet("{id:guid}/audit")]
    [ProducesResponseType(typeof(List<SanctionAuditLogDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAudit(Guid id, CancellationToken ct)
    {
        var logs = await _sanctioningService.GetAuditLogsAsync(id, ct);
        var dtos = logs.Select(l => new SanctionAuditLogDto(
            l.Id,
            l.SanctionRequestId,
            l.FromStatus.ToString(),
            l.ToStatus.ToString(),
            l.ChangedBy,
            l.Reason,
            l.Timestamp,
            l.CorrelationId
        )).ToList();

        return Ok(dtos);
    }

    private string GetCallerEmail()
    {
        return User.FindFirstValue(ClaimTypes.Email)
            ?? User.FindFirstValue("email")
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? "unknown";
    }

    private static SanctionRequestDto MapToDto(SanctionRequest r) => new(
        r.Id,
        r.ExternalProjectId,
        r.ExternalTenantId,
        r.UserId,
        r.AccountId,
        r.RequestedAmount,
        r.Currency,
        r.Purpose,
        r.RiskScore,
        r.KycStatus.ToString(),
        r.AmlStatus.ToString(),
        r.Status.ToString(),
        r.ApprovedAmount,
        r.DecisionReason,
        r.FtkTransactionRef,
        r.IdempotencyKey,
        r.CreatedAt,
        r.UpdatedAt,
        r.CreatedBy
    );
}