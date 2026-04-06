using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SecureFileTransfer.Utils;

namespace SecureFileTransfer.Network;

public class ClientSession : IDisposable
{
    public TcpClient Client { get; }
    public NetworkStream Stream { get; }
    public SemaphoreSlim SendLock { get; } = new SemaphoreSlim(1, 1);

    public ClientSession(TcpClient client)
    {
        Client = client;
        Stream = client.GetStream();
    }

    public void Dispose()
    {
        try { Client.Close(); } catch { }
        SendLock.Dispose();
    }
}

public class CentralHubServer
{
    private readonly int _port;
    private TcpListener? _listener;
    private readonly ConcurrentDictionary<string, ClientSession> _connectedClients = new();
    private CancellationTokenSource? _cts;
    private TaskCompletionSource<bool> _startupTcs = new();

    public CentralHubServer(int port = 5000)
    {
        _port = port;
    }

    public Task WaitForStartAsync() => _startupTcs.Task;

    public async Task StartAsync()
    {
        _cts = new CancellationTokenSource();
        _startupTcs = new TaskCompletionSource<bool>();
        
        try
        {
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();
            Logger.Log($"[Server Hub] Đang chạy trên port {_port}...");
            _startupTcs.TrySetResult(true);

            while (!_cts.Token.IsCancellationRequested)
            {
                TcpClient client = await _listener.AcceptTcpClientAsync(_cts.Token);
                _ = HandleClientAsync(client, _cts.Token);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Logger.Log($"[Server Hub] Lỗi khởi động: {ex.Message}");
            _startupTcs.TrySetException(ex);
        }
        finally
        {
            _listener?.Stop();
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        foreach (var session in _connectedClients.Values)
        {
            session.Dispose();
        }
        _connectedClients.Clear();
        _listener?.Stop();
        Logger.Log("[Server Hub] Đã dừng máy chủ.");
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        string? clientName = null;
        var session = new ClientSession(client);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                NetworkMessage message = await NetworkMessage.ReadFromStreamAsync(session.Stream, cancellationToken);

                switch (message.Command)
                {
                    case CommandType.Login:
                        clientName = message.SenderName;
                        if (_connectedClients.TryGetValue(clientName, out var oldSession))
                        {
                            oldSession.Dispose();
                        }
                            
                        _connectedClients[clientName] = session;
                        Logger.Log($"[Server Hub] {clientName} vừa kết nối.");
                        
                        await BroadcastOnlineListAsync(cancellationToken);
                        break;

                    case CommandType.FileTransferInit:
                    case CommandType.FileChunk:
                        await RouteMessageAsync(message, cancellationToken);
                        break;

                    case CommandType.Disconnect:
                        return;
                }
            }
        }
        catch (Exception ex) when (ex is IOException || ex is EndOfStreamException || ex is OperationCanceledException || ex is InvalidDataException) { }
        catch (Exception ex)
        {
            Logger.Log($"[Server Hub] Lỗi tại Client {clientName}: {ex.Message}");
        }
        finally
        {
            if (clientName != null && _connectedClients.TryRemove(clientName, out var removedSession))
            {
                Logger.Log($"[Server Hub] {clientName} đã ngắt kết nối.");
                removedSession.Dispose();
                _ = BroadcastOnlineListAsync(CancellationToken.None);
            }
            else
            {
                session.Dispose();
            }
        }
    }

    private async Task RouteMessageAsync(NetworkMessage message, CancellationToken cancellationToken)
    {
        if (_connectedClients.TryGetValue(message.TargetName, out var targetSession))
        {
            try
            {
                await targetSession.SendLock.WaitAsync(cancellationToken);
                try
                {
                    await message.WriteToStreamAsync(targetSession.Stream, cancellationToken);
                }
                finally
                {
                    targetSession.SendLock.Release();
                }
            }
            catch (Exception)
            {
                Logger.Log($"[Server Hub] Lỗi định tuyến tới {message.TargetName} (Mất kết nối)");
            }
        }
        else
        {
            Logger.Log($"[Server Hub] Không tìm thấy user {message.TargetName} để định tuyến file.");
        }
    }

    private async Task BroadcastOnlineListAsync(CancellationToken cancellationToken)
    {
        var userList = _connectedClients.Keys.ToList();
        byte[] payloadBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(userList));

        var updateMessage = new NetworkMessage
        {
            Command = CommandType.OnlineListUpdate,
            SenderName = "Server",
            TargetName = "Broadcast",
            PayloadLength = payloadBytes.Length,
            Payload = payloadBytes
        };

        foreach (var kvp in _connectedClients)
        {
            try
            {
                var session = kvp.Value;
                await session.SendLock.WaitAsync(cancellationToken);
                try
                {
                    await updateMessage.WriteToStreamAsync(session.Stream, cancellationToken);
                }
                finally
                {
                    session.SendLock.Release();
                }
            }
            catch { }
        }
    }
}
