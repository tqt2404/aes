using SecureFileTransfer.Models;
using SecureFileTransfer.Security;
using System.Text;
using Xunit;

namespace SecureFileTransfer.Tests;

/// <summary>
/// Tests for AES cipher with different key sizes (128, 192, 256-bit).
/// Refactored to use enterprise-named classes and standard .NET crypto libraries.
/// </summary>
public class AesSizesTest_Refactored
{
    [Fact]
    public void TestAes128Encrypt_Decrypt()
    {
        // Arrange
        byte[] plaintext = Encoding.UTF8.GetBytes("Test data for AES-128 encryption");
        string password = "TestPassword123";
        byte[] salt = CryptographyProvider.GetRandomBytes(16);
        byte[] iv = CryptographyProvider.GetRandomBytes(16);

        // Act
        byte[] derivedKey = CryptographyProvider.DeriveKeyFromPassword(password, salt, 100000, 16);
        var aes = AesCipherFactory.CreateAes(derivedKey, AesKeySize.AES128);
        var cbc = new CbcModeOperations(aes, iv);

        byte[] encrypted = cbc.Encrypt(plaintext);
        
        // For decryption, need new instance with same IV
        var aes2 = AesCipherFactory.CreateAes(derivedKey, AesKeySize.AES128);
        var cbc2 = new CbcModeOperations(aes2, iv);
        byte[] decrypted = cbc2.Decrypt(encrypted);

        // Assert
        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void TestAes192Encrypt_Decrypt()
    {
        // Arrange
        byte[] plaintext = Encoding.UTF8.GetBytes("Test data for AES-192 encryption");
        string password = "TestPassword123";
        byte[] salt = CryptographyProvider.GetRandomBytes(16);
        byte[] iv = CryptographyProvider.GetRandomBytes(16);

        // Act
        byte[] derivedKey = CryptographyProvider.DeriveKeyFromPassword(password, salt, 100000, 24);
        var aes = AesCipherFactory.CreateAes(derivedKey, AesKeySize.AES192);
        var cbc = new CbcModeOperations(aes, iv);

        byte[] encrypted = cbc.Encrypt(plaintext);
        
        // For decryption, need new instance with same IV
        var aes2 = AesCipherFactory.CreateAes(derivedKey, AesKeySize.AES192);
        var cbc2 = new CbcModeOperations(aes2, iv);
        byte[] decrypted = cbc2.Decrypt(encrypted);

        // Assert
        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void TestAes256Encrypt_Decrypt()
    {
        // Arrange
        byte[] plaintext = Encoding.UTF8.GetBytes("Test data for AES-256 encryption");
        string password = "TestPassword123";
        byte[] salt = CryptographyProvider.GetRandomBytes(16);
        byte[] iv = CryptographyProvider.GetRandomBytes(16);

        // Act
        byte[] derivedKey = CryptographyProvider.DeriveKeyFromPassword(password, salt, 100000, 32);
        var aes = AesCipherFactory.CreateAes(derivedKey, AesKeySize.AES256);
        var cbc = new CbcModeOperations(aes, iv);

        byte[] encrypted = cbc.Encrypt(plaintext);
        
        // For decryption, need new instance with same IV
        var aes2 = AesCipherFactory.CreateAes(derivedKey, AesKeySize.AES256);
        var cbc2 = new CbcModeOperations(aes2, iv);
        byte[] decrypted = cbc2.Decrypt(encrypted);

        // Assert
        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void TestAesCipherFactory_KeyLengths()
    {
        // Act & Assert
        Assert.Equal(16, AesCipherFactory.GetKeyLength(AesKeySize.AES128));
        Assert.Equal(24, AesCipherFactory.GetKeyLength(AesKeySize.AES192));
        Assert.Equal(32, AesCipherFactory.GetKeyLength(AesKeySize.AES256));
    }

    [Fact]
    public void TestAesCipherFactory_RoundCounts()
    {
        // Act & Assert
        Assert.Equal(10, AesCipherFactory.GetRoundCount(AesKeySize.AES128));
        Assert.Equal(12, AesCipherFactory.GetRoundCount(AesKeySize.AES192));
        Assert.Equal(14, AesCipherFactory.GetRoundCount(AesKeySize.AES256));
    }
}
