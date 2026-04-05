# Kiến Trúc Ứng Dụng – Clean Architecture & Dependency Injection

## 1. Tổng Quan

`SecureFileTransfer` được tổ chức theo mô hình **Layered Clean Architecture** kết hợp **Microsoft.Extensions.DependencyInjection**. Mạng (Network) và Bảo mật (Security) là hai lớp hoàn toàn tách biệt, giao tiếp với nhau qua Orchestrator (`FileTransferManager`) – không bao giờ phụ thuộc chéo.

```
┌─────────────────────────────────────────────────┐
│  UI Layer  (WinForms)                           │
│  MainForm → SenderView / ReceiverView           │
└────────────────────┬────────────────────────────┘
                     │ injects
┌────────────────────▼────────────────────────────┐
│  Orchestration Layer                            │
│  FileTransferManager                            │
└─────────┬────────────────┬───────────────────────┘
          │                │ injects
┌─────────▼──────┐ ┌───────▼──────────────────────┐
│ Security Layer │ │ Network Layer                 │
│ IAesCryptography│ │ HubTcpClient                 │
│ AesCryptography │ │ CentralHubServer             │
│ Service         │ │ NetworkMessage               │
└────────────────┘ └──────────────────────────────┘
```

---

## 2. Directory Layout Thực Tế

```text
src/
├── Models/
│   ├── Models.cs           # FileMetadata, TransferProgress, AesKeySize, TransferState
│   └── AppConfig.cs        # Cấu hình appsettings.json (VaultPath, ServerPort...)
│
├── Security/
│   ├── AesCryptographyService.cs   # Orchestrate encrypt/decrypt (HMAC + PBKDF2 + AES-CBC)
│   ├── Aes256CoreImpl.cs           # Tự cài đặt AES từ đầu (S-Box, FIPS-197)
│   ├── AesCipherFactory.cs         # Factory tạo IAes theo AesKeySize
│   ├── CbcModeOperations.cs        # CBC chaining (XOR + EncryptRaw/DecryptRaw)
│   └── CryptographyProvider.cs     # PBKDF2, RandomBytes wrapper
│
├── Network/
│   ├── NetworkMessage.cs           # Frame protocol (Command + SenderName + Payload)
│   ├── CentralHubServer.cs         # Hub server: route frames giữa các clients
│   └── HubTcpClient.cs             # Client: kết nối Hub, send/receive frames
│
├── Services/
│   ├── FileTransferManager.cs      # Orchestrator: ghép Security + Network + UI events
│   │   └── HubChunkStream          # Stream adapter: Write → SendFileChunkAsync
│   └── DatabaseService.cs          # SQLite log lịch sử truyền file
│
├── Utils/
│   ├── Helpers.cs                  # HashHelper (SHA-256), InputValidator
│   └── InputValidator.cs           # Guard Clauses dùng chung
│
├── UI/
│   ├── MainForm.cs                 # Shell: sidebar nav, progress bar, log panel
│   ├── UserControls/
│   │   ├── SenderView.cs           # UI gửi file (chọn file, pass, key size, user đích)
│   │   ├── ReceiverView.cs         # UI nhận + giải mã file
│   │   └── ModernUIComponents.cs   # ModernButton, ModernProgressBar
│   └── Styles/
│       └── ThemeColors.cs          # Light/Dark theme tokens
│
├── Program.cs                      # Entry point: Host + DI container setup
├── appsettings.json                # Cấu hình môi trường
└── SecureFileTransfer.csproj
```

---

## 3. Dependency Injection Container (Program.cs)

```csharp
Host.CreateDefaultBuilder()
    .ConfigureServices((ctx, services) => {
        services.Configure<AppConfig>(ctx.Configuration.GetSection("AppConfig"));

        // Security
        services.AddSingleton<IAesCryptography, AesCryptographyService>();

        // Network
        services.AddSingleton<HubTcpClient>();
        services.AddSingleton<CentralHubServer>();

        // Data
        services.AddSingleton<DatabaseService>();

        // Orchestration
        services.AddSingleton<FileTransferManager>();

        // UI
        services.AddSingleton<MainForm>(sp => new MainForm(
            sp.GetRequiredService<FileTransferManager>(),
            sp.GetRequiredService<HubTcpClient>(),
            sp.GetRequiredService<CentralHubServer>(),
            sp.GetRequiredService<IOptions<AppConfig>>()
        ));
    });
```

Toàn bộ singleton → đảm bảo một `HubTcpClient` duy nhất, một `CentralHubServer` duy nhất, không leak connection.

---

## 4. Bảo Mật Đa Tầng (Dual-Layer Security)

### Tầng 1 – HMAC-SHA256 (Integrity của Ciphertext)

```
Scope bảo vệ: [MetaLen][Metadata][Salt][IV][Ciphertext] ← toàn bộ
              └───────────────────────────────────────────┘
                              HMAC-SHA256 (32B ở EOF)
```

- Detect giả mạo / lỗi đường truyền ở mức **byte-for-byte**
- Dùng mô hình **Encrypt-then-MAC** → không bao giờ decrypt nếu MAC fail
- So sánh **constant-time** (`ConstantTimeEquals`) → miễn nhiễm timing attack

### Tầng 2 – SHA-256 (Integrity của Plaintext gốc)

```
Sender: hash = SHA256(original_file)     → ghi vào FileMetadata.Sha256Hash
Receiver: SHA256(decrypted_file) == hash → VerifyIntegrityAsync()
```

- Xác nhận file sau giải mã **byte-for-byte khớp** với file gốc trước mã hóa
- Phát hiện lỗi không phải do giả mạo mà do disk/network corruption

### Tầng 3 – Giới Hạn Đầu Vào (DoS / OOM Protection)

| Cơ chế | Giá trị | Vị trí |
|---|---|---|
| Payload chunk tối đa | 50 MB | `NetworkMessage.MaxPayloadLength` |
| String (tên file) tối đa | 1024 bytes | `NetworkMessage.MaxStringLength` |
| Metadata JSON tối đa | 4096 bytes | `AesCryptographyService.DecryptStreamAsync` |
| Tổng bytes nhận/session | `FileSize × 1.1`, hard cap 2 GB | `FileTransferManager.MaxSessionBytes` |

---

## 5. Nguyên Tắc Clean Code Áp Dụng

| Nguyên tắc | Biểu hiện trong code |
|---|---|
| **Guard Clauses** | `ArgumentException.ThrowIfNullOrWhiteSpace`, `ArgumentNullException.ThrowIfNull` ở đầu mọi public method |
| **Single Responsibility** | Network / Security / UI không chạm nhau trực tiếp |
| **Interface Segregation** | `IAesCryptography` – UI chỉ biết interface, không biết `Aes256CoreImpl` |
| **Dependency Inversion** | `FileTransferManager` nhận `IAesCryptography` qua constructor, không `new` trực tiếp |
| **`using` / `IDisposable`** | Toàn bộ `Stream`, `TcpClient`, `SemaphoreSlim` đều `Dispose` đúng lúc |
| **Async-all-the-way** | `async Task` xuyên suốt từ UI đến `NetworkStream.ReadAsync` |
| **Không ghi plaintext** | Hub chỉ relay bytes mã hóa, Vault chỉ lưu `.enc` cho đến khi user chủ động giải mã |