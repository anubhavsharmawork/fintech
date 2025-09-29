using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using AccountService.Data;

namespace AccountService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AccountsController : ControllerBase
{
    private readonly AccountDbContext _context;
    private readonly ILogger<AccountsController> _logger;

    public AccountsController(AccountDbContext context, ILogger<AccountsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetAccounts()
    {
        var userIdClaim = User.FindFirst("sub")?.Value ?? User.FindFirst("id")?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var accounts = await _context.Accounts
            .Where(a => a.UserId == userId)
            .ToListAsync();

        return Ok(accounts.Select(a => new {
            id = a.Id,
            accountNumber = a.AccountNumber,
            accountType = a.AccountType,
            balance = a.Balance,
            currency = a.Currency,
            createdAt = a.CreatedAt
        }));
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateAccount([FromBody] CreateAccountRequest request)
    {
        var userIdClaim = User.FindFirst("sub")?.Value ?? User.FindFirst("id")?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AccountNumber = GenerateAccountNumber(),
            AccountType = request.AccountType,
            Balance = 0,
            Currency = request.Currency ?? "USD",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Accounts.Add(account);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Account created: {AccountId} for user {UserId}", account.Id, userId);

        return Ok(new {
            id = account.Id,
            accountNumber = account.AccountNumber,
            accountType = account.AccountType,
            balance = account.Balance,
            currency = account.Currency
        });
    }

    [HttpGet("{id}/balance")]
    [Authorize]
    public async Task<IActionResult> GetBalance(Guid id)
    {
        var userIdClaim = User.FindFirst("sub")?.Value ?? User.FindFirst("id")?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);
        if (account == null)
            return NotFound();

        return Ok(new { balance = account.Balance, currency = account.Currency });
    }

    private static string GenerateAccountNumber()
    {
        var random = new Random();
        return random.Next(1000000000, int.MaxValue).ToString();
    }
}

public record CreateAccountRequest(string AccountType, string? Currency);