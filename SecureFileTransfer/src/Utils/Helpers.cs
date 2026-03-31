using System.Security.Cryptography;
using System.Text;

namespace SecureFileTransfer.Utils;

public static class HashHelper
{
    public static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct = default)
    {
        using SHA256 sha256 = SHA256.Create();
        using FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        byte[] hashBytes = await sha256.ComputeHashAsync(fs, ct);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public static string ComputeSha256(string filePath)
    {
        return ComputeSha256Async(filePath).GetAwaiter().GetResult();
    }

    public static byte[] GetKeyFromPassword(string password)
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes(password));
    }
}

public static class Logger
{
    public static event Action<string>? OnLog;

    public static void Log(string message)
    {
        // Use Dispatcher or similar if called from non-UI thread in WPF, 
        // but here we just invoke the event.
        OnLog?.Invoke($"[{DateTime.Now:HH:mm:ss}] {message}");
    }
}
