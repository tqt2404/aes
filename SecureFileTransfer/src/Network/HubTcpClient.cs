using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SecureFileTransfer.Utils;
using SecureFileTransfer.Models;

namespace SecureFileTransfer.Network;

public class HubTcpClient : IDisposable
{
    private TcpClient? _client;
    private NetworkStream? _stream;
    private string _myDisplayName = string.Empty;
    private CancellationTokenSource? _listenCts;
    
    // Khóa (lock) để tránh Race Condition khi ghi nhiều chunks liên tiếp lên cùng 1 luồng TCP
    private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);

    public bool IsConnected => _client?.Connected == true;

    // UI Events
    public event Action<List<string>>? OnOnlineListUpdated;
    public event Action<NetworkMessage>? OnFileChunkReceived;
    public event Action? OnDisconnected;

    public async Task ConnectAsync(string serverIp, int port, string myDisplayName)
    {
        _myDisplayName = myDisplayName;
        _client = new TcpClient();
        
        await _client.ConnectAsync(serverIp, port);
        _stream = _client.GetStream();

        var loginMsg = new NetworkMessage
        {
            Command = CommandType.Login,
            SenderName = _myDisplayName,
            TargetName = "Server"
        };
        await SendMessageSafeAsync(loginMsg);

        _listenCts = new CancellationTokenSource();
        _ = StartListeningAsync(_listenCts.Token);
    }

    private async Task StartListeningAsync(CancellationToken cancellationToken)
    {
        if (_stream == null) return;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var msg = await NetworkMessage.ReadFromStreamAsync(_stream, cancellationToken);
                
                if (msg.Command == CommandType.OnlineListUpdate)
                {
                    var json = System.Text.Encoding.UTF8.GetString(msg.Payload);
                    var users = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
                    users.Remove(_myDisplayName);
                    OnOnlineListUpdated?.Invoke(users);
                }
                else if (msg.Command == CommandType.FileTransferInit || msg.Command == CommandType.FileChunk)
                {
                    OnFileChunkReceived?.Invoke(msg);
                }
            }
        }
        catch (Exception ex) when (ex is IOException || ex is EndOfStreamException || ex is OperationCanceledException || ex is ObjectDisposedException)
        {
            Logger.Log("[Client] Mất kết nối tới Server Hub.");
            HandleDisconnect();
        }
        catch (Exception ex)
        {
            Logger.Log($"[Client] Error: {ex.Message}");
            System.IO.File.AppendAllText("client_error.txt", ex.ToString() + "\n");
            HandleDisconnect();
        }
    }

    private void HandleDisconnect()
    {
        Disconnect();
        OnDisconnected?.Invoke();
    }

    // Wrap việc ghi vào stream bên trong bộ đệm Lock
    private async Task SendMessageSafeAsync(NetworkMessage msg, CancellationToken ct = default)
    {
        if (_stream == null) throw new InvalidOperationException("Chưa kết nối tới Hub.");

        await _sendLock.WaitAsync(ct);
        try
        {
            await msg.WriteToStreamAsync(_stream, ct);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public async Task SendFileInitAsync(string targetName, FileMetadata metadata)
    {
        byte[] payload = metadata.Serialize();
        var initMsg = new NetworkMessage
        {
            Command = CommandType.FileTransferInit,
            SenderName = _myDisplayName,
            TargetName = targetName,
            Payload = payload,
            PayloadLength = payload.Length
        };
        await SendMessageSafeAsync(initMsg);
    }

    public async Task SendFileChunkAsync(string targetName, byte[] encryptedData, int length)
    {
        byte[] exactPayload = new byte[length];
        Array.Copy(encryptedData, exactPayload, length);

        var chunkMsg = new NetworkMessage
        {
            Command = CommandType.FileChunk,
            SenderName = _myDisplayName,
            TargetName = targetName,
            Payload = exactPayload,
            PayloadLength = length
        };
        await SendMessageSafeAsync(chunkMsg);
    }

    public async Task SendFileTransferCompleteAsync(string targetName)
    {
        var endMsg = new NetworkMessage
        {
            Command = CommandType.FileChunk,
            SenderName = _myDisplayName,
            TargetName = targetName,
            Payload = Array.Empty<byte>(),
            PayloadLength = 0
        };
        await SendMessageSafeAsync(endMsg);
    }

    public void Disconnect()
    {
        try
        {
            if (_stream != null && _client?.Connected == true)
            {
                var msg = new NetworkMessage 
                { 
                    Command = CommandType.Disconnect, SenderName = _myDisplayName, TargetName = "Server" 
                };
                // Đồng bộ thay vì bất đồng bộ lúc đóng app để tránh app ngắt trước khi gửi xong tín hiệu
                _sendLock.Wait(500); 
                try { msg.WriteToStreamAsync(_stream).Wait(500); }
                finally { _sendLock.Release(); }
            }
        }
        catch { }

        Dispose();
    }

    public void Dispose()
    {
        _listenCts?.Cancel();
        _stream?.Dispose();
        _client?.Dispose();
        _sendLock.Dispose();
    }
}
