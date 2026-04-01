# Secure File Transfer - ASP.NET Core MVC Web Dashboard

## 📋 Overview

This is a modern ASP.NET Core MVC web dashboard for the **Secure File Transfer** system. It provides a user-friendly interface for:

- **Trạm Gửi (Sender)** - Encrypt and send files to a remote receiver
- **Trạm Nhận (Receiver)** - Listen for incoming encrypted files and decrypt them
- **Lịch Sử (History)** - View all transfer transactions

## 🏗️ Project Structure

```
SecureFileTransfer/Web/
├── Program.cs                 # Application entry point with DI setup
├── appsettings.json          # Configuration file
├── Web.csproj               # Project file
├── Controllers/
│   └── TransferController.cs # Main controller handling all requests
└── Views/
    ├── Shared/
    │   ├── _Layout.cshtml     # Master layout (Bootstrap 5)
    │   ├── _ViewStart.cshtml  # View startup configuration
    │   ├── _ViewImports.cshtml# Shared imports
    │   └── Error.cshtml       # Error page
    └── Transfer/
        ├── Sender.cshtml      # Sender interface
        ├── Receiver.cshtml    # Receiver interface
        └── History.cshtml     # Transaction history
```

## 🚀 Quick Start

### Prerequisites

- .NET 8.0 SDK or later
- Visual Studio 2022 or VS Code
- SQL Server (for transaction logging)

### 1. Setup Database

Run this SQL script to create the TransferLogs table:

```sql
CREATE TABLE TransferLogs (
    id INT PRIMARY KEY IDENTITY(1,1),
    FileName NVARCHAR(255) NOT NULL,
    FileSize BIGINT NOT NULL,
    SenderIp NVARCHAR(50) NOT NULL,
    ReceiverIp NVARCHAR(50) NOT NULL,
    Status NVARCHAR(100) NOT NULL,
    Timestamp DATETIME DEFAULT GETDATE()
);
```

### 2. Configure Settings

Edit `appsettings.json`:

```json
{
  "AppConfig": {
    "DefaultIp": "127.0.0.1",
    "DefaultPort": 8080,
    "Database": {
      "Server": "your-server",
      "DatabaseName": "SV_Info",
      "Uid": "sa",
      "Pwd": "your-password"
    }
  }
}
```

### 3. Build & Run

```bash
cd SecureFileTransfer/Web
dotnet restore
dotnet build
dotnet run
```

The application will be available at: `https://localhost:5001`

## 🎯 Features

### Trạm Gửi (Sender)
- ✅ Input destination IP and port
- ✅ File upload with drag-and-drop support
- ✅ AES-256 encryption with HMAC-SHA256 integrity verification
- ✅ Real-time progress tracking
- ✅ Automatic logging to database

### Trạm Nhận (Receiver)
- ✅ Listen on specified port
- ✅ Automatic decryption of received files
- ✅ Local file decryption utility
- ✅ Progress indication
- ✅ Transaction logging

### Lịch Sử (History)
- ✅ View all transfer logs
- ✅ File size and timestamp information
- ✅ Transfer status indicators
- ✅ Auto-refresh every 5 seconds

## 🔐 Security Features

The web dashboard integrates with the existing **SecureFileTransfer** library:

- **AES-256 CBC** encryption
- **PBKDF2** key derivation (600,000 iterations)
- **HMAC-SHA256** authentication
- **Salt** per file (random, 16 bytes)
- **IV** per file (random, 16 bytes)

## 🎨 UI/UX

- **Modern Design**: Bootstrap 5 with custom styling
- **Responsive Layout**: Sidebar navigation + main content area
- **Dark Theme**: Professional enterprise dashboard
- **Vietnamese Language**: Fully localized interface
- **Real-time Feedback**: Success/error alerts with auto-dismiss

## 🔧 Dependency Injection

The `Program.cs` sets up all required services:

```csharp
builder.Services.AddScoped<IAesCryptography, AesService>();
builder.Services.AddScoped<ITcpClient, TcpSender>();
builder.Services.AddScoped<ITcpServer, TcpReceiver>();
builder.Services.AddScoped<DatabaseService>();
builder.Services.AddScoped<FileTransferManager>();
```

## 📝 API Endpoints

### Transfer Controller

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/Transfer/Index` | Dashboard home |
| GET | `/Transfer/Sender` | Sender page |
| POST | `/Transfer/SendFile` | Send encrypted file |
| GET | `/Transfer/Receiver` | Receiver page |
| POST | `/Transfer/StartReceiver` | Start listening |
| POST | `/Transfer/DecryptFile` | Decrypt local file |
| GET | `/Transfer/History` | View transaction logs |

## 🐛 Troubleshooting

### Connection Timeout
- Verify destination IP and port are correct
- Check firewall settings
- Ensure receiver is listening on the specified port

### Incorrect Password Error
- Verify sender and receiver use the same password
- Check for typos in the password field

### Database Connection Error
- Verify SQL Server is running
- Check connection string in `appsettings.json`
- Ensure the database and table exist

## 📦 Dependencies

- Microsoft.AspNetCore.App (8.0.0+)
- SecureFileTransfer.csproj (core library)
- Bootstrap 5.3.0
- Bootstrap Icons 1.11.0

## 🌐 Browser Support

- Chrome 90+
- Firefox 88+
- Safari 14+
- Edge 90+

## 📄 License

Same as the main SecureFileTransfer project.

## 👥 Author

Built as a modern web wrapper for the SecureFileTransfer system.

---

**Note**: This is a web interface wrapper around the existing SecureFileTransfer core library. All core functionality remains unchanged and secure.
