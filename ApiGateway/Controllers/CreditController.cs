using ApiGateway.Data;
using ApiGateway.Models;
using Contracts.Events;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApiGateway.Controllers;

[ApiController]
[Route("api/v1/ftk/credit")]
[Authorize(AuthenticationSchemes = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)]
public class CreditController : ControllerBase
{
    private readonly CreditDbContext _context;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<CreditController> _logger;

    public CreditController(
        CreditDbContext context,
        IPublishEndpoint publishEndpoint,
        ILogger<CreditController> logger)
    {
        _context = context;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    /// <summary>
    /// Returns the credit facility for the authenticated user and wallet, creating one if none exists.
    /// </summary>
    [HttpGet("facility")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetFacility([FromQuery] string walletAddress, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(walletAddress))
            return BadRequest(new { message = "walletAddress query parameter is required." });

        var wallet = walletAddress.Trim().ToLowerInvariant();

        var facility = await _context.CreditFacilities
            .FirstOrDefaultAsync(f => f.UserId == userId && f.WalletAddress == wallet, ct);

        if (facility is null)
        {
            facility = new CreditFacility
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                WalletAddress = wallet,
                CreditLimit = 10_000m,
                DrawnAmount = 0m,
                OutstandingBalance = 0m,
                Currency = "FTK",
                Status = CreditFacilityStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            _context.CreditFacilities.Add(facility);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Credit facility created: {FacilityId} for user {UserId} wallet {Wallet}",
                facility.Id, userId, wallet);
        }

        return Ok(MapFacility(facility));
    }

    /// <summary>
    /// Requests a drawdown against the user's credit facility.
    /// </summary>
    [HttpPost("drawdown")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Drawdown([FromBody] DrawdownRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        if (request.Amount <= 0)
            return BadRequest(new { message = "Amount must be greater than zero." });

        if (string.IsNullOrWhiteSpace(request.WalletAddress))
            return BadRequest(new { message = "WalletAddress is required." });

        var wallet = request.WalletAddress.Trim().ToLowerInvariant();

        var facility = await _context.CreditFacilities
            .FirstOrDefaultAsync(f => f.UserId == userId && f.WalletAddress == wallet, ct);

        if (facility is null)
            return NotFound(new { message = "No credit facility found for this wallet." });

        if (facility.Status != CreditFacilityStatus.Active)
            return BadRequest(new { message = "Credit facility is not active." });

        var available = facility.CreditLimit - facility.DrawnAmount;
        if (request.Amount > available)
            return BadRequest(new { message = $"Insufficient available credit. Available: {available:F2} {facility.Currency}." });

        facility.DrawnAmount += request.Amount;
        facility.OutstandingBalance += request.Amount;
        facility.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(ct);

        await _publishEndpoint.Publish(new DrawdownRequested(
            facility.Id,
            userId,
            wallet,
            request.Amount,
            facility.Currency,
            facility.OutstandingBalance,
            DateTime.UtcNow
        ), ct);

        _logger.LogInformation(
            "Drawdown of {Amount} {Currency} on facility {FacilityId} for user {UserId}",
            request.Amount, facility.Currency, facility.Id, userId);

        return Ok(MapFacility(facility));
    }

    /// <summary>
    /// Submits a repayment against the user's credit facility.
    /// </summary>
    [HttpPost("repayment")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Repayment([FromBody] RepaymentRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        if (request.Amount <= 0)
            return BadRequest(new { message = "Amount must be greater than zero." });

        if (string.IsNullOrWhiteSpace(request.WalletAddress))
            return BadRequest(new { message = "WalletAddress is required." });

        var wallet = request.WalletAddress.Trim().ToLowerInvariant();

        var facility = await _context.CreditFacilities
            .FirstOrDefaultAsync(f => f.UserId == userId && f.WalletAddress == wallet, ct);

        if (facility is null)
            return NotFound(new { message = "No credit facility found for this wallet." });

        if (request.Amount > facility.OutstandingBalance)
            return BadRequest(new { message = $"Repayment exceeds outstanding balance of {facility.OutstandingBalance:F2} {facility.Currency}." });

        var repayment = new CreditRepayment
        {
            Id = Guid.NewGuid(),
            FacilityId = facility.Id,
            UserId = userId,
            Amount = request.Amount,
            Currency = facility.Currency,
            Status = "Completed",
            CreatedAt = DateTimeOffset.UtcNow
        };

        facility.OutstandingBalance -= request.Amount;
        facility.UpdatedAt = DateTimeOffset.UtcNow;

        _context.CreditRepayments.Add(repayment);
        await _context.SaveChangesAsync(ct);

        await _publishEndpoint.Publish(new RepaymentCompleted(
            repayment.Id,
            facility.Id,
            userId,
            wallet,
            repayment.Amount,
            facility.Currency,
            facility.OutstandingBalance,
            repayment.Status,
            DateTime.UtcNow
        ), ct);

        _logger.LogInformation(
            "Repayment {RepaymentId} of {Amount} {Currency} on facility {FacilityId} for user {UserId}",
            repayment.Id, repayment.Amount, facility.Currency, facility.Id, userId);

        return Ok(new
        {
            repayment = MapRepayment(repayment),
            facility = MapFacility(facility)
        });
    }

    /// <summary>
    /// Returns the repayment history for a credit facility.
    /// </summary>
    [HttpGet("repayments")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRepayments([FromQuery] string walletAddress, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(walletAddress))
            return BadRequest(new { message = "walletAddress query parameter is required." });

        var wallet = walletAddress.Trim().ToLowerInvariant();

        var facility = await _context.CreditFacilities
            .FirstOrDefaultAsync(f => f.UserId == userId && f.WalletAddress == wallet, ct);

        if (facility is null)
            return Ok(Array.Empty<object>());

        var repayments = await _context.CreditRepayments
            .Where(r => r.FacilityId == facility.Id)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        return Ok(repayments.Select(MapRepayment));
    }

    private bool TryGetUserId(out Guid userId)
    {
        var claim = User.FindFirst("sub")?.Value ?? User.FindFirst("id")?.Value;
        return Guid.TryParse(claim, out userId);
    }

    private static object MapFacility(CreditFacility f) => new
    {
        id = f.Id,
        userId = f.UserId,
        walletAddress = f.WalletAddress,
        creditLimit = f.CreditLimit,
        drawnAmount = f.DrawnAmount,
        outstandingBalance = f.OutstandingBalance,
        availableCredit = f.CreditLimit - f.DrawnAmount,
        currency = f.Currency,
        status = f.Status.ToString(),
        createdAt = f.CreatedAt,
        updatedAt = f.UpdatedAt
    };

    private static object MapRepayment(CreditRepayment r) => new
    {
        id = r.Id,
        facilityId = r.FacilityId,
        amount = r.Amount,
        currency = r.Currency,
        status = r.Status,
        createdAt = r.CreatedAt
    };
}

public record DrawdownRequest(string WalletAddress, decimal Amount);
public record RepaymentRequest(string WalletAddress, decimal Amount);
