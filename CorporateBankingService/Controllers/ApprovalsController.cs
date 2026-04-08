using Contracts;
using CorporateBankingService.Models.Dtos;
using CorporateBankingService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CorporateBankingService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ApprovalsController : FintechControllerBase
{
    private readonly IApprovalService _approvalService;

    public ApprovalsController(IApprovalService approvalService)
    {
        _approvalService = approvalService;
    }

    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingBatches()
    {
        try
        {
            var callerRole = User.FindFirst("organisation_role")?.Value ?? string.Empty;
            var result = await _approvalService.GetPendingBatchesAsync(CurrentOrganisationId, callerRole);
            return Ok(result);
        }
        catch (MissingClaimUnauthorizedException)
        {
            return Unauthorized();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpGet("{batchId}")]
    public async Task<IActionResult> GetBatchDetail(Guid batchId)
    {
        try
        {
            var result = await _approvalService.GetBatchDetailAsync(batchId, CurrentOrganisationId);
            if (result is null) return NotFound();
            return Ok(result);
        }
        catch (MissingClaimUnauthorizedException)
        {
            return Unauthorized();
        }
    }

    [HttpPost("{batchId}/decide")]
    public async Task<IActionResult> Decide(Guid batchId, [FromBody] ApprovalDecisionRequest request)
    {
        try
        {
            var callerRole = User.FindFirst("organisation_role")?.Value ?? string.Empty;
            var result = await _approvalService.DecideAsync(batchId, CurrentOrganisationId, CurrentUserId, callerRole, request);
            if (result is null) return NotFound();
            return Ok(result);
        }
        catch (MissingClaimUnauthorizedException)
        {
            return Unauthorized();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
