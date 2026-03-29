using System.Security.Cryptography;
using System.Text;

namespace SecureFileTransfer.Utils;

public static class HashHelper
{
    public static string ComputeSha256(string filePath)
    {
        using SHA256 sha256 = SHA256.Create();
        using FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        byte[] hashBytes = sha256.ComputeHash(fs);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }

    public static byte[] GetKeyFromPassword(string password)
    {
        using SHA256 sha256 = SHA256.Create();
        return sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
    }
}

public static class Logger
{
    public static event Action<string>? OnLog;

    public static void Log(string message)
    {
        OnLog?.Invoke($"[{DateTime.Now:HH:mm:ss}] {message}");
    }
}
