using SecureFileTransfer.Security;
using SecureFileTransfer.Utils;
using System.Text;
using Xunit;

namespace SecureFileTransfer.Tests;

public class IntegrityAndSecurityTests : IDisposable
{
    private readonly AesService _aesService;
    private readonly string _testDir;
    private readonly string _originalFile;
    private readonly string _encryptedFile;
    private readonly string _decryptedFile;
    private readonly string _password = "TestPassword@123";

    public IntegrityAndSecurityTests()
    {
        _aesService = new AesService();
        _testDir = Path.Combine(Path.GetTempPath(), $"SecureFileTransfer_Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);

        _originalFile = Path.Combine(_testDir, "original.txt");
        _encryptedFile = Path.Combine(_testDir, "encrypted.enc");
        _decryptedFile = Path.Combine(_testDir, "decrypted.txt");
    }

    [Fact]
    public void SHA256_VerifyHashConsistency()
    {
        // Arrange
        string testContent = "Enterprise Secure Data - Hash Test 123!@#";
        File.WriteAllText(_originalFile, testContent);

        // Act
        string hash1 = HashHelper.ComputeSha256(_originalFile);
        string hash2 = HashHelper.ComputeSha256(_originalFile);

        // Assert
        Assert.Equal(hash1, hash2, StringComparer.OrdinalIgnoreCase);
        Assert.True(hash1.Length == 64); // SHA-256 is 64 hex characters
    }

    [Fact]
    public void SHA256_DifferentFilesHaveDifferentHashes()
    {
        // Arrange
        string file1 = Path.Combine(_testDir, "file1.txt");
        string file2 = Path.Combine(_testDir, "file2.txt");
        
        File.WriteAllText(file1, "Content A");
        File.WriteAllText(file2, "Content B");

        // Act
        string hash1 = HashHelper.ComputeSha256(file1);
        string hash2 = HashHelper.ComputeSha256(file2);

        // Assert
        Assert.NotEqual(hash1, hash2, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void SHA256_SingleBitChangeDetected()
    {
        // Arrange
        string testContent = "Important Data";
        File.WriteAllText(_originalFile, testContent);
        string originalHash = HashHelper.ComputeSha256(_originalFile);

        // Act - Modify content by 1 character
        File.WriteAllText(_originalFile, "Important Data!");
        string modifiedHash = HashHelper.ComputeSha256(_originalFile);

        // Assert
        Assert.NotEqual(originalHash, modifiedHash, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Encryption_FileHashChangesAfterEncryption()
    {
        // Arrange
        string testContent = "Secret Content";
        File.WriteAllText(_originalFile, testContent);
        string originalHash = HashHelper.ComputeSha256(_originalFile);

        // Act
        _aesService.EncryptFile(_originalFile, _encryptedFile, _password);
        string encryptedHash = HashHelper.ComputeSha256(_encryptedFile);

        // Assert
        Assert.NotEqual(originalHash, encryptedHash);
        Assert.True(File.Exists(_encryptedFile));
    }

    [Fact]
    public void Decryption_FileHashReturnsToOriginal()
    {
        // Arrange
        string testContent = "Sensitive Enterprise Data";
        File.WriteAllText(_originalFile, testContent);
        string originalHash = HashHelper.ComputeSha256(_originalFile);

        // Act
        _aesService.EncryptFile(_originalFile, _encryptedFile, _password);
        _aesService.DecryptFile(_encryptedFile, _decryptedFile, _password);
        string decryptedHash = HashHelper.ComputeSha256(_decryptedFile);

        // Assert
        Assert.Equal(originalHash, decryptedHash, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void LargeFile_EncryptionIntegrity()
    {
        // Arrange - Create 10MB test file
        byte[] largeData = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("A", 10 * 1024 * 1024)));
        File.WriteAllBytes(_originalFile, largeData);
        string originalHash = HashHelper.ComputeSha256(_originalFile);

        // Act
        _aesService.EncryptFile(_originalFile, _encryptedFile, _password);
        _aesService.DecryptFile(_encryptedFile, _decryptedFile, _password);
        string decryptedHash = HashHelper.ComputeSha256(_decryptedFile);

        // Assert
        Assert.Equal(originalHash, decryptedHash);
        Assert.Equal(largeData.Length, new FileInfo(_decryptedFile).Length);
    }

    [Fact]
    public void Tampering_CorruptedEncryptedFileCausesDecryptionFailure()
    {
        // Arrange - Create larger test file to ensure it's at least 200 bytes
        string largeContent = string.Concat(Enumerable.Repeat("Test Data Content ", 20));
        File.WriteAllText(_originalFile, largeContent);
        _aesService.EncryptFile(_originalFile, _encryptedFile, _password);

        // Act - Corrupt the encrypted file
        byte[] encryptedBytes = File.ReadAllBytes(_encryptedFile);
        
        // Only corrupt if file is large enough (after IV + salt)
        if (encryptedBytes.Length > 100)
        {
            encryptedBytes[100] ^= 0xFF; // Flip bits at offset 100
            File.WriteAllBytes(_encryptedFile, encryptedBytes);

            // Assert - Should throw exception due to HMAC verification failure
            Assert.ThrowsAny<Exception>(() =>
                _aesService.DecryptFile(_encryptedFile, _decryptedFile, _password));
        }
    }

    [Fact]
    public void SpecialCharacters_ValidEncryption()
    {
        // Arrange
        string specialContent = "Test!@#$%^&*()_+-=[]{}|;':\",./<>?\\n\\r\\t";
        File.WriteAllText(_originalFile, specialContent, Encoding.UTF8);

        // Act
        _aesService.EncryptFile(_originalFile, _encryptedFile, _password);
        _aesService.DecryptFile(_encryptedFile, _decryptedFile, _password);
        string result = File.ReadAllText(_decryptedFile, Encoding.UTF8);

        // Assert
        Assert.Equal(specialContent, result);
    }

    [Fact]
    public void BinaryFile_EncryptionIntegrity()
    {
        // Arrange - Create binary test data
        byte[] binaryData = new byte[1024];
        Random random = new Random(42);
        random.NextBytes(binaryData);
        File.WriteAllBytes(_originalFile, binaryData);
        string originalHash = HashHelper.ComputeSha256(_originalFile);

        // Act
        _aesService.EncryptFile(_originalFile, _encryptedFile, _password);
        _aesService.DecryptFile(_encryptedFile, _decryptedFile, _password);
        string decryptedHash = HashHelper.ComputeSha256(_decryptedFile);

        // Assert
        Assert.Equal(originalHash, decryptedHash);
        byte[] decryptedData = File.ReadAllBytes(_decryptedFile);
        Assert.Equal(binaryData, decryptedData);
    }

    [Fact]
    public void EmptyFile_HandledCorrectly()
    {
        // Arrange
        File.WriteAllText(_originalFile, "");

        // Act & Assert
        _aesService.EncryptFile(_originalFile, _encryptedFile, _password);
        _aesService.DecryptFile(_encryptedFile, _decryptedFile, _password);
        
        Assert.True(File.Exists(_decryptedFile));
        string content = File.ReadAllText(_decryptedFile);
        Assert.Empty(content);
    }

    [Fact]
    public void Unicode_ContentPreserved()
    {
        // Arrange - Vietnamese, Chinese, Arabic text
        string unicodeContent = "Hello 世界 مرحبا Xin chào हेलो שלום";
        File.WriteAllText(_originalFile, unicodeContent, Encoding.UTF8);

        // Act
        _aesService.EncryptFile(_originalFile, _encryptedFile, _password);
        _aesService.DecryptFile(_encryptedFile, _decryptedFile, _password);
        string result = File.ReadAllText(_decryptedFile, Encoding.UTF8);

        // Assert
        Assert.Equal(unicodeContent, result);
    }

    [Fact]
    public void PasswordStength_WeakPasswordAccepted()
    {
        // Note: Current implementation doesn't enforce password strength
        // This test documents current behavior
        string weakPassword = "123";
        File.WriteAllText(_originalFile, "Test");

        // Act & Assert - Should still work
        _aesService.EncryptFile(_originalFile, _encryptedFile, weakPassword);
        _aesService.DecryptFile(_encryptedFile, _decryptedFile, weakPassword);
        Assert.True(File.Exists(_decryptedFile));
    }

    [Fact]
    public void StressTest_HighSpeedStreaming()
    {
        // Arrange - 25MB Stress Test
        byte[] largeData = new byte[25 * 1024 * 1024];
        new Random(1337).NextBytes(largeData);
        File.WriteAllBytes(_originalFile, largeData);
        string originalHash = HashHelper.ComputeSha256(_originalFile);

        // Act
        _aesService.EncryptFile(_originalFile, _encryptedFile, _password);
        _aesService.DecryptFile(_encryptedFile, _decryptedFile, _password);
        string decryptedHash = HashHelper.ComputeSha256(_decryptedFile);

        // Assert
        Assert.Equal(originalHash, decryptedHash);
        Assert.Equal(largeData.Length, new FileInfo(_decryptedFile).Length);
    }

    [Fact]
    public void MultipleEncryptions_DifferentIVs()
    {
        // Arrange - Encrypt same file twice
        File.WriteAllText(_originalFile, "Same Content");
        string enc1 = Path.Combine(_testDir, "enc1.enc");
        string enc2 = Path.Combine(_testDir, "enc2.enc");

        // Act
        _aesService.EncryptFile(_originalFile, enc1, _password);
        _aesService.EncryptFile(_originalFile, enc2, _password);
        byte[] bytes1 = File.ReadAllBytes(enc1);
        byte[] bytes2 = File.ReadAllBytes(enc2);

        // Assert - Encrypted files should be different due to random IVs
        Assert.NotEqual(bytes1, bytes2);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, true);
        }
        catch { }
    }
}

public class InputValidationTests
{
    [Theory]
    [InlineData("192.168.1.1")]
    [InlineData("127.0.0.1")]
    [InlineData("8.8.8.8")]
    [InlineData("10.0.0.1")]
    public void ValidIP_Accepted(string ip)
    {
        Assert.True(InputValidator.IsValidIpAddress(ip));
    }

    [Theory]
    [InlineData("999.999.999.999")]
    [InlineData("192.168.1")]
    [InlineData("not.an.ip.address")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("192.168.1.256")]
    public void InvalidIP_Rejected(string ip)
    {
        Assert.False(InputValidator.IsValidIpAddress(ip));
    }

    [Theory]
    [InlineData("8080")]
    [InlineData("80")]
    [InlineData("443")]
    [InlineData("65535")]
    [InlineData("1")]
    public void ValidPort_Accepted(string port)
    {
        Assert.True(InputValidator.IsValidPort(port));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("65536")]
    [InlineData("-1")]
    [InlineData("99999")]
    [InlineData("notaport")]
    [InlineData("")]
    public void InvalidPort_Rejected(string port)
    {
        Assert.False(InputValidator.IsValidPort(port));
    }

    [Theory]
    [InlineData("ValidPass@123")]
    [InlineData("12345678")]
    [InlineData("abcdefgh")]
    public void StrongPassword_Accepted(string password)
    {
        Assert.True(InputValidator.IsValidPassword(password));
    }

    [Theory]
    [InlineData("short")]
    [InlineData("1234567")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void WeakPassword_Rejected(string password)
    {
        Assert.False(InputValidator.IsValidPassword(password));
    }
}
