using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using SecureFileTransfer.Models;
using SecureFileTransfer.Utils;

namespace SecureFileTransfer.Services;

public class DatabaseService
{
    private readonly string _connectionString;

    public DatabaseService(IOptions<AppConfig> config)
    {
        var db = config.Value.Database;
        _connectionString = $"Server={db.Server};Database={db.DatabaseName};User ID={db.Uid};Password={db.Pwd};TrustServerCertificate=True;";
    }

    public async Task LogTransferAsync(string fileName, long fileSize, string senderIp, string receiverIp, string status)
    {
        try
        {
            using SqlConnection conn = new(_connectionString);
            await conn.OpenAsync();
            
            // Note: Table 'TransferLogs' should be created beforehand
            string query = "INSERT INTO TransferLogs (FileName, FileSize, SenderIp, ReceiverIp, Status, Timestamp) " +
                           "VALUES (@fileName, @fileSize, @senderIp, @receiverIp, @status, GETDATE())";
            
            using SqlCommand cmd = new(query, conn);
            cmd.Parameters.AddWithValue("@fileName", fileName);
            cmd.Parameters.AddWithValue("@fileSize", fileSize);
            cmd.Parameters.AddWithValue("@senderIp", senderIp);
            cmd.Parameters.AddWithValue("@receiverIp", receiverIp);
            cmd.Parameters.AddWithValue("@status", status);

            await cmd.ExecuteNonQueryAsync();
            Logger.Log("Transfer logged to database.");
        }
        catch (Exception)
        {
            // Silently fail if database is not configured/reachable
            // This prevents confusing the user since transfer still works
        }
    }
}
