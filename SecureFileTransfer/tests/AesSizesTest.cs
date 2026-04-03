using SecureFileTransfer.Models;
using SecureFileTransfer.Security;
using System.Text;
using Xunit;

namespace SecureFileTransfer.Tests;

public class AesSizesTest
{
    [Fact]
    public void TestAes128Encrypt_Decrypt()
    {
        // Arrange
        byte[] plaintext = Encoding.UTF8.GetBytes("Test data for AES-128 encryption");
        string password = "TestPassword123";
        byte[] salt = SecureRandom.GetBytes(16);

        // Act
        byte[] derivedKey = CustomPbkdf2.DeriveKey(password, salt, 100000, 16);
        var aes = AesFactory.CreateAes(derivedKey, AesKeySize.AES128);
        var cbc = new CustomCbcMode(aes, SecureRandom.GetBytes(16));

        byte[] encrypted = cbc.Encrypt(plaintext);
        byte[] decrypted = cbc.Decrypt(encrypted);

        // Assert
        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void TestAes192Encrypt_Decrypt()
    {
        // Arrange
        byte[] plaintext = Encoding.UTF8.GetBytes("Test data for AES-192 encryption");
        string password = "TestPassword123";
        byte[] salt = SecureRandom.GetBytes(16);

        // Act
        byte[] derivedKey = CustomPbkdf2.DeriveKey(password, salt, 100000, 24);
        var aes = AesFactory.CreateAes(derivedKey, AesKeySize.AES192);
        var cbc = new CustomCbcMode(aes, SecureRandom.GetBytes(16));

        byte[] encrypted = cbc.Encrypt(plaintext);
        byte[] decrypted = cbc.Decrypt(encrypted);

        // Assert
        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void TestAes256Encrypt_Decrypt()
    {
        // Arrange
        byte[] plaintext = Encoding.UTF8.GetBytes("Test data for AES-256 encryption");
        string password = "TestPassword123";
        byte[] salt = SecureRandom.GetBytes(16);

        // Act
        byte[] derivedKey = CustomPbkdf2.DeriveKey(password, salt, 100000, 32);
        var aes = AesFactory.CreateAes(derivedKey, AesKeySize.AES256);
        var cbc = new CustomCbcMode(aes, SecureRandom.GetBytes(16));

        byte[] encrypted = cbc.Encrypt(plaintext);
        byte[] decrypted = cbc.Decrypt(encrypted);

        // Assert
        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void TestAesFactory_KeyLengths()
    {
        // Act & Assert
        Assert.Equal(16, AesFactory.GetKeyLength(AesKeySize.AES128));
        Assert.Equal(24, AesFactory.GetKeyLength(AesKeySize.AES192));
        Assert.Equal(32, AesFactory.GetKeyLength(AesKeySize.AES256));
    }

    [Fact]
    public void TestAesFactory_RoundCounts()
    {
        // Act & Assert
        Assert.Equal(10, AesFactory.GetRoundCount(AesKeySize.AES128));
        Assert.Equal(12, AesFactory.GetRoundCount(AesKeySize.AES192));
        Assert.Equal(14, AesFactory.GetRoundCount(AesKeySize.AES256));
    }
}
