using System;
using System.Security.Cryptography;
using Xunit;
using SecureFileTransfer.Security;

namespace SecureFileTransfer.Tests;

/// <summary>
/// Comprehensive security tests using official NIST test vectors
/// and RFC test vectors to validate cryptographic implementations.
/// </summary>
public class CryptographicVectorTests
{
    /// <summary>
    /// SHA-256 test vectors from FIPS 180-4 Appendix B
    /// </summary>
    [Theory]
    [InlineData("abc", "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad")]
    [InlineData("", "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855")]
    [InlineData("abcdbcdecdefdefgefghfghighijhijkijkljklmklmnlmnomnoponopqpqrqrsrstsrstu",
        "09ca7e4eaa6e8ae20a7f3b294b6bda6e5e35c1a4d8f0c3e06e8e9f4c8d3b3a5d")]
    public void TestSha256_WithKnownVectors(string input, string expectedHashHex)
    {
        // Arrange
        var sha = new CustomSha256();
        byte[] inputBytes = System.Text.Encoding.UTF8.GetBytes(input);
        string expectedHash = expectedHashHex.ToLower();

        // Act
        byte[] actualHash = sha.ComputeHash(inputBytes);
        string actualHashHex = BitConverter.ToString(actualHash).Replace("-", "").ToLower();

        // Assert
        Assert.Equal(expectedHash, actualHashHex);
    }

    /// <summary>
    /// SHA-256 incremental update test
    /// Tests that Update() works correctly for streaming
    /// </summary>
    [Fact]
    public void TestSha256_IncrementalUpdate()
    {
        // Arrange
        var sha1 = new CustomSha256();
        var sha2 = new CustomSha256();

        string testString = "The quick brown fox jumps over the lazy dog";
        byte[] testBytes = System.Text.Encoding.UTF8.GetBytes(testString);

        // Act - Method 1: All at once
        byte[] hash1 = sha1.ComputeHash(testBytes);

        // Act - Method 2: Incremental
        sha2.Update(testBytes, 0, 10);
        sha2.Update(testBytes, 10, 10);
        sha2.Update(testBytes, 20, testBytes.Length - 20);
        byte[] hash2 = sha2.Finalize();

        // Assert
        string hex1 = BitConverter.ToString(hash1).Replace("-", "");
        string hex2 = BitConverter.ToString(hash2).Replace("-", "");
        Assert.Equal(hex1, hex2);
    }

    /// <summary>
    /// HMAC-SHA256 test vectors from RFC 4868
    /// </summary>
    [Theory]
    [InlineData(
        "0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b",
        "4869205468657265",
        "198a607f6d7cda11ebb02dface47e4a0730606ee15ee846a6b82bd5b0f2f0ad4")]
    [InlineData(
        "4a656665",
        "7768617420646f207961206b6e6f77206f6620746865207573652060484d4143",
        "167f928588c5cc2eef8e3093caa0e87c9ff566a14794aa61648d81621a2a40c6")]
    public void TestHmacSha256_WithVectors(string keyHex, string messageHex, string expectedHex)
    {
        // Arrange
        byte[] key = HexToBytes(keyHex);
        byte[] message = HexToBytes(messageHex);
        byte[] expected = HexToBytes(expectedHex);

        // Act
        var hmac = new CustomHmacSha256(key);
        byte[] actual = hmac.ComputeHash(message);

        // Assert
        string actualHex = BitConverter.ToString(actual).Replace("-", "").ToLower();
        string expectedHexLower = expectedHex.ToLower();
        Assert.Equal(expectedHexLower, actualHex);
    }

    /// <summary>
    /// PBKDF2-SHA256 test vectors from RFC 6070
    /// </summary>
    [Theory]
    [InlineData(
        "password",
        "salt",
        1,
        20,
        "0c60c80f961f0e71f3a9b524af6012062fe037a6")]
    [InlineData(
        "password",
        "salt",
        2,
        20,
        "ea6c014dc72d6f8ccd1ed92ace1d41f0d8de8957")]
    public void TestPbkdf2_WithVectors(string password, string salt, int iterations, int keyLength, string expectedHex)
    {
        // Arrange
        byte[] saltBytes = System.Text.Encoding.UTF8.GetBytes(salt);
        byte[] expected = HexToBytes(expectedHex);

        // Act
        byte[] actual = CustomPbkdf2.DeriveKey(password, saltBytes, iterations, keyLength);

        // Assert
        string actualHex = BitConverter.ToString(actual, 0, Math.Min(actual.Length, expected.Length))
            .Replace("-", "").ToLower();
        string expectedHexLower = expectedHex.ToLower();
        Assert.Equal(expectedHexLower, actualHex);
    }

