using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SecureFileTransfer.Services;
using System.Diagnostics;

namespace SecureFileTransfer.Web.Controllers;

public class TransferController : Controller
{
    private readonly FileTransferManager _transferManager;
    private readonly DatabaseService _databaseService;
    private readonly ILogger<TransferController> _logger;
    private readonly string _tempFolder;

    public TransferController(FileTransferManager transferManager, DatabaseService databaseService, ILogger<TransferController> logger)
    {
        _transferManager = transferManager;
        _databaseService = databaseService;
        _logger = logger;
        _tempFolder = @"D:\aes\WebData";
        Directory.CreateDirectory(_tempFolder);
    }

    // Dashboard Home
    public IActionResult Index()
    {
        return RedirectToAction("Sender");
    }

    // Trạm Gửi (Sender) - GET
    public IActionResult Sender()
    {
        return View();
    }

    // Trạm Gửi (Sender) - POST
    [HttpPost]
    public async Task<IActionResult> SendFile(IFormFile file, string destinationIp, int port, string aesKey)
    {
        try
        {
            if (file == null || file.Length == 0)
                return BadRequest("Vui lòng chọn file để gửi.");

            if (string.IsNullOrWhiteSpace(destinationIp) || port <= 0 || port > 65535)
                return BadRequest("IP hoặc Port không hợp lệ.");

            if (string.IsNullOrWhiteSpace(aesKey) || aesKey.Length < 8)
                return BadRequest("Khóa AES phải có ít nhất 8 ký tự.");

            // Save uploaded file to temp location
            string tempFilePath = Path.Combine(_tempFolder, file.FileName);
            using (var stream = new FileStream(tempFilePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Encrypt and send
            var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
            await _transferManager.EncryptAndSendAsync(tempFilePath, destinationIp, port, aesKey, null, cts.Token);

            // Cleanup (commented for demo purposes - files are kept for verification)
            // if (System.IO.File.Exists(tempFilePath))
            //     System.IO.File.Delete(tempFilePath);

            TempData["Success"] = $"✓ Gửi file '{file.FileName}' thành công!";
            return RedirectToAction("Sender");
        }
        catch (TimeoutException ex)
        {
            _logger.LogError(ex, "Connection timeout");
            TempData["Error"] = "❌ Kết nối bị timeout. Kiểm tra IP và Port nhé.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Send error");
            TempData["Error"] = $"❌ Lỗi: {ex.Message}";
        }

        return RedirectToAction("Sender");
    }

    // Trạm Nhận (Receiver) - GET
    public IActionResult Receiver()
    {
        return View();
    }

    // Trạm Nhận (Receiver) - Start Listening
    [HttpPost]
    public async Task<IActionResult> StartReceiver(int port, string aesKey)
    {
        try
        {
            if (port <= 0 || port > 65535)
                return BadRequest("Port không hợp lệ.");

            if (string.IsNullOrWhiteSpace(aesKey) || aesKey.Length < 8)
                return BadRequest("Khóa AES phải có ít nhất 8 ký tự.");

            string saveFolder = Path.Combine(_tempFolder, "Received");
            Directory.CreateDirectory(saveFolder);

            var cts = new CancellationTokenSource(TimeSpan.FromMinutes(30));
            string resultPath = await _transferManager.ReceiveOnlyAsync(port, saveFolder, cts.Token);

            TempData["Success"] = $"✓ Nhận file mã hóa thành công!<br>File được lưu tại: {resultPath}<br>Hãy tải file .enc lên để giải mã nếu cần.";
            return RedirectToAction("Receiver");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Receiver error");
            TempData["Error"] = $"❌ Lỗi nhận file: {ex.Message}";
        }

        return RedirectToAction("Receiver");
    }

    // Decrypt local file
    [HttpPost]
    public async Task<IActionResult> DecryptFile(IFormFile file, string aesKey)
    {
        try
        {
            if (file == null || file.Length == 0)
                return BadRequest("Vui lòng chọn file để giải mã.");

            if (string.IsNullOrWhiteSpace(aesKey) || aesKey.Length < 8)
                return BadRequest("Khóa AES phải có ít nhất 8 ký tự.");

            string tempEncFile = Path.Combine(_tempFolder, file.FileName);
            string decryptedFile = Path.Combine(_tempFolder, Path.GetFileNameWithoutExtension(file.FileName) + "_decrypted");

            // Save uploaded file
            using (var stream = new FileStream(tempEncFile, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Decrypt
            var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            await _transferManager.LocalDecryptAsync(tempEncFile, decryptedFile, aesKey);

            // Cleanup temp encrypted file (commented for demo purposes - files are kept for verification)
            // if (System.IO.File.Exists(tempEncFile))
            //     System.IO.File.Delete(tempEncFile);

            // Return decrypted file as download
            var fileBytes = System.IO.File.ReadAllBytes(decryptedFile);
            var fileName = Path.GetFileNameWithoutExtension(file.FileName);
            return File(fileBytes, "application/octet-stream", $"{fileName}_decrypted");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Decrypt error");
            TempData["Error"] = $"❌ Lỗi giải mã: {ex.Message}";
        }

        return RedirectToAction("Receiver");
    }

    // Lịch sử Giao dịch (History) - GET
    public IActionResult History()
    {
        var logs = GetTransferLogs();
        return View(logs);
    }

    // Get transfer logs from database
    private List<TransferLogViewModel> GetTransferLogs()
    {
        var logs = new List<TransferLogViewModel>();
        try
        {
            using (SqlConnection conn = new("Server=127.0.0.1;Database=SV_Info;User ID=sa;Password=ChangeMe@123;TrustServerCertificate=True;"))
            {
                conn.Open();
                string query = "SELECT TOP 100 FileName, FileSize, SenderIp, ReceiverIp, Status, Timestamp FROM TransferLogs ORDER BY Timestamp DESC";

                using (SqlCommand cmd = new(query, conn))
                {
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            logs.Add(new TransferLogViewModel
                            {
                                FileName = reader["FileName"].ToString() ?? "",
                                FileSize = reader["FileSize"] is DBNull ? 0 : (long)reader["FileSize"],
                                SenderIp = reader["SenderIp"].ToString() ?? "",
                                ReceiverIp = reader["ReceiverIp"].ToString() ?? "",
                                Status = reader["Status"].ToString() ?? "",
                                Timestamp = reader["Timestamp"] is DBNull ? DateTime.MinValue : (DateTime)reader["Timestamp"]
                            });
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving transfer logs");
        }

        return logs;
    }

    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}

public class TransferLogViewModel
{
    public string FileName { get; set; } = "";
    public long FileSize { get; set; }
    public string SenderIp { get; set; } = "";
    public string ReceiverIp { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime Timestamp { get; set; }

    public string FileSizeFormatted => FormatBytes(FileSize);

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}

public class ErrorViewModel
{
    public string? RequestId { get; set; }
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}
