using System.Security.Cryptography;
using System.Text;

namespace UserService.Services;

/// <summary>
/// PBKDF2-SHA256 password hasher (310,000 iterations per OWASP recommendation).
/// Supports legacy SHA256 hashes for backward compatibility.
/// </summary>
public sealed class PasswordHasherService : IPasswordHasher
{
    private const int Iterations = 310_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        return $"v1${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
    }

    public bool Verify(string password, string hash)
    {
        var parts = hash.Split('$');
        if (parts.Length == 4 && parts[0] == "v1")
        {
            try
            {
                if (!int.TryParse(parts[1], out var iterations)) return false;
                var salt = Convert.FromBase64String(parts[2]);
                var expected = Convert.FromBase64String(parts[3]);
                var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
                return CryptographicOperations.FixedTimeEquals(actual, expected);
            }
            catch
            {
                return false;
            }
        }

        // Legacy SHA256 + "salt" format (backward compatibility)
        try
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password + "salt"));
            return CryptographicOperations.FixedTimeEquals(bytes, Convert.FromBase64String(hash));
        }
        catch
        {
            return false;
        }
    }
}
