using ApiGateway.Configuration;
using ApiGateway.Models;
using Microsoft.Extensions.Options;

namespace ApiGateway.Services.Underwriting;

public class KycPassedRule : IUnderwritingRule
{
    private readonly UnderwritingSettings _settings;

    public KycPassedRule(IOptions<UnderwritingSettings> settings)
    {
        _settings = settings.Value;
    }

    public int Evaluate(SanctionRequest request, int priorApprovedCount)
    {
        return request.KycStatus == KycStatus.Passed
            ? -_settings.KycPassedRiskReduction
            : 0;
    }
}
