# Module Mật Mã AES-256 – Encrypt-then-MAC (HMAC-SHA256)

## 1. Tổng Quan

Module `AesCryptographyService` thực hiện mã hóa/giải mã AES-256 hoàn toàn **tự viết từ đầu** (không dùng `System.Security.Cryptography.Aes`), kết hợp:

| Thành phần | Chi tiết |
|---|---|
| Lõi AES | `Aes256CoreImpl.cs` – tự cài đặt SubBytes, ShiftRows, MixColumns, AddRoundKey |
| Chế độ vận hành | CBC (`CbcModeOperations.cs`) – chaining block bằng XOR trước encrypt |
| Đệm | PKCS#7 – block cuối được padding đúng chuẩn, validated khi giải mã |
 | KDF | PBKDF2 (`Rfc2898DeriveBytes`) – **600.000 vòng lặp** (OWASP 2023), SHA-256 |
| Xác thực | HMAC-SHA256 – mô hình **Encrypt-then-MAC** |
| Key size | AES-128 / AES-192 / **AES-256** (tự động detect từ metadata khi giải mã) |

---

## 2. Định Dạng File Mã Hóa (.enc)

```
┌──────────────────────────────────────────────────────────────────┐
│ [MetaLen: 4B Big-Endian] [Metadata JSON: NB]                     │
│ [Salt: 16B] [IV: 16B]                                            │
│ [Ciphertext: MB  ← AES-CBC encrypted blocks]                     │
│ [HMAC-SHA256: 32B  ← EOF tag]                                    │
└──────────────────────────────────────────────────────────────────┘
```

**Metadata JSON** (`FileMetadata`) gồm: `FileName`, `FileSize`, `Sha256Hash`, `EncryptionType`, `KeySize`.

> **Lưu ý:** `MetadataLength` được ghi theo Big-Endian (`Array.Reverse`) để nhất quán cross-platform.

---

## 3. Phái Sinh Khoá (Key Derivation)

```csharp
// Một lần DeriveBytes duy nhất cho cả AES Key + HMAC Key
byte[] derivedBytes = PBKDF2(password, salt, iterations: 600_000, length: keyLength + 32);

byte[] aesKey  = derivedBytes[0..keyLength];   // 32B cho AES-256
byte[] hmacKey = derivedBytes[keyLength..];    // 32B cho HMAC
```

- **Salt:** 16 bytes ngẫu nhiên mạnh (`RandomNumberGenerator`) – sinh mới mỗi phiên
- **IV:** 16 bytes ngẫu nhiên mạnh – sinh mới mỗi phiên

---

## 4. Quy Trình Mã Hóa (1-Pass Streaming)

```
FileStream (plaintext)
    │
    ├─ Chunk 64KB ──→ cbcMode.EncryptRaw() ──→ FileStream (ciphertext)
    │                         │
    │                  hmac.TransformBlock()   ← ciphertext chunk bơm vào HMAC
    │
    └─ Final block ──→ AddPkcs7Padding() ──→ EncryptRaw() ──→ hmac.TransformBlock()
                                                                       │
                                                          hmac.TransformFinalBlock()
                                                                       │
                                                              Write HMAC[32B] → EOF
```

**HMAC feed toàn bộ theo thứ tự:**
```
MetadataLengthBytes(4) → MetadataJSON(N) → Salt(16) → IV(16) → Ciphertext(all chunks)
```

**Lưu ý kỹ thuật streaming:** Do AES-CBC yêu cầu block 16 bytes, plaintext được tích lũy trong `List<byte>` rồi flush đủ block. Block cuối cùng được padding PKCS#7 trước khi encrypt.

---

## 5. Quy Trình Giải Mã – Verify-then-Decrypt

### 5.1 Bước HMAC Verification (không giải mã trước)

```csharp
// 1. Seek về EOF−32 để đọc HMAC tag gốc
input.Seek(-HMAC_SIZE, SeekOrigin.End);
byte[] hmacReceived = ReadExactly(32);

// 2. Tái tạo HMAC trên cùng thứ tự byte như lúc mã hóa
hmacVerify.TransformBlock(metadataLengthBytes, ...)
hmacVerify.TransformBlock(metadataJson, ...)
hmacVerify.TransformBlock(salt, ...)
hmacVerify.TransformBlock(iv, ...)
// Đọc toàn bộ ciphertext theo chunk 64KB để hash (KHÔNG giải mã)
while (remaining > 0) { hmacVerify.TransformBlock(chunk, ...) }

// 3. So sánh constant-time
if (!ConstantTimeEquals(hmacComputed, hmacReceived))
    throw new CryptographicException("Lỗi HMAC: Mật khẩu sai hoặc dữ liệu bị giả mạo.");
```

### 5.2 Validation metadata length (chống OOM)

```csharp
if (metadataLength <= 0 || metadataLength > 4096)
    throw new CryptographicException($"Invalid metadata length: {metadataLength}.");
```

### 5.3 Chỉ giải mã sau khi HMAC pass

```csharp
// Seek về đầu ciphertext → decrypt từng chunk → strip PKCS#7 ở block cuối
input.Seek(encryptedDataStart, SeekOrigin.Begin);
while (processed < encryptedDataLength)
{
    byte[] decrypted = cbcMode.DecryptRaw(chunk);
    if (isLastBlock) decrypted = RemovePkcs7Padding(decrypted);
    output.Write(decrypted);
}
```

---

## 6. Bảo Mật Đảm Bảo

| Thuộc tính | Cơ chế |
|---|---|
| **Tamper Detection** | HMAC-SHA256 bao phủ 100% bytes output (Header + toàn bộ Ciphertext) |
| **Anti-Timing Attack** | `ConstantTimeEquals()` dùng XOR accumulation, không short-circuit |
| **Key Isolation** | AES key và HMAC key hoàn toàn độc lập, phái sinh từ cùng PBKDF2 |
| **Replay Attack** | Salt + IV ngẫu nhiên mới mỗi phiên → cùng file + cùng pass ≠ cùng ciphertext |
| **OOM Protection (metadata)** | Validate `metadataLength ≤ 4096` trước `new byte[N]` |
| **Seekable requirement** | `DecryptStreamAsync` yêu cầu `CanSeek = true` để đọc HMAC từ EOF |

---

## 7. Interface Công Khai

```csharp
public interface IAesCryptography
{
    // File-level (synchronous wrapper)
    void EncryptFile(string inputFile, string outputFile, string password, AesKeySize keySize);
    void DecryptFile(string inputFile, string outputFile, string password);

    // Stream-level (async, dùng khi truyền qua mạng)
    Task EncryptStreamAsync(Stream input, Stream output, string password,
                            string fileName, AesKeySize keySize, CancellationToken ct);
    Task DecryptStreamAsync(Stream input, Stream output, string password, CancellationToken ct);
}
```