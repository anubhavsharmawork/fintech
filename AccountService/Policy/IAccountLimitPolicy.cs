namespace AccountService.Policy;

/// <summary>
/// Outcome of a limit check. Controllers must not inspect raw counts or thresholds —
/// only this result is consumed.
/// </summary>
public sealed class LimitCheckResult
{
    public bool IsAllowed { get; private init; }
    public string? ErrorCode { get; private init; }
    public string? ErrorMessage { get; private init; }

    public static LimitCheckResult Allow() => new() { IsAllowed = true };

    public static LimitCheckResult Deny(string errorCode, string errorMessage) =>
        new() { IsAllowed = false, ErrorCode = errorCode, ErrorMessage = errorMessage };
}

/// <summary>
/// Encapsulates all account and bank connection limit logic.
/// Controllers must call this interface and never contain raw numeric thresholds.
/// </summary>
public interface IAccountLimitPolicy
{
    /// <summary>
    /// Checks whether the user is permitted to create a new internal account.
    /// Validates KYC status and the per-client-type account count limit.
    /// </summary>
    Task<LimitCheckResult> CanCreateAccountAsync(Guid userId, string clientType, CancellationToken ct = default);

    /// <summary>
    /// Checks whether the user is permitted to add a new external bank connection.
    /// Validates KYC status and the per-client-type connection count limit.
    /// </summary>
    Task<LimitCheckResult> CanAddBankConnectionAsync(Guid userId, string clientType, CancellationToken ct = default);
}
