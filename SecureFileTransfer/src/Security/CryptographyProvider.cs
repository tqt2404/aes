using System.Security.Cryptography;

namespace SecureFileTransfer.Security;

/// <summary>
/// Centralized cryptographic primitives: random generation and PBKDF2 key derivation.
/// All other hash/HMAC operations are performed directly in AesCryptographyService
/// using incremental TransformBlock API (required for streaming 1-pass HMAC).
/// </summary>
public static class CryptographyProvider
{
    /// <summary>
    /// Generate cryptographically secure random bytes using RandomNumberGenerator.
    /// </summary>
    public static byte[] GetRandomBytes(int length)
    {
        if (length <= 0)
            throw new ArgumentException("Length must be positive", nameof(length));

        byte[] buffer = new byte[length];
        RandomNumberGenerator.Fill(buffer);
        return buffer;
    }

    /// <summary>
    /// Derive AES key + HMAC key from password using PBKDF2-SHA256 (RFC 2898).
    /// Recommended minimum: 600,000 iterations (OWASP 2023).
    /// </summary>
    public static byte[] DeriveKeyFromPassword(string password, byte[] salt, int iterations, int desiredLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        ArgumentNullException.ThrowIfNull(salt);

        if (iterations < 100_000)
            throw new ArgumentException("Iterations must be at least 100,000", nameof(iterations));
        if (desiredLength <= 0)
            throw new ArgumentException("Desired length must be positive", nameof(desiredLength));

        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(desiredLength);
    }
}
