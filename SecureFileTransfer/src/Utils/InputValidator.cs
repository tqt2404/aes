using System.Net;

namespace SecureFileTransfer.Utils;

/// <summary>
/// Provides input validation utilities for the application
/// </summary>
public static class InputValidator
{
    /// <summary>
    /// Validates IP address format (must be exactly 4 octets for IPv4 or valid IPv6)
    /// </summary>
    public static bool IsValidIpAddress(string ip)
    {
        if (string.IsNullOrWhiteSpace(ip)) return false;
        
        // Try to parse
        if (!IPAddress.TryParse(ip.Trim(), out var address)) return false;
        
        // For IPv4, ensure it has exactly 4 octets (not shortened versions like "192.168.1")
        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            // IPv4-only validation: must have exactly 4 parts
            string trimmed = ip.Trim();
            var parts = trimmed.Split('.');
            
            // Must have exactly 4 parts
            if (parts.Length != 4) return false;
            
            // Each part must be a valid number between 0-255
            foreach (var part in parts)
            {
                if (!byte.TryParse(part, out _)) return false;
            }
            
            return true;
        }
        
        // IPv6 is always valid if TryParse succeeded
        return true;
    }

    /// <summary>
    /// Validates port number range (1-65535)
    /// </summary>
    public static bool IsValidPort(string portStr)
    {
        if (!int.TryParse(portStr, out int port)) return false;
        return port >= 1 && port <= 65535;
    }

    /// <summary>
    /// Validates password strength (minimum: 8 characters)
    /// </summary>
    public static bool IsValidPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password)) return false;
        return password.Length >= 8;
    }

    /// <summary>
    /// Validates file path exists
    /// </summary>
    public static bool IsValidFilePath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return false;
        return File.Exists(filePath);
    }

    /// <summary>
    /// Validates folder path exists
    /// </summary>
    public static bool IsValidFolderPath(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath)) return false;
        return Directory.Exists(folderPath);
    }
}
