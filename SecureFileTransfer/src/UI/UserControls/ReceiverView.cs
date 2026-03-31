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

    private TextBox txtPort = default!, txtKey = default!;
    private Label lblFileName = default!;
    private ModernButton btnSelectEncFile = default!, btnReceiveNetwork = default!, btnDecryptOnly = default!;

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

        // Header
        var lblHeader = new Label { 
            Name = "lblHeader",
            Text = "Hệ thống Nhận & Giải mã Dữ liệu", 
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

        // 1. Network Station Card
        var cardNetwork = new ModernCard { Width = 750, Height = 180, Margin = new Padding(0, 0, 0, 20) };
        cardNetwork.Controls.Add(CreateGroupTitle("CẤU HÌNH TRẠM THU DỮ LIỆU"));
        
        int startY = 60;
        txtPort = CreateModernInput("Cổng mạng lắng nghe (Port):", _config.DefaultPort.ToString(), cardNetwork, ref startY);
        
        btnReceiveNetwork = CreateModernButton("KÍCH HOẠT TRẠM THU", ThemeColors.Success, 300, 45);
        btnReceiveNetwork.Location = new Point(410, startY - 60); // Inline with port
        btnReceiveNetwork.Click += async (s, e) => await ReceiveNetworkActionAsync();
        cardNetwork.Controls.Add(btnReceiveNetwork);
        pnlContent.Controls.Add(cardNetwork);

        // 2. Storage Decryption Card
        var cardDecrypt = new ModernCard { Width = 750, Height = 280, Margin = new Padding(0, 0, 0, 20) };
        cardDecrypt.Controls.Add(CreateGroupTitle("GIẢI MÃ TỆP TIN LƯU TRỮ (.ENC)"));
        
        btnSelectEncFile = CreateModernButton("Duyệt tệp .enc...", ThemeColors.ButtonSecondary, 200, 45);
        btnSelectEncFile.Location = new Point(20, 60);
        btnSelectEncFile.Click += SelectEncFileClick;
        cardDecrypt.Controls.Add(btnSelectEncFile);

        lblFileName = new Label { 
            Text = "Chưa có file nào được chọn", 
            ForeColor = ThemeColors.TextSecondary, 
            Font = ThemeColors.TitleFont, 
            AutoSize = true, 
            Location = new Point(235, 72)
        };
        cardDecrypt.Controls.Add(lblFileName);

        int keyY = 130;
        txtKey = CreateModernInput("Khóa bảo mật để giải mã (Key):", "", cardDecrypt, ref keyY, isPassword: true);
        pnlContent.Controls.Add(cardDecrypt);

        // Actions Bottom
        var pnlActions = new FlowLayoutPanel { 
            Dock = DockStyle.Bottom, 
            Height = 80, 
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 15, 0, 0)
        };
        
        btnDecryptOnly = CreateModernButton("GIẢI MÃ DỮ LIỆU", ThemeColors.Primary, 260, 50);
        btnDecryptOnly.Font = ThemeColors.TitleFont;
        btnDecryptOnly.Click += async (s, e) => await DecryptLocalActionAsync();

        pnlActions.Controls.Add(btnDecryptOnly);
        layout.Controls.Add(pnlActions, 0, 2);
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
            Width = 350, // Slightly smaller for receiver port
            BackColor = ThemeColors.InputBackground, 
            ForeColor = ThemeColors.TextPrimary, 
            BorderStyle = BorderStyle.FixedSingle, 
            PasswordChar = isPassword ? '*' : '\0',
            Location = new Point(20, startY + 25),
            Font = new Font("Segoe UI", 12F) 
        };
        if (isPassword) t.Width = 710;
        
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
                TransferProgressChanged?.Invoke(100, $"Giải mã hoàn tất: {Path.GetFileName(sfd.FileName)}");
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
        
        lblFileName.ForeColor = (_selectedEncFile != "") ? ThemeColors.TextAccent : ThemeColors.TextSecondary;
    }

    private void ApplyThemeToCard(ModernCard card)
    {
        foreach (Control c in card.Controls) {
            if (c is Label lbl) {
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
    private void HandleError(string prefix, Exception ex, string? customMsg = null) {
        string error = customMsg ?? ex.Message;
        Logger.Log($"{prefix}: {error}");
        MessageBox.Show($"{prefix}:\n{error}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        TransferProgressChanged?.Invoke(0, prefix + " thất bại.");
    }
}
