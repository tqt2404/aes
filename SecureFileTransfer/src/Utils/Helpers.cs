using System.Security.Cryptography;

namespace SecureFileTransfer.Utils;

/// <summary>
/// File hashing helper. Uses SHA-256 to compute integrity hashes of files on disk.
/// </summary>
public static class HashHelper
{
    public static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct = default)
    {
        using SHA256 sha256 = SHA256.Create();
        using FileStream fs = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        byte[] hashBytes = await sha256.ComputeHashAsync(fs, ct);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}

/// <summary>
/// Lightweight application logger. Subscribers (e.g. UI log panel) attach via OnLog event.
/// </summary>
public static class Logger
{
    public static event Action<string>? OnLog;

    public static void Log(string message) =>
        OnLog?.Invoke($"[{DateTime.Now:HH:mm:ss}] {message}");
}
