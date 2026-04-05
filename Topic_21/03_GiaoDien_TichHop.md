# Giao Diện & Trải Nghiệm Người Dùng (WinForms)

## 1. Công Nghệ & Kiến Trúc UI

- **Nền tảng:** .NET WinForms (Borderless custom-rendered window)
- **Pattern bắt lỗi:** Exception Pattern Matching theo từng loại (`CryptographicException`, `SocketException`, `IOException`, `OperationCanceledException`...) thay vì chuỗi if/else so sánh message
- **Theme:** Light / Dark mode chuyển đổi động qua `ThemeColors.ToggleTheme()` – event `ThemeChanged` broadcast tới toàn bộ control
- **Custom Controls:** `ModernButton`, `ModernProgressBar` (`src/UI/UserControls/ModernUIComponents.cs`)

---

## 2. Kiến Trúc Phân Tách View

UI chia 3 lớp tách bạch hoàn toàn:

```
MainForm  (Shell – Layout, Nav, Server toggle)
  ├── SenderView    (src/UI/UserControls/SenderView.cs)
  └── ReceiverView  (src/UI/UserControls/ReceiverView.cs)
```

**MainForm** chỉ giữ:
- Sidebar navigation (chuyển `SenderView` ↔ `ReceiverView`)
- `ModernProgressBar` + `lblStatus` – cập nhật tiến trình từ event `OnReceiveProgress`
- Nút **CHẠY/DỪNG SERVER NGẦM** – bật/tắt `CentralHubServer` trực tiếp trên cùng máy
- Log panel (`TextBox` read-only) nhận log từ `Logger.Log()`

**SenderView** chịu trách nhiệm:
- Chọn file, nhập mật khẩu, chọn AES key size (128/192/256)
- Chọn user đích từ danh sách online (cập nhật real-time từ sự kiện `OnOnlineListUpdated`)
- Gọi `FileTransferManager.EncryptAndSendAsync()` trên `Task.Run()`, báo cáo tiến trình qua `IProgress<TransferProgress>`

**ReceiverView** chịu trách nhiệm:
- Nhận tự động khi `OnEncryptedFileReady` kích hoạt (không cần thao tác thủ công)
- Nhập mật khẩu và gọi `DecryptReadyFileAsync()` để giải mã file đã nhận
- Chức năng **Giải mã file cục bộ** (`DecryptLocalFileAsync`) – mở file `.enc` độc lập với mạng

---

## 3. Luồng Khởi Động & Đăng Nhập Hub

Khi mở app lần đầu, `MainForm_Load` gọi `RequestLoginAsync()` hiện dialog:

| Trường | Mô tả |
|---|---|
| Hub IP | Địa chỉ CentralHubServer (mặc định `127.0.0.1`) |
| Tên hiển thị | Tên định danh trên mạng (random `User_XXXX` nếu bỏ trống) |
| Checkbox "Chạy kèm Server ngầm" | Nếu check → khởi `CentralHubServer.StartAsync()` trước khi connect |

```csharp
await _hubClient.ConnectAsync(ip, port: 5000, displayName);
// → Gửi Login frame → Server broadcast OnlineList mới tới tất cả clients
```

Nếu kết nối thất bại → popup báo lỗi, không đóng app.

---

## 4. Luồng Thao Tác End-to-End

### Gửi File (SenderView)
```
[Chọn file] → [Nhập pass + Key Size] → [Chọn user đích] → [BẤM GỬI]
      ↓
Guard Clauses (file tồn tại, pass không rỗng, user đã chọn)
      ↓
FileTransferManager.EncryptAndSendAsync()
      ↓
      ├── Tính SHA-256 hash của file gốc
      ├── SendFileInitAsync(metadata JSON)
      ├── EncryptStreamAsync → HubChunkStream → SendFileChunkAsync (chunk 64KB)
      └── SendFileTransferCompleteAsync (PayloadLength = 0)
```

### Nhận & Giải Mã (ReceiverView)
```
[Hub route FileTransferInit] → StartReceivingSession() + ghi .enc temp
[Hub route FileChunk × N]   → Write to FileStream
[Hub route FileChunk(0)]    → Đóng stream → popup "Có tệp mới!"
      ↓
[Người dùng nhập mật khẩu] → DecryptReadyFileAsync()
      ↓
      ├── DecryptStreamAsync (HMAC verify → AES decrypt)
      ├── VerifyIntegrityAsync (SHA-256 so khớp với metadata)
      ├── Xóa file .enc tạm
      └── Mở Explorer tại file đích
```

---

## 5. Cơ Chế An Toàn UI

| Tình huống | Cơ chế xử lý |
|---|---|
| Thao tác nặng (encrypt/decrypt) | Chạy trên `Task.Run()`, cập nhật UI qua `Invoke()` |
| Mất kết nối Hub | `OnDisconnected` → disable nút Gửi, hiện thông báo |
| File `.enc` nhận vượt giới hạn | `FileTransferManager` huỷ session, xóa tệp tạm, ghi log |
| Đường dẫn lưu file nguy hiểm | Path traversal check: `Path.GetFullPath(finalPath).StartsWith(VaultPath)` |
| App đóng khi server đang chạy | `btnClose.Click` gọi `_hubServer.Stop()` trước `Application.Exit()` |
| Tên file trùng lặp | Tự động thêm timestamp `HHmmss` vào tên file đích |

---

## 6. Tùy Chỉnh

Thư mục lưu file mặc định: `{AppContext.BaseDirectory}/Vault/`  
Cấu hình qua `appsettings.json` → section `AppConfig` → inject qua `IOptions<AppConfig>`.