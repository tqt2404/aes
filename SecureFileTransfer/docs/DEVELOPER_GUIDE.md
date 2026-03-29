# 📘 Sổ tay Developer Mới Nhập Môn (Developer Onboarding Guide)

Chào mừng bạn đến với nhóm kỹ sư thiết kế core Backend & Security cho bộ máy **SecureFileTransfer**. Trong tài liệu chuẩn doanh nghiệp (Enterprise Standard) này, chúng tôi sẽ dẫn dắt bạn đi thẳng vào "tim" của mã nguồn.

---

## 🔄 1. Thiết Kế Kiến trúc Phần mềm (Micro-Layered Architecture)
Dự án **tuyệt đối không** sử dụng phương pháp nhồi nhét code bừa bãi. Code được tách thành các dự án/lớp chia tách rõ như sau:

*   `UI/ (Giao diện)`: Tầng duy nhất tương tác người dùng. **KHÔNG CHỨA LOGIC CỐT LÕI**. Tầng này tuân thủ kỹ thuật "Exception Pattern Matching". Mọi lỗi sẽ được ném lên bởi Controller dưới, và tầng này sẽ rẽ nhánh (Catch `SocketException` hay `CryptographicException` tuỳ ý).
*   `Services/ (Orchestrator - Nhạc trưởng)`: `FileTransferManager.cs`. Đây là cầu nối trung chuyển tệp tin. Lớp này nhận vào các Dependency Injection (`ITcpClient`, `ITcpServer`, `IAesCryptography`). Mở rộng cực kỳ dễ dàng khi bổ sung `IUdpClient` vào về sau.
*   `Network/ (TCP Cốt lõi)`: `TcpSender` và `TcpReceiver`. Khai báo nguyên lý Mạng Tĩnh chuẩn chỉ, tuân thủ `using NetworkStream` dọn dẹp cặn bã RAM.
*   `Security/ (Cơ quan Mật Mã)`: `AesService.cs`. Nơi ẩn chứa Trí tuệ của thuật toán Zero-Temp File siêu việt. Gót chân Achilles của mọi hệ bảo mật đã được xóa bỏ tại đây.

---

## ⚡ 2. Thuật toán Zero-Temp File (Tuyệt Mật)
Nếu là Coder mới, 90% các bạn mã hóa File to sẽ làm như sau (Tốn bộ nhớ x3):
1. Quét File A -> Mã hóa AES -> Đẩy ra file tạm `A.tmp`.
2. Quét file `A.tmp` lết lết tính mã chữ ký bảo vệ (MAC).
3. Gộp Mã chữ ký lấy từ thuật toán + Nhồi lại file hộc máu `A.tmp` -> Thành file `.enc`.
Bởi cách mã hóa này, Disk I/O tốn 3 lượt read/write. Ổ cứng HDD sẽ lăn ra chết. 

**Trong App Của Chúng Ta:**
Luồng Stream được xử lý một chạm (1-Pass) tinh khiết.
* FileStream Output được khởi tạo. 
* Ghi Header. Nhét Header vô luồng HMAC.
* Bọc Input bằng CryptoStream. Read tệp gốc 80KB -> Mã hóa -> Ném ra Output, và cũng nắn cục đó nhét vào `HMAC.TransformBlock()`. 
* Cuối chu kỳ (EndOfFile/EOF), Đóng chốt băm `HMAC.TransformFinalBlock()` ra 32 byte cuối cùng rớt đúng bộ nhớ. 

👉 Mở `AesService.cs` và xem vùng **Extract Method** (Mô-đun hàm 15 dòng) dưới đáy, bạn sẽ không khỏi kinh ngạc về sự vĩ đại của Single Responsibility Code (Nguyên lý Rắn SOLID).

---

## 🚦 3. Quy chuẩn Đóng code PUSH CODE (Coding Mores)
- **Luôn Luôn Guard Clauses:** Mở bất kỳ hàm nào `(string path, int age, ...)`, hãy đặt chốt cửa cho nó bằng các lệnh như `ArgumentException.ThrowIf...` đầu dòng 1. App văng báo Exception càng sớm càng bớt ung thư Debug.
- **Biến Implicit Type `var`:** Trong C#, vui lòng sử dụng `var` ở vế trái nếu bên vế phải đã quá rõ ràng về Type `(var list = new List<Apple>())`.
- **Tạo Sub-method (Extract Method):** Nếu bạn thấy khối Code của bạn dài quá 20 dòng. Xin hãy bôi đen nó -> Chột phải -> `Extract Method` ra hàm nhỏ (`Helper()`) cho dễ thở.

---

## 🧪 4. Check Test Trước Khởi Lệnh (Testing Ground)
Trái tim của Developer là Test rào. Các bài Test đã được dựng form sẵn tại `tests/SecureFileTransfer.Tests.csproj`. 
- Trong đó file `AesServiceTests.cs` là xương sống, chúng được code giả lập hành vi Man-In-The-Middle attack (Đứng móc data sửa 1 bit đuôi file lấy đi mã băm). 
- Bấm lệnh `dotnet test` - Nếu TẤT CẢ các file báo Passed 100%, bạn mới được phép PR (Pull Request).

Xin Cảm Ơn, chúc bạn có những dải Code Sạch Cấp Doanh nghiệp tại đây!
