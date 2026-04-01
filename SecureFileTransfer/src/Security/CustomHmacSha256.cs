using System;

namespace SecureFileTransfer.Security;

/// <summary>
/// Custom HMAC-SHA256 implementation từ scratch.
/// </summary>
public class CustomHmacSha256
{
    private const int BLOCK_SIZE = 64;
    private const int HASH_SIZE = 32;
    
    private byte[] opad = new byte[BLOCK_SIZE];
    private byte[] ipad = new byte[BLOCK_SIZE];
    private CustomSha256 innerHash = new();

    public CustomHmacSha256(byte[] key)
    {
        if (key.Length > BLOCK_SIZE)
        {
            key = new CustomSha256().ComputeHash(key);
        }

        // Pad key
        Array.Fill(opad, (byte)0x5c);
        Array.Fill(ipad, (byte)0x36);

        for (int i = 0; i < key.Length; i++)
        {
            opad[i] ^= key[i];
            ipad[i] ^= key[i];
        }
    }

    public byte[] ComputeHash(byte[] data)
    {
        // Inner hash: H(ipad ∥ message)
        byte[] innerData = new byte[ipad.Length + data.Length];
        Array.Copy(ipad, innerData, ipad.Length);
        Array.Copy(data, 0, innerData, ipad.Length, data.Length);

        byte[] innerResult = new CustomSha256().ComputeHash(innerData);

        // Outer hash: H(opad ∥ H(ipad ∥ message))
        byte[] outerData = new byte[opad.Length + innerResult.Length];
        Array.Copy(opad, outerData, opad.Length);
        Array.Copy(innerResult, 0, outerData, opad.Length, innerResult.Length);

        return new CustomSha256().ComputeHash(outerData);
    }
}
