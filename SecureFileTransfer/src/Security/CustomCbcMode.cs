using System;

namespace SecureFileTransfer.Security;

// DEPRECATED: Replaced by CbcModeOperations.cs
// This class is kept for backward compatibility during transition period.
// NEW: Use CbcModeOperations instead

/// <summary>
/// Custom CBC (Cipher Block Chaining) mode implementation từ SCRATCH.
/// Tự xử lý IV chaining, block processing.
/// Padding được xử lý ở AesService level.
/// 
/// DEPRECATED - Use CbcModeOperations instead
/// </summary>
public class CustomCbcMode
{
    private readonly Aes256CoreImpl aes;
    private byte[] lastCipherBlock;  // Maintain state for chaining
    private const int BLOCK_SIZE = 16;

    public CustomCbcMode(Aes256CoreImpl aesInstance, byte[] initialVector)
    {
        if (initialVector.Length != BLOCK_SIZE)
            throw new ArgumentException($"IV phải là {BLOCK_SIZE} bytes", nameof(initialVector));

        aes = aesInstance;
        lastCipherBlock = new byte[BLOCK_SIZE];
        Array.Copy(initialVector, lastCipherBlock, BLOCK_SIZE);
    }

    /// <summary>
    /// Encrypt raw blocks without padding (for streaming).
    /// Data must be multiple of BLOCK_SIZE.
    /// State is maintained for proper chaining between calls.
    /// </summary>
    public byte[] EncryptRaw(byte[] plaintext)
    {
        if (plaintext.Length % BLOCK_SIZE != 0)
            throw new ArgumentException($"Plaintext phải là multiple của {BLOCK_SIZE}", nameof(plaintext));

        byte[] ciphertext = new byte[plaintext.Length];
        byte[] previousCipherBlock = new byte[BLOCK_SIZE];
        Array.Copy(lastCipherBlock, previousCipherBlock, BLOCK_SIZE);

        for (int block = 0; block < plaintext.Length; block += BLOCK_SIZE)
        {
            // XOR plaintext block với previous ciphertext (hoặc IV)
            byte[] xorBlock = new byte[BLOCK_SIZE];
            for (int i = 0; i < BLOCK_SIZE; i++)
                xorBlock[i] = (byte)(plaintext[block + i] ^ previousCipherBlock[i]);

            // Encrypt block
            byte[] encryptedBlock = new byte[BLOCK_SIZE];
            aes.EncryptBlock(xorBlock, 0, encryptedBlock, 0);

            Array.Copy(encryptedBlock, 0, ciphertext, block, BLOCK_SIZE);
            Array.Copy(encryptedBlock, previousCipherBlock, BLOCK_SIZE);
        }

        // Save last ciphertext block for next call
        Array.Copy(previousCipherBlock, lastCipherBlock, BLOCK_SIZE);

        return ciphertext;
    }

    /// <summary>
    /// Decrypt raw blocks without unpadding (for streaming).
    /// Data must be multiple of BLOCK_SIZE.
    /// State is maintained for proper chaining between calls.
    /// </summary>
    public byte[] DecryptRaw(byte[] ciphertext)
    {
        if (ciphertext.Length % BLOCK_SIZE != 0)
            throw new ArgumentException($"Ciphertext phải là multiple của {BLOCK_SIZE}", nameof(ciphertext));

        byte[] plaintext = new byte[ciphertext.Length];
        byte[] previousCipherBlock = new byte[BLOCK_SIZE];
        Array.Copy(lastCipherBlock, previousCipherBlock, BLOCK_SIZE);

        for (int block = 0; block < ciphertext.Length; block += BLOCK_SIZE)
        {
            // Decrypt block
            byte[] decryptedBlock = new byte[BLOCK_SIZE];
            aes.DecryptBlock(ciphertext, block, decryptedBlock, 0);

            // XOR với previous ciphertext
            byte[] plaintextBlock = new byte[BLOCK_SIZE];
            for (int i = 0; i < BLOCK_SIZE; i++)
                plaintextBlock[i] = (byte)(decryptedBlock[i] ^ previousCipherBlock[i]);

            Array.Copy(plaintextBlock, 0, plaintext, block, BLOCK_SIZE);
            Array.Copy(ciphertext, block, previousCipherBlock, 0, BLOCK_SIZE);
        }

        // Save last ciphertext block for next call
        Array.Copy(previousCipherBlock, lastCipherBlock, BLOCK_SIZE);

        return plaintext;
    }

    /// <summary>
    /// Encrypt plaintext with automatic PKCS7 padding.
    /// Convenience method for simple use cases (not for streams).
    /// WARNING: Each call creates new state - use EncryptRaw for streaming.
    /// </summary>
    public byte[] Encrypt(byte[] plaintext)
    {
        byte[] padded = AddPkcs7Padding(plaintext);
        return EncryptRaw(padded);
    }

    /// <summary>
    /// Decrypt ciphertext with automatic PKCS7 unpadding.
    /// Convenience method for simple use cases (not for streams).
    /// </summary>
    public byte[] Decrypt(byte[] ciphertext)
    {
        byte[] plain = DecryptRaw(ciphertext);
        return RemovePkcs7Padding(plain);
    }

    private static byte[] AddPkcs7Padding(byte[] data)
    {
        int paddingLen = BLOCK_SIZE - (data.Length % BLOCK_SIZE);
        byte[] padded = new byte[data.Length + paddingLen];
        Array.Copy(data, padded, data.Length);
        for (int i = 0; i < paddingLen; i++)
            padded[data.Length + i] = (byte)paddingLen;
        return padded;
    }

    private static byte[] RemovePkcs7Padding(byte[] data)
    {
        if (data.Length == 0)
            throw new ArgumentException("Data cannot be empty for padding removal");

        int paddingLen = data[data.Length - 1];
        if (paddingLen <= 0 || paddingLen > BLOCK_SIZE)
            throw new ArgumentException("Invalid or corrupted padding");

        for (int i = 0; i < paddingLen; i++)
        {
            if (data[data.Length - 1 - i] != paddingLen)
                throw new ArgumentException("Invalid or corrupted padding");
        }

        byte[] unpadded = new byte[data.Length - paddingLen];
        Array.Copy(data, unpadded, unpadded.Length);
        return unpadded;
    }
}
