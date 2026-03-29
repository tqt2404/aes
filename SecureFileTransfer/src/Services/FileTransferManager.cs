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

    public async Task EncryptAndSendAsync(string filePath, string ip, int port, string password, IProgress<TransferProgress>? progress = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(ip);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        if (!File.Exists(filePath)) throw new FileNotFoundException("Tệp gốc không tồn tại.", filePath);

        string encryptedPath = filePath + ".enc";
        string originalFileName = Path.GetFileName(filePath);

        // Calculate SHA-256 hash of original file BEFORE encryption for integrity verification
        string originalSha256 = HashHelper.ComputeSha256(filePath);
        Logger.Log($"📊 SHA-256 hash: {originalSha256.Substring(0, 16)}...");

        Logger.Log("🔒 Đang mã hóa bảo mật (PBKDF2 + AES-256)...");
        await Task.Run(() => _crypto.EncryptFile(filePath, encryptedPath, password));

        Logger.Log("📡 Đang truyền tệp qua mạng...");
        await _client.SendFileAsync(ip, port, encryptedPath, originalFileName, originalSha256, progress);

        _ = _db.LogTransferAsync(Path.GetFileName(filePath), new FileInfo(filePath).Length, "Local", ip, "Sent");

        if (File.Exists(encryptedPath)) File.Delete(encryptedPath);
        Logger.Log("✅ Truyền tệp thành công!");
    }

    public async Task<string> ReceiveAndDecryptAsync(int port, string saveFolder, string password, CancellationToken ct, IProgress<TransferProgress>? progress = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(saveFolder);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        
        string tempEncrypted = Path.Combine(saveFolder, "incoming_transfer.tmp");
        var metadata = await _server.StartListeningAsync(port, tempEncrypted, ct, progress);

        Logger.Log($"🔓 Đang giải mã '{metadata.FileName}'...");
        string finalPath = Path.Combine(saveFolder, metadata.FileName);
        
        await Task.Run(() => _crypto.DecryptFile(tempEncrypted, finalPath, password));
        
        // ✅ NEW: Verify file integrity using SHA-256
        if (!string.IsNullOrEmpty(metadata.Sha256Hash))
        {
            string decryptedSha256 = HashHelper.ComputeSha256(finalPath);
            if (decryptedSha256.Equals(metadata.Sha256Hash, StringComparison.OrdinalIgnoreCase))
            {
                Logger.Log("✅ Kiểm tra tính toàn vẹn: THÀNH CÔNG (SHA-256 khớp)");
            }
            else
            {
                Logger.Log($"❌ LỖI NGHIÊM TRỌNG: File bị hỏng hoặc bị giả mạo!");
                Logger.Log($"   Kỳ vọng: {metadata.Sha256Hash.Substring(0, 32)}...");
                Logger.Log($"   Thực tế:  {decryptedSha256.Substring(0, 32)}...");
                throw new Exception("TAMPER_DETECTED: File integrity check failed!");
            }
        }
        
        // Fire-and-forget DB logging so it doesn't block the UI
        _ = _db.LogTransferAsync(metadata.FileName, metadata.FileSize, "Remote", "Local", "Received");

        if (File.Exists(tempEncrypted)) File.Delete(tempEncrypted);
        Logger.Log("✅ Hoàn tất! Tệp đã giải mã tại: " + Path.GetFullPath(finalPath));
        return Path.GetFullPath(finalPath);
    }

    public async Task<string> ReceiveOnlyAsync(int port, string saveFolder, CancellationToken ct, IProgress<TransferProgress>? progress = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(saveFolder);

        string initialTemp = Path.Combine(saveFolder, "receiving_" + Guid.NewGuid().ToString("N").Substring(0,8) + ".tmp");
        var metadata = await _server.StartListeningAsync(port, initialTemp, ct, progress);

        string fileName = metadata.FileName;
        if (!fileName.EndsWith(".enc", StringComparison.OrdinalIgnoreCase)) {
            fileName += ".enc";
        }

        string finalEncPath = Path.Combine(saveFolder, fileName);
        if (File.Exists(finalEncPath)) File.Delete(finalEncPath);
        File.Move(initialTemp, finalEncPath);

        var finalInfo = new FileInfo(finalEncPath);
        Logger.Log($"📂 Đã nhận và lưu tệp mã hóa tại: {finalEncPath} ({finalInfo.Length} bytes)");
        _ = _db.LogTransferAsync(metadata.FileName, metadata.FileSize, "Remote", "Local", "Received (Encrypted)");
        return Path.GetFullPath(finalEncPath);
    }

    public async Task LocalEncryptAsync(string inputPath, string outputPath, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        Logger.Log("Mã hóa nội bộ (không gửi)...");
        await Task.Run(() => _crypto.EncryptFile(inputPath, outputPath, password));
        Logger.Log("✅ Mã hóa xong. File lưu tại: " + Path.GetFullPath(outputPath));
    }

    public async Task LocalDecryptAsync(string inputPath, string outputPath, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        Logger.Log("Giải mã nội bộ (không nhận)...");
        await Task.Run(() => _crypto.DecryptFile(inputPath, outputPath, password));
        Logger.Log("✅ Giải mã xong. File lưu tại: " + Path.GetFullPath(outputPath));
    }
}
