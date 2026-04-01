using System.Security.Cryptography;
using SecureFileTransfer.Models;

namespace SecureFileTransfer.Security;

// Disable usage of .NET built-in Aes - using custom implementation only
#pragma warning disable SYSLIB0011

public interface IAesCryptography
{
    void EncryptFile(string inputFile, string outputFile, string password);
    void DecryptFile(string inputFile, string outputFile, string password);

    Task EncryptStreamAsync(Stream input, Stream output, string password, CancellationToken ct = default);
    Task DecryptStreamAsync(Stream input, Stream output, string password, CancellationToken ct = default);
}

public class AesService : IAesCryptography
{
    private const int IV_SIZE = 16;
    private const int SALT_SIZE = 16;
    private const int HMAC_SIZE = 32;
    private const int ITERATIONS = 600000;
    private const int BUFFER_SIZE = 65536;

    public async Task EncryptStreamAsync(Stream input, Stream output, string password, CancellationToken ct = default)
    {
        // 1. Generate salt (random)
        var customRandom = new CustomRandom();
        byte[] salt = customRandom.GetBytes(SALT_SIZE);
        byte[] iv = customRandom.GetBytes(IV_SIZE);
        
        // 2. Derive encryption keys từ password (custom PBKDF2)
        var (aesKey, hmacKey) = DeriveKeys(password, salt);

        // 3. Tạo custom AES và CBC mode
        var customAes = new CustomAes256(aesKey);
        var cbcMode = new CustomCbcMode(customAes, iv);

        // 4. Write header (Salt + IV)
        byte[] header = new byte[SALT_SIZE + IV_SIZE];
        Buffer.BlockCopy(salt, 0, header, 0, SALT_SIZE);
        Buffer.BlockCopy(iv, 0, header, SALT_SIZE, IV_SIZE);
        
        await output.WriteAsync(header, 0, header.Length, ct);

        // 5. HMAC để verify integrity (custom HMAC-SHA256)
        var hmacAlgo = new CustomHmacSha256(hmacKey);
        byte[] headerHmac = hmacAlgo.ComputeHash(header);

        // 6. Encrypt toàn bộ file bằng CBC mode
        byte[] buffer = new byte[BUFFER_SIZE];
        int read;
        
        // Accumulate plaintext blocks
        List<byte> plaintextBlocks = new();

        while ((read = await input.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
        {
            plaintextBlocks.AddRange(buffer.Take(read));
        }

        if (plaintextBlocks.Count > 0)
        {
            // Encrypt tất cả dữ liệu cùng lúc với CBC mode
            byte[] allPlaintext = plaintextBlocks.ToArray();
            byte[] allCiphertext = cbcMode.Encrypt(allPlaintext);
            
            // Write ciphertext
            await output.WriteAsync(allCiphertext, 0, allCiphertext.Length, ct);

            // Compute HMAC over header + ciphertext
            byte[] fullData = new byte[header.Length + allCiphertext.Length];
            Array.Copy(header, fullData, header.Length);
            Array.Copy(allCiphertext, 0, fullData, header.Length, allCiphertext.Length);
            
            byte[] finalHmac = hmacAlgo.ComputeHash(fullData);
            await output.WriteAsync(finalHmac, 0, HMAC_SIZE, ct);
        }
    }

    public async Task DecryptStreamAsync(Stream input, Stream output, string password, CancellationToken ct = default)
    {
        if (!input.CanSeek) 
            throw new NotSupportedException("DecryptStreamAsync cần seekable input để verify HMAC");

        long totalLength = input.Length;
        if (totalLength < SALT_SIZE + IV_SIZE + HMAC_SIZE) 
            throw new CryptographicException("File format không hợp lệ");

        // 1. Đọc HMAC từ cuối file
        input.Seek(-HMAC_SIZE, SeekOrigin.End);
        byte[] hmacReceived = new byte[HMAC_SIZE];
        await input.ReadExactlyAsync(hmacReceived, 0, HMAC_SIZE, ct);

        // 2. Đọc header (salt + IV)
        input.Seek(0, SeekOrigin.Begin);
        byte[] header = new byte[SALT_SIZE + IV_SIZE];
        await input.ReadExactlyAsync(header, 0, header.Length, ct);
        
        byte[] salt = new byte[SALT_SIZE];
        byte[] iv = new byte[IV_SIZE];
        Buffer.BlockCopy(header, 0, salt, 0, SALT_SIZE);
        Buffer.BlockCopy(header, SALT_SIZE, iv, 0, IV_SIZE);

        // 3. Derive keys (custom PBKDF2)
        var (aesKey, hmacKey) = DeriveKeys(password, salt);

        // 4. Verify HMAC (custom HMAC-SHA256)
        input.Seek(0, SeekOrigin.Begin);
        long encryptedDataLen = totalLength - HMAC_SIZE;
        byte[] allData = new byte[encryptedDataLen];
        await input.ReadExactlyAsync(allData, 0, (int)encryptedDataLen, ct);
        
        var hmacAlgo = new CustomHmacSha256(hmacKey);
        byte[] hmacComputed = hmacAlgo.ComputeHash(allData);
        
        if (!ConstantTimeEquals(hmacComputed, hmacReceived))
            throw new CryptographicException("Lỗi bảo mật: Khóa không chính xác hoặc dữ liệu bị thay đổi");

        // 5. Extract ciphertext (skip header)
        byte[] ciphertext = new byte[encryptedDataLen - (SALT_SIZE + IV_SIZE)];
        Array.Copy(allData, SALT_SIZE + IV_SIZE, ciphertext, 0, ciphertext.Length);

        // 6. Decrypt sử dụng custom AES + CBC mode
        var customAes = new CustomAes256(aesKey);
        var cbcMode = new CustomCbcMode(customAes, iv);
        byte[] plaintext = cbcMode.Decrypt(ciphertext);

        // 7. Write decrypted data
        await output.WriteAsync(plaintext, 0, plaintext.Length, ct);
    }

    private bool ConstantTimeEquals(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        int result = 0;
        for (int i = 0; i < a.Length; i++)
            result |= a[i] ^ b[i];
        return result == 0;
    }

    public void EncryptFile(string inputFile, string outputFile, string password)
    {
        using var fsIn = new FileStream(inputFile, FileMode.Open, FileAccess.Read);
        using var fsOut = new FileStream(outputFile, FileMode.Create, FileAccess.Write);
        EncryptStreamAsync(fsIn, fsOut, password).GetAwaiter().GetResult();
    }

    public void DecryptFile(string inputFile, string outputFile, string password)
    {
        using var fsIn = new FileStream(inputFile, FileMode.Open, FileAccess.Read);
        using var fsOut = new FileStream(outputFile, FileMode.Create, FileAccess.Write);
        DecryptStreamAsync(fsIn, fsOut, password).GetAwaiter().GetResult();
    }

    private (byte[] AesKey, byte[] HmacKey) DeriveKeys(string password, byte[] salt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        ArgumentNullException.ThrowIfNull(salt);
        if (salt.Length != SALT_SIZE) 
            throw new ArgumentException($"Salt phải là {SALT_SIZE} bytes", nameof(salt));

        // Sử dụng custom PBKDF2
        byte[] derivedBytes = CustomPbkdf2.DeriveKey(password, salt, ITERATIONS, 64);
        
        byte[] aesKey = new byte[32];
        byte[] hmacKey = new byte[32];
        Buffer.BlockCopy(derivedBytes, 0, aesKey, 0, 32);
        Buffer.BlockCopy(derivedBytes, 32, hmacKey, 0, 32);
        
        return (aesKey, hmacKey);
    }
}
