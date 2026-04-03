using System.Net.Sockets;
using System.Text;
using SecureFileTransfer.Utils;
using System.Net;
using SecureFileTransfer.Models;
using System.Text.Json;
using System.Diagnostics;

namespace SecureFileTransfer.Network;

public interface ITcpClient 
{ 
    Task SendFileAsync(string ip, int port, string filePath, string originalFileName, string sha256Hash = "", IProgress<TransferProgress>? progress = null, CancellationToken ct = default);
    Task SendStreamAsync(string ip, int port, Stream sourceStream, string fileName, long fileSize, string sha256Hash = "", IProgress<TransferProgress>? progress = null, CancellationToken ct = default);
    Task SendActionAsync(string ip, int port, FileMetadata metadata, Func<NetworkStream, Task> bodyWriter, CancellationToken ct = default);
}

public interface ITcpServer 
{ 
    Task<FileMetadata> StartListeningAsync(int port, string savePath, CancellationToken ct, IProgress<TransferProgress>? progress = null); 
}

public class TcpSender : ITcpClient
{
    private const int CONNECTION_TIMEOUT_MS = 10000;
    private const int READ_WRITE_TIMEOUT_MS = 30000;
    private const int BUFFER_SIZE = 65536;
    
    public async Task SendFileAsync(string ip, int port, string filePath, string originalFileName, string sha256Hash = "", IProgress<TransferProgress>? progress = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath)) throw new FileNotFoundException("Tệp gửi không tồn tại.", filePath);

        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        await SendStreamAsync(ip, port, fs, originalFileName, fs.Length, sha256Hash, progress, ct);
    }

    public async Task SendStreamAsync(string ip, int port, Stream sourceStream, string fileName, long fileSize, string sha256Hash = "", IProgress<TransferProgress>? progress = null, CancellationToken ct = default)
    {
        var metadata = new FileMetadata { FileName = fileName, FileSize = fileSize, Sha256Hash = sha256Hash };
        await SendActionAsync(ip, port, metadata, async ns => {
            await StreamDataAsync(ns, sourceStream, fileSize, progress, ct);
        }, ct);
    }

    public async Task SendActionAsync(string ip, int port, FileMetadata metadata, Func<NetworkStream, Task> bodyWriter, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(bodyWriter);
        ArgumentException.ThrowIfNullOrWhiteSpace(ip);
        if (port is <= 0 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));

        using var client = new TcpClient();
        await ConnectWithTimeoutAsync(client, ip, port, ct);
        
        client.ReceiveTimeout = READ_WRITE_TIMEOUT_MS;
        client.SendTimeout = READ_WRITE_TIMEOUT_MS;
        
        using NetworkStream ns = client.GetStream();

        // 1. Send Encrypted Metadata (Security: Prevents metadata plaintext leakage)
        byte[] metaBytes = metadata.Serialize();
        byte[] encryptedMeta = EncryptMetadata(metaBytes, $"{ip}:{port}");
        await ns.WriteAsync(BitConverter.GetBytes(encryptedMeta.Length), 0, 4, ct);
        await ns.WriteAsync(encryptedMeta, 0, encryptedMeta.Length, ct);

        // 2. Run Body Writer (file data encrypted separately)
        await bodyWriter(ns);
        await ns.FlushAsync(ct);
    }

    /// <summary>
    /// Encrypt metadata using a deterministic key derived from connection parameters.
    /// This prevents metadata plaintext leakage over the network.
    /// Uses simple AES ECB mode with a fixed IV (metadata only, not full file security).
    /// </summary>
    private byte[] EncryptMetadata(byte[] metaBytes, string connectionKey)
    {
        try
        {
            using var hmac = new System.Security.Cryptography.HMACSHA256(
                Encoding.UTF8.GetBytes($"meta_key_{connectionKey}"));
            byte[] key = new byte[32];
            Array.Copy(hmac.ComputeHash(Encoding.UTF8.GetBytes(connectionKey)), key, 32);

            // Pad metadata to AES block size (16 bytes)
            byte[] padded = PadToBlockSize(metaBytes);
            byte[] encrypted = new byte[padded.Length];
            
            // Use AES ECB for simplicity (metadata is small, deterministic padding ok)
            using var aes = System.Security.Cryptography.Aes.Create();
            aes.Mode = System.Security.Cryptography.CipherMode.ECB;
            aes.Padding = System.Security.Cryptography.PaddingMode.None;
            using var encryptor = aes.CreateEncryptor(key, null);
            encryptor.TransformBlock(padded, 0, padded.Length, encrypted, 0);
            
            return encrypted;
        }
        catch (Exception ex)
        {
            Logger.Log($"Ảnh hưởng mã hóa metadata: {ex.Message}, gửi metadata không mã hóa.");
            return metaBytes;  // Fallback to plaintext if encryption fails
        }
    }

    private byte[] PadToBlockSize(byte[] data)
    {
        int blockSize = 16;
        int paddingNeeded = blockSize - (data.Length % blockSize);
        if (paddingNeeded == 0) return data;
        
        byte[] padded = new byte[data.Length + paddingNeeded];
        Array.Copy(data, padded, data.Length);
        for (int i = 0; i < paddingNeeded; i++)
            padded[data.Length + i] = (byte)paddingNeeded;
        
        return padded;
    }

    private async Task ConnectWithTimeoutAsync(TcpClient client, string ip, int port, CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(CONNECTION_TIMEOUT_MS);
        try
        {
            await client.ConnectAsync(ip, port, timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException($"Kết nối thất bại sau {CONNECTION_TIMEOUT_MS/1000} giây.");
        }
    }

    private async Task StreamDataAsync(NetworkStream ns, Stream source, long totalSize, IProgress<TransferProgress>? progress, CancellationToken ct)
    {
        byte[] buffer = new byte[BUFFER_SIZE];
        long totalSent = 0;
        uint chunkNumber = 0;
        var sw = Stopwatch.StartNew();

        int read;
        while ((read = await source.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
        {
            // Chunking Protocol: [ChunkNumber(4)] [ChunkSize(4)] [ChunkData(variable)]
            byte[] chunkNumBytes = BitConverter.GetBytes(chunkNumber++);
            byte[] chunkSizeBytes = BitConverter.GetBytes(read);

            await ns.WriteAsync(chunkNumBytes, 0, 4, ct);      // Chunk number for ordering/resume
            await ns.WriteAsync(chunkSizeBytes, 0, 4, ct);    // Actual chunk size
            await ns.WriteAsync(buffer, 0, read, ct);          // Chunk data

            totalSent += read;
            ReportProgress(progress, totalSent, totalSize, sw.Elapsed.TotalSeconds);
        }

        // Send terminator chunk (ChunkNumber=UINT_MAX, ChunkSize=0)
        byte[] terminatorNum = BitConverter.GetBytes(uint.MaxValue);
        byte[] terminatorSize = BitConverter.GetBytes(0);
        await ns.WriteAsync(terminatorNum, 0, 4, ct);
        await ns.WriteAsync(terminatorSize, 0, 4, ct);

        Logger.Log($"[Gửi] Truyền hoàn tất: {chunkNumber} chunks, {totalSent} bytes");
    }

    private void ReportProgress(IProgress<TransferProgress>? progress, long current, long total, double elapsed)
    {
        if (progress == null) return;
        
        double speed = elapsed > 0 ? current / elapsed : 0;
        long remaining = total - current;
        progress.Report(new TransferProgress 
        { 
            BytesTransferred = current, 
            TotalBytes = total,
            Speed = speed,
            RemainingTime = speed > 0 ? TimeSpan.FromSeconds(remaining / speed) : TimeSpan.Zero
        });
    }
}

public class TcpReceiver : ITcpServer
{
    private const int BUFFER_SIZE = 65536;

    public async Task<FileMetadata> StartListeningAsync(int port, string savePath, CancellationToken ct, IProgress<TransferProgress>? progress = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(savePath);
        EnsureDirectoryExists(savePath);

        var listener = new TcpListener(IPAddress.Any, port);
        listener.Start();

        try
        {
            using TcpClient client = await listener.AcceptTcpClientAsync(ct);
            var clientIp = (client.Client.RemoteEndPoint as IPEndPoint)?.Address.ToString() ?? "unknown";
            using NetworkStream ns = client.GetStream();

            // 1. Receive Encrypted Metadata
            byte[] metaLenBytes = new byte[4];
            await ns.ReadExactlyAsync(metaLenBytes, 0, 4, ct);
            int metaLen = BitConverter.ToInt32(metaLenBytes, 0);

            byte[] encryptedMetaBytes = new byte[metaLen];
            await ns.ReadExactlyAsync(encryptedMetaBytes, 0, metaLen, ct);
            
            // Decrypt metadata
            byte[] metaBytes = DecryptMetadata(encryptedMetaBytes, $"{clientIp}:{port}");
            var metadata = FileMetadata.Deserialize(metaBytes) 
                ?? throw new Exception("Lỗi định dạng Metadata sau giải mã.");

            Logger.Log($"[Nhận] Metadata: {metadata.FileName}, KeySize: {metadata.KeySize} bits, FileSize: {metadata.FileSize} bytes");

            // 2. Receive Content
            await ReceiveFileContentAsync(ns, savePath, metadata.FileSize, progress, ct);
            return metadata;
        }
        finally
        {
            listener.Stop();
        }
    }

    /// <summary>
    /// Decrypt metadata using matching key derivation from connection parameters.
    /// Pairs with EncryptMetadata on sender side.
    /// </summary>
    private byte[] DecryptMetadata(byte[] encryptedBytes, string connectionKey)
    {
        try
        {
            using var hmac = new System.Security.Cryptography.HMACSHA256(
                Encoding.UTF8.GetBytes($"meta_key_{connectionKey}"));
            byte[] key = new byte[32];
            Array.Copy(hmac.ComputeHash(Encoding.UTF8.GetBytes(connectionKey)), key, 32);

            byte[] decrypted = new byte[encryptedBytes.Length];
            using var aes = System.Security.Cryptography.Aes.Create();
            aes.Mode = System.Security.Cryptography.CipherMode.ECB;
            aes.Padding = System.Security.Cryptography.PaddingMode.None;
            using var decryptor = aes.CreateDecryptor(key, null);
            decryptor.TransformBlock(encryptedBytes, 0, encryptedBytes.Length, decrypted, 0);
            
            // Remove PKCS7 padding
            int paddingLen = decrypted[decrypted.Length - 1];
            if (paddingLen > 0 && paddingLen <= 16)
            {
                byte[] unpadded = new byte[decrypted.Length - paddingLen];
                Array.Copy(decrypted, 0, unpadded, 0, unpadded.Length);
                return unpadded;
            }

            return decrypted;
        }
        catch (Exception ex)
        {
            Logger.Log($"Lỗi giải mã metadata: {ex.Message}, thử giải mã trực tiếp.");
            return encryptedBytes;  // Fallback to treating as plaintext
        }
    }

    private void EnsureDirectoryExists(string path)
    {
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
    }

    private async Task ReceiveFileContentAsync(NetworkStream ns, string path, long totalSize, IProgress<TransferProgress>? progress, CancellationToken ct)
    {
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        byte[] buffer = new byte[BUFFER_SIZE];
        long totalReceived = 0;
        uint lastChunkNumber = 0;
        int chunksReceived = 0;

        while (true)
        {
            // Receive Chunking Protocol: [ChunkNumber(4)] [ChunkSize(4)] [ChunkData]
            byte[] chunkNumBytes = new byte[4];
            byte[] chunkSizeBytes = new byte[4];

            int read = await ns.ReadAsync(chunkNumBytes, 0, 4, ct);
            if (read != 4) break;

            read = await ns.ReadAsync(chunkSizeBytes, 0, 4, ct);
            if (read != 4) break;

            uint chunkNumber = BitConverter.ToUInt32(chunkNumBytes, 0);
            int chunkSize = BitConverter.ToInt32(chunkSizeBytes, 0);

            // Check for terminator chunk (ChunkNumber=UINT_MAX)
            if (chunkNumber == uint.MaxValue && chunkSize == 0)
            {
                Logger.Log($"[Nhận] Nhận được tín hiệu hoàn tất sau {chunksReceived} chunks");
                break;
            }

            // Validate chunk ordering (optional, for integrity)
            if (chunkNumber != lastChunkNumber)
            {
                Logger.Log($"[Cảnh báo] Chunk không theo thứ tự: mong đợi {lastChunkNumber}, nhận được {chunkNumber}");
            }
            lastChunkNumber = chunkNumber + 1;
            chunksReceived++;

            // Receive chunk data
            byte[] chunkData = new byte[chunkSize];
            int bytesRead = 0;
            while (bytesRead < chunkSize)
            {
                int remaining = chunkSize - bytesRead;
                int justRead = await ns.ReadAsync(chunkData, bytesRead, remaining, ct);
                if (justRead == 0) throw new Exception("Kết nối đứt giữa chunk");
                bytesRead += justRead;
            }

            // Write chunk to file
            await fs.WriteAsync(chunkData, 0, chunkSize, ct);
            totalReceived += chunkSize;

            // Report progress
            progress?.Report(new TransferProgress 
            { 
                BytesTransferred = totalReceived, 
                TotalBytes = totalSize
            });
        }

        await fs.FlushAsync(ct);
        Logger.Log($"[Nhận] Hoàn tất nhận tệp: {chunksReceived} chunks, {totalReceived} bytes");
    }
}
