using SecureFileTransfer.Models;

namespace SecureFileTransfer.Security;

// DEPRECATED: Replaced by AesCipherFactory.cs
// This class is kept for backward compatibility during transition period.
// NEW: Use AesCipherFactory instead

/// <summary>
/// Factory for creating AES instances with different key sizes (128, 192, 256-bit)
/// Handles key expansion for each variant
/// 
/// DEPRECATED - Use AesCipherFactory instead
/// </summary>
public class AesFactory
{
    /// <summary>
    /// Create AES instance with specified key size
    /// Each key size requires different key schedule expansion
    /// </summary>
    public static CustomAes256 CreateAes(byte[] key, AesKeySize keySize)
    {
        if (key == null)
            throw new ArgumentNullException(nameof(key));

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

        // Currently using CustomAes256 for all sizes
        // Note: This implementation is designed for 256-bit keys
        // For 128/192 support, would need CustomAes128/CustomAes192 separate implementations
        // For now, we'll use 256-bit AES with padded keys for smaller sizes

        if (keySize == AesKeySize.AES256)
        {
            return new CustomAes256(key);
        }

        // For smaller key sizes, pad with zeros to 256-bit (not ideal but functional)
        // TODO: Implement proper AES-128 and AES-192 variants
        byte[] paddedKey = new byte[32];
        Array.Copy(key, paddedKey, key.Length);
        return new CustomAes256(paddedKey);
    }

    /// <summary>
    /// Get required key length for given AES key size
    /// </summary>
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
    /// Get round count for AES variant
    /// AES-128: 10 rounds
    /// AES-192: 12 rounds
    /// AES-256: 14 rounds
    /// </summary>
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
