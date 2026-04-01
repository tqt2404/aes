using System;

namespace SecureFileTransfer.Security;

/// <summary>
/// Simple random number generator - không dùng thư viện có sẵn.
/// CẢNH BÁO: Đây là ví dụ giáo dục, không an toàn cho cryptography thực tế!
/// </summary>
public class CustomRandom
{
    private ulong state;

    public CustomRandom()
    {
        // Seed từ system time
        state = (ulong)DateTime.UtcNow.Ticks;
        // Mix bits
        state ^= (state << 21);
        state ^= (state >> 35);
        state ^= (state << 4);
    }

    public byte[] GetBytes(int length)
    {
        byte[] result = new byte[length];
        for (int i = 0; i < length; i++)
        {
            result[i] = (byte)Next();
        }
        return result;
    }

    private int Next()
    {
        // Simple LCG (Linear Congruential Generator)
        state = state * 6364136223846793005UL + 1442695040888963407UL;
        return (int)(state >> 32);
    }
}
