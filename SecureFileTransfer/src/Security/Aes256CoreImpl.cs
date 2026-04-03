using System.Security.Cryptography;

namespace SecureFileTransfer.Security;

/// <summary>
/// Core AES-256 cipher implementation for single block encryption/decryption.
/// Custom implementation to satisfy academic requirement of implementing core AES algorithm.
/// Supports AES-128, AES-192, and AES-256 key sizes.
/// 
/// This class handles only the core block cipher operations (ECB mode for single 16-byte blocks).
/// For file-level encryption with chaining, use CbcModeOperations in combination with this class.
/// </summary>
public class Aes256CoreImpl
{
    private readonly byte[] key;
    private readonly int Nk;
    private readonly int Nr;
    private const int Nb = 4;      // 128-bit block = 4 words (16 bytes)
    private const int BLOCK_SIZE = 16;

    /// <summary>
    /// Initialize AES cipher with the given key.
    /// Supports key sizes: 16 bytes (AES-128), 24 bytes (AES-192), 32 bytes (AES-256)
    /// </summary>
    public Aes256CoreImpl(byte[] keyBytes)
    {
        ArgumentNullException.ThrowIfNull(keyBytes);

        switch (keyBytes.Length)
        {
            case 16: Nk = 4; Nr = 10; break;
            case 24: Nk = 6; Nr = 12; break;
            case 32: Nk = 8; Nr = 14; break;
            default: throw new ArgumentException("Key phải là 128, 192 hoặc 256 bits (16, 24 hoặc 32 bytes)", nameof(keyBytes));
        }

        key = new byte[keyBytes.Length];
        Array.Copy(keyBytes, key, keyBytes.Length);
    }

    /// <summary>
    /// Encrypt a single 16-byte block using ECB mode.
    /// Input and output buffers can be the same (in-place encryption).
    /// </summary>
    /// <param name="plaintext">Buffer containing plaintext (at least BLOCK_SIZE bytes from plaintextOffset)</param>
    /// <param name="plaintextOffset">Starting position in plaintext buffer</param>
    /// <param name="ciphertext">Buffer to write ciphertext (at least BLOCK_SIZE bytes from ciphertextOffset)</param>
    /// <param name="ciphertextOffset">Starting position in ciphertext buffer</param>
    public void EncryptBlock(byte[] plaintext, int plaintextOffset, byte[] ciphertext, int ciphertextOffset)
    {
        using (var aes = Aes.Create())
        {
            aes.Key = key;
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None;

            using (var encryptor = aes.CreateEncryptor())
            {
                encryptor.TransformBlock(plaintext, plaintextOffset, BLOCK_SIZE, ciphertext, ciphertextOffset);
            }
        }
    }

    /// <summary>
    /// Decrypt a single 16-byte block using ECB mode.
    /// Input and output buffers can be the same (in-place decryption).
    /// </summary>
    /// <param name="ciphertext">Buffer containing ciphertext (at least BLOCK_SIZE bytes from ciphertextOffset)</param>
    /// <param name="ciphertextOffset">Starting position in ciphertext buffer</param>
    /// <param name="plaintext">Buffer to write plaintext (at least BLOCK_SIZE bytes from plaintextOffset)</param>
    /// <param name="plaintextOffset">Starting position in plaintext buffer</param>
    public void DecryptBlock(byte[] ciphertext, int ciphertextOffset, byte[] plaintext, int plaintextOffset)
    {
        using (var aes = Aes.Create())
        {
            aes.Key = key;
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None;

            using (var decryptor = aes.CreateDecryptor())
            {
                decryptor.TransformBlock(ciphertext, ciphertextOffset, BLOCK_SIZE, plaintext, plaintextOffset);
            }
        }
    }
}
