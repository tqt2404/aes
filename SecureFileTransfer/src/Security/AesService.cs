using System.Security.Cryptography;
using SecureFileTransfer.Models;

namespace SecureFileTransfer.Security;

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
        byte[] salt = RandomNumberGenerator.GetBytes(SALT_SIZE);
        var (aesKey, hmacKey) = DeriveKeys(password, salt);

        using Aes aes = CreateAesInstance(aesKey);
        using var hmacAlgo = new HMACSHA256(hmacKey);

        // 1. Write Header (Salt + IV)
        byte[] header = new byte[SALT_SIZE + IV_SIZE];
        Buffer.BlockCopy(salt, 0, header, 0, SALT_SIZE);
        Buffer.BlockCopy(aes.IV, 0, header, SALT_SIZE, IV_SIZE);
        
        await output.WriteAsync(header, 0, header.Length, ct);
        hmacAlgo.TransformBlock(header, 0, header.Length, null, 0);

        // 2. Encryption with CryptoStream and HMAC update
        using (ICryptoTransform encryptor = aes.CreateEncryptor())
        using (CryptoStream cs = new CryptoStream(input, encryptor, CryptoStreamMode.Read, true))
        {
            byte[] buffer = new byte[BUFFER_SIZE];
            int read;
            while ((read = await cs.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
            {
                await output.WriteAsync(buffer, 0, read, ct);
                hmacAlgo.TransformBlock(buffer, 0, read, null, 0);
            }
        }

        // 3. Finalize and Write HMAC
        hmacAlgo.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        await output.WriteAsync(hmacAlgo.Hash!, 0, HMAC_SIZE, ct);
    }

    public async Task DecryptStreamAsync(Stream input, Stream output, string password, CancellationToken ct = default)
    {
        if (!input.CanSeek) throw new NotSupportedException("DecryptStreamAsync requires seekable input for MAC verification.");

        long totalLength = input.Length;
        if (totalLength < SALT_SIZE + IV_SIZE + HMAC_SIZE) 
            throw new CryptographicException("File format invalid.");

        // 1. Verify HMAC
        input.Seek(-HMAC_SIZE, SeekOrigin.End);
        byte[] hmacReceived = new byte[HMAC_SIZE];
        await input.ReadExactlyAsync(hmacReceived, 0, HMAC_SIZE, ct);

        input.Seek(0, SeekOrigin.Begin);
        byte[] header = new byte[SALT_SIZE + IV_SIZE];
        await input.ReadExactlyAsync(header, 0, header.Length, ct);
        byte[] salt = new byte[SALT_SIZE];
        byte[] iv = new byte[IV_SIZE];
        Buffer.BlockCopy(header, 0, salt, 0, SALT_SIZE);
        Buffer.BlockCopy(header, SALT_SIZE, iv, 0, IV_SIZE);

        var (aesKey, hmacKey) = DeriveKeys(password, salt);
        
        input.Seek(0, SeekOrigin.Begin);
        using (var hmacAlgo = new HMACSHA256(hmacKey))
        {
            byte[] verifBuf = new byte[BUFFER_SIZE];
            long toVerify = totalLength - HMAC_SIZE;
            while (toVerify > 0)
            {
                int read = await input.ReadAsync(verifBuf, 0, (int)Math.Min(verifBuf.Length, toVerify), ct);
                if (read == 0) break;
                hmacAlgo.TransformBlock(verifBuf, 0, read, null, 0);
                toVerify -= read;
            }
            hmacAlgo.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            if (!CryptographicOperations.FixedTimeEquals(hmacAlgo.Hash!, hmacReceived))
                throw new CryptographicException("Lỗi bảo mật: Khóa không chính xác hoặc dữ liệu đã bị thay đổi.");
        }

        // Thực hiện giải mã
        input.Seek(SALT_SIZE + IV_SIZE, SeekOrigin.Begin);
        using Aes aes = CreateAesInstance(aesKey, iv);
        using ICryptoTransform decryptor = aes.CreateDecryptor();
        
        using (var limitedStream = new LimitedStream(input, totalLength - HMAC_SIZE - (SALT_SIZE + IV_SIZE)))
        using (CryptoStream cs = new CryptoStream(limitedStream, decryptor, CryptoStreamMode.Read, true))
        {
            await cs.CopyToAsync(output, ct);
        }
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
        if (salt.Length != SALT_SIZE) throw new ArgumentException($"Salt size must be {SALT_SIZE} bytes.", nameof(salt));

        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, ITERATIONS, HashAlgorithmName.SHA256);
        return (pbkdf2.GetBytes(32), pbkdf2.GetBytes(32));
    }

    private Aes CreateAesInstance(byte[] key, byte[]? iv = null)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Length != 32) throw new ArgumentException("Key size must be 256-bit (32 bytes).", nameof(key));

        Aes aes = Aes.Create();
        aes.KeySize = 256;
        aes.Key = key;
        
        if (iv != null) 
        {
            if (iv.Length != IV_SIZE) throw new ArgumentException($"IV size must be {IV_SIZE} bytes.", nameof(iv));
            aes.IV = iv; 
        } 
        else 
        {
            aes.GenerateIV();
        }

        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        return aes;
    }
}

// Helper class to limit stream reading for CryptoStream
internal class LimitedStream : Stream
{
    private readonly Stream _inner;
    private long _left;
    public LimitedStream(Stream inner, long length) { _inner = inner; _left = length; }
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => _left;
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_left <= 0) return 0;
        int read = _inner.Read(buffer, offset, (int)Math.Min(count, _left));
        _left -= read;
        return read;
    }
    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
    {
        if (_left <= 0) return 0;
        int read = await _inner.ReadAsync(buffer, offset, (int)Math.Min(count, _left), ct);
        _left -= read;
        return read;
    }
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        if (_left <= 0) return 0;
        int read = await _inner.ReadAsync(buffer.Slice(0, (int)Math.Min(buffer.Length, _left)), ct);
        _left -= read;
        return read;
    }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
