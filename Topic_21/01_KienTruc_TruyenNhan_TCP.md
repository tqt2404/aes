# Yêu cầu phát triển: Khối Truyền/Nhận dữ liệu qua mạng (TCP/IP)

**1. Công nghệ & Thư viện:**
- Ngôn ngữ: C# (.NET 6 hoặc .NET 8).
- Thư viện mạng: `System.Net.Sockets`, `System.Net`.
- Xử lý luồng: Sử dụng `async/await` (`System.Threading.Tasks`) và `NetworkStream` để không làm đơ giao diện khi truyền tệp khối lượng lớn.
- Bắt lỗi an toàn: Sử dụng `ArgumentException` và Guard Clauses ở đỉnh hàm (Clean Code).

**2. Cấu trúc Client (Máy gửi):**
- Interface: `ITcpClient` và Class `TcpSender`.
- Nhiệm vụ: 
  + Kết nối đến IP và Port của máy nhận qua `TcpClient`.
  + Đóng gói Metadata tệp tin (Tên file, Kích thước, và Mã băm tính toàn vẹn SHA-256) thành một chuỗi JSON.
  + Truyền đi **4 bytes đầu tiên** chứa độ dài của khối JSON, tiếp đến là khối JSON Metadata.
  + Chuyển luồng đọc tệp tin đã mã hóa trực tiếp qua `NetworkStream`.

**3. Cấu trúc Server (Máy nhận):**
- Interface: `ITcpServer` và Class `TcpReceiver`.
- Nhiệm vụ:
  + Mở cổng bằng `TcpListener` và luôn kiểm tra điều kiện Port/IP bằng Guard Clauses.
  + Khi có kết nối, đọc chính xác 4 bytes cấu trúc để lấy độ dài Metadata JSON.
  + Khôi phục đối tượng `FileMetadata` từ chuỗi JSON.
  + Sử dụng vòng lặp `while` từ `NetworkStream` cho đến khi thu thập đủ số bytes báo cáo trong `FileSize`, ghi luồng này xuống file `.enc` tạm thời để chờ quy trình giải mã sau đó.

**4. Lưu ý về Tuân thủ Kỹ thuật:**
- Cấu hình Timeout: Cài đặt `ReadTimeout`, `WriteTimeout` ở Client và `CancellationToken` ở Server để chống treo khi đường truyền ngắt đột ngột.
- Tuân thủ cực đoan Clean Code: Mọi stream và luồng đều phải được khai báo trong ngữ cảnh `using` để giải phóng khi tác vụ kết thúc hoặc khi Exception nổ ra.