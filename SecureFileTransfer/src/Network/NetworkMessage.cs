using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SecureFileTransfer.Network;

public enum CommandType : byte
{
    Login = 1,
    OnlineListUpdate = 2,
    FileTransferInit = 3,
    FileChunk = 4,
    Disconnect = 5
}

public class NetworkMessage
{
    public CommandType Command { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string TargetName { get; set; } = string.Empty;
    public int PayloadLength { get; set; }
    public byte[] Payload { get; set; } = Array.Empty<byte>();

    // Protective limits to prevent memory overflow from bad data
    private const int MaxStringLength = 256;
    private const int MaxPayloadLength = 50 * 1024 * 1024; // 50MB reasonable single chunk limit

    public async Task WriteToStreamAsync(NetworkStream stream, CancellationToken cancellationToken = default)
    {
        // 1. Command (1 byte)
        stream.WriteByte((byte)Command);

        // 2. SenderName
        await WriteStringAsync(stream, SenderName, cancellationToken);

        // 3. TargetName (Could be "Server" or another user)
        await WriteStringAsync(stream, TargetName, cancellationToken);

        // 4. Payload Length
        byte[] lengthBytes = BitConverter.GetBytes(PayloadLength);
        await stream.WriteAsync(lengthBytes, 0, 4, cancellationToken);

        // 5. Payload
        if (PayloadLength > 0 && Payload != null)
        {
            await stream.WriteAsync(Payload, 0, PayloadLength, cancellationToken);
        }
    }

    public static async Task<NetworkMessage> ReadFromStreamAsync(NetworkStream stream, CancellationToken cancellationToken = default)
    {
        var message = new NetworkMessage();

        // 1. Read Command
        byte[] cmdByte = new byte[1];
        int read = await stream.ReadAsync(cmdByte, 0, 1, cancellationToken);
        if (read == 0) throw new EndOfStreamException("Connection closed by remote host.");
        message.Command = (CommandType)cmdByte[0];

        // 2. Read SenderName
        message.SenderName = await ReadStringAsync(stream, cancellationToken);

        // 3. Read TargetName
        message.TargetName = await ReadStringAsync(stream, cancellationToken);

        // 4. Read PayloadLength
        byte[] lengthBytes = new byte[4];
        await ReadExactlyAsync(stream, lengthBytes, 4, cancellationToken);
        message.PayloadLength = BitConverter.ToInt32(lengthBytes, 0);

        if (message.PayloadLength < 0 || message.PayloadLength > MaxPayloadLength)
            throw new InvalidDataException($"Invalid payload length: {message.PayloadLength}");

        // 5. Read Payload
        if (message.PayloadLength > 0)
        {
            message.Payload = new byte[message.PayloadLength];
            await ReadExactlyAsync(stream, message.Payload, message.PayloadLength, cancellationToken);
        }

        return message;
    }

    private static async Task WriteStringAsync(NetworkStream stream, string value, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(value))
        {
            await stream.WriteAsync(BitConverter.GetBytes(0), 0, 4, cancellationToken);
            return;
        }

        byte[] bytes = Encoding.UTF8.GetBytes(value);
        if (bytes.Length > MaxStringLength) throw new InvalidDataException("String exceeds max length.");

        await stream.WriteAsync(BitConverter.GetBytes(bytes.Length), 0, 4, cancellationToken);
        await stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken);
    }

    private static async Task<string> ReadStringAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        byte[] lengthBytes = new byte[4];
        await ReadExactlyAsync(stream, lengthBytes, 4, cancellationToken);
        int length = BitConverter.ToInt32(lengthBytes, 0);

        if (length == 0) return string.Empty;
        if (length < 0 || length > MaxStringLength) throw new InvalidDataException("Invalid string length.");

        byte[] strBytes = new byte[length];
        await ReadExactlyAsync(stream, strBytes, length, cancellationToken);
        return Encoding.UTF8.GetString(strBytes);
    }

    // Helper to ensure we read exact byte frames (TCP might split packets)
    private static async Task ReadExactlyAsync(NetworkStream stream, byte[] buffer, int count, CancellationToken token)
    {
        int offset = 0;
        while (offset < count)
        {
            int read = await stream.ReadAsync(buffer, offset, count - offset, token);
            if (read == 0) throw new EndOfStreamException("Connection dropped prematurely.");
            offset += read;
        }
    }
}
