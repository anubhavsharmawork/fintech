using System.Security.Cryptography;
using System.Text;

namespace AccountService.Services;

/// <summary>
/// AES-256-CBC encryption service for sensitive fields stored at rest.
/// Requires ENCRYPTION_KEY to be a 32-byte base64-encoded value in configuration.
/// </summary>
public sealed class EncryptionService : IEncryptionService
{
    private readonly byte[] _key;

    public EncryptionService(IConfiguration configuration)
    {
        var keyBase64 = configuration["ENCRYPTION_KEY"]
            ?? throw new InvalidOperationException(
                "ENCRYPTION_KEY is not configured. Generate a 32-byte base64 key and set it as an environment variable.");

        _key = Convert.FromBase64String(keyBase64);
        if (_key.Length != 32)
            throw new InvalidOperationException("ENCRYPTION_KEY must decode to exactly 32 bytes (256-bit AES key).");
    }

    public string Encrypt(string plaintext)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();
        using var encryptor = aes.CreateEncryptor();
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var cipherBytes = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);
        var result = new byte[aes.IV.Length + cipherBytes.Length];
        aes.IV.CopyTo(result, 0);
        cipherBytes.CopyTo(result, aes.IV.Length);
        return Convert.ToBase64String(result);
    }

    public string Decrypt(string ciphertext)
    {
        var data = Convert.FromBase64String(ciphertext);
        if (data.Length < 16)
            throw new CryptographicException("Invalid ciphertext length.");
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = data[..16];
        using var decryptor = aes.CreateDecryptor();
        var plainBytes = decryptor.TransformFinalBlock(data, 16, data.Length - 16);
        return Encoding.UTF8.GetString(plainBytes);
    }
}
