using SecureFileTransfer.Network;
using SecureFileTransfer.Security;
using SecureFileTransfer.Utils;
using SecureFileTransfer.Models;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Windows.Forms;

namespace SecureFileTransfer.Services;

public class FileTransferManager
{
    private readonly IAesCryptography _crypto;
    private readonly DatabaseService _db;
    public HubTcpClient? HubClient { get; set; }

    // Path settings
    public string DefaultVaultPath { get; set; }

    // Receiving states
    private FileStream? _receivingStream;
    public string? TempReceivingPath { get; private set; }
    public FileMetadata? CurrentIncomingMetadata { get; private set; }
    private long _receivedBytes;
    
    // Callbacks for UI
    public event Action<TransferProgress>? OnReceiveProgress;
    public event Action<FileMetadata>? OnTransferInitReceived;
    public event Action<string>? OnEncryptedFileReady;

    // Session protection: maximum bytes receivable per transfer session
    // Capped at 2GB; also enforced per-session based on metadata.FileSize
    private const long MaxSessionBytes = 2L * 1024 * 1024 * 1024;

    public FileTransferManager(IAesCryptography crypto, DatabaseService db)
    {
        _crypto = crypto;
        _db = db;
        // Sử dụng AppContext.BaseDirectory thay vì MyDocuments để tránh lỗi OneDrive/Permission
        DefaultVaultPath = Path.Combine(AppContext.BaseDirectory, "Vault");
        if (!Directory.Exists(DefaultVaultPath)) 
        {
            Directory.CreateDirectory(DefaultVaultPath);
        }
    }

    public void AttachHubClient(HubTcpClient client)
    {
        HubClient = client;
        HubClient.OnFileChunkReceived += HandleIncomingMessage;
    }

    public async Task EncryptAndSendAsync(string filePath, string targetUserName, string password, AesKeySize keySize = AesKeySize.AES256, IProgress<TransferProgress>? progress = null, CancellationToken ct = default)
    {
        if (HubClient == null || !HubClient.IsConnected)
            throw new InvalidOperationException("Chưa kết nối tới Hub Server.");

        ValidateFile(filePath);

        string fileName = Path.GetFileName(filePath);
        long originalFileSize = new FileInfo(filePath).Length;
        string hash = await HashHelper.ComputeSha256Async(filePath, ct);

        long estimatedMetadataLen = 85; 
        long encryptedSize = 4 + estimatedMetadataLen + 16 + 16 + ((originalFileSize / 16) + 1) * 16 + 32;
        var metadata = new FileMetadata 
        { 
            FileName = fileName, 
            FileSize = encryptedSize, 
            Sha256Hash = hash, 
            EncryptionType = keySize,
            KeySize = (int)keySize
        };

        Logger.Log($"Mã hóa và truyền tải tới '{targetUserName}': {fileName} (AES-{keySize})");

        await HubClient.SendFileInitAsync(targetUserName, metadata);

        using var fsIn = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var chunkStream = new HubChunkStream(HubClient, targetUserName, progress, encryptedSize);

        await _crypto.EncryptStreamAsync(fsIn, chunkStream, password, fileName, keySize, ct);
        await chunkStream.FlushAsync(ct);
        
        await HubClient.SendFileTransferCompleteAsync(targetUserName);

        _ = _db.LogTransferAsync(fileName, originalFileSize, "Local", targetUserName, "Sent via Hub");
        Logger.Log($"Truyền tệp tới {targetUserName} hoàn tất.");
    }

    public void StartReceivingSession()
    {
        if (!Directory.Exists(DefaultVaultPath))
            Directory.CreateDirectory(DefaultVaultPath);
            
        TempReceivingPath = Path.Combine(DefaultVaultPath, $"CryptoVault_{Guid.NewGuid():N}.enc");
        _receivingStream = new FileStream(TempReceivingPath, FileMode.Create, FileAccess.Write, FileShare.None);
        _receivedBytes = 0;
    }

