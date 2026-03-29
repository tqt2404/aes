using System;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SecureFileTransfer.Models;
using SecureFileTransfer.Services;
using SecureFileTransfer.UI.Styles;
using SecureFileTransfer.Utils;
using System.Security.Cryptography;

namespace SecureFileTransfer.UI.UserControls;

public class ReceiverView : UserControl
{
    private readonly FileTransferManager _manager;
    private readonly AppConfig _config;

    private CancellationTokenSource? _receiveCts;
    private string _selectedEncFile = "";
    
    // Thuộc tính để cho MainForm biết mình đang nhận Network Request or Not
    public bool IsListening => _receiveCts != null;

    public event Action<int, string>? TransferProgressChanged;
    public event Action<string>? RequestOpenFolder;
    public event Action<bool>? ListeningStateChanged; // Gửi signal (để update UI hay SenderView)

    private TextBox txtPort, txtKey;
    private Label lblFileName;
    private Button btnSelectEncFile, btnReceiveNetwork, btnDecryptOnly;

    public ReceiverView(FileTransferManager manager, AppConfig config)
    {
        _manager = manager;
        _config = config;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this.BackColor = ThemeColors.PanelSurface;
        this.ForeColor = ThemeColors.TextPrimary;
        this.Font = ThemeColors.BodyFont;
        this.Dock = DockStyle.Fill;
        this.Padding = new Padding(30);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        this.Controls.Add(layout);

        var lblHeader = new Label { Text = "Hệ thống Nhận && Giải mã Dữ liệu", Font = ThemeColors.HeaderFont, ForeColor = ThemeColors.TextAccent, AutoSize = true, Margin = new Padding(0, 0, 0, 20) };
        layout.Controls.Add(lblHeader, 0, 0);

        var pnlContent = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        layout.Controls.Add(pnlContent, 0, 1);

        // Network Reception First
        pnlContent.Controls.Add(CreateGroupTitle("Cấu hình Trạm thu dữ liệu"));
        
        txtPort = CreateInput("Cổng mạng lắng nghe (Port):", _config.DefaultPort.ToString(), pnlContent);
        
        btnReceiveNetwork = CreateButton("KÍCH HOẠT TRẠM THU", ThemeColors.Success, width: 350);
        btnReceiveNetwork.Font = ThemeColors.TitleFont;
        btnReceiveNetwork.Click += async (s, e) => await ReceiveNetworkActionAsync();
        pnlContent.Controls.Add(btnReceiveNetwork);

        // Local Decryption Selection
        pnlContent.Controls.Add(CreateGroupTitle("Giải mã tệp tin lưu trữ (.enc)"));
        
        var pnlFile = new FlowLayoutPanel { Width = 700, Height = 60, FlowDirection = FlowDirection.LeftToRight };
        btnSelectEncFile = CreateButton("Duyệt tệp .enc...", ThemeColors.ButtonSecondary);
        btnSelectEncFile.Click += SelectEncFileClick;
        pnlFile.Controls.Add(btnSelectEncFile);

        lblFileName = new Label { Text = "Chưa có file nào được chọn", ForeColor = ThemeColors.Warning, Font = new Font("Segoe UI", 10F, FontStyle.Italic), AutoSize = true, Margin = new Padding(15, 10, 0, 0) };
        pnlFile.Controls.Add(lblFileName);
        pnlContent.Controls.Add(pnlFile);

        // Decrypt key
        txtKey = CreateInput("Khóa bảo mật để giải mã (Key):", "", pnlContent, isPassword: true);

        var pnlActions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 70, FlowDirection = FlowDirection.RightToLeft };
        btnDecryptOnly = CreateButton("GIẢI MÃ DỮ LIỆU", ThemeColors.Primary, width: 250);
        btnDecryptOnly.Font = ThemeColors.TitleFont;
        btnDecryptOnly.Click += async (s, e) => await DecryptLocalActionAsync();

