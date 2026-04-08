using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using AccountService.Filters;
using AccountService.Services;
using Contracts;

namespace AccountService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AccountsController : FintechControllerBase
{
    private readonly IAccountService _accountService;

    public AccountsController(IAccountService accountService)
    {
        _accountService = accountService;
    }

    [HttpGet]
    [Authorize]
    [ETagFilter]
    public async Task<IActionResult> GetAccounts()
    {
        try
        {
            var result = await _accountService.GetAccountsAsync(CurrentUserId);
            return Ok(result);
        }
        catch (MissingClaimUnauthorizedException)
        {
            return Unauthorized();
        }
    }

    /// <summary>
    /// Returns all accounts for a given organisation (corporate cash-position aggregation).
    /// </summary>
    [HttpGet("organisation/{organisationId}")]
    [Authorize]
    [ETagFilter]
    public async Task<IActionResult> GetOrganisationAccounts(Guid organisationId)
    {
        try
        {
            if (CurrentOrganisationId != organisationId)
                return Forbid();

            var result = await _accountService.GetOrganisationAccountsAsync(organisationId);
            return Ok(result);
        }
        catch (MissingClaimUnauthorizedException)
        {
            return Unauthorized();
        }
    }

    [HttpPost]
    [Authorize]
    [IdempotencyFilter]
    public async Task<IActionResult> CreateAccount([FromBody] CreateAccountRequest request)
    {
        try
        {
            var clientType = User.FindFirst("client_type")?.Value ?? "Individual";
            Guid? organisationId = Guid.TryParse(User.FindFirst("organisation_id")?.Value, out var orgId) ? orgId : null;

            var result = await _accountService.CreateAccountAsync(CurrentUserId, clientType, organisationId, request);
            return Ok(result);
        }
        catch (MissingClaimUnauthorizedException)
        {
            return Unauthorized();
        }
        catch (AccountLimitExceededException ex)
        {
            return UnprocessableEntity(new { errorCode = ex.ErrorCode, message = ex.Message });
        }
    }

    [HttpGet("{id}/balance")]
    [Authorize]
    [ETagFilter]
    public async Task<IActionResult> GetBalance(Guid id)
    {
        try
        {
            var result = await _accountService.GetBalanceAsync(CurrentUserId, id);
            if (result is null)
                return NotFound();

            return Ok(result);
        }
        catch (MissingClaimUnauthorizedException)
        {
            return Unauthorized();
        }
    }
}

public record CreateAccountRequest(string AccountType, string? Currency);

public record AccountDto(
    Guid Id,
    string AccountNumber,
    string AccountType,
    decimal Balance,
    string Currency,
    DateTime CreatedAt,
    string? ClientType,
    Guid? OrganisationId,
    decimal AvailableBalance,
    decimal HeldBalance
);