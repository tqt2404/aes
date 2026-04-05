# Yêu cầu phát triển: Module Xử lý Mật mã Cao cấp (AES-256 & HMAC)

**1. Công nghệ & Thư viện:**
- Thư viện Cốt lõi: `System.Security.Cryptography`.
- Clean Code: Sử dụng Extract Method tách nhỏ phương thức, tuân thủ Trách nhiệm Đơn lẻ (Single Responsibility) và ép Validation đầu vào.

**2. Thiết lập Thuật toán Mật mã:**
- Hệ AES: Cơ chế Cipher Block Chaining (`CBC`), đệm chuẩn `PKCS7`.
- Tiêu chuẩn KDF (Key Derivation Function): Áp dụng `Rfc2898DeriveBytes` (PBKDF2) chạy vòng lặp 100,000 lần (Iterations) để đúc thành AES Key (32 bytes) và HMAC Key (32 bytes) từ Mật khẩu (String) do người dùng chọn.
- Randomness: Sinh ngẫu nhiên `Salt (16 bytes)` và `IV (16 bytes)` cho mọi phiên mã hóa.
aes bắt buộc viết từ đầu không được dùng thư viện có sẵn

**3. Quy trình Mã khóa Tối ưu 1-Pass (Zero-Temp-File):**
- Định dạng Tệp Mã hóa Đầu ra (Tension Format):
  + **Header:** `Salt (16)` + `IV (16)`
  + **Body:** `Ciphertext` (... bytes)
  + **Footer (EOF):** `HMAC-SHA256 (32)`
- Hành vi thực thi (Code Flow):
  + Ghi Header trực tiếp xuống FileStream đích. Bơm bản sao vào bộ băm HMAC.
  + Bọc luồng tệp tin gốc vào `CryptoStreamMode.Read` để thuật toán ngắt Ciphertext theo từng Chunk (định cỡ 80KB).
  + Ghi khối Ciphertext ra đĩa đồng thời nhét qua kênh `hmac.TransformBlock()` trên cùng 1 vòng lặp (1-pass), tuyệt đối không dùng tệp `.tmp`.
  + Ấn định `hmac.Hash` ngay tại đuôi tệp tin cuối cùng (EOF).

**4. Quy trình Giải khóa Định vị & Tamper Detection:**
- Truy xuất ngược (EOF Seek): Nhảy kim xuống 32 bytes dưới đáy để móc ra khối HMAC gốc do Sender gửi tới. Tiếp tục truy xuất 32 bytes đầu (Salt, IV).
- Xác thực Bất Khả xâm phạm (Encrypt-then-MAC): 
  + Tính toán lại thuật toán băm HMAC trên khối Header + BodyCipher. 
  + Xài hàm Constant-time `CryptographicOperations.FixedTimeEquals()` để đối chiếu chéo. Nếu chữ ký báo lỗi (Pass sai/File hỏng), ném văng `CryptographicException` cự tuyệt giải mã.
- Chỉ khi chữ ký xác thực, mới tiến hành bung khối AES Ciphertext.