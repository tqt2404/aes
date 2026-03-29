using System;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows.Forms;
using SecureFileTransfer.Models;
using SecureFileTransfer.Services;
using SecureFileTransfer.UI.Styles;
using SecureFileTransfer.Utils;

namespace SecureFileTransfer.UI.UserControls;

public class SenderView : UserControl
{
    private readonly FileTransferManager _manager;
    private readonly AppConfig _config;

    private string _selectedFile = "";
    private bool _isReceiverReady = false; // Need a way to set this or check if it's local only

    // Events to update UI on MainForm
    public event Action<int, string>? TransferProgressChanged;
    public event Action<string>? RequestOpenFolder;

    // Controls
    private TextBox txtIp, txtPort, txtKey;
    private Label lblFileName;
    private Button btnSelectFile, btnSend, btnEncryptOnly;

    public SenderView(FileTransferManager manager, AppConfig config)
    {
        _manager = manager;
        _config = config;
        InitializeComponent();
    }

    // Public method to set Receiver Ready flag if testing locally
    public void SetReceiverReadyStatus(bool isReady)
    {
        _isReceiverReady = isReady;
    }

    private void InitializeComponent()
    {
        this.BackColor = ThemeColors.PanelSurface;
        this.ForeColor = ThemeColors.TextPrimary;
        this.Font = ThemeColors.BodyFont;
        this.Dock = DockStyle.Fill;
        this.Padding = new Padding(30);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Header
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Content
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Actions
        this.Controls.Add(layout);

        // Header
        var lblHeader = new Label { Text = "Hệ thống Gửi && Bảo mật Dữ liệu", Font = ThemeColors.HeaderFont, ForeColor = ThemeColors.TextAccent, AutoSize = true, Margin = new Padding(0, 0, 0, 20) };
        layout.Controls.Add(lblHeader, 0, 0);

        // Content Panel
        var pnlContent = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        layout.Controls.Add(pnlContent, 0, 1);

        // 1. File Selection
        pnlContent.Controls.Add(CreateGroupTitle("Cấu hình tệp tin"));
        
        var pnlFile = new FlowLayoutPanel { Width = 700, Height = 60, FlowDirection = FlowDirection.LeftToRight };
        btnSelectFile = CreateButton("Duyệt tệp tin...", ThemeColors.ButtonSecondary);
        btnSelectFile.Click += SelectFileClick;
        pnlFile.Controls.Add(btnSelectFile);

        lblFileName = new Label { Text = "Chưa có file nào được chọn", ForeColor = ThemeColors.Warning, Font = new Font("Segoe UI", 10F, FontStyle.Italic), AutoSize = true, Margin = new Padding(15, 10, 0, 0) };
        pnlFile.Controls.Add(lblFileName);
        pnlContent.Controls.Add(pnlFile);

        // 2. Network & Crypto Settings
        pnlContent.Controls.Add(CreateGroupTitle("Tham số truyền nhận & Bảo mật"));
        
        txtIp = CreateInput("Địa chỉ IP (Máy nhận):", _config.DefaultIp, pnlContent);
        txtPort = CreateInput("Cổng dịch vụ (Port):", _config.DefaultPort.ToString(), pnlContent);
        txtKey = CreateInput("Khóa bảo mật (AES-256 Key):", "", pnlContent, isPassword: true);

        // Actions Bottom
        var pnlActions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 70, FlowDirection = FlowDirection.RightToLeft };
        
        btnSend = CreateButton("MÃ HÓA && CHUYỂN TỆP", ThemeColors.Primary, width: 260);
        btnSend.Font = ThemeColors.TitleFont;
        btnSend.Click += async (s, e) => await SendActionAsync();
        
        btnEncryptOnly = CreateButton("LƯU TRỮ MÃ HÓA CỤC BỘ", ThemeColors.ButtonSecondary, width: 260);
        btnEncryptOnly.Click += async (s, e) => await EncryptLocalActionAsync();

        pnlActions.Controls.Add(btnSend);
        pnlActions.Controls.Add(btnEncryptOnly);
        layout.Controls.Add(pnlActions, 0, 2);

