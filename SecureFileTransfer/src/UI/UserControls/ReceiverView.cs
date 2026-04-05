using System;
using System.Drawing;
using System.IO;
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

    public event Action<int, string>? TransferProgressChanged;
    public event Action<string>? RequestOpenFolder;

    private TextBox txtVaultPath = default!;
    private ModernButton btnBrowseVault = default!;
    
    private Label lblIncomingStatus = default!;
    private TextBox txtKey = default!;
    private ModernButton btnDecrypt = default!;
    
    public ReceiverView(FileTransferManager manager, AppConfig config)
    {
        _manager = manager;
        _config = config;
        InitializeComponent();

        _manager.OnTransferInitReceived += (meta) => {
            InvokeSafe(() => {
                lblIncomingStatus.Text = $"Đang nhận: {meta.FileName}...";
                lblIncomingStatus.ForeColor = ThemeColors.Warning;
            });
        };

        _manager.OnEncryptedFileReady += (senderName) => {
            InvokeSafe(() => {
                lblIncomingStatus.Text = $"Hoàn tất tải về tự động! Đã nhận từ {senderName} \n\nNơi chứa gốc: {_manager.TempReceivingPath}";
                lblIncomingStatus.ForeColor = ThemeColors.Success;
                btnDecrypt.Enabled = true;
                txtKey.Enabled = true;
            });
        };
    }

    private void InvokeSafe(Action action)
    {
        if (this.IsHandleCreated) {
            if (this.InvokeRequired) this.Invoke(action);
            else action();
        }
        else {
            try { action(); } catch { } 
        }
    }

    private ModernButton btnBrowseLocal = default!;

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

        var lblHeader = new Label { 
            Name = "lblHeader", Text = "Nhận & Giải mã Tự động từ Hub", 
            Font = ThemeColors.HeaderFont, ForeColor = ThemeColors.TextAccent, 
            AutoSize = true, Margin = new Padding(0, 0, 0, 20) 
        };
        layout.Controls.Add(lblHeader, 0, 0);

        ThemeColors.ThemeChanged += ApplyTheme;

        var pnlContent = new FlowLayoutPanel { 
            Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(0, 0, 20, 0)
        };
        layout.Controls.Add(pnlContent, 0, 1);

        // Vault Setup Card
        var cardVault = new ModernCard { Width = 750, Height = 130, Margin = new Padding(0, 0, 0, 20) };
        cardVault.Controls.Add(new Label { Text = "THƯ MỤC CẤT GIỮ TÀI LIỆU (VAULT)", ForeColor = ThemeColors.TextAccent, Font = ThemeColors.LabelFont, AutoSize = true, Location = new Point(20, 15) });
        
        txtVaultPath = new TextBox { Width = 550, BackColor = ThemeColors.InputBackground, ForeColor = ThemeColors.TextPrimary, BorderStyle = BorderStyle.FixedSingle, Location = new Point(20, 50), Font = new Font("Segoe UI", 12F), ReadOnly = true, Text = _manager.DefaultVaultPath };
        cardVault.Controls.Add(txtVaultPath);

        btnBrowseVault = new ModernButton { Text = "THAY ĐỔI", Width = 140, Height = 32, BackColor = ThemeColors.ButtonSecondary, ForeColor = Color.White, Font = new Font("Segoe UI", 10F, FontStyle.Bold), Location = new Point(590, 50) };
        btnBrowseVault.Click += (s, e) => {
            using var fbd = new FolderBrowserDialog { Description = "Chọn thư mục bảo mật mặc định" };
            if (fbd.ShowDialog() == DialogResult.OK) {
                _manager.DefaultVaultPath = fbd.SelectedPath;
                txtVaultPath.Text = fbd.SelectedPath;
            }
        };
        cardVault.Controls.Add(btnBrowseVault);
        pnlContent.Controls.Add(cardVault);


        // Decrypt Wait Card
        var cardDecrypt = new ModernCard { Width = 750, Height = 250, Margin = new Padding(0, 0, 0, 20) };
        cardDecrypt.Controls.Add(new Label { Text = "TRẠM CHỜ NHẬN TỆP TIN TỪ HUB SERVER", ForeColor = ThemeColors.TextAccent, Font = ThemeColors.LabelFont, AutoSize = true, Location = new Point(20, 20) });
        
        btnBrowseLocal = new ModernButton { Text = "CHỌN TỆP LOCAL", Width = 150, Height = 35, BackColor = ThemeColors.ButtonSecondary, ForeColor = Color.White, Font = new Font("Segoe UI", 9F, FontStyle.Bold), Location = new Point(580, 12) };
        btnBrowseLocal.Click += SelectLocalFileToDecrypt;
        cardDecrypt.Controls.Add(btnBrowseLocal);

        lblIncomingStatus = new Label { Text = "Chưa có tệp nào gửi đến bạn. Hệ thống đang lắng nghe...", ForeColor = ThemeColors.TextSecondary, Font = new Font("Segoe UI", 11F, FontStyle.Italic), AutoSize = true, Location = new Point(20, 60), MaximumSize = new Size(710, 0) };
        cardDecrypt.Controls.Add(lblIncomingStatus);

        int keyY = 130;
        var lblKeyPrompt = new Label { Text = "Nhập mật khẩu (Key) để giải mã tệp tin này:", ForeColor = ThemeColors.TextSecondary, Font = ThemeColors.BodyFont, AutoSize = true, Location = new Point(20, keyY) };
        cardDecrypt.Controls.Add(lblKeyPrompt);
        
        txtKey = new TextBox { Width = 710, BackColor = ThemeColors.InputBackground, ForeColor = ThemeColors.TextPrimary, BorderStyle = BorderStyle.FixedSingle, PasswordChar = '*', Location = new Point(20, keyY + 25), Font = new Font("Segoe UI", 12F), Enabled = false };
        cardDecrypt.Controls.Add(txtKey);

        // Tự động đẩy TextBox nhập mật khẩu và kéo giãn chiều cao Card khi đường dẫn dài sinh ra WordWrap
        lblIncomingStatus.SizeChanged += (s, e) => {
            int newY = lblIncomingStatus.Bottom + 20;
            lblKeyPrompt.Top = newY;
            txtKey.Top = newY + 25;
            cardDecrypt.Height = txtKey.Bottom + 20;
        };

        pnlContent.Controls.Add(cardDecrypt);

        var pnlActions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 80, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 15, 0, 0) };
        
        btnDecrypt = new ModernButton { Text = "GIẢI MÃ TỆP NHẬN", Width = 260, Height = 50, BackColor = ThemeColors.Primary, ForeColor = Color.White, Font = new Font("Segoe UI", 10F, FontStyle.Bold), Margin = new Padding(10, 0, 0, 0), Enabled = false };
        btnDecrypt.Click += async (s, e) => await DecryptActionAsync();

        pnlActions.Controls.Add(btnDecrypt);
        layout.Controls.Add(pnlActions, 0, 2);
    }

    private string? _localSelectedFile = null;

    private void SelectLocalFileToDecrypt(object? sender, EventArgs e)
    {
        using OpenFileDialog ofd = new() { Title = "Chọn tệp mã hóa (.enc) để giải mã", Filter = "Encrypted Files (*.enc)|*.enc|All Files (*.*)|*.*" };
        if (ofd.ShowDialog() == DialogResult.OK)
        {
            _localSelectedFile = ofd.FileName;
            lblIncomingStatus.Text = $"Đã chọn tệp Local: {Path.GetFileName(_localSelectedFile)}\n\nNơi chứa gốc: {_localSelectedFile}";
            lblIncomingStatus.ForeColor = ThemeColors.TextAccent;
            btnDecrypt.Enabled = true;
            txtKey.Enabled = true;
            btnDecrypt.Text = "GIẢI MÃ TỆP LOCAL";
        }
    }

    private async Task DecryptActionAsync() {
        if (string.IsNullOrEmpty(txtKey.Text)) { MessageBox.Show("Vui lòng nhập mật khẩu giải mã!"); return; }

        try {
            btnDecrypt.Enabled = false;
            txtKey.Enabled = false;
            
            string finalPath;
            if (!string.IsNullOrEmpty(_localSelectedFile))
            {
                // Giải mã file local custom
                finalPath = await _manager.DecryptLocalFileAsync(_localSelectedFile, txtKey.Text.Trim(), CancellationToken.None);
                _localSelectedFile = null;
            }
            else
            {
                // Giải mã file từ Hub
                finalPath = await _manager.DecryptReadyFileAsync(txtKey.Text.Trim(), CancellationToken.None);
            }
            
            TransferProgressChanged?.Invoke(100, $"Giải mã hoàn tất: {Path.GetFileName(finalPath)}");
            RequestOpenFolder?.Invoke(finalPath);
            MessageBox.Show($"Giải mã thành công!\nTệp đã được lưu tại hộp Vault:\n{finalPath}", "Hoàn tất", MessageBoxButtons.OK, MessageBoxIcon.Information);
            
            lblIncomingStatus.Text = "Chưa có tệp nào gửi đến bạn. Hệ thống đang lắng nghe...";
            lblIncomingStatus.ForeColor = ThemeColors.TextSecondary;
            txtKey.Text = "";
            btnDecrypt.Text = "GIẢI MÃ TỆP NHẬN";
        }
        catch (CryptographicException cx) { 
            MessageBox.Show($"Lỗi giải mã:\n{cx.Message}", "Cảnh báo bảo mật", MessageBoxButtons.OK, MessageBoxIcon.Error);
            btnDecrypt.Enabled = true;
            txtKey.Enabled = true;
        }
        catch (Exception ex) { 
            Logger.Log($"Lỗi: {ex.Message}");
            MessageBox.Show(ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error); 
            btnDecrypt.Enabled = true;
            txtKey.Enabled = true;
        }
    }

    private void ApplyTheme()
    {
        this.BackColor = ThemeColors.PanelSurface;
        this.ForeColor = ThemeColors.TextPrimary;

        foreach (Control c in this.Controls) {
            if (c is TableLayoutPanel tlp) {
                foreach (Control sub in tlp.Controls) {
                    if (sub is Label lbl && lbl.Name == "lblHeader") lbl.ForeColor = ThemeColors.TextAccent;
                    if (sub is FlowLayoutPanel flp) {
                        foreach (Control item in flp.Controls) {
                            if (item is ModernCard card) ApplyThemeToCard(card);
                        }
                    }
                }
            }
        }
    }

    private void ApplyThemeToCard(ModernCard card)
    {
        foreach (Control c in card.Controls) {
            if (c is Label lbl && lbl.Font == ThemeColors.LabelFont) lbl.ForeColor = ThemeColors.TextAccent;
            if (c is TextBox txt) { txt.BackColor = ThemeColors.InputBackground; txt.ForeColor = ThemeColors.TextPrimary; }
        }
    }
}
