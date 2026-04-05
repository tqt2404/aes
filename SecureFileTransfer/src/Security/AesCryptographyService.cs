using System.Security.Cryptography;
using System.Text;
using SecureFileTransfer.Models;

namespace SecureFileTransfer.Security;

/// <summary>
/// Interface for AES-based file encryption and decryption services.
/// </summary>
public interface IAesCryptography
{
    void EncryptFile(string inputFile, string outputFile, string password, AesKeySize keySize = AesKeySize.AES256);
    void DecryptFile(string inputFile, string outputFile, string password);

    Task EncryptStreamAsync(Stream input, Stream output, string password, string fileName = "data", AesKeySize keySize = AesKeySize.AES256, CancellationToken ct = default);
    Task DecryptStreamAsync(Stream input, Stream output, string password, CancellationToken ct = default);
}

/// <summary>
/// AES cryptography service for secure file encryption and decryption.
/// 
/// Uses fully custom AES core implementation (Aes256Impl - from scratch) combined with:
/// - CBC mode for chaining (CbcModeOperations)
/// - PKCS7 padding for block alignment
/// - PBKDF2 key derivation (600,000 iterations)
/// - HMAC-SHA256 for integrity verification
/// 
/// File format: [MetadataLength(4)] [Metadata(JSON)] [Salt(16)] [IV(16)] [EncryptedData] [HMAC(32)]
/// </summary>
public class AesCryptographyService : IAesCryptography
{
    private const int IV_SIZE = 16;
    private const int SALT_SIZE = 16;
    private const int HMAC_SIZE = 32;
    private const int ITERATIONS = 600000;  // PBKDF2 iterations (OWASP 2023 recommendation)
    private const int BUFFER_SIZE = 65536;  // 64 KB streaming buffer
    private const int METADATA_LENGTH_SIZE = 4;  // 4-byte length prefix

    /// <summary>
    /// Encrypt stream with specified AES key size.
    /// Handles large files efficiently using streaming.
    /// </summary>
    public async Task EncryptStreamAsync(Stream input, Stream output, string password, string fileName = "data", AesKeySize keySize = AesKeySize.AES256, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);

        // 1. Create metadata - lưu tên file gốc để restore sau khi giải mã
        var metadata = new FileMetadata
        {
            FileName = Path.GetFileName(fileName),
            FileSize = input.Length > 0 ? input.Length : 0,
            Sha256Hash = "",  // Will compute after encryption
            EncryptionType = keySize
        };

        byte[] metadataJson = metadata.Serialize();
        if (metadataJson.Length > int.MaxValue)
            throw new InvalidOperationException("Metadata too large");

        // 2. Generate cryptographic random values
        byte[] salt = CryptographyProvider.GetRandomBytes(SALT_SIZE);
        byte[] iv = CryptographyProvider.GetRandomBytes(IV_SIZE);

        // 3. Derive keys based on key size
        int keyLength = AesCipherFactory.GetKeyLength(keySize);
        byte[] derivedBytes = CryptographyProvider.DeriveKeyFromPassword(password, salt, ITERATIONS, keyLength + 32);
        byte[] aesKey = new byte[keyLength];
        byte[] hmacKey = new byte[32];
        Array.Copy(derivedBytes, 0, aesKey, 0, keyLength);
        Array.Copy(derivedBytes, keyLength, hmacKey, 0, 32);

        // 4. Create AES and CBC mode
        var aes = AesCipherFactory.CreateAes(aesKey, keySize);
        var cbcMode = new CbcModeOperations(aes, iv);

        // 5. Write metadata header + start incremental HMAC computation
        byte[] metadataLengthBytes = BitConverter.GetBytes(metadataJson.Length);
        if (BitConverter.IsLittleEndian) Array.Reverse(metadataLengthBytes);

        // Initialize HMAC to cover ALL bytes written to the output (header + ciphertext)
        using var hmac = new System.Security.Cryptography.HMACSHA256(hmacKey);
        hmac.TransformBlock(metadataLengthBytes, 0, METADATA_LENGTH_SIZE, null, 0);
        hmac.TransformBlock(metadataJson, 0, metadataJson.Length, null, 0);
        hmac.TransformBlock(salt, 0, SALT_SIZE, null, 0);
        hmac.TransformBlock(iv, 0, IV_SIZE, null, 0);

        await output.WriteAsync(metadataLengthBytes, 0, METADATA_LENGTH_SIZE, ct);
        await output.WriteAsync(metadataJson, 0, metadataJson.Length, ct);
        await output.WriteAsync(salt, 0, SALT_SIZE, ct);
        await output.WriteAsync(iv, 0, IV_SIZE, ct);

