namespace SecureFileTransfer.Security;

/// <summary>
/// CBC (Cipher Block Chaining) Mode implementation.
/// Chains block encryption/decryption operations using initialization vector (IV).
/// 
/// This class works in conjunction with Aes256Impl (fully custom AES from scratch)
/// to provide complete file encryption with proper IV chaining and state management for streaming.
/// 
/// Note: Padding is handled at the service level (AesCryptographyService).
/// This class only handles raw block operations for maximum flexibility.
/// </summary>
public class CbcModeOperations
{
    private readonly Aes256Impl aes;
    private byte[] lastCipherBlock;  // Maintain state for chaining
    private const int BLOCK_SIZE = 16;

    /// <summary>
    /// Initialize CBC mode with an AES cipher and initialization vector.
    /// </summary>
    /// <param name="aesInstance">AES cipher instance (Aes256Impl - custom AES from scratch)</param>
    /// <param name="initialVector">16-byte initialization vector</param>
    public CbcModeOperations(Aes256Impl aesInstance, byte[] initialVector)
    {
        ArgumentNullException.ThrowIfNull(aesInstance);
        ArgumentNullException.ThrowIfNull(initialVector);

        if (initialVector.Length != BLOCK_SIZE)
            throw new ArgumentException($"IV must be {BLOCK_SIZE} bytes", nameof(initialVector));

        aes = aesInstance;
        lastCipherBlock = new byte[BLOCK_SIZE];
        Array.Copy(initialVector, lastCipherBlock, BLOCK_SIZE);
    }

    /// <summary>
    /// Encrypt raw blocks without padding (for streaming).
    /// Data must be a multiple of BLOCK_SIZE (16 bytes).
    /// State is maintained for proper chaining between calls.
    /// </summary>
    /// <param name="plaintext">Plaintext data to encrypt (must be multiple of 16 bytes)</param>
    /// <returns>Encrypted ciphertext</returns>
    public byte[] EncryptRaw(byte[] plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        if (plaintext.Length % BLOCK_SIZE != 0)
            throw new ArgumentException($"Plaintext must be multiple of {BLOCK_SIZE}", nameof(plaintext));

        byte[] ciphertext = new byte[plaintext.Length];
        byte[] previousCipherBlock = new byte[BLOCK_SIZE];
        Array.Copy(lastCipherBlock, previousCipherBlock, BLOCK_SIZE);

        for (int block = 0; block < plaintext.Length; block += BLOCK_SIZE)
        {
            // CBC: XOR plaintext block with previous ciphertext block (or IV for first block)
            byte[] xorBlock = new byte[BLOCK_SIZE];
            for (int i = 0; i < BLOCK_SIZE; i++)
                xorBlock[i] = (byte)(plaintext[block + i] ^ previousCipherBlock[i]);

            // Encrypt block using custom Aes256Impl
            byte[] encryptedBlock = new byte[BLOCK_SIZE];
            aes.EncryptBlock(xorBlock, 0, encryptedBlock, 0);

            Array.Copy(encryptedBlock, 0, ciphertext, block, BLOCK_SIZE);
            Array.Copy(encryptedBlock, previousCipherBlock, BLOCK_SIZE);
        }

        // Save last ciphertext block for next call (state for chaining)
        Array.Copy(previousCipherBlock, lastCipherBlock, BLOCK_SIZE);

        return ciphertext;
    }

    /// <summary>
    /// Decrypt raw blocks without unpadding (for streaming).
    /// Data must be a multiple of BLOCK_SIZE (16 bytes).
    /// State is maintained for proper chaining between calls.
    /// </summary>
    /// <param name="ciphertext">Ciphertext data to decrypt (must be multiple of 16 bytes)</param>
    /// <returns>Decrypted plaintext</returns>
    public byte[] DecryptRaw(byte[] ciphertext)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);

        if (ciphertext.Length % BLOCK_SIZE != 0)
            throw new ArgumentException($"Ciphertext must be multiple of {BLOCK_SIZE}", nameof(ciphertext));

        byte[] plaintext = new byte[ciphertext.Length];
        byte[] previousCipherBlock = new byte[BLOCK_SIZE];
        Array.Copy(lastCipherBlock, previousCipherBlock, BLOCK_SIZE);

        for (int block = 0; block < ciphertext.Length; block += BLOCK_SIZE)
        {
            // Decrypt block using custom Aes256Impl
            byte[] decryptedBlock = new byte[BLOCK_SIZE];
            aes.DecryptBlock(ciphertext, block, decryptedBlock, 0);

            // CBC: XOR decrypted block with previous ciphertext block
            byte[] plaintextBlock = new byte[BLOCK_SIZE];
            for (int i = 0; i < BLOCK_SIZE; i++)
                plaintextBlock[i] = (byte)(decryptedBlock[i] ^ previousCipherBlock[i]);

            Array.Copy(plaintextBlock, 0, plaintext, block, BLOCK_SIZE);
            Array.Copy(ciphertext, block, previousCipherBlock, 0, BLOCK_SIZE);
        }

        // Save last ciphertext block for next call (state for chaining)
        Array.Copy(previousCipherBlock, lastCipherBlock, BLOCK_SIZE);

        return plaintext;
    }

    /// <summary>
    /// Encrypt plaintext with automatic PKCS7 padding.
    /// Convenience method for simple use cases (not for streams).
    /// WARNING: Each call creates new state - use EncryptRaw for streaming operations.
    /// </summary>
    public byte[] Encrypt(byte[] plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        byte[] padded = AddPkcs7Padding(plaintext);
        return EncryptRaw(padded);
    }

    /// <summary>
    /// Decrypt ciphertext with automatic PKCS7 unpadding.
    /// Convenience method for simple use cases (not for streams).
    /// </summary>
    public byte[] Decrypt(byte[] ciphertext)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);
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
