using System;

namespace TransactionService.Models.Dtos;

public class BudgetAggregationDto
{
    public decimal Fun { get; set; }
    public decimal Fixed { get; set; }
    public decimal Future { get; set; }
    public decimal Total { get; set; }
    public PeriodDto Period { get; set; } = new();
}

public class PeriodDto
{
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
}
