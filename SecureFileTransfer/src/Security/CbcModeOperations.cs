namespace SecureFileTransfer.Security;

/// <summary>
/// CBC (Cipher Block Chaining) mode for streaming AES encryption/decryption.
/// Works with AesCoreImpl (custom FIPS 197 AES from scratch).
/// 
/// Padding is handled externally by AesCryptographyService – this class
/// only performs raw block operations so chaining state stays consistent
/// across multiple streaming calls.
/// </summary>
public class CbcModeOperations
{
    private readonly AesCoreImpl _aes;
    private byte[] _lastCipherBlock;
    private const int BLOCK_SIZE = 16;

    public CbcModeOperations(AesCoreImpl aesInstance, byte[] iv)
    {
        ArgumentNullException.ThrowIfNull(aesInstance);
        ArgumentNullException.ThrowIfNull(iv);

        if (iv.Length != BLOCK_SIZE)
            throw new ArgumentException($"IV must be {BLOCK_SIZE} bytes", nameof(iv));

        _aes = aesInstance;
        _lastCipherBlock = (byte[])iv.Clone();
    }

    /// <summary>
    /// Encrypt raw blocks (no padding). Data must be a multiple of 16 bytes.
    /// Chaining state is preserved between calls for streaming.
    /// </summary>
    public byte[] EncryptRaw(byte[] plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        if (plaintext.Length % BLOCK_SIZE != 0)
            throw new ArgumentException($"Plaintext must be a multiple of {BLOCK_SIZE}", nameof(plaintext));

        byte[] ciphertext = new byte[plaintext.Length];
        byte[] prev = (byte[])_lastCipherBlock.Clone();

        for (int offset = 0; offset < plaintext.Length; offset += BLOCK_SIZE)
        {
            byte[] xored = new byte[BLOCK_SIZE];
            for (int i = 0; i < BLOCK_SIZE; i++)
                xored[i] = (byte)(plaintext[offset + i] ^ prev[i]);

            byte[] block = new byte[BLOCK_SIZE];
            _aes.EncryptBlock(xored, 0, block, 0);
            Buffer.BlockCopy(block, 0, ciphertext, offset, BLOCK_SIZE);
            prev = block;
        }

        _lastCipherBlock = prev;
        return ciphertext;
    }

    /// <summary>
    /// Decrypt raw blocks (no unpadding). Data must be a multiple of 16 bytes.
    /// Chaining state is preserved between calls for streaming.
    /// </summary>
    public byte[] DecryptRaw(byte[] ciphertext)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);
        if (ciphertext.Length % BLOCK_SIZE != 0)
            throw new ArgumentException($"Ciphertext must be a multiple of {BLOCK_SIZE}", nameof(ciphertext));

        byte[] plaintext = new byte[ciphertext.Length];
        byte[] prev = (byte[])_lastCipherBlock.Clone();

        for (int offset = 0; offset < ciphertext.Length; offset += BLOCK_SIZE)
        {
            byte[] block = new byte[BLOCK_SIZE];
            _aes.DecryptBlock(ciphertext, offset, block, 0);

            for (int i = 0; i < BLOCK_SIZE; i++)
                plaintext[offset + i] = (byte)(block[i] ^ prev[i]);

            prev = new byte[BLOCK_SIZE];
            Buffer.BlockCopy(ciphertext, offset, prev, 0, BLOCK_SIZE);
        }

        _lastCipherBlock = prev;
        return plaintext;
    }
}
