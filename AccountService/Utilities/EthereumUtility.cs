using System.Text.RegularExpressions;

namespace AccountService.Utilities;

public static class EthereumUtility
{
    /// <summary>
    /// Validates a hexadecimal Ethereum address and returns a trimmed value.
    /// Throws <see cref="ArgumentException"/> if invalid.
    /// </summary>
    public static string ValidateAndChecksumAddress(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("Invalid Ethereum address format", nameof(address));
        }

        var normalized = address.Trim();

        if (!normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Invalid Ethereum address format", nameof(address));
        }

        if (normalized.Length != 42)
        {
            throw new ArgumentException("Address must be 42 characters (0x + 40 hex)", nameof(address));
        }

        if (!Regex.IsMatch(normalized.Substring(2), "^[0-9a-fA-F]{40}$"))
        {
            throw new ArgumentException("Address must be hexadecimal", nameof(address));
        }

        return normalized;
    }
}
