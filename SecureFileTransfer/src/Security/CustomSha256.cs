using System;

namespace SecureFileTransfer.Security;

/// <summary>
/// Custom SHA256 implementation từ scratch (không dùng thư viện).
/// FIPS 180-4 specification.
/// </summary>
public class CustomSha256
{
    private static readonly uint[] k = {
        0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5,
        0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3, 0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174,
        0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc, 0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
        0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7, 0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967,
        0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13, 0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85,
        0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3, 0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
        0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
        0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208, 0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2
    };

    private uint[] state = new uint[8];

    public CustomSha256()
    {
        Reset();
    }

    public void Reset()
    {
        state[0] = 0x6a09e667;
        state[1] = 0xbb67ae85;
        state[2] = 0x3c6ef372;
        state[3] = 0xa54ff53a;
        state[4] = 0x510e527f;
        state[5] = 0x9b05688c;
        state[6] = 0x1f83d9ab;
        state[7] = 0x5be0cd19;
    }

    public void Update(byte[] data)
    {
        if (data == null) return;
        Update(data, 0, data.Length);
    }

    public void Update(byte[] data, int offset, int len)
    {
        byte[] buffer = new byte[64];
        int blockSize = 0;

        for (int i = offset; i < offset + len; i++)
        {
            buffer[blockSize++] = data[i];
            if (blockSize == 64)
            {
                ProcessBlock(buffer);
                blockSize = 0;
            }
        }

        if (blockSize > 0)
        {
            // Padding sẽ xử lý tại Finalize
            Array.Copy(buffer, 0, buffer, 0, blockSize);
        }
    }

    public byte[] Finalize(byte[]? input = null)
    {
        byte[] buffer = new byte[64];
        int count = 0;
        int totalBytes = 0;

        if (input != null)
        {
            totalBytes = input.Length;
            for (int i = 0; i < input.Length; i++)
            {
                buffer[count++] = input[i];
                if (count == 64)
                {
                    ProcessBlock(buffer);
                    count = 0;
                }
            }
        }

        // Add padding
        buffer[count++] = 0x80;
        while (count < 56)
        {
            buffer[count++] = 0x00;
        }

        // Add length (in bits)
        ulong bitLength = (ulong)totalBytes * 8;
        for (int i = 7; i >= 0; i--)
        {
            buffer[56 + i] = (byte)(bitLength >> (8 * (7 - i)));
        }

        ProcessBlock(buffer);

        byte[] result = new byte[32];
        for (int i = 0; i < 8; i++)
        {
            result[i * 4 + 0] = (byte)(state[i] >> 24);
            result[i * 4 + 1] = (byte)(state[i] >> 16);
            result[i * 4 + 2] = (byte)(state[i] >> 8);
            result[i * 4 + 3] = (byte)(state[i]);
        }

        Reset();
        return result;
    }

    public byte[] ComputeHash(byte[] data)
    {
        Reset();
        return Finalize(data);
    }

    private void ProcessBlock(byte[] block)
    {
        uint[] w = new uint[64];

        for (int i = 0; i < 16; i++)
        {
            w[i] = ((uint)block[i * 4] << 24) |
                   ((uint)block[i * 4 + 1] << 16) |
                   ((uint)block[i * 4 + 2] << 8) |
                   ((uint)block[i * 4 + 3]);
        }

        for (int i = 16; i < 64; i++)
        {
            uint s0 = RightRotate(w[i - 15], 7) ^ RightRotate(w[i - 15], 18) ^ (w[i - 15] >> 3);
            uint s1 = RightRotate(w[i - 2], 17) ^ RightRotate(w[i - 2], 19) ^ (w[i - 2] >> 10);
            w[i] = w[i - 16] + s0 + w[i - 7] + s1;
        }

        uint a = state[0];
        uint b = state[1];
        uint c = state[2];
        uint d = state[3];
        uint e = state[4];
        uint f = state[5];
        uint g = state[6];
        uint h = state[7];

        for (int i = 0; i < 64; i++)
        {
            uint S1 = RightRotate(e, 6) ^ RightRotate(e, 11) ^ RightRotate(e, 25);
            uint ch = (e & f) ^ ((~e) & g);
            uint temp1 = h + S1 + ch + k[i] + w[i];
            uint S0 = RightRotate(a, 2) ^ RightRotate(a, 13) ^ RightRotate(a, 22);
            uint maj = (a & b) ^ (a & c) ^ (b & c);
            uint temp2 = S0 + maj;

            h = g;
            g = f;
            f = e;
            e = d + temp1;
            d = c;
            c = b;
            b = a;
            a = temp1 + temp2;
        }

        state[0] += a;
        state[1] += b;
        state[2] += c;
        state[3] += d;
        state[4] += e;
        state[5] += f;
        state[6] += g;
        state[7] += h;
    }

    private uint RightRotate(uint value, int count)
    {
        return (value >> count) | (value << (32 - count));
    }
}