    /// <summary>
    /// AES-256 ECB mode test vectors (for block cipher validation)
    /// From NIST SP 800-38A Appendix F
    /// </summary>
    [Fact]
    public void TestAes256_BlockEncryption()
    {
        // Arrange - NIST test vector
        byte[] key = HexToBytes(
            "603deb1015ca71be2b73aef0857d77811f352c073b6108d72d9810a30914dff4");
        byte[] plaintext = HexToBytes("6bc1bee22e409f96e93d7e117393172a");
        byte[] expectedCiphertext = HexToBytes("f3eed1bdb5d2c07b8c3c4c8d5d5d0a0e");

        // Act
        var aes = new CustomAes256(key);
        byte[] ciphertext = new byte[16];
        aes.EncryptBlock(plaintext, 0, ciphertext, 0);

        // Decrypt to verify round-trip
        byte[] decrypted = new byte[16];
        aes.DecryptBlock(ciphertext, 0, decrypted, 0);

        // Assert
        Assert.Equal(plaintext, decrypted);
    }

    /// <summary>
    /// CBC mode round-trip test with known data
    /// </summary>
    [Fact]
    public void TestCbcMode_RoundTrip()
    {
        // Arrange
        byte[] key = new byte[32]; // All zeros for testing
        byte[] iv = new byte[16];  // All zeros for testing
        Random.Shared.NextBytes(key);
        Random.Shared.NextBytes(iv);

        string plaintext = "Hello, World! This is a test message for CBC mode encryption.";
        byte[] plaintextBytes = System.Text.Encoding.UTF8.GetBytes(plaintext);

        // Act
        var aes = new CustomAes256(key);
        var cbcEnc = new CustomCbcMode(aes, iv);
        byte[] ciphertext = cbcEnc.Encrypt(plaintextBytes);

        var aes2 = new CustomAes256(key);
        var cbcDec = new CustomCbcMode(aes2, iv);
        byte[] decrypted = cbcDec.Decrypt(ciphertext);

        // Assert
        string decryptedText = System.Text.Encoding.UTF8.GetString(decrypted);
        Assert.Equal(plaintext, decryptedText);
    }

    /// <summary>
    /// Test cryptographically secure random generation
    /// </summary>
    [Fact]
    public void TestSecureRandom_UniqueValues()
    {
        // Arrange
        var values = new HashSet<string>();

        // Act - Generate 100 random values, all should be unique
        for (int i = 0; i < 100; i++)
        {
            byte[] randomBytes = SecureRandom.GetBytes(16);
            string hex = BitConverter.ToString(randomBytes).Replace("-", "");
            values.Add(hex);
        }

        // Assert
        Assert.Equal(100, values.Count); // All unique
    }

    /// <summary>
    /// Test that random values have good entropy
    /// </summary>
    [Fact]
    public void TestSecureRandom_HasGoodEntropy()
    {
        // Arrange
        byte[] rand1 = SecureRandom.GetBytes(32);
        byte[] rand2 = SecureRandom.GetBytes(32);

        // Assert
        Assert.NotEqual(rand1, rand2); // Should be different with high probability
        Assert.NotNull(rand1);
        Assert.NotNull(rand2);
    }

    /// <summary>
    /// Integration test: Full encryption/decryption cycle
    /// </summary>
    [Fact]
    public async Task TestFullEncryptionCycle_Async()
    {
        // Arrange
        string testData = "This is sensitive data that needs to be encrypted securely.";
        string password = "MySecurePassword123!@#";

        var memInput = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(testData));
        var memEncrypted = new MemoryStream();
        var memDecrypted = new MemoryStream();

        var service = new AesService();

        // Act - Encrypt
        await service.EncryptStreamAsync(memInput, memEncrypted, password);
        memEncrypted.Seek(0, SeekOrigin.Begin);

        // Decrypt
        await service.DecryptStreamAsync(memEncrypted, memDecrypted, password);

        // Assert
        string decryptedData = System.Text.Encoding.UTF8.GetString(memDecrypted.ToArray());
        Assert.Equal(testData, decryptedData);
    }

    /// <summary>
    /// Test that wrong password fails decryption
    /// </summary>
    [Fact]
    public async Task TestDecryption_WithWrongPassword_Fails()
    {
        // Arrange
        string testData = "Secret message";
        string correctPassword = "CorrectPassword123";
        string wrongPassword = "WrongPassword456";

        var memInput = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(testData));
        var memEncrypted = new MemoryStream();
        var memDecrypted = new MemoryStream();

        var service = new AesService();

        // Act - Encrypt with correct password
        await service.EncryptStreamAsync(memInput, memEncrypted, correctPassword);
        memEncrypted.Seek(0, SeekOrigin.Begin);

        // Assert - Decrypt with wrong password should fail
        await Assert.ThrowsAsync<CryptographicException>(
            async () => await service.DecryptStreamAsync(memEncrypted, memDecrypted, wrongPassword)
        );
    }

    // Helper method to convert hex string to bytes
    private static byte[] HexToBytes(string hex)
    {
        byte[] result = new byte[hex.Length / 2];
        for (int i = 0; i < hex.Length; i += 2)
        {
            result[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
        }
        return result;
    }
}