    private void HandleIncomingMessage(NetworkMessage msg)
    {
        if (msg.Command == CommandType.FileTransferInit)
        {
            CurrentIncomingMetadata = FileMetadata.Deserialize(msg.Payload);
            Logger.Log($"[Hub] Có yêu cầu gửi tệp: {CurrentIncomingMetadata.FileName} từ {msg.SenderName}");
            
            StartReceivingSession();
            
            OnTransferInitReceived?.Invoke(CurrentIncomingMetadata);
        }
        else if (msg.Command == CommandType.FileChunk)
        {
            if (msg.PayloadLength == 0) // Completed
            {
                _receivingStream?.Dispose();
                _receivingStream = null;
                
                if (TempReceivingPath != null && CurrentIncomingMetadata != null)
                {
                    Logger.Log($"[Hub] Đã tải xong bản mã hóa. Đang chờ giải mã.");
                    OnReceiveProgress?.Invoke(new TransferProgress 
                    { 
                        BytesTransferred = CurrentIncomingMetadata.FileSize, 
                        TotalBytes = CurrentIncomingMetadata.FileSize 
                    });
                    OnEncryptedFileReady?.Invoke(msg.SenderName);
                    
                    // Show global message box right away so the user doesn't miss it
                    Task.Run(() => {
                        MessageBox.Show(
                            $"Bạn vừa nhận thành công một tệp mã hóa từ {msg.SenderName}!\nNơi lưu: {TempReceivingPath}\n\nHãy vào mục Nhận Dữ Liệu để nhập khoá và mở tệp.", 
                            "Cảnh báo Bảo mật - Có Tệp Mới", 
                            MessageBoxButtons.OK, 
                            MessageBoxIcon.Information);
                    });
                }
            }
            else if (_receivingStream != null)
            {
                // === SESSION BYTE CAP: Chống Chunk Flooding Attack ===
                // Tính ngưỡng cho phép: FileSize đã khai báo + 10% dung sai overhead
                long allowedMax = CurrentIncomingMetadata != null
                    ? (long)(CurrentIncomingMetadata.FileSize * 1.1)
                    : MaxSessionBytes;
                allowedMax = Math.Min(allowedMax, MaxSessionBytes); // Hard cap tuyệt đối 2GB

                if (_receivedBytes + msg.PayloadLength > allowedMax)
                {
                    Logger.Log($"[Security] Session bị huỷ: nhận vượt quá giới hạn ({_receivedBytes + msg.PayloadLength} > {allowedMax} bytes). Có thể bị tấn công flooding.");
                    _receivingStream?.Dispose();
                    _receivingStream = null;
                    if (TempReceivingPath != null && File.Exists(TempReceivingPath))
                        File.Delete(TempReceivingPath);
                    TempReceivingPath = null;
                    CurrentIncomingMetadata = null;
                    return;
                }

                _receivingStream.Write(msg.Payload, 0, msg.PayloadLength);
                _receivedBytes += msg.PayloadLength;
                
                OnReceiveProgress?.Invoke(new TransferProgress 
                { 
                    BytesTransferred = _receivedBytes, 
                    TotalBytes = CurrentIncomingMetadata?.FileSize ?? 0 
                });
            }
        }
    }

    public async Task<string> DecryptReadyFileAsync(string password, CancellationToken ct)
    {
        if (TempReceivingPath == null || CurrentIncomingMetadata == null || !File.Exists(TempReceivingPath))
            throw new InvalidOperationException("Không có tệp mã hóa nào đang chờ để giải mã.");

        string sanitizedFileName = Path.GetFileName(CurrentIncomingMetadata.FileName);
        string finalPath = Path.Combine(DefaultVaultPath, sanitizedFileName);

        if (!Path.GetFullPath(finalPath).StartsWith(Path.GetFullPath(DefaultVaultPath)))
            throw new UnauthorizedAccessException("Path traversal attack detected!");

        Logger.Log($"[Giải mã] Tệp tin: {CurrentIncomingMetadata.FileName}");
        
        using (var fsIn = new FileStream(TempReceivingPath, FileMode.Open, FileAccess.Read))
        using (var fsOut = new FileStream(finalPath, FileMode.Create, FileAccess.Write))
        {
            await _crypto.DecryptStreamAsync(fsIn, fsOut, password, ct);
        }

        await VerifyIntegrityAsync(finalPath, CurrentIncomingMetadata.Sha256Hash, ct);
        
        _ = _db.LogTransferAsync(CurrentIncomingMetadata.FileName, CurrentIncomingMetadata.FileSize, "Remote Hub", "Local", "Received");
        
        // Cleanup temp file since we have the decrypted one
        File.Delete(TempReceivingPath);
        TempReceivingPath = null;
        CurrentIncomingMetadata = null;

        return Path.GetFullPath(finalPath);
    }

