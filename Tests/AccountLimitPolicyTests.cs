using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using AccountService.Data;
using AccountService.Models;
using AccountService.Policy;
using AccountService.Services;

namespace Tests;

/// <summary>
/// Isolation tests for AccountLimitPolicy.
/// Uses InMemory EF, a mock IKycStatusClient, and explicit IOptions — no HTTP, no real DB.
/// </summary>
public class AccountLimitPolicyTests
{
    // ── helpers ────────────────────────────────────────────────────────────────

    private static AccountDbContext BuildDb() =>
        new(new DbContextOptionsBuilder<AccountDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static IOptions<AccountLimitsOptions> BuildOptions(int individualAccounts = 3, int individualConnections = 2, int corporateAccounts = 10, int corporateConnections = 8) =>
        Options.Create(new AccountLimitsOptions
        {
            Individual = new ClientTypeLimits { MaxAccounts = individualAccounts, MaxBankConnections = individualConnections },
            Corporate = new ClientTypeLimits { MaxAccounts = corporateAccounts, MaxBankConnections = corporateConnections }
        });

    private static AccountLimitPolicy BuildPolicy(
        AccountDbContext db,
        string kycStatus = "Verified",
        IOptions<AccountLimitsOptions>? options = null) =>
        new(db,
            new StubKycStatusClient(kycStatus),
            options ?? BuildOptions(),
            NullLogger<AccountLimitPolicy>.Instance);

    private static Account MakeAccount(Guid userId) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        AccountNumber = Guid.NewGuid().ToString("N")[..10],
        AccountType = "Checking",
        Currency = "NZD",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static BankConnection MakeConnection(Guid userId) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        BankId = $"bank_{Guid.NewGuid():N}",
        BankName = "Test Bank",
        BankLogo = "🏦",
        Status = "Active",
        AccessToken = "tok",
        TokenExpiresAt = DateTime.UtcNow.AddDays(90),
        ConnectedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    // ── account creation limits ─────────────────────────────────────────────

    [Fact]
    public async Task CanCreateAccount_AllowsWhenBelowLimit()
    {
        var db = BuildDb();
        var userId = Guid.NewGuid();
        db.Accounts.Add(MakeAccount(userId)); // 1 of 3
        await db.SaveChangesAsync();

        var result = await BuildPolicy(db).CanCreateAccountAsync(userId, "Individual");

        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task CanCreateAccount_AllowsWhenAtExactlyZero()
    {
        var db = BuildDb();
        var result = await BuildPolicy(db).CanCreateAccountAsync(Guid.NewGuid(), "Individual");

        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task CanCreateAccount_DeniesWhenAtLimit_Individual()
    {
        var db = BuildDb();
        var userId = Guid.NewGuid();
        for (var i = 0; i < 3; i++) db.Accounts.Add(MakeAccount(userId));
        await db.SaveChangesAsync();

        var result = await BuildPolicy(db).CanCreateAccountAsync(userId, "Individual");

        result.IsAllowed.Should().BeFalse();
        result.ErrorCode.Should().Be("ACCOUNT_LIMIT_EXCEEDED");
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CanCreateAccount_CorporateAllowsHigherLimit()
    {
        var db = BuildDb();
        var userId = Guid.NewGuid();

        // 3 accounts — would be blocked for Individual, allowed for Corporate (limit=10)
        for (var i = 0; i < 3; i++) db.Accounts.Add(MakeAccount(userId));
        await db.SaveChangesAsync();

        var individualResult = await BuildPolicy(db).CanCreateAccountAsync(userId, "Individual");
        var corporateResult = await BuildPolicy(db).CanCreateAccountAsync(userId, "Corporate");

        individualResult.IsAllowed.Should().BeFalse("Individual limit is 3");
        corporateResult.IsAllowed.Should().BeTrue("Corporate limit is 10");
    }

    [Fact]
    public async Task CanCreateAccount_OnlyCountsOwnAccounts_NotOtherUsers()
    {
        var db = BuildDb();
        var userId = Guid.NewGuid();
        var otherId = Guid.NewGuid();

        // Fill another user's quota but not this user's
        for (var i = 0; i < 3; i++) db.Accounts.Add(MakeAccount(otherId));
        await db.SaveChangesAsync();

        var result = await BuildPolicy(db).CanCreateAccountAsync(userId, "Individual");

        result.IsAllowed.Should().BeTrue("other users' accounts must not affect this user's limit");
    }

    // ── bank connection limits ──────────────────────────────────────────────

    [Fact]
    public async Task CanAddBankConnection_AllowsWhenBelowLimit()
    {
        var db = BuildDb();
        var userId = Guid.NewGuid();
        db.BankConnections.Add(MakeConnection(userId)); // 1 of 2
        await db.SaveChangesAsync();

        var result = await BuildPolicy(db).CanAddBankConnectionAsync(userId, "Individual");

        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task CanAddBankConnection_DeniesWhenAtLimit_Individual()
    {
        var db = BuildDb();
        var userId = Guid.NewGuid();
        for (var i = 0; i < 2; i++) db.BankConnections.Add(MakeConnection(userId));
        await db.SaveChangesAsync();

        var result = await BuildPolicy(db).CanAddBankConnectionAsync(userId, "Individual");

        result.IsAllowed.Should().BeFalse();
        result.ErrorCode.Should().Be("CONNECTION_LIMIT_EXCEEDED");
    }

    [Fact]
    public async Task CanAddBankConnection_CorporateAllowsHigherLimit()
    {
        var db = BuildDb();
        var userId = Guid.NewGuid();

        // 2 connections — blocked for Individual (limit=2), allowed for Corporate (limit=8)
        for (var i = 0; i < 2; i++) db.BankConnections.Add(MakeConnection(userId));
        await db.SaveChangesAsync();

        var individualResult = await BuildPolicy(db).CanAddBankConnectionAsync(userId, "Individual");
        var corporateResult = await BuildPolicy(db).CanAddBankConnectionAsync(userId, "Corporate");

        individualResult.IsAllowed.Should().BeFalse("Individual limit is 2");
        corporateResult.IsAllowed.Should().BeTrue("Corporate limit is 8");
    }

    // ── KYC enforcement ────────────────────────────────────────────────────

    [Fact]
    public async Task CanCreateAccount_BlockedWhenKycPending()
    {
        var db = BuildDb();
        var result = await BuildPolicy(db, kycStatus: "Pending").CanCreateAccountAsync(Guid.NewGuid(), "Individual");

        result.IsAllowed.Should().BeFalse();
        result.ErrorCode.Should().Be("KYC_REQUIRED");
        result.ErrorMessage.Should().Contain("Pending");
    }

    [Fact]
    public async Task CanCreateAccount_BlockedWhenKycRejected()
    {
        var db = BuildDb();
        var result = await BuildPolicy(db, kycStatus: "Rejected").CanCreateAccountAsync(Guid.NewGuid(), "Individual");

        result.IsAllowed.Should().BeFalse();
        result.ErrorCode.Should().Be("KYC_REQUIRED");
        result.ErrorMessage.Should().Contain("Rejected");
    }

    [Fact]
    public async Task CanAddBankConnection_BlockedWhenKycPending()
    {
        var db = BuildDb();
        var result = await BuildPolicy(db, kycStatus: "Pending").CanAddBankConnectionAsync(Guid.NewGuid(), "Individual");

        result.IsAllowed.Should().BeFalse();
        result.ErrorCode.Should().Be("KYC_REQUIRED");
    }

    [Fact]
    public async Task CanCreateAccount_AllowedWhenKycVerified()
    {
        var db = BuildDb();
        var result = await BuildPolicy(db, kycStatus: "Verified").CanCreateAccountAsync(Guid.NewGuid(), "Individual");

        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task CanCreateAccount_KycBlockTakesPrecedenceOverCountLimit()
    {
        // Even at zero accounts, KYC Rejected must block before the count is even evaluated
        var db = BuildDb();
        var result = await BuildPolicy(db, kycStatus: "Rejected").CanCreateAccountAsync(Guid.NewGuid(), "Individual");

        result.IsAllowed.Should().BeFalse();
        result.ErrorCode.Should().Be("KYC_REQUIRED");
    }

    // ── test double ────────────────────────────────────────────────────────

    private sealed class StubKycStatusClient : IKycStatusClient
    {
        private readonly string _status;
        public StubKycStatusClient(string status) => _status = status;
        public Task<string> GetKycStatusAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(_status);
    }
}
