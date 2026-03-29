using System.Net.Sockets;
using System.Text;
using SecureFileTransfer.Utils;
using System.Net;
using SecureFileTransfer.Models;
using System.Text.Json;

namespace SecureFileTransfer.Network;

public interface ITcpClient { Task SendFileAsync(string ip, int port, string filePath, string originalFileName, string sha256Hash = "", IProgress<TransferProgress>? progress = null); }
public interface ITcpServer { Task<FileMetadata> StartListeningAsync(int port, string savePath, CancellationToken ct, IProgress<TransferProgress>? progress = null); }

public class TcpSender : ITcpClient
{
        private const int CONNECTION_TIMEOUT_MS = 10000; // 10 seconds
        private const int READ_WRITE_TIMEOUT_MS = 30000; // 30 seconds
        
        public async Task SendFileAsync(string ip, int port, string filePath, string originalFileName, string sha256Hash = "", IProgress<TransferProgress>? progress = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(ip);
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            ArgumentException.ThrowIfNullOrWhiteSpace(originalFileName);
            if (port <= 0 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port), "Port out of range.");
            if (!File.Exists(filePath)) throw new FileNotFoundException("Tệp gửi không tồn tại.", filePath);

            using TcpClient client = new TcpClient();
            using var cts = new CancellationTokenSource(CONNECTION_TIMEOUT_MS);
            try
            {
                await client.ConnectAsync(ip, port, cts.Token);
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException("Kết nối timeout - Server không phản hồi (10 giây)");
            }
            
            // Set read/write timeouts to detect disconnections
            client.ReceiveTimeout = READ_WRITE_TIMEOUT_MS;
            client.SendTimeout = READ_WRITE_TIMEOUT_MS;
            
            using NetworkStream stream = client.GetStream();

            FileInfo fileInfo = new FileInfo(filePath);
            var metadata = new FileMetadata 
            { 
                FileName = originalFileName, 
                FileSize = fileInfo.Length,
                Sha256Hash = sha256Hash  // ✅ Include integrity hash
            };
            string json = System.Text.Json.JsonSerializer.Serialize(metadata);
            byte[] metaBytes = Encoding.UTF8.GetBytes(json);

        // Header: 4 bytes metadata length + metadata
        await stream.WriteAsync(BitConverter.GetBytes(metaBytes.Length), 0, 4);
        await stream.WriteAsync(metaBytes, 0, metaBytes.Length);

        using FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        byte[] buffer = new byte[65536];
        int bytesRead;
        long totalSent = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        while ((bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            await stream.WriteAsync(buffer, 0, bytesRead);
            totalSent += bytesRead;
            
            if (progress != null)
            {
                double elapsed = sw.Elapsed.TotalSeconds;
                double speed = elapsed > 0 ? totalSent / elapsed : 0;
                long remainingBytes = fileInfo.Length - totalSent;
                TimeSpan remainingTime = speed > 0 ? TimeSpan.FromSeconds(remainingBytes / speed) : TimeSpan.Zero;

                progress.Report(new TransferProgress { 
                    BytesTransferred = totalSent, 
                    TotalBytes = fileInfo.Length,
                    Speed = speed,
                    RemainingTime = remainingTime
                });
            }
        }
    }
}

public class TcpReceiver : ITcpServer
{
    // Helper to read exactly N bytes from a stream
    private async Task<byte[]> ReadExactlyAsync(NetworkStream stream, int count, CancellationToken ct)
    {
        byte[] buffer = new byte[count];
        int offset = 0;
        while (offset < count)
        {
            int read = await stream.ReadAsync(buffer, offset, count - offset, ct);
            if (read == 0) throw new Exception("Kết nối bị ngắt đột ngột khi đang đọc Metadata.");
            offset += read;
        }
        return buffer;
    }

    public async Task<FileMetadata> StartListeningAsync(int port, string savePath, CancellationToken ct, IProgress<TransferProgress>? progress = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(savePath);
        if (port <= 0 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port), "Port out of range.");
        string? dir = Path.GetDirectoryName(savePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

        TcpListener listener = new TcpListener(IPAddress.Any, port);
        listener.Start();
        Logger.Log($"[Server] Đang đợi kết nối tại cổng {port}...");

        try
        {
            using TcpClient client = await listener.AcceptTcpClientAsync(ct);
            using NetworkStream stream = client.GetStream();

            // 1. Read Metadata Header (4 bytes length) - Use ReadExactly
            byte[] metaLenBytes = await ReadExactlyAsync(stream, 4, ct);
            int metaLen = BitConverter.ToInt32(metaLenBytes, 0);

            // 2. Read Metadata Body - Use ReadExactly
            byte[] metaBytes = await ReadExactlyAsync(stream, metaLen, ct);
            var metadata = System.Text.Json.JsonSerializer.Deserialize<FileMetadata>(Encoding.UTF8.GetString(metaBytes)) 
                           ?? throw new Exception("Lỗi định dạng Metadata.");

            // 3. Read File Data
            using (FileStream fs = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                byte[] buffer = new byte[65536];
                int read;
                long receivedBytes = 0;

                while (receivedBytes < metadata.FileSize)
                {
                    read = await stream.ReadAsync(buffer, 0, buffer.Length, ct);
                    if (read == 0) break;
                    await fs.WriteAsync(buffer, 0, read, ct);
                    receivedBytes += read;

                    progress?.Report(new TransferProgress { 
                        TotalBytes = metadata.FileSize, 
                        BytesTransferred = receivedBytes 
                    });
                }
                await fs.FlushAsync(ct);
            }
            
            Logger.Log($"[TCP] Đã nhận xong khối dữ liệu '{metadata.FileName}' ({metadata.FileSize} bytes).");
            return metadata;
        }
        finally
        {
            listener.Stop();
            listener.Dispose();
        }
    }
}