    public async Task<string> DecryptLocalFileAsync(string localEncPath, string password, CancellationToken ct)
    {
        if (!File.Exists(localEncPath)) throw new FileNotFoundException("Tệp mã hóa không tồn tại.");

        // Đọ các byte đầu tiên để lấy tên file gốc từ metadata tích hợp
        string originalFileName;
        using (var fsCheck = new FileStream(localEncPath, FileMode.Open, FileAccess.Read))
        {
            byte[] lenBytes = new byte[4];
            await fsCheck.ReadExactlyAsync(lenBytes, 0, 4, ct);
            if (System.BitConverter.IsLittleEndian) System.Array.Reverse(lenBytes);
            int metaLen = System.BitConverter.ToInt32(lenBytes, 0);
            byte[] metaBytes = new byte[metaLen];
            await fsCheck.ReadExactlyAsync(metaBytes, 0, metaLen, ct);
            var embeddedMeta = FileMetadata.Deserialize(metaBytes);
            // Lấy tên gốc nếu có, fallback về ".dat" nếu metadata cũ
            originalFileName = (embeddedMeta != null && !string.IsNullOrWhiteSpace(embeddedMeta.FileName) && embeddedMeta.FileName != "data")
                ? Path.GetFileName(embeddedMeta.FileName)
                : "Decrypted_" + Path.GetFileNameWithoutExtension(localEncPath) + ".dat";
        }

        string finalPath = Path.Combine(DefaultVaultPath, originalFileName);

        // Tránh ghi đè nếu file đã tồn tại
        if (File.Exists(finalPath))
        {
            string nameNoExt = Path.GetFileNameWithoutExtension(originalFileName);
            string ext = Path.GetExtension(originalFileName);
            finalPath = Path.Combine(DefaultVaultPath, $"{nameNoExt}_{System.DateTime.Now:HHmmss}{ext}");
        }

        Logger.Log($"[Giải mã Local] Tệp tin: {Path.GetFileName(localEncPath)} -> {Path.GetFileName(finalPath)}");

        using (var fsIn = new FileStream(localEncPath, FileMode.Open, FileAccess.Read))
        using (var fsOut = new FileStream(finalPath, FileMode.Create, FileAccess.Write))
        {
            await _crypto.DecryptStreamAsync(fsIn, fsOut, password, ct);
        }

        return Path.GetFullPath(finalPath);
    }


    public async Task EncryptLocalStorageAsync(string sourcePath, string destPath, string password, AesKeySize keySize = AesKeySize.AES256, IProgress<TransferProgress>? progress = null, CancellationToken ct = default)
    {
        ValidateFile(sourcePath);
        
        string fileName = Path.GetFileName(sourcePath);
        long originalFileSize = new FileInfo(sourcePath).Length;
        string hash = await HashHelper.ComputeSha256Async(sourcePath, ct);

        // Ước tính kích thước bản mã (metadata + overhead) để báo cáo progress
        long estimatedMetadataLen = 85; 
        long encryptedSize = 4 + estimatedMetadataLen + 16 + 16 + ((originalFileSize / 16) + 1) * 16 + 32;

        Logger.Log($"[Mã hóa Local] Đang xử lý: {fileName} -> {Path.GetFileName(destPath)}");

        using var fsIn = new FileStream(sourcePath, FileMode.Open, FileAccess.Read);
        using var fsOut = new FileStream(destPath, FileMode.Create, FileAccess.Write);
        
        using var progressStream = new ProgressWrappedStream(fsOut, progress, encryptedSize);
        
        await _crypto.EncryptStreamAsync(fsIn, progressStream, password, fileName, keySize, ct);
        await progressStream.FlushAsync(ct);
        
        Logger.Log($"[Mã hóa Local] Đã lưu tệp được mã hóa tại: {destPath}");
    }

    private class ProgressWrappedStream : Stream
    {
        private readonly Stream _inner;
        private readonly IProgress<TransferProgress>? _progress;
        private readonly long _total;
        private long _written;

        public ProgressWrappedStream(Stream inner, IProgress<TransferProgress>? progress, long total)
        {
            _inner = inner;
            _progress = progress;
            _total = total;
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct)
        {
            await _inner.WriteAsync(buffer, offset, count, ct);
            _written += count;
            _progress?.Report(new TransferProgress { BytesTransferred = _written, TotalBytes = _total });
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default)
        {
            await _inner.WriteAsync(buffer, ct);
            _written += buffer.Length;
            _progress?.Report(new TransferProgress { BytesTransferred = _written, TotalBytes = _total });
        }

        public override void Flush() => _inner.Flush();
        public override Task FlushAsync(CancellationToken ct) => _inner.FlushAsync(ct);
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => _inner.SetLength(value);
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }
        public override void Write(byte[] buffer, int offset, int count)
        {
            _inner.Write(buffer, offset, count);
            _written += count;
            _progress?.Report(new TransferProgress { BytesTransferred = _written, TotalBytes = _total });
        }
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

public class HubChunkStream : Stream
{
    private readonly HubTcpClient _client;
    private readonly string _targetName;
    private readonly IProgress<TransferProgress>? _progress;
    private readonly long _totalFrames;
    private long _sent;

    public HubChunkStream(HubTcpClient client, string targetName, IProgress<TransferProgress>? progress, long totalFrames)
    {
        _client = client;
        _targetName = targetName;
        _progress = progress;
        _totalFrames = totalFrames;
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await _client.SendFileChunkAsync(_targetName, buffer.ToArray(), buffer.Length);
        _sent += buffer.Length;
        _progress?.Report(new TransferProgress { BytesTransferred = _sent, TotalBytes = _totalFrames });
    }
    
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        byte[] payload = new byte[count];
        Array.Copy(buffer, offset, payload, 0, count);
        var task = _client.SendFileChunkAsync(_targetName, payload, count);
        
        _sent += count;
        _progress?.Report(new TransferProgress { BytesTransferred = _sent, TotalBytes = _totalFrames });
        
        return task;
    }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => WriteAsync(buffer, offset, count, CancellationToken.None).Wait();
}
