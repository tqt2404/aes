using SecureFileTransfer.Security;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace SecureFileTransfer.Tests;

public class AesServiceTests : IDisposable
{
    private readonly AesService _service;
    private readonly string _testInput = "test_input.txt";
    private readonly string _testEncrypted = "test.enc";
    private readonly string _testDecrypted = "test_decrypted.txt";
    private readonly string _password = "StrongPass123!";
    private readonly string _wrongPassword = "WrongPass456!";

    public AesServiceTests()
    {
        _service = new AesService();
        File.WriteAllText(_testInput, "Enterprise Secure Data 123");
    }

    [Fact]
    public void EncryptAndDecrypt_ShouldRestoreOriginalContent()
    {
        // Act
        _service.EncryptFile(_testInput, _testEncrypted, _password);
        _service.DecryptFile(_testEncrypted, _testDecrypted, _password);

        // Assert
        string original = File.ReadAllText(_testInput);
        string decrypted = File.ReadAllText(_testDecrypted);
        Assert.Equal(original, decrypted);
    }

    [Fact]
    public void Decrypt_WithWrongKey_ShouldThrowException()
    {
        // Arrange
        _service.EncryptFile(_testInput, _testEncrypted, _password);

        // Act & Assert
        // Now it throws Exception (or CryptographicException) because of HMAC failure or Decrypt failure
        Assert.ThrowsAny<Exception>(() => 
            _service.DecryptFile(_testEncrypted, _testDecrypted, _wrongPassword));
    }

    [Fact]
    public void TamperDetection_ShouldThrowException()
    {
        // Arrange
        _service.EncryptFile(_testInput, _testEncrypted, _password);

        // Tamper with the encrypted file (change a byte in the ciphertext area)
        byte[] data = File.ReadAllBytes(_testEncrypted);
        data[data.Length - 1] ^= 0xFF; 
        File.WriteAllBytes(_testEncrypted, data);

        // Act & Assert
        var ex = Assert.ThrowsAny<CryptographicException>(() => 
            _service.DecryptFile(_testEncrypted, _testDecrypted, _password));
        Assert.Contains("LỖI BẢO MẬT", ex.Message);
    }

    public void Dispose()
    {
        if (File.Exists(_testInput)) File.Delete(_testInput);
        if (File.Exists(_testEncrypted)) File.Delete(_testEncrypted);
        if (File.Exists(_testDecrypted)) File.Delete(_testDecrypted);
    }
}
