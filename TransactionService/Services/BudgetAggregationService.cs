using Microsoft.EntityFrameworkCore;
using TransactionService.Constants;
using TransactionService.Data;
using TransactionService.Models.Dtos;

namespace TransactionService.Services;

public class BudgetAggregationService
{
    private readonly TransactionDbContext _context;

    public BudgetAggregationService(TransactionDbContext context)
    {
        _context = context;
    }

    public async Task<BudgetAggregationDto> GetBudgetAsync(
        Guid accountId,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        var transactions = await _context.Transactions
            .AsNoTracking()
            .Where(t => t.AccountId == accountId && t.CreatedAt >= from && t.CreatedAt <= to)
            .ToListAsync(cancellationToken);

        decimal fun = 0m;
        decimal fixedTotal = 0m;
        decimal future = 0m;

        foreach (var transaction in transactions)
        {
            if (string.IsNullOrWhiteSpace(transaction.SpendingType))
            {
                continue;
            }

            if (!SpendingTypeConstants.IsValid(transaction.SpendingType))
            {
                continue;
            }

            var normalized = SpendingTypeConstants.Normalize(transaction.SpendingType);
            switch (normalized.ToUpperInvariant())
            {
                case "FUN":
                    fun += transaction.Amount;
                    break;
                case "FIXED":
                    fixedTotal += transaction.Amount;
                    break;
                case "FUTURE":
                    future += transaction.Amount;
                    break;
            }
        }

        return new BudgetAggregationDto
        {
            Fun = fun,
            Fixed = fixedTotal,
            Future = future,
            Total = fun + fixedTotal + future,
            Period = new PeriodDto
            {
                From = from.ToString("O"),
                To = to.ToString("O")
            }
        };
    }
}
