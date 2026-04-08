using ApiGateway.Configuration;
using ApiGateway.Models;
using Microsoft.Extensions.Options;

namespace ApiGateway.Services.Underwriting;

public class NoPriorApprovalsRule : IUnderwritingRule
{
    private readonly UnderwritingSettings _settings;

    public NoPriorApprovalsRule(IOptions<UnderwritingSettings> settings)
    {
        _settings = settings.Value;
    }

    public int Evaluate(SanctionRequest request, int priorApprovedCount)
    {
        return priorApprovedCount == 0
            ? _settings.NoPriorApprovalsRiskPoints
            : 0;
    }
}
