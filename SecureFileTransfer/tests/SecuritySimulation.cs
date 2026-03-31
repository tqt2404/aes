using System.Security.Cryptography;
using SecureFileTransfer.Security;
using Xunit;
using Xunit.Abstractions;

namespace SecureFileTransfer.Tests;

public class SecuritySimulation
{
    private readonly ITestOutputHelper _output;
    public SecuritySimulation(ITestOutputHelper output) => _output = output;

    [Fact]
    public void BitFlippingAttack_HMAC_Detection_Demo()
    {
        _output.WriteLine("=== KICH BAN: TAN CONG BIT-FLIPPING ===");
        var aes = new AesService();
        string password = "TopSecretPassword123!";
        string original = "DU LIEU MAT: Ngay 31.03.2026 thuc hien kiem tra tinh toan ven.";
        
        byte[] originalBytes = System.Text.Encoding.UTF8.GetBytes(original);
        using var msIn = new MemoryStream(originalBytes);
        using var msEnc = new MemoryStream();

        // 1. Ma hoa
        aes.EncryptStreamAsync(msIn, msEnc, password).Wait();
        byte[] encryptedBytes = msEnc.ToArray();
        _output.WriteLine($"[1] Da ma hoa du lieu. Kich thuoc: {encryptedBytes.Length} bytes.");

        // 2. Tan cong Bit-flip (Dao bit tai vi tri ciphertext)
        // Vi tri 40 nam sau Salt (16) va IV (16)
        int attackOffset = 40;
        encryptedBytes[attackOffset] ^= 0xFF;
        _output.WriteLine($"[2] DA CAN THIEP: Dao bit tai offset {attackOffset}.");

        // 3. Thu giai ma
        _output.WriteLine("[3] Dang thu giai ma tep tin da bi thau tom...");
        using var msToDec = new MemoryStream(encryptedBytes);
        using var msDec = new MemoryStream();

        var ex = Assert.ThrowsAsync<CryptographicException>(async () => 
            await aes.DecryptStreamAsync(msToDec, msDec, password)).Result;

        _output.WriteLine($"[4] KET QUA: He thong da ngan chan giai ma!");
        _output.WriteLine($"    Thong bao loi: {ex.Message}");
        
        Assert.Contains("Khóa không chính xác hoặc dữ liệu đã bị thay đổi", ex.Message);
    }
}
