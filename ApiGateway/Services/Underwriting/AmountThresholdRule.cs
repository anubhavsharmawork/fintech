using ApiGateway.Configuration;
using ApiGateway.Models;
using Microsoft.Extensions.Options;

namespace ApiGateway.Services.Underwriting;

public class AmountThresholdRule : IUnderwritingRule
{
    private readonly UnderwritingSettings _settings;

    public AmountThresholdRule(IOptions<UnderwritingSettings> settings)
    {
        _settings = settings.Value;
    }

    public int Evaluate(SanctionRequest request, int priorApprovedCount)
    {
        return request.RequestedAmount > _settings.AmountThreshold
            ? _settings.AmountThresholdRiskPoints
            : 0;
    }
}
