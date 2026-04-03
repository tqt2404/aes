using System.Text.Json;
using System.Text;

namespace SecureFileTransfer.Models;

public enum TransferState
{
    Idle,
    Encrypting,
    Connecting,
    Sending,
    Receiving,
    Decrypting,
    Completed,
    Error
}

/// <summary>
/// AES key sizes supported by the system
/// </summary>
public enum AesKeySize
{
    AES128 = 128,
    AES192 = 192,
    AES256 = 256
}

public class FileMetadata
{
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string Sha256Hash { get; set; } = string.Empty;
    public int KeySize { get; set; } 

    /// <summary>
    /// Encryption type used (determines key derivation size)
    /// </summary>
    public AesKeySize EncryptionType { get; set; } = AesKeySize.AES256;

    public byte[] Serialize()
    {
        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(this));
    }

    public static FileMetadata? Deserialize(byte[] data)
    {
        if (data == null || data.Length == 0) return null;
        return JsonSerializer.Deserialize<FileMetadata>(Encoding.UTF8.GetString(data));
    }
}

public class TransferProgress
{
    public long BytesTransferred { get; set; }
    public long TotalBytes { get; set; }
    public double ProgressPercentage => TotalBytes > 0 ? (double)BytesTransferred / TotalBytes * 100 : 0;
    public double Speed { get; set; } // Bytes per second
    public TimeSpan RemainingTime { get; set; }
}
