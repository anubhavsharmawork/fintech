using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using AccountService.Data;
using AccountService.Models;

namespace AccountService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BankConnectionsController : ControllerBase
{
    private readonly AccountDbContext _context;
    private readonly ILogger<BankConnectionsController> _logger;

    // Mock bank data for demo purposes (simulating Open Banking providers)
    private static readonly List<AvailableBank> MockBanks = new()
    {
        new AvailableBank("nz_anz", "ANZ New Zealand", "🏦", "NZ"),
        new AvailableBank("nz_asb", "ASB Bank", "🏛️", "NZ"),
        new AvailableBank("nz_bnz", "BNZ", "🏦", "NZ"),
        new AvailableBank("nz_westpac", "Westpac NZ", "🔴", "NZ"),
        new AvailableBank("nz_kiwibank", "Kiwibank", "🥝", "NZ"),
        new AvailableBank("au_commbank", "CommBank", "🟡", "AU"),
        new AvailableBank("au_nab", "NAB", "🔴", "AU"),
        new AvailableBank("au_westpac", "Westpac AU", "🔴", "AU"),
        new AvailableBank("uk_hsbc", "HSBC UK", "🔴", "UK"),
        new AvailableBank("uk_barclays", "Barclays", "🔵", "UK")
    };

    public BankConnectionsController(AccountDbContext context, ILogger<BankConnectionsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get list of available banks for connection.
    /// </summary>
    [HttpGet("available")]
    [Authorize]
    public IActionResult GetAvailableBanks([FromQuery] string? country = null)
    {
        var banks = string.IsNullOrWhiteSpace(country)
            ? MockBanks
            : MockBanks.Where(b => b.Country.Equals(country, StringComparison.OrdinalIgnoreCase)).ToList();

        return Ok(banks);
    }

    /// <summary>
    /// Get user's connected banks.
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetConnectedBanks()
    {
        var userIdClaim = User.FindFirst("sub")?.Value ?? User.FindFirst("id")?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var connections = await _context.BankConnections
            .Where(bc => bc.UserId == userId)
            .Select(bc => new
            {
                bc.Id,
                bc.BankId,
                bc.BankName,
                bc.BankLogo,
                bc.Status,
                bc.ConnectedAt
            })
            .ToListAsync();

        return Ok(connections);
    }

    /// <summary>
    /// Connect to a bank (mock OAuth flow for demo).
    /// </summary>
    [HttpPost("connect")]
    [Authorize]
    public async Task<IActionResult> ConnectBank([FromBody] ConnectBankRequest request)
    {
        var userIdClaim = User.FindFirst("sub")?.Value ?? User.FindFirst("id")?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var bank = MockBanks.FirstOrDefault(b => b.Id == request.BankId);
        if (bank == null)
            return BadRequest(new { message = "Invalid bank ID" });

        // Check if already connected
        var existing = await _context.BankConnections
            .FirstOrDefaultAsync(bc => bc.UserId == userId && bc.BankId == request.BankId);

        if (existing != null)
            return Conflict(new { message = "Bank already connected" });

        // Create mock connection (in real scenario, this would be after OAuth callback)
        var connection = new BankConnection
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            BankId = bank.Id,
            BankName = bank.Name,
            BankLogo = bank.Logo,
            Status = "Active",
            AccessToken = $"mock_token_{Guid.NewGuid():N}",
            TokenExpiresAt = DateTime.UtcNow.AddDays(90),
            ConnectedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.BankConnections.Add(connection);

        // Generate mock external accounts for the connected bank
        var mockAccounts = GenerateMockAccounts(connection.Id, userId, bank.Name);
        _context.ExternalBankAccounts.AddRange(mockAccounts);

        await _context.SaveChangesAsync();

        _logger.LogInformation("Bank connected: {BankId} for user {UserId}", bank.Id, userId);

        return Ok(new
        {
            connectionId = connection.Id,
            bankId = bank.Id,
            bankName = bank.Name,
            status = connection.Status,
            accountsImported = mockAccounts.Count
        });
    }

    /// <summary>
    /// Disconnect a bank.
    /// </summary>
    [HttpDelete("{connectionId}")]
    [Authorize]
    public async Task<IActionResult> DisconnectBank(Guid connectionId)
    {
        var userIdClaim = User.FindFirst("sub")?.Value ?? User.FindFirst("id")?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var connection = await _context.BankConnections
            .FirstOrDefaultAsync(bc => bc.Id == connectionId && bc.UserId == userId);

        if (connection == null)
            return NotFound();

        _context.BankConnections.Remove(connection);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Bank disconnected: {ConnectionId} for user {UserId}", connectionId, userId);

        return NoContent();
    }

    /// <summary>
    /// Get accounts from all connected banks.
    /// </summary>
    [HttpGet("accounts")]
    [Authorize]
    public async Task<IActionResult> GetExternalAccounts()
    {
        var userIdClaim = User.FindFirst("sub")?.Value ?? User.FindFirst("id")?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var accounts = await _context.ExternalBankAccounts
            .Include(a => a.BankConnection)
            .Where(a => a.UserId == userId)
            .Select(a => new
            {
                a.Id,
                a.AccountName,
                a.AccountType,
                a.AccountNumber,
                a.Balance,
                a.Currency,
                a.LastSyncedAt,
                bankName = a.BankConnection != null ? a.BankConnection.BankName : null,
                bankLogo = a.BankConnection != null ? a.BankConnection.BankLogo : null
            })
            .ToListAsync();

        return Ok(accounts);
    }

    /// <summary>
    /// Sync accounts for a specific bank connection (refresh balances).
    /// </summary>
    [HttpPost("{connectionId}/sync")]
    [Authorize]
    public async Task<IActionResult> SyncBankAccounts(Guid connectionId)
    {
        var userIdClaim = User.FindFirst("sub")?.Value ?? User.FindFirst("id")?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var connection = await _context.BankConnections
            .FirstOrDefaultAsync(bc => bc.Id == connectionId && bc.UserId == userId);

        if (connection == null)
            return NotFound();

        // Mock: Update balances with random changes
        var accounts = await _context.ExternalBankAccounts
            .Where(a => a.BankConnectionId == connectionId)
            .ToListAsync();

        var random = new Random();
        foreach (var account in accounts)
        {
            // Simulate balance changes (±5%)
            var change = (decimal)(random.NextDouble() * 0.1 - 0.05);
            account.Balance += account.Balance * change;
            account.LastSyncedAt = DateTime.UtcNow;
        }

        connection.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Bank accounts synced: {ConnectionId} for user {UserId}", connectionId, userId);

        return Ok(new { message = "Accounts synced successfully", syncedAt = DateTime.UtcNow });
    }

    private static List<ExternalBankAccount> GenerateMockAccounts(Guid connectionId, Guid userId, string bankName)
    {
        var random = new Random();
        var accounts = new List<ExternalBankAccount>();

        // Generate 1-3 mock accounts per bank
        var accountCount = random.Next(1, 4);
        var accountTypes = new[] { "Checking", "Savings", "Credit Card" };

        for (int i = 0; i < accountCount; i++)
        {
            var type = accountTypes[i % accountTypes.Length];
            accounts.Add(new ExternalBankAccount
            {
                Id = Guid.NewGuid(),
                BankConnectionId = connectionId,
                UserId = userId,
                ExternalAccountId = $"ext_{Guid.NewGuid():N}",
                AccountName = $"{bankName} {type}",
                AccountType = type,
                AccountNumber = $"****{random.Next(1000, 9999)}",
                Balance = Math.Round((decimal)(random.NextDouble() * 10000 + 500), 2),
                Currency = "NZD",
                LastSyncedAt = DateTime.UtcNow
            });
        }

        return accounts;
    }
}

public record AvailableBank(string Id, string Name, string Logo, string Country);
public record ConnectBankRequest(string BankId);