        // 6. Encrypt data in chunks, feeding each encrypted chunk into HMAC
        byte[] buffer = new byte[BUFFER_SIZE];
        int read;
        List<byte> allData = new();

        while ((read = await input.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
        {
            allData.AddRange(buffer.Take(read));

            int fullBlocks = (allData.Count / 16) * 16;
            if (fullBlocks > 0)
            {
                byte[] chunk = allData.Take(fullBlocks).ToArray();
                byte[] encrypted = cbcMode.EncryptRaw(chunk);
                // Hash the ciphertext chunk
                hmac.TransformBlock(encrypted, 0, encrypted.Length, null, 0);
                await output.WriteAsync(encrypted, 0, encrypted.Length, ct);
                allData.RemoveRange(0, fullBlocks);
            }
        }

        // 7. Final block with PKCS7 padding
        {
            byte[] finalChunk = allData.Count > 0 ? allData.ToArray() : Array.Empty<byte>();
            byte[] paddedFinal = AddPkcs7Padding(finalChunk);
            byte[] encrypted = cbcMode.EncryptRaw(paddedFinal);
            hmac.TransformBlock(encrypted, 0, encrypted.Length, null, 0);
            await output.WriteAsync(encrypted, 0, encrypted.Length, ct);
        }

        // 8. Finalize HMAC and write at the end
        hmac.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        byte[] finalHmac = hmac.Hash!;
        await output.WriteAsync(finalHmac, 0, HMAC_SIZE, ct);
    }

    /// <summary>
    /// Decrypt stream (automatically detects AES key size from metadata).
    /// Verifies integrity using HMAC-SHA256.
    /// </summary>
    public async Task DecryptStreamAsync(Stream input, Stream output, string password, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);

        if (!input.CanSeek)
            throw new NotSupportedException("DecryptStreamAsync requires seekable stream");

        long totalLength = input.Length;
        if (totalLength < METADATA_LENGTH_SIZE + SALT_SIZE + IV_SIZE + HMAC_SIZE)
            throw new CryptographicException("Invalid file format: file too small");

        // 1. Read metadata length
        byte[] metadataLengthBytes = new byte[METADATA_LENGTH_SIZE];
        await input.ReadExactlyAsync(metadataLengthBytes, 0, METADATA_LENGTH_SIZE, ct);
        if (BitConverter.IsLittleEndian) Array.Reverse(metadataLengthBytes);
        int metadataLength = BitConverter.ToInt32(metadataLengthBytes, 0);

        // 2. Read and deserialize metadata
        byte[] metadataJson = new byte[metadataLength];
        await input.ReadExactlyAsync(metadataJson, 0, metadataLength, ct);
        var metadata = FileMetadata.Deserialize(metadataJson);
        if (metadata == null)
            throw new CryptographicException("Invalid metadata");

        // 3. Read salt and IV
        byte[] salt = new byte[SALT_SIZE];
        byte[] iv = new byte[IV_SIZE];
        await input.ReadExactlyAsync(salt, 0, SALT_SIZE, ct);
        await input.ReadExactlyAsync(iv, 0, IV_SIZE, ct);

        // 4. Read HMAC from end
        input.Seek(-HMAC_SIZE, SeekOrigin.End);
        byte[] hmacReceived = new byte[HMAC_SIZE];
        await input.ReadExactlyAsync(hmacReceived, 0, HMAC_SIZE, ct);

        // 5. Derive keys using detected key size
        int keyLength = AesCipherFactory.GetKeyLength(metadata.EncryptionType);
        byte[] derivedBytes = CryptographyProvider.DeriveKeyFromPassword(password, salt, ITERATIONS, keyLength + 32);
        byte[] aesKey = new byte[keyLength];
        byte[] hmacKey = new byte[32];
        Array.Copy(derivedBytes, 0, aesKey, 0, keyLength);
        Array.Copy(derivedBytes, keyLength, hmacKey, 0, 32);

        // 6. Verify HMAC - phải tính trên Header + toàn bộ Ciphertext (1-pass)
        long encryptedDataStart = METADATA_LENGTH_SIZE + metadataLength + SALT_SIZE + IV_SIZE;
        long encryptedDataLength = totalLength - HMAC_SIZE - encryptedDataStart;

        // Giữ lại original metadataLengthBytes để đưa vào HMAC
        byte[] metadataLengthBytesForHmac = new byte[METADATA_LENGTH_SIZE];
        input.Seek(0, SeekOrigin.Begin);
        await input.ReadExactlyAsync(metadataLengthBytesForHmac, 0, METADATA_LENGTH_SIZE, ct);

        using var hmacVerify = new System.Security.Cryptography.HMACSHA256(hmacKey);
        hmacVerify.TransformBlock(metadataLengthBytesForHmac, 0, METADATA_LENGTH_SIZE, null, 0);
        hmacVerify.TransformBlock(metadataJson, 0, metadataLength, null, 0);
        hmacVerify.TransformBlock(salt, 0, SALT_SIZE, null, 0);
        hmacVerify.TransformBlock(iv, 0, IV_SIZE, null, 0);

        // Seek tới đầu ciphertext rồi mới đọc để hash
        input.Seek(encryptedDataStart, SeekOrigin.Begin);
        byte[] hmacBuf = new byte[BUFFER_SIZE];
        long remaining = encryptedDataLength;
        while (remaining > 0)
        {
            int toRead = (int)Math.Min(BUFFER_SIZE, remaining);
            await input.ReadExactlyAsync(hmacBuf, 0, toRead, ct);
            hmacVerify.TransformBlock(hmacBuf, 0, toRead, null, 0);
            remaining -= toRead;
        }
        hmacVerify.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        byte[] hmacComputed = hmacVerify.Hash!;

        if (!ConstantTimeEquals(hmacComputed, hmacReceived))
            throw new CryptographicException("Lỗi phân giải HMAC: Mật khẩu không đúng hoặc tệp dữ liệu đã bị biến đổi/sứt mẻ trong quá trình truyền.");

        // 7. Seek back to start of ciphertext and decrypt
        input.Seek(encryptedDataStart, SeekOrigin.Begin);
        var aes = AesCipherFactory.CreateAes(aesKey, metadata.EncryptionType);
        var cbcMode = new CbcModeOperations(aes, iv);

        long processed = 0;
        byte[] buffer = new byte[BUFFER_SIZE];

        while (processed < encryptedDataLength)
        {
            int toRead = (int)Math.Min(BUFFER_SIZE, encryptedDataLength - processed);
            await input.ReadExactlyAsync(buffer, 0, toRead, ct);

            byte[] encryptedBlock = new byte[toRead];
            Array.Copy(buffer, 0, encryptedBlock, 0, toRead);

            byte[] decrypted = cbcMode.DecryptRaw(encryptedBlock);

            if (processed + toRead == encryptedDataLength)
                decrypted = RemovePkcs7Padding(decrypted);

            await output.WriteAsync(decrypted, 0, decrypted.Length, ct);
            processed += toRead;
        }
    }

