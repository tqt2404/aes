using System;

namespace SecureFileTransfer.Security;

/// <summary>
/// Custom PBKDF2 implementation từ scratch (RFC 2898).
/// Sử dụng HMAC-SHA256 làm PRF.
/// </summary>
public class CustomPbkdf2
{
    public static byte[] DeriveKey(string password, byte[] salt, int iterations, int keySize)
    {
        byte[] passwordBytes = System.Text.Encoding.UTF8.GetBytes(password);
        return DeriveKey(passwordBytes, salt, iterations, keySize);
    }

    public static byte[] DeriveKey(byte[] password, byte[] salt, int iterations, int keySize)
    {
        var hmac = new CustomHmacSha256(password);
        int hashSize = 32; // SHA256 = 32 bytes
        int blockCount = (keySize + hashSize - 1) / hashSize;
        
        byte[] result = new byte[keySize];

        for (int blockIndex = 1; blockIndex <= blockCount; blockIndex++)
        {
            byte[] blockResult = ComputeBlock(hmac, salt, iterations, blockIndex);
            
            int copyLen = Math.Min(hashSize, keySize - (blockIndex - 1) * hashSize);
            Array.Copy(blockResult, 0, result, (blockIndex - 1) * hashSize, copyLen);
        }

        return result;
    }

    private static byte[] ComputeBlock(CustomHmacSha256 prf, byte[] salt, int iterations, int blockIndex)
    {
        byte[] blockNumBytes = new byte[4];
        blockNumBytes[0] = (byte)(blockIndex >> 24);
        blockNumBytes[1] = (byte)(blockIndex >> 16);
        blockNumBytes[2] = (byte)(blockIndex >> 8);
        blockNumBytes[3] = (byte)blockIndex;

        byte[] input = new byte[salt.Length + 4];
        Array.Copy(salt, input, salt.Length);
        Array.Copy(blockNumBytes, 0, input, salt.Length, 4);

        byte[] u = prf.ComputeHash(input);
        byte[] blockResult = new byte[u.Length];
        Array.Copy(u, blockResult, u.Length);

        for (int i = 1; i < iterations; i++)
        {
            u = prf.ComputeHash(u);
            for (int j = 0; j < blockResult.Length; j++)
            {
                blockResult[j] ^= u[j];
            }
        }

        return blockResult;
    }
}
