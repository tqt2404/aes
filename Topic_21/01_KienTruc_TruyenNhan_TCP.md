# Kiến Trúc Truyền/Nhận Dữ Liệu – Hub-and-Spoke TCP/IP

## 1. Tổng Quan Kiến Trúc

Hệ thống sử dụng mô hình **Hub-and-Spoke**: tất cả máy trạm (Client) đều kết nối vào một máy chủ trung tâm (`CentralHubServer`). Server không lưu trữ hay giải mã dữ liệu — chỉ định tuyến (route) gói tin nguyên vẹn từ Sender → Receiver theo tên đích (`TargetName`).

```
[Client A] ──→ [CentralHubServer :5000] ──→ [Client B]
[Client C] ──→                          ──→ [Client D]
```

**Thư viện:**
- `System.Net.Sockets` – `TcpListener`, `TcpClient`, `NetworkStream`
- `System.Threading.Tasks` – `async/await` toàn bộ stack I/O
- `System.Collections.Concurrent` – `ConcurrentDictionary` quản lý session đa luồng
- `System.Text.Json` – Serialize danh sách user online

---

## 2. Giao Thức Khung Tin (NetworkMessage Frame)

Mọi giao tiếp đều đóng gói qua `NetworkMessage` với cấu trúc nhị phân cố định:

```
[Cmd: 1B] [SenderLen: 4B] [Sender: NB] [TargetLen: 4B] [Target: NB] [PayloadLen: 4B] [Payload: NB]
```

| Trường | Kích thước | Mô tả |
|---|---|---|
| `Command` | 1 byte | Enum: `Login=1`, `OnlineListUpdate=2`, `FileTransferInit=3`, `FileChunk=4`, `Disconnect=5` |
| `SenderName` | 4B (len) + NB | Tên người gửi (UTF-8, tối đa **1024 bytes**) |
| `TargetName` | 4B (len) + NB | Tên đích định tuyến (UTF-8, tối đa **1024 bytes**) |
| `PayloadLength` | 4 bytes | Kích thước payload (0 = kết thúc phiên) |
| `Payload` | N bytes | Nội dung thực tế (chunk mã hóa, JSON metadata...) |

**Giới hạn bảo vệ (chống OOM/Abuse):**
```csharp
private const int MaxStringLength  = 1024;              // Tên file Unicode tiếng Việt
private const int MaxPayloadLength = 50 * 1024 * 1024;  // 50MB per single chunk (hard cap)
```
Nếu `PayloadLength < 0` hoặc `> 50MB` → ném `InvalidDataException` ngay lập tức, đóng session.

**Đọc khung tin chống packet-split (TCP fragmentation):**
```csharp
private static async Task ReadExactlyAsync(NetworkStream stream, byte[] buffer, int count, CancellationToken token)
{
    int offset = 0;
    while (offset < count)
    {
        int read = await stream.ReadAsync(buffer, offset, count - offset, token);
        if (read == 0) throw new EndOfStreamException("Connection dropped prematurely.");
        offset += read;
    }
}
```

---

## 3. CentralHubServer – Máy Chủ Định Tuyến

**File:** `src/Network/CentralHubServer.cs`

```csharp
// Lưu trữ tất cả session đang kết nối (thread-safe)
ConcurrentDictionary<string, ClientSession> _connectedClients

// Mỗi ClientSession bao gồm:
//   - TcpClient + NetworkStream
//   - SemaphoreSlim SendLock (1,1)  ← chống race condition ghi song song
```

**Luồng xử lý mỗi Client:**
1. Nhận `Login` → đăng ký tên, broadcast danh sách online mới tới tất cả
2. Nhận `FileTransferInit` / `FileChunk` → gọi `RouteMessageAsync()` chuyển tiếp nguyên gói tới `TargetName`
3. Nhận `Disconnect` / mất kết nối → xoá session, broadcast lại danh sách

**Ghi có khoá (SendLock):**
```csharp
await targetSession.SendLock.WaitAsync(cancellationToken);
try { await message.WriteToStreamAsync(targetSession.Stream, cancellationToken); }
finally { targetSession.SendLock.Release(); }
```

---

## 4. HubTcpClient – Máy Trạm

**File:** `src/Network/HubTcpClient.cs`

| Sự kiện | Mô tả |
|---|---|
| `OnOnlineListUpdated` | Cập nhật danh sách user đang online vào UI |
| `OnFileChunkReceived` | Nhận chunk mã hóa (FileTransferInit + FileChunk) |
| `OnDisconnected` | Trigger khi mất kết nối với Hub |

**Chống race condition ghi (SemaphoreSlim):**
```csharp
private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);

private async Task SendMessageSafeAsync(NetworkMessage msg, CancellationToken ct = default)
{
    await _sendLock.WaitAsync(ct);
    try { await msg.WriteToStreamAsync(_stream, ct); }
    finally { _sendLock.Release(); }
}
```

---

## 5. Luồng Truyền File End-to-End

```
Sender                        CentralHubServer                 Receiver
  │                                 │                               │
  ├──FileTransferInit(metadata)────→│──route──────────────────────→│
  │                                 │                               │ StartReceivingSession()
  ├──FileChunk(encrypted bytes)────→│──route──────────────────────→│ Write to .enc temp
  ├──FileChunk(encrypted bytes)────→│──route──────────────────────→│ Write to .enc temp
  ├──FileChunk(PayloadLen=0)───────→│──route──────────────────────→│ Transfer hoàn tất
  │                                 │                               │ → OnEncryptedFileReady
```

**Signal kết thúc:** Gửi một `FileChunk` có `PayloadLength = 0` (zero-byte chunk) → Receiver đóng `FileStream`, kích hoạt popup thông báo.

---

## 6. Tuân Thủ Kỹ Thuật

- **`using` / `IDisposable`:** Toàn bộ `FileStream`, `TcpClient`, `NetworkStream` đều nằm trong `using` block
- **`CancellationToken`:** Truyền xuyên suốt từ UI xuống tới `ReadExactlyAsync`
- **Guard Clauses:** `ArgumentException.ThrowIfNullOrWhiteSpace`, `ArgumentNullException.ThrowIfNull` ở đầu mọi public method
- **Không lưu plaintext:** Hub chỉ relay bytes mã hóa, không bao giờ thấy nội dung gốc