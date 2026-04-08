namespace TransactionService.Models.Dtos;

/// <summary>
/// Query parameters for filtering transactions.
/// All parameters are optional and composable.
/// </summary>
public record TransactionFilterDto
{
    /// <summary>Start of date range filter (inclusive, ISO 8601).</summary>
    public DateTime? FromDate { get; init; }

    /// <summary>End of date range filter (inclusive, ISO 8601).</summary>
    public DateTime? ToDate { get; init; }

    /// <summary>Transaction type filter (case-insensitive: debit, credit, transfer).</summary>
    public string? TransactionType { get; init; }

    /// <summary>Minimum amount filter (inclusive).</summary>
    public decimal? MinAmount { get; init; }

    /// <summary>Maximum amount filter (inclusive).</summary>
    public decimal? MaxAmount { get; init; }

    /// <summary>Partial case-insensitive match on Description.</summary>
    public string? SearchTerm { get; init; }
}

/// <summary>
/// Response envelope for paginated, filtered transaction queries.
/// </summary>
public record TransactionPagedResponse<T>
{
    public required IEnumerable<T> Data { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }
    public required int TotalFilteredCount { get; init; }
}
