using SecureFileTransfer.Network;
using SecureFileTransfer.Security;
using SecureFileTransfer.Utils;
using SecureFileTransfer.Models;

namespace SecureFileTransfer.Services;

public class FileTransferManager
{
    private readonly IAesCryptography _crypto;
    private readonly ITcpClient _client;
    private readonly ITcpServer _server;
    private readonly DatabaseService _db;

    public FileTransferManager(IAesCryptography crypto, ITcpClient client, ITcpServer server, DatabaseService db)
    {
        _crypto = crypto;
        _client = client;
        _server = server;
        _db = db;
    }

    public async Task EncryptAndSendAsync(string filePath, string ip, int port, string password, AesKeySize keySize = AesKeySize.AES256, IProgress<TransferProgress>? progress = null, CancellationToken ct = default)
    {
        ValidateFile(filePath);

        string fileName = Path.GetFileName(filePath);
        long originalFileSize = new FileInfo(filePath).Length;
        string hash = await HashHelper.ComputeSha256Async(filePath, ct);

        // Kích thước tệp mã hóa: MetadataLength(4) + Metadata(JSON) + Salt(16) + IV(16) + Ciphertext(Padded) + HMAC(32)
        long encryptedSize = 4 + 100 + 16 + 16 + ((originalFileSize / 16) + 1) * 16 + 32;
        var metadata = new FileMetadata 
        { 
            FileName = fileName, 
            FileSize = encryptedSize, 
            Sha256Hash = hash, 
            EncryptionType = keySize,
            KeySize = (int)keySize  // Store the key size for receiver to auto-detect
        };

        Logger.Log($"Mã hóa và truyền tải: {fileName} (AES-{keySize}, KeySize={metadata.KeySize} bits)");

        await _client.SendActionAsync(ip, port, metadata, async ns =>
        {
            using var fsIn = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            await _crypto.EncryptStreamAsync(fsIn, ns, password, keySize, ct);
        }, ct);

        _ = _db.LogTransferAsync(fileName, originalFileSize, "Local", ip, "Sent");
        Logger.Log("Truyền tệp hoàn tất.");
    }

    public async Task<string> ReceiveAndDecryptAsync(int port, string saveFolder, string password, CancellationToken ct, IProgress<TransferProgress>? progress = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(saveFolder);

        // Path Traversal Protection
        // Sanitize save folder path internally when metadata is received in DecryptAndVerifyAsync
        
        // Ensure Directory Exists and handle UnauthorizedAccessException
        try 
        {
            if (!Directory.Exists(saveFolder)) 
                Directory.CreateDirectory(saveFolder);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new UnauthorizedAccessException($"[Security Error] Access denied creating folder: {ex.Message}", ex);
        }
        
        string tempEnc = Path.Combine(saveFolder, $"incoming_{Guid.NewGuid():N}.tmp");
        try 
        {
            var metadata = await _server.StartListeningAsync(port, tempEnc, ct, progress);
            return await DecryptAndVerifyAsync(metadata, tempEnc, saveFolder, password, ct);
        }
        finally 
        {
            if (File.Exists(tempEnc)) File.Delete(tempEnc);
        }
    }

    private async Task<string> DecryptAndVerifyAsync(FileMetadata metadata, string sourcePath, string saveFolder, string password, CancellationToken ct)
    {
        // Path Traversal Protection
        string sanitizedFileName = Path.GetFileName(metadata.FileName);
        string finalPath = Path.Combine(saveFolder, sanitizedFileName);

        if (!Path.GetFullPath(finalPath).StartsWith(Path.GetFullPath(saveFolder)))
        {
            throw new UnauthorizedAccessException("Path traversal attack detected!");
        }

        Logger.Log($"Giải mã tệp tin: {metadata.FileName} -> {sanitizedFileName}");
        
        using (var fsIn = new FileStream(sourcePath, FileMode.Open, FileAccess.Read))
        using (var fsOut = new FileStream(finalPath, FileMode.Create, FileAccess.Write))
        {
            await _crypto.DecryptStreamAsync(fsIn, fsOut, password, ct);
        }

        await VerifyIntegrityAsync(finalPath, metadata.Sha256Hash, ct);
        
        _ = _db.LogTransferAsync(metadata.FileName, metadata.FileSize, "Remote", "Local", "Received");
        return Path.GetFullPath(finalPath);
    }

    public async Task LocalEncryptAsync(string inputPath, string outputPath, string password, AesKeySize keySize = AesKeySize.AES256)
    {
        ValidateFile(inputPath);
        using var fsIn = new FileStream(inputPath, FileMode.Open, FileAccess.Read);
        using var fsOut = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        await _crypto.EncryptStreamAsync(fsIn, fsOut, password, keySize);
    }

    public async Task LocalDecryptAsync(string inputPath, string outputPath, string password)
    {
        ValidateFile(inputPath);
        using var fsIn = new FileStream(inputPath, FileMode.Open, FileAccess.Read);
        using var fsOut = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        await _crypto.DecryptStreamAsync(fsIn, fsOut, password);
    }

    public async Task<string> ReceiveOnlyAsync(int port, string saveFolder, CancellationToken ct, IProgress<TransferProgress>? progress = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(saveFolder);
        
        try 
        {
            if (!Directory.Exists(saveFolder)) 
                Directory.CreateDirectory(saveFolder);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new UnauthorizedAccessException($"[Security Error] Access denied creating folder: {ex.Message}", ex);
        }

        string temp = Path.Combine(saveFolder, $"receiving_{Guid.NewGuid():N}.tmp");
        
        var metadata = await _server.StartListeningAsync(port, temp, ct, progress);
        
        // Path Traversal Protection
        string sanitizedFileName = Path.GetFileName(metadata.FileName);
        string fileName = sanitizedFileName.EndsWith(".enc", StringComparison.OrdinalIgnoreCase) 
                          ? sanitizedFileName : sanitizedFileName + ".enc";
        
        string finalPath = Path.Combine(saveFolder, fileName);

        if (!Path.GetFullPath(finalPath).StartsWith(Path.GetFullPath(saveFolder)))
        {
            throw new UnauthorizedAccessException("Path traversal attack detected!");
        }

        if (File.Exists(finalPath)) File.Delete(finalPath);
        File.Move(temp, finalPath);

        _ = _db.LogTransferAsync(metadata.FileName, metadata.FileSize, "Remote", "Local", "Received (Encrypted)");
        return Path.GetFullPath(finalPath);
    }

    private void ValidateFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path)) throw new FileNotFoundException("Tệp tin không tồn tại.", path);
    }

    private async Task VerifyIntegrityAsync(string path, string expectedHash, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(expectedHash)) return;
        
        string actualHash = await HashHelper.ComputeSha256Async(path, ct);
        if (!actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception("Lỗi: Tính toàn vẹn của tệp không khớp.");
        }
        Logger.Log("Kiểm tra tính toàn vẹn thành công.");
    }
}
