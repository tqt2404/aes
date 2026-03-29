<div align="center">
  <h1>🛡️ SecureFileTransfer (Enterprise Edition)</h1>
  <p><b>Hệ thống Truyền nhận Tệp tin Bảo mật Cấp độ Kỹ thuật Phần mềm (Software Engineering) Cao cấp</b></p>
</div>

---

## 📖 Tổng quan (Overview)
**SecureFileTransfer** là một ứng dụng Desktop (WinForms) mã nguồn mở, cho phép mã hóa và truyền tệp tin với mọi dung lượng qua mạng LAN/Internet bằng giao thức TCP/IP. Khác với các hệ thống đồ án bình thường, lõi của dự án đã được "Đại Tu (Refactoring)" tuân thủ 100% các tiêu chuẩn Doanh nghiệp khắt khe nhất: Pattern Matching, Thuật toán Mã hóa Zero-Temp File I/O nhanh gấp 3 lần, cùng Guard Clauses ở mọi Controller.

---

## ✨ Tính Năng Nổi Bật Kỹ Thuật (Enterprise Features)
- **🚀 Mã Hóa Siêu Tốc (1-Pass Zero-Temp CryptoStream):** Không giống các module mã hóa truyền thống phải lưu ra một tệp `.tmp` tạm thời làm nghẽn cổ chai ổ cứng. Thuật toán AES mới phân rã Data thành các Chunk và kẹp mã băm HMAC-SHA256 trên **cùng một luồng truyền tải bộ nhớ 1 chiều**. Tốc độ I/O ổ đĩa tăng gấp 3 lần, tiêu thụ cực thấp RAM.
- **🔐 Bảo Mật Kép (Dual-Layer Anti-Tamper Integrity):**
  - **Lớp HMAC (Network):** AES kết hợp chữ ký **HMAC-SHA256** ở vị trí đuôi khối dữ liệu (EOF). Bất kỳ 1 bit nào bị sửa đội bởi Hacker ở môi trường mạng trung gian (Man-in-the-middle) đều sẽ khiến hệ thống tự vệ văng Exception và khóa thuật toán giải mã ngay lập tức.
  - **Lớp Metadata JSON:** Payload đính kèm đối tượng Metadata băm gốc, giúp file tải về được đối soát 100% dung sai.
- **🛡️ Clean Code & Pattern Matching:** Mọi Endpoint tiếp nhận đều được áp **Guard Clauses** (`ArgumentException.ThrowIfNull...`). Hệ thống Validation của UI bắt lỗi thông qua **Exception Pattern Matching** C# (`CryptographicException`, `SocketException`), đảm bảo hiển thị đúng 100% lỗi nguyên bản mà không bị sụp TCP Session.
- **🔄 Dual-Session Asynchronous UI:** Giao diện phân vùng Mạng độc lập (SENDER và RECEIVER hoạt động song song trên cùng 1 phiên), có Guard Check để ngăn Receiver/Sender đá chéo nhau gây "Deadlock Connection" khi cài đặt Test Localhost. 

---

## 🛠 Yêu Cầu Môi Trường (Prerequisites)
- [x] **.NET 8.0 SDK** (Hoặc bản Runtime cho Desktop tương thích).
- [x] Visual Studio 2022 (nếu thao tác dev).

---

## 📥 Hướng Dẫn Biên Dịch (Build & Run)
Tại thư mục gốc của kho chứa (Repository):

```powershell
# 1. Tải và phục hồi các thư viện nuget 
dotnet restore

# 2. Xây dựng bản Release
dotnet build -c Release

# 3. Kích hoạt phần mềm Start
dotnet run --project src/SecureFileTransfer.csproj
```

**✅ Hệ Thống Unit Test Tự Động (CI/CD Quality Control):**
Dự án được bảo vệ bằng 45 Test cases đảm bảo các luồng Fake Stream giả mạo hay tấn công chỉnh sửa File đều bị khóa cứng.
```powershell
dotnet test tests/SecureFileTransfer.Tests.csproj
```

---

## 📖 Sổ Tay Sử Dụng Phím Nóng (Quick Usage)

Ứng dụng cung cấp bảng điều khiển "Kép (Dual)" cho mục đích thao diễn ngay trên 1 chiếc máy:

### 📥 1. Khởi tạo MÁY NHẬN (Receiver Mode)
1. Trong Panel RECEIVER (Màu xanh/đỏ), chọn Port nhận dữ liệu (Mặc định `8080`).
2. Nhấn nút **CHỈ NHẬN FILE .ENC**. Hộp thoại hiện lên hỏi bạn nơi cất các file Tải về.
3. Ứng dụng khóa cổng và chính thức "Lắng nghe" Mạng.

### 📤 2. Thao Tác MÁY GỬI (Sender Mode)
1. Phải đáp ứng điều kiện Máy Nhận đã mở trạng thái Lắng Nghe (để chống dội IP cục bộ).
2. Tới phần **Tùy chọn Tệp**, nhấn **Select File** để tải tư liệu nhạy cảm (MP4, PDF, Database...).
3. Nhập **IP** (Sử dụng `127.0.0.1` nếu gửi cục bộ) và **Port** (Khớp với Receiver). 
4. Cài đặt **Mật mã AES** (>8 kí tự) siêu bảo mật. (Ví dụ: `PassBaoMat@123`).
5. Bấm **MÃ HÓA & GỬI TỚI RECEIVER**. Module Manager sẽ thực thi thuật toán băm-âm-thầm (Zero-temp) và chuyển thẳng số dữ liệu AES qua TCP tới tay người nhận.

### 🔐 3. Bóc Tách Bản Quyền (Decryption Mode - Thao tác bởi Máy Nhận)
1. Khi file `.enc` đã được truyền xong 100%. Quay về phần "Xử Lý Nội Bộ".
2. Bấm "Select Encrypted File(.enc)" chỉ định tới file vừa tải hồi nãy.
3. Gõ siêu chính xác **Mật mã AES** từ MÁY GỬI mớm cho bạn.
4. Bấm **GIẢI MÃ NỘI BỘ**. Nút chốt chặn cuối cùng kiểm duyệt HMAC sẽ báo hiệu *Giải thành công* hoặc *Từ chối mở trói dữ liệu*.

---

## 📚 Tài Liệu Dành Cho Nhà Phát Triển (Onboarding Guide)
Bạn là Developer mới gia nhập Team Backend hoặc Security? Đừng lo, mọi nguyên lý Dependency Injection, Pattern luồng AES 1-pass và Sơ trúc Kiến tạo đều nằm trong Sổ tay của dev:

👉 **[Xem `docs/DEVELOPER_GUIDE.md` ngay để nắm bắt Cấu trúc Mã nguồn Core Hệ thống](docs/DEVELOPER_GUIDE.md)**
