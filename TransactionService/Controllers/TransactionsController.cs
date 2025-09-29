using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using MassTransit;
using TransactionService.Data;
using Contracts.Events;

namespace TransactionService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TransactionsController : ControllerBase
{
    private readonly TransactionDbContext _context;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<TransactionsController> _logger;

    public TransactionsController(
        TransactionDbContext context, 
        IPublishEndpoint publishEndpoint,
        ILogger<TransactionsController> logger)
    {
        _context = context;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetTransactions([FromQuery] Guid? accountId = null)
    {
        var userIdClaim = User.FindFirst("sub")?.Value ?? User.FindFirst("id")?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var query = _context.Transactions.Where(t => t.UserId == userId);
        
        if (accountId.HasValue)
            query = query.Where(t => t.AccountId == accountId.Value);

        var transactions = await query
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        return Ok(transactions.Select(t => new {
            id = t.Id,
            accountId = t.AccountId,
            amount = t.Amount,
            currency = t.Currency,
            type = t.Type,
            description = t.Description,
            createdAt = t.CreatedAt
        }));
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateTransaction([FromBody] CreateTransactionRequest request)
    {
        var userIdClaim = User.FindFirst("sub")?.Value ?? User.FindFirst("id")?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            AccountId = request.AccountId,
            UserId = userId,
            Amount = request.Amount,
            Currency = request.Currency ?? "USD",
            Type = request.Type,
            Description = request.Description,
            CreatedAt = DateTime.UtcNow
        };

        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        // Publish event to RabbitMQ
        var transactionCreated = new TransactionCreated(
            transaction.Id,
            transaction.AccountId,
            transaction.UserId,
            transaction.Amount,
            transaction.Currency,
            transaction.Type,
            transaction.CreatedAt
        );

        await _publishEndpoint.Publish(transactionCreated);

        _logger.LogInformation("Transaction created: {TransactionId} for account {AccountId}", 
            transaction.Id, transaction.AccountId);

        return Ok(new {
            id = transaction.Id,
            accountId = transaction.AccountId,
            amount = transaction.Amount,
            currency = transaction.Currency,
            type = transaction.Type,
            description = transaction.Description,
            createdAt = transaction.CreatedAt
        });
    }
}

public record CreateTransactionRequest(
    Guid AccountId, 
    decimal Amount, 
    string Type, 
    string Description, 
    string? Currency);