using Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransactionService.Filters;
using TransactionService.Models.Dtos;
using TransactionService.Services;

namespace TransactionService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TransactionsController : FintechControllerBase
{
    private readonly ITransactionService _transactionService;
    private const int DefaultPageSize = 50;

    public TransactionsController(ITransactionService transactionService)
    {
        _transactionService = transactionService;
    }

    [HttpGet]
    [Authorize]
    [ETagFilter]
    public async Task<IActionResult> GetTransactions(
        [FromQuery] Guid? accountId = null, 
        [FromQuery] Guid? batchId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] string? transactionType = null,
        [FromQuery] decimal? minAmount = null,
        [FromQuery] decimal? maxAmount = null,
        [FromQuery] string? searchTerm = null)
    {
        // Validate date range
        if (fromDate.HasValue && toDate.HasValue && fromDate.Value > toDate.Value)
        {
            return BadRequest(new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                Title = "Invalid date range",
                Status = 400,
                Detail = "fromDate cannot be greater than toDate."
            });
        }

        // Validate amount range
        if (minAmount.HasValue && maxAmount.HasValue && minAmount.Value > maxAmount.Value)
        {
            return BadRequest(new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                Title = "Invalid amount range",
                Status = 400,
                Detail = "minAmount cannot be greater than maxAmount."
            });
        }

        try
        {
            var filter = new TransactionFilterDto
            {
                FromDate = fromDate,
                ToDate = toDate,
                TransactionType = transactionType,
                MinAmount = minAmount,
                MaxAmount = maxAmount,
                SearchTerm = searchTerm
            };

            var result = await _transactionService.GetTransactionsAsync(
                CurrentUserId,
                accountId,
                batchId,
                page,
                pageSize,
                filter);

            return Ok(result);
        }
        catch (MissingClaimUnauthorizedException)
        {
            return Unauthorized();
        }
    }

    [HttpGet("organisation/{organisationId}")]
    [Authorize]
    [ETagFilter]
    public async Task<IActionResult> GetOrganisationTransactions(Guid organisationId)
    {
        try
        {
            if (CurrentOrganisationId != organisationId)
                return Forbid();

            var result = await _transactionService.GetOrganisationTransactionsAsync(organisationId);
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
    public async Task<IActionResult> CreateTransaction([FromBody] CreatePaymentRequestDto request)
    {
        try
        {
            var clientType = User.FindFirst("client_type")?.Value ?? "Individual";
            Guid? orgId = Guid.TryParse(User.FindFirst("organisation_id")?.Value, out var parsedOrgId) ? parsedOrgId : null;

            var result = await _transactionService.CreateTransactionAsync(CurrentUserId, clientType, orgId, request);
            return Ok(result);
        }
        catch (MissingClaimUnauthorizedException)
        {
            return Unauthorized();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

public record TransactionDto(
    Guid Id,
    Guid AccountId,
    decimal Amount,
    string Currency,
    string Type,
    string? Description,
    string? SpendingType,
    string? TxHash,
    DateTime CreatedAt,
    string? ClientType,
    Guid? OrganisationId,
    Guid? PaymentBatchId,
    string Status
);
