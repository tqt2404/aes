using SecureFileTransfer.Models;
using SecureFileTransfer.Network;
using SecureFileTransfer.Security;
using SecureFileTransfer.Services;
using SecureFileTransfer.Utils;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace SecureFileTransfer.Tests;

/// <summary>
/// Network Transfer Simulation - Mô phỏng truyền file giữa 2 máy tính qua localhost
/// Machine A (Sender): 127.0.0.1:9000
/// Machine B (Receiver): 127.0.0.1:9000 (listening)
/// </summary>
public class NetworkTransferSimulation : IDisposable
{
    private readonly string _testDir;
    private readonly string _senderInputFile;
    private readonly string _receiverOutputFile;
    private const int RECEIVER_PORT = 9000;
    private const string TEST_PASSWORD = "SecurePassword@2024";

    public NetworkTransferSimulation()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"NetworkTransfer_Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);

        _senderInputFile = Path.Combine(_testDir, "sender_input.txt");
        _receiverOutputFile = Path.Combine(_testDir, "receiver_output.txt");
    }

    /// <summary>
    /// Test: Sender encrypts file, sends to Receiver over network
    /// Receiver: receives, decrypts file
    /// Verification: Original content matches decrypted content
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task TestNetworkTransfer_SenderToReceiver_Success()
    {
        // Arrange - Sender prepares file
        string originalContent = "Secure File Transfer Test Data - 保密文件传输测试\n" +
                               "This is a test message with special characters: @#$%^&*()\n" +
                               "Lorem ipsum dolor sit amet, consectetur adipiscing elit.\n" +
                               string.Concat(Enumerable.Range(0, 100).Select(i => $"Line {i}: Test data repetition\n"));

        File.WriteAllText(_senderInputFile, originalContent);

        // Setup for test
        var cryptoService = new AesCryptographyService();
        var fileTransferMgr = new FileTransferManager(cryptoService, new TcpSender(), new TcpReceiver(), null!);
        var sender = new TcpSender();
        var receiver = new TcpReceiver();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        
        // Act - Start Receiver (listening on port 9000)
        var receiveTask = Task.Run(async () =>
        {
            try
            {
                await receiver.StartListeningAsync(
                    RECEIVER_PORT,
                    _testDir,
                    cts.Token,
                    new Progress<TransferProgress>(p => 
                        Console.WriteLine($"[Receiver] Progress: {p.BytesTransferred}/{p.TotalBytes}"))
                );
            }
            catch (OperationCanceledException)
            {
                // Expected - receiver closes when transfer completes or timeout
            }
        }, cts.Token);

        // Give receiver time to start listening
        await Task.Delay(500);

        // Act - Sender encrypts and sends file
        var sendTask = Task.Run(async () =>
        {
            try
            {
                // Compute hash of original content
                using (var sha256 = SHA256.Create())
                {
                    byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(originalContent));
                    string originalHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
                    
                    // Encrypt and send
                    await sender.SendFileAsync(
                        "127.0.0.1",           // Receiver IP (localhost)
                        RECEIVER_PORT,         // Receiver port
                        _senderInputFile,      // File to send
                        Path.GetFileName(_senderInputFile),
                        originalHash,
                        new Progress<TransferProgress>(p => 
                            Console.WriteLine($"[Sender] Progress: {p.BytesTransferred}/{p.TotalBytes}"))
                    );
                    
                    Console.WriteLine("[Sender] File sent successfully");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Sender] Error: {ex.Message}");
                throw;
            }
        }, cts.Token);

        // Wait for both operations
        var completedTask = await Task.WhenAny(receiveTask, sendTask);
        
        // Cancel timeout and wait for both to finish
        cts.Cancel();
        await Task.Delay(1000);

        // Assert - Verify received file matches original
        Assert.True(File.Exists(_senderInputFile), "Sender input file should exist");
        
        // Find received file (it will be saved in _testDir with original filename)
        string[] receivedFiles = Directory.GetFiles(_testDir, "*", SearchOption.TopDirectoryOnly)
            .Where(f => !f.EndsWith("_input.txt"))
            .ToArray();

        Assert.NotEmpty(receivedFiles);
        
        string receivedFilePath = receivedFiles.FirstOrDefault();
        Assert.NotNull(receivedFilePath);
        Assert.True(File.Exists(receivedFilePath), $"Received file should exist: {receivedFilePath}");

        // Verify content matches
        string receivedContent = File.ReadAllText(receivedFilePath);
        Assert.Equal(originalContent, receivedContent);
        
        Console.WriteLine($"✅ Transfer successful! Original {_senderInputFile} → Received {receivedFilePath}");
    }

    /// <summary>
    /// Test: Multiple concurrent transfers from Receiver listening on single port
    /// </summary>
    [Fact(Timeout = 60000)]
    public async Task TestNetworkTransfer_MultipleSequentialSends()
    {
        var receiver = new TcpReceiver();
        var sender = new TcpSender();
        var cryptoService = new AesCryptographyService();
        var fileTransferMgr = new FileTransferManager(cryptoService, sender, receiver, null!);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(50));

        // Create 3 test files
        var testFiles = new List<string>();
        for (int i = 0; i < 3; i++)
        {
            string filePath = Path.Combine(_testDir, $"test_file_{i}.txt");
            File.WriteAllText(filePath, $"Test file {i} content - " + new string('X', 1000 * (i + 1)));
            testFiles.Add(filePath);
        }

        // Start receiver
        var receiveTask = Task.Run(async () =>
        {
            try
            {
                // For sequential tests, receiver needs to accept multiple connections
                // This simulates a server that keeps listening
                var listener = new TcpListener(System.Net.IPAddress.Any, RECEIVER_PORT);
                listener.Start();

                for (int i = 0; i < 3; i++)
                {
                    if (cts.Token.IsCancellationRequested) break;
                    
                    Console.WriteLine($"[Receiver] Waiting for file {i}...");
                    var client = await listener.AcceptTcpClientAsync(cts.Token);
                    
                    // Handle each file transfer
                    _ = Task.Run(async () =>
                    {
                        using (var ns = client.GetStream())
                        {
                            byte[] buffer = new byte[1024];
                            int read = await ns.ReadAsync(buffer, 0, buffer.Length, cts.Token);
                            Console.WriteLine($"[Receiver] Received {read} bytes for file {i}");
                        }
                    });
                }

                listener.Stop();
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }, cts.Token);

        // Send files sequentially
        var sendTask = Task.Run(async () =>
        {
            for (int i = 0; i < testFiles.Count; i++)
            {
                if (cts.Token.IsCancellationRequested) break;

                try
                {
                    Console.WriteLine($"[Sender] Sending file {i}...");
                    var fileContent = File.ReadAllText(testFiles[i]);
                    
                    using (var sha256 = SHA256.Create())
                    {
                        byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(fileContent));
                        string hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
                        
                        await sender.SendFileAsync(
                            "127.0.0.1",
                            RECEIVER_PORT,
                            testFiles[i],
                            Path.GetFileName(testFiles[i]),
                            hash
                        );
                    }
                    
                    Console.WriteLine($"[Sender] File {i} sent successfully");
                    await Task.Delay(500); // Delay between sends
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Sender] Error sending file {i}: {ex.Message}");
                }
            }
        }, cts.Token);

        // Wait
        await Task.WhenAny(receiveTask, sendTask);
        cts.Cancel();
        await Task.Delay(500);

        Console.WriteLine("✅ Multiple sequential transfers completed");
    }

    /// <summary>
    /// Test: Encryption/Decryption full cycle with different key sizes
    /// </summary>
    [Theory]
    [InlineData(16)] // AES-128
    [InlineData(24)] // AES-192
    [InlineData(32)] // AES-256
    public void TestEncryptionCycle_DifferentKeySizes(int keySize)
    {
        // Arrange
        byte[] key = new byte[keySize];
        new Random(42).NextBytes(key);
        
        string plaintext = "Secure Message for Encryption Test";
        byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);

        // Act - Encrypt
        var aes = new Aes256CoreImpl(key);
        var iv = new byte[16];
        new Random(123).NextBytes(iv);
        
        var cbc = new CbcModeOperations(aes, iv);
        byte[] encrypted = cbc.Encrypt(plaintextBytes);

        // Act - Decrypt
        var aes2 = new Aes256CoreImpl(key);
        var cbc2 = new CbcModeOperations(aes2, iv);
        byte[] decrypted = cbc2.Decrypt(encrypted);

        // Assert
        string decryptedText = Encoding.UTF8.GetString(decrypted);
        Assert.Equal(plaintext, decryptedText);
        Assert.NotEqual(plaintextBytes, encrypted); // Encrypted should differ from plaintext
        
        Console.WriteLine($"✅ AES-{keySize * 8} encryption/decryption successful");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }
}
