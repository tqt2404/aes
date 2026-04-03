using System.Security.Cryptography;
using System.Text;

namespace SecureFileTransfer.Security;

/// <summary>
/// Cryptographic provider using standard .NET System.Security.Cryptography libraries.
/// Centralized access to key derivation, HMAC, random number generation, and hashing.
/// </summary>
public static class CryptographyProvider
{
    /// <summary>
    /// Generate cryptographically secure random bytes.
    /// Replaces custom SecureRandom implementation.
    /// </summary>
    public static byte[] GetRandomBytes(int length)
    {
        if (length <= 0)
            throw new ArgumentException("Length must be positive", nameof(length));

        byte[] buffer = new byte[length];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(buffer);
        }
        return buffer;
    }

    /// <summary>
    /// Derive encryption key from password using PBKDF2 (RFC 2898).
    /// Replaces custom CustomPbkdf2 implementation.
    /// </summary>
    /// <param name="password">Password to derive key from</param>
    /// <param name="salt">Random salt (should be cryptographically secure)</param>
    /// <param name="iterations">Number of iterations (recommended: 600000 or higher for 2023+)</param>
    /// <param name="desiredLength">Desired length of derived key in bytes</param>
    /// <returns>Derived key bytes</returns>
    public static byte[] DeriveKeyFromPassword(string password, byte[] salt, int iterations, int desiredLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        ArgumentNullException.ThrowIfNull(salt);

        if (iterations < 100000)
            throw new ArgumentException("Iterations should be at least 100000 for security", nameof(iterations));

        if (desiredLength <= 0)
            throw new ArgumentException("Desired length must be positive", nameof(desiredLength));

        using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256))
        {
            return pbkdf2.GetBytes(desiredLength);
        }
    }

    /// <summary>
    /// Compute HMAC-SHA256 of data.
    /// Replaces custom CustomHmacSha256 implementation.
    /// </summary>
    /// <param name="data">Data to authenticate</param>
    /// <param name="key">HMAC key</param>
    /// <returns>HMAC-SHA256 digest (32 bytes)</returns>
    public static byte[] ComputeHmacSha256(byte[] data, byte[] key)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(key);

        using (var hmac = new HMACSHA256(key))
        {
            return hmac.ComputeHash(data);
        }
    }

    /// <summary>
    /// Compute HMAC-SHA256 asynchronously from stream.
    /// Useful for large files to avoid memory overhead.
    /// </summary>
    public static async Task<byte[]> ComputeHmacSha256Async(Stream data, byte[] key, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(key);

        using (var hmac = new HMACSHA256(key))
        {
            return await Task.Run(() => hmac.ComputeHash(data), ct);
        }
    }

    /// <summary>
    /// Compute SHA256 hash of data.
    /// </summary>
    public static byte[] ComputeSha256(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return SHA256.HashData(data);
    }

    /// <summary>
    /// Compute SHA256 hash asynchronously from stream.
    /// </summary>
    public static async Task<byte[]> ComputeSha256Async(Stream data, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(data);

        using (var sha256 = SHA256.Create())
        {
            return await sha256.ComputeHashAsync(data, ct);
        }
    }

    /// <summary>
    /// Convert byte array to hexadecimal string (lowercase).
    /// </summary>
    public static string BytesToHex(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// Convert hexadecimal string to byte array.
    /// </summary>
    public static byte[] HexToBytes(string hex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hex);

        if (hex.Length % 2 != 0)
            throw new ArgumentException("Hex string must have even number of characters", nameof(hex));

        return Convert.FromHexString(hex);
    }
}
