using System.Globalization;
using System.Security.Cryptography;
using AccountService.Controllers;
using AccountService.Data;
using AccountService.Policy;
using Microsoft.EntityFrameworkCore;

namespace AccountService.Services;

public sealed class AccountLimitExceededException : Exception
{
    public AccountLimitExceededException(string? errorCode, string message) : base(message)
    {
        ErrorCode = errorCode;
    }

    public string? ErrorCode { get; }
}

public class AccountService : IAccountService
{
    private readonly AccountDbContext _context;
    private readonly ILogger<AccountService> _logger;
    private readonly ICacheService _cacheService;
    private readonly IAccountLimitPolicy _limitPolicy;

    private const int AccountsCacheTtlMinutes = 5;

    public AccountService(
        AccountDbContext context,
        ILogger<AccountService> logger,
        ICacheService cacheService,
        IAccountLimitPolicy limitPolicy)
    {
        _context = context;
        _logger = logger;
        _cacheService = cacheService;
        _limitPolicy = limitPolicy;
    }

    private static string GetUserAccountsCacheKey(Guid userId) => $"user:{userId}:list";

    public async Task<object> GetAccountsAsync(Guid userId)
    {
        var cacheKey = GetUserAccountsCacheKey(userId);
        var cached = await _cacheService.GetAsync<List<AccountDto>>(cacheKey);
        if (cached is not null)
        {
            return cached.Select(MapAccountResponse);
        }

        var accounts = await _context.Accounts
            .Where(a => a.UserId == userId)
            .ToListAsync();

        var accountIds = accounts.Select(a => a.Id).ToList();

        // Held balance = sum of outgoing (debit) amounts on batch-linked transactions
        // (PaymentBatchId IS NOT NULL means the transaction belongs to a batch that may
        // still be pending approval or execution). No Status column exists in this schema.
        var heldByAccount = await _context.LedgerTransactions
            .Where(t => accountIds.Contains(t.AccountId)
                        && t.Type == "debit"
                        && t.PaymentBatchId != null)
            .GroupBy(t => t.AccountId)
            .Select(g => new { AccountId = g.Key, Held = g.Sum(t => t.Amount) })
            .ToDictionaryAsync(x => x.AccountId, x => x.Held);

        var dtos = accounts.Select(a =>
        {
            var held = heldByAccount.TryGetValue(a.Id, out var h) ? h : 0m;
            return new AccountDto(
                a.Id,
                a.AccountNumber,
                a.AccountType,
                a.Balance,
                a.Currency,
                a.CreatedAt,
                a.ClientType,
                a.OrganisationId,
                AvailableBalance: a.Balance - held,
                HeldBalance: held
            );
        }).ToList();

        await _cacheService.SetAsync(cacheKey, dtos, AccountsCacheTtlMinutes);

        return dtos.Select(MapAccountResponse);
    }

    public async Task<object> GetOrganisationAccountsAsync(Guid organisationId)
    {
        var accounts = await _context.Accounts
            .Where(a => a.OrganisationId == organisationId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        var accountIds = accounts.Select(a => a.Id).ToList();

        var heldByAccount = await _context.LedgerTransactions
            .Where(t => accountIds.Contains(t.AccountId)
                        && t.Type == "debit"
                        && t.PaymentBatchId != null)
            .GroupBy(t => t.AccountId)
            .Select(g => new { AccountId = g.Key, Held = g.Sum(t => t.Amount) })
            .ToDictionaryAsync(x => x.AccountId, x => x.Held);

        return accounts.Select(a =>
        {
            var held = heldByAccount.TryGetValue(a.Id, out var h) ? h : 0m;
            return MapAccountResponse(a, held);
        });
    }

    public async Task<object> CreateAccountAsync(Guid userId, string clientType, Guid? organisationId, CreateAccountRequest request)
    {
        var limitCheck = await _limitPolicy.CanCreateAccountAsync(userId, clientType);
        if (!limitCheck.IsAllowed)
        {
            throw new AccountLimitExceededException(limitCheck.ErrorCode, limitCheck.ErrorMessage ?? "Account creation is not allowed.");
        }

        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AccountNumber = await GenerateAccountNumberAsync(),
            AccountType = request.AccountType,
            Balance = 0,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "NZD" : request.Currency,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            ClientType = clientType,
            OrganisationId = organisationId
        };

        _context.Accounts.Add(account);
        await _context.SaveChangesAsync();
        await _cacheService.RemoveAsync(GetUserAccountsCacheKey(userId));

        _logger.LogInformation("Account created: {AccountId} for user {UserId} clientType={ClientType}", account.Id, userId, clientType);

        return MapAccountResponse(account, heldBalance: 0m);
    }

    public async Task<object?> GetBalanceAsync(Guid userId, Guid accountId)
    {
        var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId);
        if (account is null)
        {
            return null;
        }

        return new
        {
            balance = account.Balance,
            currency = account.Currency
        };
    }

    private async Task<string> GenerateAccountNumberAsync()
    {
        string accountNumber;
        do
        {
            accountNumber = RandomNumberGenerator.GetInt32(1000000000, int.MaxValue).ToString(CultureInfo.InvariantCulture);
        }
        while (await _context.Accounts.AnyAsync(a => a.AccountNumber == accountNumber));

        return accountNumber;
    }

    private static object MapAccountResponse(AccountDto account) => new
    {
        id = account.Id,
        accountNumber = account.AccountNumber,
        accountType = account.AccountType,
        balance = account.Balance,
        currency = account.Currency,
        createdAt = account.CreatedAt,
        clientType = account.ClientType,
        organisationId = account.OrganisationId,
        availableBalance = account.AvailableBalance,
        heldBalance = account.HeldBalance
    };

    private static object MapAccountResponse(Account account, decimal heldBalance) => new
    {
        id = account.Id,
        accountNumber = account.AccountNumber,
        accountType = account.AccountType,
        balance = account.Balance,
        currency = account.Currency,
        createdAt = account.CreatedAt,
        clientType = account.ClientType,
        organisationId = account.OrganisationId,
        availableBalance = account.Balance - heldBalance,
        heldBalance
    };
}
