# Kiến trúc Ứng dụng Bền Vững (Clean Architecture/DI)

**1. Tổng Quan Kiến Trúc Nền Tảng:**
Dự án `SecureFileTransfer` phải được kiến thiết theo mô thức Hướng Dịch vụ (Micro-Layer/Service). Bằng Dependency Injection, Mạng (Network) và Bảo mật (AES) là hai bánh răng hoán đổi dễ dàng mà lớp Giao diện không can thiệp sát vách. Code được làm sạch (Clean Code) 100% nhờ Extract Method và các luồng Guard Clauses.

**2. Sơ đồ Directory Layout:**

```text
src/
├── Models/                 # DTOs
│   ├── FileMetadata.cs     # Metadata đóng gói JSON (FileName, Size, SHA-256 Integrity)
│   └── TransferProgress.cs # Báo cáo % và Tốc độ Kbps
│
├── Security/               # Phân Hệ Cryptography 
│   ├── IAesCryptography.cs # Hợp đồng Thuật toán
│   └── AesService.cs       # Luồng Zero-Temp-File HMAC, ngắt khối phương thức sạch sẽ
│
├── Network/                # Phân Hệ TCP Sockets
│   ├── ITcpClient.cs       
│   ├── ITcpServer.cs       
│   ├── TcpSender.cs        # (Chứa Guard Clauses kiểm soát Input dòng đầu tiên)
│   └── TcpReceiver.cs      
│
├── Services/               # Nhạc trưởng (Orchestrator) 
│   └── FileTransferManager.cs # Tích hợp DI truyền khối data liên ngành
│
├── Utils/                  # Cụm Thư Viện Mini
│   ├── Logger.cs           
│   └── HashHelper.cs       # Hỗ trợ tính băm SHA-256 trên tầng logic Application
│
└── UI/                     # Presentation Layer
    └── MainForm.cs         # Pattern Matching kiểm soát UI và Event Handlers
```

**3. Tiêu chí Bảo Mật Toàn Vẹn Cấp Độ Cao Hết Cỡ (Dual-Layer Integrity):**
- Hiệu suất Vận hành: Cấu trúc 1-pass (Zero-Temp) cho hiệu năng Disk I/O cao gấp 3 lần bình thường do không nén file đệm. Phù hợp Transfer tệp hàng Gigabyte.
- **Tầng Băng thông AES:** 
  + Thả băm **HMAC-SHA256** xuống tận cùng khối tập tin (.enc) (EOF). Bảo chứng tuyệt đối luồng byte nguyên hiện trạng, không xô lệch 1-bit. Thuộc mô hình Anti-Tamper.
- **Tầng Logic File:** 
  + Trích xuất băm **SHA-256 Hash Gốc** đính vào tệp JSON cấp Server. Người nhận bung ngược ra so lại để tin tưởng 1000% rác hay lỗi quá trình download không làm rách file của họ.