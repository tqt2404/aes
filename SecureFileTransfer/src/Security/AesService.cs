using System.Security.Cryptography;
using SecureFileTransfer.Models;

namespace SecureFileTransfer.Security;

public interface IAesCryptography
{
    void EncryptFile(string inputFile, string outputFile, string password);
    void DecryptFile(string inputFile, string outputFile, string password);
}

public class AesService : IAesCryptography
{
    private const int IV_SIZE = 16;
    private const int SALT_SIZE = 16;
    private const int HMAC_SIZE = 32;
    private const int ITERATIONS = 100000;
    private const int BUFFER_SIZE = 81920;

    public void EncryptFile(string inputFile, string outputFile, string password)
    {
        ValidateFilePathsAndPassword(inputFile, outputFile, password);

        byte[] salt = GenerateRandomBytes(SALT_SIZE);
        var (aesKey, hmacKey) = DeriveKeys(password, salt);

        using Aes aes = CreateAesInstance(aesKey);
        using var hmacAlgo = new HMACSHA256(hmacKey);
        using FileStream fsOutput = new FileStream(outputFile, FileMode.Create, FileAccess.Write);
        
        WriteAndHashHeader(fsOutput, salt, aes.IV, hmacAlgo);
        
        using (FileStream fsInput = new FileStream(inputFile, FileMode.Open, FileAccess.Read))
        {
            ProcessEncryption(fsInput, fsOutput, aes, hmacAlgo);
        }

        FinalizeAndWriteHmac(fsOutput, hmacAlgo);
    }

    public void DecryptFile(string inputFile, string outputFile, string password)
    {
        ValidateFilePathsAndPassword(inputFile, outputFile, password);

        using FileStream fsInput = new FileStream(inputFile, FileMode.Open, FileAccess.Read);
        ValidateFileSize(fsInput);

        byte[] hmacReceived = ReadHmacFromFooter(fsInput);
        var (salt, iv) = ReadHeader(fsInput);

        var (aesKey, hmacKey) = DeriveKeys(password, salt);
        long ciphertextLength = fsInput.Length - HMAC_SIZE - (SALT_SIZE + IV_SIZE);

        VerifyHmacIntegrity(fsInput, hmacKey, hmacReceived, ciphertextLength);
        
        using Aes aes = CreateAesInstance(aesKey, iv);
        using FileStream fsOutput = new FileStream(outputFile, FileMode.Create, FileAccess.Write);
        
        ProcessDecryption(fsInput, fsOutput, aes, ciphertextLength);
    }

    // --- Extracted Helper Methods (Clean Code) ---

    private void ValidateFilePathsAndPassword(string inputFile, string outputFile, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        if (!File.Exists(inputFile)) 
            throw new FileNotFoundException("Input file not found.", inputFile);
    }

    private void ValidateFileSize(FileStream fs)
    {
        if (fs.Length < SALT_SIZE + IV_SIZE + HMAC_SIZE) 
            throw new CryptographicException("File bị lỗi hoặc định dạng không hợp lệ.");
    }

    private byte[] GenerateRandomBytes(int size)
    {
        byte[] bytes = new byte[size];
        using (var rng = RandomNumberGenerator.Create()) 
            rng.GetBytes(bytes);
        return bytes;
    }

