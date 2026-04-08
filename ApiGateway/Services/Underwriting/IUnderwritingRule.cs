using ApiGateway.Models;

namespace ApiGateway.Services.Underwriting;

public interface IUnderwritingRule
{
    int Evaluate(SanctionRequest request, int priorApprovedCount);
}
