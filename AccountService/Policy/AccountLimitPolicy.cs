using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AccountService.Data;

namespace AccountService.Policy;

public sealed class AccountLimitPolicy : IAccountLimitPolicy
{
    private readonly AccountDbContext _db;
    private readonly IKycStatusClient _kycClient;
    private readonly AccountLimitsOptions _limits;
    private readonly ILogger<AccountLimitPolicy> _logger;

    public AccountLimitPolicy(
        AccountDbContext db,
        IKycStatusClient kycClient,
        IOptions<AccountLimitsOptions> limits,
        ILogger<AccountLimitPolicy> logger)
    {
        _db = db;
        _kycClient = kycClient;
        _limits = limits.Value;
        _logger = logger;
    }

    public async Task<LimitCheckResult> CanCreateAccountAsync(Guid userId, string clientType, CancellationToken ct = default)
    {
        var kycResult = await CheckKycAsync(userId, ct);
        if (!kycResult.IsAllowed) return kycResult;

        var limits = GetLimits(clientType);
        var current = await _db.Accounts.CountAsync(a => a.UserId == userId, ct);

        if (current >= limits.MaxAccounts)
        {
            _logger.LogWarning(
                "[LimitPolicy] Account creation blocked. UserId={UserId} ClientType={ClientType} CurrentCount={Current} Limit={Limit}",
                userId, clientType, current, limits.MaxAccounts);

            return LimitCheckResult.Deny(
                "ACCOUNT_LIMIT_EXCEEDED",
                $"You have reached the maximum of {limits.MaxAccounts} accounts allowed for {clientType} clients.");
        }

        return LimitCheckResult.Allow();
    }

    public async Task<LimitCheckResult> CanAddBankConnectionAsync(Guid userId, string clientType, CancellationToken ct = default)
    {
        var kycResult = await CheckKycAsync(userId, ct);
        if (!kycResult.IsAllowed) return kycResult;

        var limits = GetLimits(clientType);
        var current = await _db.BankConnections.CountAsync(bc => bc.UserId == userId, ct);

        if (current >= limits.MaxBankConnections)
        {
            _logger.LogWarning(
                "[LimitPolicy] Bank connection blocked. UserId={UserId} ClientType={ClientType} CurrentCount={Current} Limit={Limit}",
                userId, clientType, current, limits.MaxBankConnections);

            return LimitCheckResult.Deny(
                "CONNECTION_LIMIT_EXCEEDED",
                $"You have reached the maximum of {limits.MaxBankConnections} bank connections allowed for {clientType} clients.");
        }

        return LimitCheckResult.Allow();
    }

    private async Task<LimitCheckResult> CheckKycAsync(Guid userId, CancellationToken ct)
    {
        var status = await _kycClient.GetKycStatusAsync(userId, ct);

        if (string.Equals(status, "Pending", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, "Rejected", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "[LimitPolicy] Action blocked by KYC status. UserId={UserId} KycStatus={KycStatus}",
                userId, status);

            return LimitCheckResult.Deny(
                "KYC_REQUIRED",
                $"Your account cannot perform this action while KYC status is '{status}'. Please complete identity verification.");
        }

        return LimitCheckResult.Allow();
    }

    private ClientTypeLimits GetLimits(string clientType) =>
        string.Equals(clientType, "Corporate", StringComparison.OrdinalIgnoreCase)
            ? _limits.Corporate
            : _limits.Individual;
}
