using System.Threading;
using AccountService.Policy;

namespace Tests.Mocks;

public class AllowAllLimitPolicy : IAccountLimitPolicy
{
    public Task<LimitCheckResult> CanCreateAccountAsync(Guid userId, string clientType, CancellationToken ct = default)
        => Task.FromResult(LimitCheckResult.Allow());

    public Task<LimitCheckResult> CanAddBankConnectionAsync(Guid userId, string clientType, CancellationToken ct = default)
        => Task.FromResult(LimitCheckResult.Allow());
}

