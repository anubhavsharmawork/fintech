namespace ApiGateway.Services;

/// <summary>
/// Generates card numbers that pass the Luhn algorithm.
/// Pure static utility — no state, no dependencies.
/// </summary>
public static class LuhnGenerator
{
    private static readonly Random Rng = new();

    /// <summary>
    /// Computes the Luhn check digit for a partial card number.
    /// The returned digit, when appended, makes the full number pass the Luhn checksum.
    /// </summary>
    public static int ComputeCheckDigit(string partial)
    {
        if (string.IsNullOrWhiteSpace(partial))
            throw new ArgumentException("Partial card number must not be empty.", nameof(partial));

        var sum = 0;
        for (var i = partial.Length - 1; i >= 0; i--)
        {
            var d = partial[i] - '0';
            var positionFromRight = partial.Length - i; // 1-based from the right of the partial
            if (positionFromRight % 2 == 1)
            {
                d *= 2;
                if (d > 9) d -= 9;
            }
            sum += d;
        }

        return (10 - (sum % 10)) % 10;
    }

    /// <summary>
    /// Validates whether a full card number passes the Luhn checksum.
    /// </summary>
    public static bool IsValid(string cardNumber)
    {
        if (string.IsNullOrWhiteSpace(cardNumber) || cardNumber.Length < 2)
            return false;

        foreach (var c in cardNumber)
        {
            if (c < '0' || c > '9') return false;
        }

        var partial = cardNumber[..^1];
        var expected = ComputeCheckDigit(partial);
        return (cardNumber[^1] - '0') == expected;
    }

    /// <summary>
    /// Generates a 16-digit card number with the given BIN prefix that passes the Luhn algorithm.
    /// </summary>
    public static string Generate(string binPrefix = "4532")
    {
        var partial = binPrefix;
        for (var i = partial.Length; i < 15; i++)
        {
            partial += Rng.Next(0, 10).ToString();
        }

        var check = ComputeCheckDigit(partial);
        return partial + check.ToString();
    }

    /// <summary>
    /// Generates a random 3-digit CVV.
    /// </summary>
    public static string GenerateCvv()
    {
        return Rng.Next(0, 1000).ToString("D3");
    }
}
