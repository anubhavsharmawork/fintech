using Contracts;
using CorporateBankingService.Models.Dtos;
using CorporateBankingService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CorporateBankingService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrganisationsController : FintechControllerBase
{
    private readonly IOrganisationService _organisationService;

    public OrganisationsController(IOrganisationService organisationService)
    {
        _organisationService = organisationService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrganisation([FromBody] CreateOrganisationRequest request)
    {
        try
        {
            var email = User.FindFirst("email")?.Value ?? "";
            var result = await _organisationService.CreateOrganisationAsync(CurrentUserId, email, request);
            return Ok(result);
        }
        catch (MissingClaimUnauthorizedException)
        {
            return Unauthorized();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{organisationId}")]
    public async Task<IActionResult> GetOrganisation(Guid organisationId)
    {
        try
        {
            var result = await _organisationService.GetOrganisationAsync(organisationId, CurrentOrganisationId);
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
    }

    [HttpGet("{organisationId}/members")]
    public async Task<IActionResult> GetMembers(Guid organisationId)
    {
        try
        {
            var result = await _organisationService.GetMembersAsync(organisationId, CurrentOrganisationId);
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

    [HttpPost("{organisationId}/members/invite")]
    public async Task<IActionResult> InviteMember(Guid organisationId, [FromBody] InviteMemberRequest request)
    {
        try
        {
            var callerRole = User.FindFirst("organisation_role")?.Value ?? "";
            var result = await _organisationService.InviteMemberAsync(organisationId, CurrentOrganisationId, callerRole, request);
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

    [HttpPut("{organisationId}/members/{memberId}/role")]
    public async Task<IActionResult> AssignRole(Guid organisationId, Guid memberId, [FromBody] AssignRoleRequest request)
    {
        try
        {
            var callerRole = User.FindFirst("organisation_role")?.Value ?? "";
            var result = await _organisationService.AssignRoleAsync(organisationId, CurrentOrganisationId, callerRole, memberId, request);
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
    }

    [HttpGet("{organisationId}/policies")]
    public async Task<IActionResult> GetPolicies(Guid organisationId)
    {
        try
        {
            var result = await _organisationService.GetPoliciesAsync(organisationId, CurrentOrganisationId);
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

    [HttpPost("{organisationId}/policies")]
    public async Task<IActionResult> CreatePolicy(Guid organisationId, [FromBody] CreateApprovalPolicyRequest request)
    {
        try
        {
            var callerRole = User.FindFirst("organisation_role")?.Value ?? "";
            var result = await _organisationService.CreatePolicyAsync(organisationId, CurrentOrganisationId, callerRole, request);
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
}
