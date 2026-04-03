using SecureFileTransfer.Models;

namespace SecureFileTransfer.Security;

/// <summary>
/// Factory for creating AES cipher instances with different key sizes (128, 192, 256-bit).
/// Responsible for key validation and cipher instantiation.
/// 
/// This factory creates Aes256CoreImpl instances (fully custom AES from scratch) that work with CbcModeOperations
/// to provide complete file encryption/decryption functionality.
/// </summary>
public class AesCipherFactory
{
    /// <summary>
    /// Create AES cipher instance with specified key size.
    /// Validates key length matches the requested AES variant.
    /// Uses Aes256CoreImpl - complete custom AES-256 implementation from scratch (FIPS 197).
    /// </summary>
    /// <param name="key">Encryption key (must be 16, 24, or 32 bytes)</param>
    /// <param name="keySize">AES variant (128, 192, or 256-bit)</param>
    /// <returns>Initialized AES cipher instance</returns>
    public static Aes256CoreImpl CreateAes(byte[] key, AesKeySize keySize)
    {
        ArgumentNullException.ThrowIfNull(key);

        int expectedKeyLength = keySize switch
        {
            AesKeySize.AES128 => 16,  // 128-bit = 16 bytes
            AesKeySize.AES192 => 24,  // 192-bit = 24 bytes
            AesKeySize.AES256 => 32,  // 256-bit = 32 bytes
            _ => throw new ArgumentException($"Unsupported key size: {keySize}")
        };

        if (key.Length != expectedKeyLength)
            throw new ArgumentException(
                $"Key size mismatch: expected {expectedKeyLength} bytes for {keySize}, got {key.Length} bytes",
                nameof(key));

        // Use Aes256CoreImpl - fully custom AES implementation from scratch (FIPS 197)
        // Supports AES-128 (Nk=4, Nr=10), AES-192 (Nk=6, Nr=12), AES-256 (Nk=8, Nr=14) automatically based on key length
        return new Aes256CoreImpl(key);
    }

    /// <summary>
    /// Get required key length for given AES key size.
    /// </summary>
    /// <param name="keySize">AES variant</param>
    /// <returns>Key length in bytes (16, 24, or 32)</returns>
    public static int GetKeyLength(AesKeySize keySize)
    {
        return keySize switch
        {
            AesKeySize.AES128 => 16,
            AesKeySize.AES192 => 24,
            AesKeySize.AES256 => 32,
            _ => throw new ArgumentException($"Unsupported key size: {keySize}")
        };
    }

    /// <summary>
    /// Get AES round count for variant.
    /// Number of rounds determines encryption strength and performance.
    /// 
    /// AES-128: 10 rounds
    /// AES-192: 12 rounds
    /// AES-256: 14 rounds
    /// </summary>
    /// <param name="keySize">AES variant</param>
    /// <returns>Number of rounds (10, 12, or 14)</returns>
    public static int GetRoundCount(AesKeySize keySize)
    {
        return keySize switch
        {
            AesKeySize.AES128 => 10,
            AesKeySize.AES192 => 12,
            AesKeySize.AES256 => 14,
            _ => throw new ArgumentException($"Unsupported key size: {keySize}")
        };
    }
}