    /// <summary>
    /// Encrypt file synchronously. Use for smaller files or when async is not available.
    /// </summary>
    public void EncryptFile(string inputFile, string outputFile, string password, AesKeySize keySize = AesKeySize.AES256)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        using var fsIn = new FileStream(inputFile, FileMode.Open, FileAccess.Read);
        using var fsOut = new FileStream(outputFile, FileMode.Create, FileAccess.Write);
        EncryptStreamAsync(fsIn, fsOut, password, fileName: Path.GetFileName(inputFile), keySize: keySize).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Decrypt file synchronously. Use for smaller files or when async is not available.
    /// </summary>
    public void DecryptFile(string inputFile, string outputFile, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        using var fsIn = new FileStream(inputFile, FileMode.Open, FileAccess.Read);
        using var fsOut = new FileStream(outputFile, FileMode.Create, FileAccess.Write);
        DecryptStreamAsync(fsIn, fsOut, password).GetAwaiter().GetResult();
    }

    private static byte[] AddPkcs7Padding(byte[] data)
    {
        int paddingLen = 16 - (data.Length % 16);
        byte[] padded = new byte[data.Length + paddingLen];
        Array.Copy(data, padded, data.Length);
        for (int i = 0; i < paddingLen; i++)
            padded[data.Length + i] = (byte)paddingLen;
        return padded;
    }

    private static byte[] RemovePkcs7Padding(byte[] data)
    {
        if (data.Length == 0)
            throw new CryptographicException("Data cannot be empty");

        int paddingLen = data[data.Length - 1];
        if (paddingLen <= 0 || paddingLen > 16)
            throw new CryptographicException("Invalid padding");

        for (int i = 0; i < paddingLen; i++)
        {
            if (data[data.Length - 1 - i] != paddingLen)
                throw new CryptographicException("Invalid padding");
        }

        byte[] unpadded = new byte[data.Length - paddingLen];
        Array.Copy(data, unpadded, unpadded.Length);
        return unpadded;
    }

    private bool ConstantTimeEquals(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        int result = 0;
        for (int i = 0; i < a.Length; i++)
            result |= a[i] ^ b[i];
        return result == 0;
    }
}
