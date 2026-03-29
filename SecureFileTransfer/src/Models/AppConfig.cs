namespace SecureFileTransfer.Models;

public class AppConfig
{
    public string DefaultIp { get; set; } = "127.0.0.1";
    public int DefaultPort { get; set; } = 8080;
    public DatabaseConfig Database { get; set; } = new();
}

public class DatabaseConfig
{
    public string Server { get; set; } = "127.0.0.1";
    public string DatabaseName { get; set; } = "SV_Info";
    public string Uid { get; set; } = "sa";
    public string Pwd { get; set; } = Environment.GetEnvironmentVariable("APP_DB_PASSWORD") ?? "ChangeMe@123";
}
