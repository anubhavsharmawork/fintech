using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransactionService.Models.Dtos;
using TransactionService.Services;

namespace TransactionService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BudgetController : ControllerBase
{
    private readonly BudgetAggregationService _budgetService;

    public BudgetController(BudgetAggregationService budgetService)
    {
        _budgetService = budgetService;
    }

    [HttpGet("budget")]
    [Authorize]
    public async Task<ActionResult<BudgetAggregationDto>> GetBudget(
        [FromQuery] Guid accountId,
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        CancellationToken cancellationToken)
    {
        if (accountId == Guid.Empty)
        {
            return BadRequest(new { error = "accountId is required" });
        }

        if (from == default || to == default || from > to)
        {
            return BadRequest(new { error = "Invalid date range" });
        }

        var budget = await _budgetService.GetBudgetAsync(accountId, from, to, cancellationToken);
        return Ok(budget);
    }
}