        pnlActions.Controls.Add(btnDecryptOnly);
        layout.Controls.Add(pnlActions, 0, 2);
    }

    private Label CreateGroupTitle(string title) {
        return new Label { Text = title.ToUpper(), ForeColor = ThemeColors.TextSecondary, Font = new Font("Segoe UI", 10F, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 20, 0, 10) };
    }

    private TextBox CreateInput(string labelText, string defaultValue, Control parent, bool isPassword = false) {
        parent.Controls.Add(new Label { Text = labelText, AutoSize = true, Margin = new Padding(0, 0, 0, 5) });
        var t = new TextBox { 
            Text = defaultValue, 
            Width = 600,
            BackColor = ThemeColors.InputBackground, 
            ForeColor = ThemeColors.TextPrimary, 
            BorderStyle = BorderStyle.FixedSingle, 
            PasswordChar = isPassword ? '*' : '\0',
            Margin = new Padding(0, 0, 0, 15),
            Font = new Font("Segoe UI", 13F) 
        };
        parent.Controls.Add(t);
        return t;
    }

    private Button CreateButton(string text, Color backColor, int width = 180) {
        var btn = new Button { Text = text, Width = width, Height = 45, BackColor = backColor, ForeColor = Color.White, Cursor = Cursors.Hand, FlatStyle = FlatStyle.Flat, Margin = new Padding(10, 0, 0, 0), Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
        btn.FlatAppearance.BorderSize = 0;
        return btn;
    }

    private void SelectEncFileClick(object? sender, EventArgs e) {
        using OpenFileDialog ofd = new() { Filter = "Encrypted files (*.enc)|*.enc", Title = "Chọn tệp mã hóa" };
        if (ofd.ShowDialog() == DialogResult.OK) { 
            _selectedEncFile = ofd.FileName; 
            lblFileName.Text = Path.GetFileName(_selectedEncFile); 
            lblFileName.ForeColor = ThemeColors.Success;
        }
    }

    private async Task ReceiveNetworkActionAsync() {
        if (!InputValidator.IsValidPort(txtPort.Text)) { ShowWarning("Số cổng không hợp lệ (1-65535)!"); return; }

        using FolderBrowserDialog fbd = new() { Description = "Chọn thư mục bảo mật để nhận tệp" };
        if (fbd.ShowDialog() == DialogResult.OK) {
            try {
                SetLoading(true);
                btnReceiveNetwork.Text = "HỦY TRẠM THU";
                btnReceiveNetwork.BackColor = ThemeColors.Danger;
                btnReceiveNetwork.Enabled = true; 
                
                if (_receiveCts != null) {
                    _receiveCts.Cancel(); _receiveCts = null; return; 
                }

                _receiveCts = new CancellationTokenSource();
                ListeningStateChanged?.Invoke(true);

                TransferProgressChanged?.Invoke(0, "Đang khởi tạo Trạm thu tại port: " + txtPort.Text);
                var p = new Progress<TransferProgress>(pr => { 
                    TransferProgressChanged?.Invoke((int)pr.ProgressPercentage, $"Đang tiếp nhận dữ liệu: {pr.ProgressPercentage:F1}%");
                });

                string finalPath = await _manager.ReceiveOnlyAsync(int.Parse(txtPort.Text), fbd.SelectedPath, _receiveCts.Token, p);
                
                TransferProgressChanged?.Invoke(100, $"Tiếp nhận dữ liệu thành công: {finalPath}");
                RequestOpenFolder?.Invoke(finalPath);
            }
            catch (OperationCanceledException) { TransferProgressChanged?.Invoke(0, "Hệ thống đã dừng Trạm thu."); }
            catch (SocketException ex) { HandleError("Lỗi kết nối", ex); }
            catch (Exception ex) { HandleError("Lỗi hệ thống", ex); }
            finally { 
                SetLoading(false);
                btnReceiveNetwork.Text = "KÍCH HOẠT TRẠM THU";
                btnReceiveNetwork.BackColor = ThemeColors.Success;
                if (_receiveCts != null) { _receiveCts.Dispose(); _receiveCts = null; }
                ListeningStateChanged?.Invoke(false);
            }
        }
    }

    private async Task DecryptLocalActionAsync() {
        if (string.IsNullOrEmpty(_selectedEncFile)) { ShowWarning("Vui lòng chọn tệp .enc!"); return; }
        if (string.IsNullOrEmpty(txtKey.Text)) { ShowWarning("Vui lòng nhập Keyword để giải mã!"); return; }

        string ext = "";
        string suggested = Path.GetFileName(_selectedEncFile);
        if (suggested.EndsWith(".enc", StringComparison.OrdinalIgnoreCase)) {
            suggested = suggested.Substring(0, suggested.Length - 4);
            ext = Path.GetExtension(suggested);
        }

        using SaveFileDialog sfd = new() { 
            FileName = suggested,
            Filter = !string.IsNullOrEmpty(ext) ? $"{ext.Substring(1).ToUpper()} Files (*{ext})|*{ext}|All Files (*.*)|*.*" : "All Files (*.*)|*.*" 
        };

        if (sfd.ShowDialog() == DialogResult.OK) {
            try {
                SetLoading(true);
                await _manager.LocalDecryptAsync(_selectedEncFile, sfd.FileName, txtKey.Text);
                TransferProgressChanged?.Invoke(100, $"✅ Giải mã hoàn tất tại: {sfd.FileName}");
                RequestOpenFolder?.Invoke(sfd.FileName);
                MessageBox.Show("Giải mã thành công!", "Hoàn tất", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (CryptographicException ex) { HandleError("Cảnh báo Bảo Mật", ex, "Sai Key hoặc tệp đã bị giả mạo/hư hỏng!"); }
            catch (Exception ex) { HandleError("Lỗi Hệ Thống", ex); }
            finally { SetLoading(false); }
        }
    }

    private void SetLoading(bool isLoading) {
        btnDecryptOnly.Enabled = !isLoading;
        btnSelectEncFile.Enabled = !isLoading;
        if (!isLoading && _receiveCts == null) {
            btnReceiveNetwork.Enabled = true;
        }
    }

    private void ShowWarning(string msg) => MessageBox.Show(msg, "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    private void HandleError(string prefix, Exception ex, string? customMsg = null) {
        string error = customMsg ?? ex.Message;
        Logger.Log($"❌ {prefix}: {error}");
        MessageBox.Show($"{prefix}:\n{error}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        TransferProgressChanged?.Invoke(0, "❌ " + prefix + " thất bại.");
    }
}