    private (byte[] AesKey, byte[] HmacKey) DeriveKeys(string password, byte[] salt)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, ITERATIONS, HashAlgorithmName.SHA256);
        return (pbkdf2.GetBytes(32), pbkdf2.GetBytes(32));
    }

    private Aes CreateAesInstance(byte[] key, byte[]? iv = null)
    {
        Aes aes = Aes.Create();
        aes.Key = key;
        if (iv != null) aes.IV = iv;
        else aes.GenerateIV();
        
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        return aes;
    }

    private void WriteAndHashHeader(FileStream fsOutput, byte[] salt, byte[] iv, HMAC hmacAlgo)
    {
        byte[] header = new byte[SALT_SIZE + IV_SIZE];
        Buffer.BlockCopy(salt, 0, header, 0, SALT_SIZE);
        Buffer.BlockCopy(iv, 0, header, SALT_SIZE, IV_SIZE);
        
        fsOutput.Write(header, 0, header.Length);
        hmacAlgo.TransformBlock(header, 0, header.Length, header, 0);
    }

    private (byte[] Salt, byte[] IV) ReadHeader(FileStream fsInput)
    {
        fsInput.Seek(0, SeekOrigin.Begin);
        byte[] salt = new byte[SALT_SIZE];
        byte[] iv = new byte[IV_SIZE];
        fsInput.ReadExactly(salt, 0, SALT_SIZE);
        fsInput.ReadExactly(iv, 0, IV_SIZE);
        return (salt, iv);
    }

    private byte[] ReadHmacFromFooter(FileStream fsInput)
    {
        fsInput.Seek(-HMAC_SIZE, SeekOrigin.End);
        byte[] hmacReceived = new byte[HMAC_SIZE];
        fsInput.ReadExactly(hmacReceived, 0, HMAC_SIZE);
        return hmacReceived;
    }

    private void ProcessEncryption(FileStream fsInput, FileStream fsOutput, Aes aes, HMAC hmacAlgo)
    {
        using ICryptoTransform encryptor = aes.CreateEncryptor();
        using CryptoStream cs = new CryptoStream(fsInput, encryptor, CryptoStreamMode.Read);
        
        byte[] buffer = new byte[BUFFER_SIZE];
        int read;
        while ((read = cs.Read(buffer, 0, buffer.Length)) > 0)
        {
            fsOutput.Write(buffer, 0, read);
            hmacAlgo.TransformBlock(buffer, 0, read, buffer, 0);
        }
    }

    private void ProcessDecryption(FileStream fsInput, FileStream fsOutput, Aes aes, long ciphertextLength)
    {
        fsInput.Seek(SALT_SIZE + IV_SIZE, SeekOrigin.Begin);
        
        using ICryptoTransform decryptor = aes.CreateDecryptor();
        using CryptoStream cs = new CryptoStream(fsOutput, decryptor, CryptoStreamMode.Write);
        
        byte[] cipherBuf = new byte[BUFFER_SIZE];
        long cipherRem = ciphertextLength;
        while (cipherRem > 0)
        {
            int toRead = (int)Math.Min(cipherBuf.Length, cipherRem);
            int read = fsInput.Read(cipherBuf, 0, toRead);
            if (read == 0) break;
            cs.Write(cipherBuf, 0, read);
            cipherRem -= read;
        }
        cs.FlushFinalBlock();
    }

    private void FinalizeAndWriteHmac(FileStream fsOutput, HMAC hmacAlgo)
    {
        hmacAlgo.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        fsOutput.Write(hmacAlgo.Hash!, 0, HMAC_SIZE);
    }

    private void VerifyHmacIntegrity(FileStream fsInput, byte[] hmacKey, byte[] hmacReceived, long ciphertextLength)
    {
        fsInput.Seek(0, SeekOrigin.Begin);
        using var hmacAlgo = new HMACSHA256(hmacKey);
        
        byte[] buffer = new byte[BUFFER_SIZE];
        long remaining = SALT_SIZE + IV_SIZE + ciphertextLength;
        while (remaining > 0)
        {
            int toRead = (int)Math.Min(buffer.Length, remaining);
            int read = fsInput.Read(buffer, 0, toRead);
            if (read == 0) break;
            hmacAlgo.TransformBlock(buffer, 0, read, buffer, 0);
            remaining -= read;
        }
        hmacAlgo.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        
        if (!CryptographicOperations.FixedTimeEquals(hmacAlgo.Hash!, hmacReceived))
        {
            throw new CryptographicException("LỖI BẢO MẬT: Mật mã (Key) không đúng hoặc tệp đã bị thay đổi (Tamper Detected).");
        }
    }
}
