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

public class FileMetadata
{
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string Sha256Hash { get; set; } = string.Empty;  // ✅ NEW: For integrity verification
}

public class TransferProgress
{
    public long BytesTransferred { get; set; }
    public long TotalBytes { get; set; }
    public double ProgressPercentage => TotalBytes > 0 ? (double)BytesTransferred / TotalBytes * 100 : 0;
    public double Speed { get; set; } // Bytes per second
    public TimeSpan RemainingTime { get; set; }
}
