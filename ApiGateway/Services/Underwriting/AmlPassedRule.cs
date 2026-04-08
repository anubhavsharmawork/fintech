using ApiGateway.Configuration;
using ApiGateway.Models;
using Microsoft.Extensions.Options;

namespace ApiGateway.Services.Underwriting;

public class AmlPassedRule : IUnderwritingRule
{
    private readonly UnderwritingSettings _settings;

    public AmlPassedRule(IOptions<UnderwritingSettings> settings)
    {
        _settings = settings.Value;
    }

    public int Evaluate(SanctionRequest request, int priorApprovedCount)
    {
        return request.AmlStatus == AmlStatus.Passed
            ? -_settings.AmlPassedRiskReduction
            : 0;
    }
}
