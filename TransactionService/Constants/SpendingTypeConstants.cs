using System;
using System.Linq;

namespace TransactionService.Constants;

public static class SpendingTypeConstants
{
    public static readonly string[] AllowedTypes = { "Fun", "Fixed", "Future" };

    public static bool IsValid(string? spendingType)
    {
        return !string.IsNullOrWhiteSpace(spendingType) &&
               AllowedTypes.Contains(spendingType, StringComparer.OrdinalIgnoreCase);
    }

    public static string Normalize(string spendingType)
    {
        return spendingType?.Trim() ?? throw new ArgumentNullException(nameof(spendingType));
    }
}