        // Drag & Drop
        this.AllowDrop = true;
        this.DragEnter += (s, e) => { if (e.Data!.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
        this.DragDrop += (s, e) => {
            if (e.Data!.GetData(DataFormats.FileDrop) is string[] f && f.Length > 0) {
                _selectedFile = f[0];
                lblFileName.Text = Path.GetFileName(_selectedFile);
                Logger.Log($"📦 Kéo thả tệp: {lblFileName.Text}");
            }
        };
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

    private Button CreateButton(string text, Color backColor, int width = 150) {
        var btn = new Button { Text = text, Width = width, Height = 45, BackColor = backColor, ForeColor = Color.White, Cursor = Cursors.Hand, FlatStyle = FlatStyle.Flat, Margin = new Padding(10, 0, 0, 0), Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
        btn.FlatAppearance.BorderSize = 0;
        return btn;
    }

    private void SelectFileClick(object? sender, EventArgs e) {
        using OpenFileDialog ofd = new() { Title = "Chọn tệp tin để gửi/mã hóa" };
        if (ofd.ShowDialog() == DialogResult.OK) { 
            _selectedFile = ofd.FileName; 
            lblFileName.Text = Path.GetFileName(_selectedFile); 
            lblFileName.ForeColor = ThemeColors.Success;
        }
    }

    private async Task SendActionAsync() {
        if (!ValidateInputs()) return;
        
        try {
            SetLoading(true);
            var p = new Progress<TransferProgress>(pr => { 
                TransferProgressChanged?.Invoke((int)pr.ProgressPercentage, $"Đang truyền: {pr.ProgressPercentage:F1}% | {pr.Speed / 1024:F1} KB/s");
            });
            
            await _manager.EncryptAndSendAsync(_selectedFile, txtIp.Text.Trim(), int.Parse(txtPort.Text), txtKey.Text.Trim(), p);
            TransferProgressChanged?.Invoke(100, "✅ Truyền mạng thành công!");
            MessageBox.Show("Gửi file thành công!", "Hoàn tất", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (SocketException ex) { HandleError("Lỗi Mạng", ex); }
        catch (IOException ex) { HandleError("Lỗi Tệp", ex); }
        catch (Exception ex) { HandleError("Lỗi Hệ Thống", ex); }
        finally { SetLoading(false); }
    }

    private async Task EncryptLocalActionAsync() {
        if (string.IsNullOrEmpty(_selectedFile)) { ShowWarning("Vui lòng chọn tệp tin!"); return; }
        if (string.IsNullOrEmpty(txtKey.Text)) { ShowWarning("Vui lòng nhập mật mã (Key) để mã hóa!"); return; }

        using SaveFileDialog sfd = new() { FileName = Path.GetFileName(_selectedFile) + ".enc", Filter = "Encrypted Files (*.enc)|*.enc" };
        if (sfd.ShowDialog() == DialogResult.OK) {
            try {
                SetLoading(true);
                await _manager.LocalEncryptAsync(_selectedFile, sfd.FileName, txtKey.Text);
                TransferProgressChanged?.Invoke(100, $"✅ Mã hóa nội bộ thành công tại: {sfd.FileName}");
                RequestOpenFolder?.Invoke(sfd.FileName);
                MessageBox.Show("Mã hóa thành công!", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { HandleError("Lỗi Mã hóa", ex); }
            finally { SetLoading(false); }
        }
    }

    private bool ValidateInputs() {
        string key = txtKey.Text.Trim();
        if (string.IsNullOrEmpty(_selectedFile)) { ShowWarning("Chưa chọn tệp!"); return false; }
        if (string.IsNullOrEmpty(key) || key.Length < 8) { ShowWarning("Mật mã phải có ít nhất 8 ký tự!"); return false; }
        if (!InputValidator.IsValidIpAddress(txtIp.Text)) { ShowWarning("IP tĩnh không hợp lệ!"); return false; }
        if (!InputValidator.IsValidPort(txtPort.Text)) { ShowWarning("Số cổng (Port) không hợp lệ (1-65535)!"); return false; }

        if ((txtIp.Text == "127.0.0.1" || txtIp.Text.ToLower() == "localhost") && !_isReceiverReady) {
            DialogResult dr = MessageBox.Show(
                "Bạn đang gửi qua Localhost (127.0.0.1) nhưng chưa khởi động 'Bên Nhận' (Listener)!\nTiếp tục gửi sẽ bị lỗi Connection Refused. Cố tình gửi?",
                "Cảnh báo", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (dr == DialogResult.No) return false;
        }

        return true;
    }

    private void SetLoading(bool isLoading) {
        btnSend.Enabled = !isLoading;
        btnEncryptOnly.Enabled = !isLoading;
        btnSelectFile.Enabled = !isLoading;
    }

    private void ShowWarning(string msg) => MessageBox.Show(msg, "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    private void HandleError(string prefix, Exception ex) {
        Logger.Log($"❌ {prefix}: {ex.Message}");
        MessageBox.Show($"{prefix}: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        TransferProgressChanged?.Invoke(0, "❌ " + prefix + " thất bại.");
    }
}
