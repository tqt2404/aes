using System;

namespace SecureFileTransfer.Security;

/// <summary>
/// Custom CBC (Cipher Block Chaining) mode implementation từ SCRATCH.
/// Tự xử lý IV chaining, block processing, và PKCS7 padding.
/// </summary>
public class CustomCbcMode
{
    private readonly CustomAes256 aes;
    private readonly byte[] iv;
    private const int BLOCK_SIZE = 16;

    public CustomCbcMode(CustomAes256 aesInstance, byte[] initialVector)
    {
        if (initialVector.Length != BLOCK_SIZE)
            throw new ArgumentException($"IV phải là {BLOCK_SIZE} bytes", nameof(initialVector));

        aes = aesInstance;
        iv = new byte[BLOCK_SIZE];
        Array.Copy(initialVector, iv, BLOCK_SIZE);
    }

    /// <summary>
    /// Encrypt dữ liệu trong CBC mode với PKCS7 padding.
    /// </summary>
    public byte[] Encrypt(byte[] plaintext)
    {
        byte[] paddedPlaintext = AddPkcs7Padding(plaintext);
        byte[] ciphertext = new byte[paddedPlaintext.Length];
        byte[] previousCipherBlock = new byte[BLOCK_SIZE];
        Array.Copy(iv, previousCipherBlock, BLOCK_SIZE);

        for (int block = 0; block < paddedPlaintext.Length; block += BLOCK_SIZE)
        {
            // XOR plaintext block với previous ciphertext (hoặc IV)
            byte[] xorBlock = new byte[BLOCK_SIZE];
            for (int i = 0; i < BLOCK_SIZE; i++)
                xorBlock[i] = (byte)(paddedPlaintext[block + i] ^ previousCipherBlock[i]);

            // Encrypt block
            byte[] encryptedBlock = new byte[BLOCK_SIZE];
            aes.EncryptBlock(xorBlock, 0, encryptedBlock, 0);

            Array.Copy(encryptedBlock, 0, ciphertext, block, BLOCK_SIZE);
            Array.Copy(encryptedBlock, previousCipherBlock, BLOCK_SIZE);
        }

        return ciphertext;
    }

    /// <summary>
    /// Decrypt dữ liệu trong CBC mode và remove PKCS7 padding.
    /// </summary>
    public byte[] Decrypt(byte[] ciphertext)
    {
        if (ciphertext.Length % BLOCK_SIZE != 0)
            throw new ArgumentException($"Ciphertext phải là multiple của {BLOCK_SIZE}", nameof(ciphertext));

        byte[] plaintext = new byte[ciphertext.Length];
        byte[] previousCipherBlock = new byte[BLOCK_SIZE];
        Array.Copy(iv, previousCipherBlock, BLOCK_SIZE);

        for (int block = 0; block < ciphertext.Length; block += BLOCK_SIZE)
        {
            // Decrypt block
            byte[] decryptedBlock = new byte[BLOCK_SIZE];
            aes.DecryptBlock(ciphertext, block, decryptedBlock, 0);

            // XOR với previous ciphertext (hoặc IV)
            byte[] plaintextBlock = new byte[BLOCK_SIZE];
            for (int i = 0; i < BLOCK_SIZE; i++)
                plaintextBlock[i] = (byte)(decryptedBlock[i] ^ previousCipherBlock[i]);

            Array.Copy(plaintextBlock, 0, plaintext, block, BLOCK_SIZE);
            Array.Copy(ciphertext, block, previousCipherBlock, 0, BLOCK_SIZE);
        }

        return RemovePkcs7Padding(plaintext);
    }

    private byte[] AddPkcs7Padding(byte[] data)
    {
        int paddingLen = BLOCK_SIZE - (data.Length % BLOCK_SIZE);
        byte[] padded = new byte[data.Length + paddingLen];
        Array.Copy(data, padded, data.Length);
        for (int i = 0; i < paddingLen; i++)
            padded[data.Length + i] = (byte)paddingLen;
        return padded;
    }

    private byte[] RemovePkcs7Padding(byte[] data)
    {
        if (data.Length == 0)
            throw new ArgumentException("Data không được trống");

        int paddingLen = data[data.Length - 1];
        if (paddingLen <= 0 || paddingLen > BLOCK_SIZE)
            throw new InvalidOperationException("Invalid padding");

        for (int i = 0; i < paddingLen; i++)
        {
            if (data[data.Length - 1 - i] != paddingLen)
                throw new InvalidOperationException("Invalid padding");
        }

        byte[] unpadded = new byte[data.Length - paddingLen];
        Array.Copy(data, unpadded, unpadded.Length);
        return unpadded;
    }
}
