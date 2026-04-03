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
    private TextBox txtIp = default!, txtPort = default!, txtKey = default!;
    private ComboBox cmbAesSize = default!;
    private Label lblFileName = default!;
    private ModernButton btnSelectFile = default!, btnSend = default!, btnEncryptOnly = default!;

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
        var lblHeader = new Label { 
            Name = "lblHeader",
            Text = "Hệ thống Gửi & Bảo mật Dữ liệu", 
            Font = ThemeColors.HeaderFont, 
            ForeColor = ThemeColors.TextAccent, 
            AutoSize = true, 
            Margin = new Padding(0, 0, 0, 30) 
        };
        layout.Controls.Add(lblHeader, 0, 0);

        ThemeColors.ThemeChanged += ApplyTheme;

        // Content Panel
        var pnlContent = new FlowLayoutPanel { 
            Dock = DockStyle.Fill, 
            AutoScroll = true, 
            FlowDirection = FlowDirection.TopDown, 
            WrapContents = false,
            Padding = new Padding(0, 0, 20, 0)
        };
        layout.Controls.Add(pnlContent, 0, 1);

        // 1. File Selection Card
        var cardFile = new ModernCard { Width = 750, Height = 140, Margin = new Padding(0, 0, 0, 20) };
        cardFile.Controls.Add(CreateGroupTitle("CẤU HÌNH TỆP TIN"));
        
        btnSelectFile = CreateModernButton("Duyệt tệp tin...", ThemeColors.ButtonSecondary, 200, 45);
        btnSelectFile.Location = new Point(20, 60);
        btnSelectFile.Click += SelectFileClick;
        cardFile.Controls.Add(btnSelectFile);

        lblFileName = new Label { 
            Text = "Chưa có file nào được chọn", 
            ForeColor = ThemeColors.TextSecondary, 
            Font = ThemeColors.TitleFont, 
            AutoSize = true, 
            Location = new Point(235, 72)
        };
        cardFile.Controls.Add(lblFileName);
        pnlContent.Controls.Add(cardFile);

        // 2. Network Card
        var cardNetwork = new ModernCard { Width = 750, Height = 360, Margin = new Padding(0, 0, 0, 20) };
        cardNetwork.Controls.Add(CreateGroupTitle("THAM SỐ TRUYỀN NHẬN & BẢO MẬT"));
        
        int startY = 60;
        txtIp = CreateModernInput("Địa chỉ IP (Máy nhận):", _config.DefaultIp, cardNetwork, ref startY);
        txtPort = CreateModernInput("Cổng dịch vụ (Port):", _config.DefaultPort.ToString(), cardNetwork, ref startY);
        txtKey = CreateModernInput("Khóa bảo mật (Password):", "", cardNetwork, ref startY, isPassword: true);

        // AES Size Selection
        cardNetwork.Controls.Add(new Label
        {
            Text = "Cỡ khóa AES:",
            ForeColor = ThemeColors.TextSecondary,
            Font = ThemeColors.BodyFont,
            AutoSize = true,
            Location = new Point(20, startY)
        });

        cmbAesSize = new ComboBox
        {
            Text = "AES-256 (Bảo mật cao)",
            Width = 710,
            BackColor = ThemeColors.InputBackground,
            ForeColor = ThemeColors.TextPrimary,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(20, startY + 25),
            Font = new Font("Segoe UI", 12F)
        };
        cmbAesSize.Items.AddRange(new[]
        {
            "AES-128 (Nhanh)",
            "AES-192 (Cân bằng)",
            "AES-256 (Bảo mật cao)"
        });
        cmbAesSize.SelectedIndex = 2;  // Default: AES-256
        cardNetwork.Controls.Add(cmbAesSize);
        startY += 85;

        pnlContent.Controls.Add(cardNetwork);

        // Actions Bottom
        var pnlActions = new FlowLayoutPanel { 
            Dock = DockStyle.Bottom, 
            Height = 80, 
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 15, 0, 0)
        };
        
        btnSend = CreateModernButton("MÃ HÓA & CHUYỂN TỆP", ThemeColors.Primary, 260, 50);
        btnSend.Font = ThemeColors.TitleFont;
        btnSend.Click += async (s, e) => await SendActionAsync();
        
        btnEncryptOnly = CreateModernButton("LƯU TRỮ MÃ HÓA CỤC BỘ", ThemeColors.ButtonSecondary, 260, 50);
        btnEncryptOnly.Click += async (s, e) => await EncryptLocalActionAsync();

        pnlActions.Controls.Add(btnSend);
        pnlActions.Controls.Add(btnEncryptOnly);
        layout.Controls.Add(pnlActions, 0, 2);

        // Drag & Drop visual logic
        this.AllowDrop = true;
        this.DragEnter += (s, e) => { 
            if (e.Data!.GetDataPresent(DataFormats.FileDrop)) {
                e.Effect = DragDropEffects.Copy;
                cardFile.BackColor = Color.FromArgb(40, 40, 40);
            }
        };
        this.DragLeave += (s, e) => { cardFile.BackColor = ThemeColors.CardBackground; };
        this.DragDrop += (s, e) => {
            cardFile.BackColor = ThemeColors.CardBackground;
            if (e.Data!.GetData(DataFormats.FileDrop) is string[] f && f.Length > 0) {
                _selectedFile = f[0];
                lblFileName.Text = Path.GetFileName(_selectedFile);
                lblFileName.ForeColor = ThemeColors.TextAccent;
                Logger.Log($"Chọn tệp qua kéo thả: {lblFileName.Text}");
            }
        };
    }

    private Label CreateGroupTitle(string title) {
        return new Label { 
            Text = title, 
            ForeColor = ThemeColors.TextAccent, 
            Font = ThemeColors.LabelFont, 
            AutoSize = true, 
            Location = new Point(20, 20) 
        };
    }

    private TextBox CreateModernInput(string labelText, string defaultValue, Control parent, ref int startY, bool isPassword = false) {
        parent.Controls.Add(new Label { 
            Text = labelText, 
            ForeColor = ThemeColors.TextSecondary, 
            Font = ThemeColors.BodyFont, 
            AutoSize = true, 
            Location = new Point(20, startY) 
        });
        
        var t = new TextBox { 
            Text = defaultValue, 
            Width = 710,
            BackColor = ThemeColors.InputBackground, 
            ForeColor = ThemeColors.TextPrimary, 
            BorderStyle = BorderStyle.FixedSingle, 
            PasswordChar = isPassword ? '*' : '\0',
            Location = new Point(20, startY + 25),
            Font = new Font("Segoe UI", 12F) 
        };
        parent.Controls.Add(t);
        startY += 85;
        return t;
    }

    private ModernButton CreateModernButton(string text, Color backColor, int width, int height) {
        return new ModernButton { 
            Text = text, 
            Width = width, 
            Height = height, 
            BackColor = backColor, 
            ForeColor = Color.White, 
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Margin = new Padding(10, 0, 0, 0)
        };
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

            // Get selected AES size
            AesKeySize keySize = cmbAesSize.SelectedIndex switch
            {
                0 => AesKeySize.AES128,
                1 => AesKeySize.AES192,
                _ => AesKeySize.AES256
            };

            await _manager.EncryptAndSendAsync(_selectedFile, txtIp.Text.Trim(), int.Parse(txtPort.Text), txtKey.Text.Trim(), keySize, p);
            TransferProgressChanged?.Invoke(100, "Truyền nhận dữ liệu thành công.");
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

                // Get selected AES size
                AesKeySize keySize = cmbAesSize.SelectedIndex switch
                {
                    0 => AesKeySize.AES128,
                    1 => AesKeySize.AES192,
                    _ => AesKeySize.AES256
                };

                await _manager.LocalEncryptAsync(_selectedFile, sfd.FileName, txtKey.Text, keySize);
                TransferProgressChanged?.Invoke(100, $"Mã hóa nội bộ thành công: {Path.GetFileName(sfd.FileName)} (AES-{keySize})");
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
            MessageBox.Show(
                "Vui lòng khởi động 'Bên Nhận' (Listener) trước khi thực hiện gửi dữ liệu qua Localhost!",
                "Yêu cầu hệ thống", MessageBoxButtons.OK, MessageBoxIcon.Stop);
            return false;
        }

        return true;
    }

    private void SetLoading(bool isLoading) {
        btnSend.Enabled = !isLoading;
        btnEncryptOnly.Enabled = !isLoading;
        btnSelectFile.Enabled = !isLoading;
    }

    private void ApplyTheme()
    {
        this.BackColor = ThemeColors.PanelSurface;
        this.ForeColor = ThemeColors.TextPrimary;

        // Find and update header manually or via controls collection
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
        
        lblFileName.ForeColor = (_selectedFile != "") ? ThemeColors.TextAccent : ThemeColors.TextSecondary;
    }

    private void ApplyThemeToCard(ModernCard card)
    {
        foreach (Control c in card.Controls) {
            if (c is Label lbl) {
                // Determine if it's a title or a field label
                if (lbl.Font == ThemeColors.LabelFont) lbl.ForeColor = ThemeColors.TextAccent;
                else lbl.ForeColor = ThemeColors.TextSecondary;
            }
            if (c is TextBox txt) {
                txt.BackColor = ThemeColors.InputBackground;
                txt.ForeColor = ThemeColors.TextPrimary;
            }
        }
    }

    private void ShowWarning(string msg) => MessageBox.Show(msg, "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    private void HandleError(string prefix, Exception ex) {
        Logger.Log($"{prefix}: {ex.Message}");
        MessageBox.Show($"{prefix}: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        TransferProgressChanged?.Invoke(0, prefix + " thất bại.");
    }
}
