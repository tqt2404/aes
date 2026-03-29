# Yêu cầu phát triển: Giao diện và Trải nghiệm Người dùng (UI/UX)

**1. Công nghệ UI:**
- Nền tảng hiển thị: .NET WinForms.
- Tương tác Lỗi (Exception Pattern Matching): Bắt lỗi bằng cấu trúc So khớp mẫu theo từng định dạng (`CryptographicException`, `SocketException`, `IOException`...) thay vì if/else so sáng chuỗi, qua đó phân loại thông báo cực kỳ sát thực.

**2. Lược đồ Giao diện Tối giản (Dual-Session Tách bạch):**
- UI chia hai khu vực riêng: SENDER (Máy gửi) và RECEIVER (Máy nhận).
- Máy Nhận: Chỉ tồn tại 1 thao tác duy nhất với Network: **"CHỈ NHẬN FILE .ENC"**. Phần bung nén AES được di dời rành rẽ sang một Section "Xử lý Nội bộ". Thiết kế UX này giúp người dùng không loạn giữa Truyền Dữ liệu (Network) và Cấu hình Bảo mật (Crypto).
- Máy Gửi: Tích hợp ràng buộc Đồng bộ. Nếu Gửi nội bộ (IP Localhost), ứng dụng sẽ Validate trạng thái của Máy Nhận (Nếu chưa bật Listen -> Chặn bằng popup cảnh báo), phòng trừ tình trạng người dùng quên thao tác gây văng socket.

**3. Tóm lược Luồng Thao tác Ứng dụng:**
- **Sender:** Chọn tệp, nhập Mật khẩu và IP -> Validate IP/Port/Pass -> Bấm Gửi.
- Ứng dụng quét Validator đầu vào (Guard Clauses). Nếu Receiver Local chưa Listen -> Hiện popup yêu cầu Configure -> Chặn.
- Mã hóa bằng cơ chế 1-Pass cực tốc AES-HMAC ở hậu trường. Gọi `ITcpClient` truyền dữ liệu đi (Json Metadata + Mật lục AES).
- **Receiver:** Bấm "Nhận", mở sẵn vòng lặp `TcpListener`, tải dòng JSON -> Tính toán kích thước -> Tải Stream và cất kho đệm dưới đuôi `.enc`. (Nhả UI báo OK).
- **Receiver (Giải mã):** Chọn tệp `.enc` vừa nhận vào máy giải mã cục bộ. Điền lại đúng Mật khẩu cũ. Thuật toán rà soát cấu trúc EOF sinh băm. Nếu báo OK: Khôi phục ảnh gốc. Chặn đứng lỗi mạo danh 100%.