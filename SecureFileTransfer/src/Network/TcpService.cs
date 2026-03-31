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

        // 1. Send Metadata
        byte[] metaBytes = metadata.Serialize();
        await ns.WriteAsync(BitConverter.GetBytes(metaBytes.Length), 0, 4, ct);
        await ns.WriteAsync(metaBytes, 0, metaBytes.Length, ct);

        // 2. Run Body Writer
        await bodyWriter(ns);
        await ns.FlushAsync(ct);
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
        var sw = Stopwatch.StartNew();

        int read;
        while ((read = await source.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
        {
            await ns.WriteAsync(buffer, 0, read, ct);
            totalSent += read;
            ReportProgress(progress, totalSent, totalSize, sw.Elapsed.TotalSeconds);
        }
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
            using NetworkStream ns = client.GetStream();

            // 1. Receive Metadata
            byte[] metaLenBytes = new byte[4];
            await ns.ReadExactlyAsync(metaLenBytes, 0, 4, ct);
            int metaLen = BitConverter.ToInt32(metaLenBytes, 0);

            byte[] metaBytes = new byte[metaLen];
            await ns.ReadExactlyAsync(metaBytes, 0, metaLen, ct);
            var metadata = FileMetadata.Deserialize(metaBytes) ?? throw new Exception("Lỗi định dạng Metadata.");

            // 2. Receive Content
            await ReceiveFileContentAsync(ns, savePath, metadata.FileSize, progress, ct);
            return metadata;
        }
        finally
        {
            listener.Stop();
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

        while (totalReceived < totalSize)
        {
            int toRead = (int)Math.Min(buffer.Length, totalSize - totalReceived);
            int read = await ns.ReadAsync(buffer, 0, toRead, ct);
            if (read == 0) break;

            await fs.WriteAsync(buffer, 0, read, ct);
            totalReceived += read;
            progress?.Report(new TransferProgress { BytesTransferred = totalReceived, TotalBytes = totalSize });
        }
        await fs.FlushAsync(ct);
    }
}
