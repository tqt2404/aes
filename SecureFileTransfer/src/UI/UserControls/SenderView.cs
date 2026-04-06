using System;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows.Forms;
using SecureFileTransfer.Models;
using SecureFileTransfer.Services;
using SecureFileTransfer.Network;
using SecureFileTransfer.UI.Styles;
using SecureFileTransfer.Utils;
using System.Collections.Generic;

namespace SecureFileTransfer.UI.UserControls;

public class SenderView : UserControl
{
    private readonly FileTransferManager _manager;
    private readonly HubTcpClient _hubClient;
    private readonly AppConfig _config;

    private string _selectedFile = "";

    public event Action<int, string>? TransferProgressChanged;
    public event Action<string>? RequestOpenFolder;

    private ComboBox cmbUsers = default!;
    private TextBox txtKey = default!;
    private ComboBox cmbAesSize = default!;
    private Label lblFileName = default!;
    private ModernButton btnSelectFile = default!, btnSend = default!, btnEncryptLocal = default!;

    public SenderView(FileTransferManager manager, HubTcpClient hubClient, AppConfig config)
    {
        _manager = manager;
        _hubClient = hubClient;
        _config = config;
        InitializeComponent();

        _hubClient.OnOnlineListUpdated += UpdateOnlineUsers;
    }

    private void UpdateOnlineUsers(List<string> users)
    {
        InvokeSafe(() => {
            string currentPlayer = cmbUsers.SelectedItem as string ?? "";
            cmbUsers.Items.Clear();
            foreach(var u in users) cmbUsers.Items.Add(u);
            
            if (cmbUsers.Items.Count > 0)
            {
                if (cmbUsers.Items.Contains(currentPlayer))
                    cmbUsers.SelectedItem = currentPlayer;
                else
                    cmbUsers.SelectedIndex = 0;
            }
        });
    }

    private void InvokeSafe(Action action)
    {
        if (this.IsHandleCreated) {
            if (this.InvokeRequired) this.Invoke(action);
            else action();
        }
        else {
            // Nếu chưa có handle, đợi khi handle được tạo xong mới thực hiện
            this.HandleCreated += (s, e) => {
                if (this.InvokeRequired) this.Invoke(action);
                else action();
            };
        }
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

        var lblHeader = new Label { 
            Name = "lblHeader", Text = "Gửi Tệp qua Hub An Toàn", 
            Font = ThemeColors.HeaderFont, ForeColor = ThemeColors.TextAccent, 
            AutoSize = true, Margin = new Padding(0, 0, 0, 30) 
        };
        layout.Controls.Add(lblHeader, 0, 0);
        ThemeColors.ThemeChanged += ApplyTheme;

        var pnlContent = new FlowLayoutPanel { 
            Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(0, 0, 20, 0)
        };
        layout.Controls.Add(pnlContent, 0, 1);

        // 1. File Selection Card
        var cardFile = new ModernCard { Width = 750, Height = 140, Margin = new Padding(0, 0, 0, 20) };
        cardFile.Controls.Add(CreateGroupTitle("CẤU HÌNH TỆP TIN"));
        
        btnSelectFile = CreateModernButton("Duyệt tệp tin...", ThemeColors.ButtonSecondary, 200, 45);
        btnSelectFile.Location = new Point(20, 60);
        btnSelectFile.Click += SelectFileClick;
        cardFile.Controls.Add(btnSelectFile);

        lblFileName = new Label { Text = "Chưa có file nào được chọn", ForeColor = ThemeColors.TextSecondary, Font = ThemeColors.TitleFont, AutoSize = true, Location = new Point(235, 72) };
        cardFile.Controls.Add(lblFileName);
        pnlContent.Controls.Add(cardFile);

        // 2. Network Card
        var cardNetwork = new ModernCard { Width = 750, Height = 300, Margin = new Padding(0, 0, 0, 20) };
        cardNetwork.Controls.Add(CreateGroupTitle("THÔNG SỐ BẢO MẬT & ĐÍCH ĐẾN"));
        
        int startY = 60;
        
        cardNetwork.Controls.Add(new Label { Text = "Chọn Người Nhận Đang Online:", ForeColor = ThemeColors.TextSecondary, Font = ThemeColors.BodyFont, AutoSize = true, Location = new Point(20, startY) });
        cmbUsers = new ComboBox { Width = 710, BackColor = ThemeColors.InputBackground, ForeColor = ThemeColors.TextPrimary, DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(20, startY + 25), Font = new Font("Segoe UI", 12F) };
        cardNetwork.Controls.Add(cmbUsers);
        startY += 85;

        txtKey = CreateModernInput("Khóa bảo mật (Password):", "", cardNetwork, ref startY, isPassword: true);

        cardNetwork.Controls.Add(new Label { Text = "Cỡ khóa AES:", ForeColor = ThemeColors.TextSecondary, Font = ThemeColors.BodyFont, AutoSize = true, Location = new Point(20, startY) });
        cmbAesSize = new ComboBox { Text = "AES-256 (Bảo mật cao)", Width = 710, BackColor = ThemeColors.InputBackground, ForeColor = ThemeColors.TextPrimary, DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(20, startY + 25), Font = new Font("Segoe UI", 12F) };
        cmbAesSize.Items.AddRange(new[] { "AES-128 (Nhanh)", "AES-192 (Cân bằng)", "AES-256 (Bảo mật cao)" });
        cmbAesSize.SelectedIndex = 2;  
        cardNetwork.Controls.Add(cmbAesSize);

        pnlContent.Controls.Add(cardNetwork);

        var pnlActions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 80, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 15, 0, 0) };
        
        btnSend = CreateModernButton("MÃ HÓA & TRUYỀN", ThemeColors.Primary, 260, 50);
        btnSend.Font = ThemeColors.TitleFont;
        btnSend.Click += async (s, e) => await SendActionAsync();
        
        pnlActions.Controls.Add(btnSend);

        btnEncryptLocal = CreateModernButton("LƯU CỤC BỘ", ThemeColors.ButtonSecondary, 260, 50);
        btnEncryptLocal.Click += async (s, e) => await EncryptLocalActionAsync();
        pnlActions.Controls.Add(btnEncryptLocal);

        layout.Controls.Add(pnlActions, 0, 2);

        this.AllowDrop = true;
        this.DragEnter += (s, e) => { if (e.Data!.GetDataPresent(DataFormats.FileDrop)) { e.Effect = DragDropEffects.Copy; cardFile.BackColor = Color.FromArgb(40, 40, 40); } };
        this.DragLeave += (s, e) => { cardFile.BackColor = ThemeColors.CardBackground; };
        this.DragDrop += (s, e) => {
            cardFile.BackColor = ThemeColors.CardBackground;
            if (e.Data!.GetData(DataFormats.FileDrop) is string[] f && f.Length > 0) {
                _selectedFile = f[0];
                lblFileName.Text = Path.GetFileName(_selectedFile);
                lblFileName.ForeColor = ThemeColors.TextAccent;
            }
        };
    }

    private Label CreateGroupTitle(string title) {
        return new Label { Text = title, ForeColor = ThemeColors.TextAccent, Font = ThemeColors.LabelFont, AutoSize = true, Location = new Point(20, 20) };
    }

    private TextBox CreateModernInput(string labelText, string defaultValue, Control parent, ref int startY, bool isPassword = false) {
        parent.Controls.Add(new Label { Text = labelText, ForeColor = ThemeColors.TextSecondary, Font = ThemeColors.BodyFont, AutoSize = true, Location = new Point(20, startY) });
        var t = new TextBox { Text = defaultValue, Width = 710, BackColor = ThemeColors.InputBackground, ForeColor = ThemeColors.TextPrimary, BorderStyle = BorderStyle.FixedSingle, PasswordChar = isPassword ? '*' : '\0', Location = new Point(20, startY + 25), Font = new Font("Segoe UI", 12F) };
        parent.Controls.Add(t);
        startY += 85;
        return t;
    }

    private ModernButton CreateModernButton(string text, Color backColor, int width, int height) {
        return new ModernButton { Text = text, Width = width, Height = height, BackColor = backColor, ForeColor = Color.White, Font = new Font("Segoe UI", 10F, FontStyle.Bold), Margin = new Padding(10, 0, 0, 0) };
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
                TransferProgressChanged?.Invoke((int)((pr.BytesTransferred * 100) / pr.TotalBytes), $"Đang truyền: {pr.BytesTransferred / 1024} KB / {pr.TotalBytes / 1024} KB");
            });

            AesKeySize keySize = cmbAesSize.SelectedIndex switch { 0 => AesKeySize.AES128, 1 => AesKeySize.AES192, _ => AesKeySize.AES256 };
            
            string targetUser = cmbUsers.SelectedItem!.ToString()!;
            await _manager.EncryptAndSendAsync(_selectedFile, targetUser, txtKey.Text.Trim(), keySize, p);
            
            TransferProgressChanged?.Invoke(100, "Truyền nhận dữ liệu qua Hub thành công.");
            MessageBox.Show("Gửi file thành công!", "Hoàn tất", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { HandleError("Lỗi Hệ Thống", ex); }
        finally { SetLoading(false); }
    }

    private async Task EncryptLocalActionAsync() 
    {
        if (string.IsNullOrEmpty(_selectedFile)) { ShowWarning("Chưa chọn tệp nguồn!"); return; }
        string key = txtKey.Text.Trim();
        if (string.IsNullOrEmpty(key) || key.Length < 8) { ShowWarning("Mật mã phải có ít nhất 8 ký tự!"); return; }

        using SaveFileDialog sfd = new() 
        {
            Title = "Chọn nơi lưu tệp đã mã hóa",
            Filter = "Encrypted files (*.enc)|*.enc|All files (*.*)|*.*",
            FileName = Path.GetFileName(_selectedFile) + ".enc"
        };

        if (sfd.ShowDialog() != DialogResult.OK) 
        {
            // Người dùng hủy bỏ hoặc không chọn
            return; 
        }

        string destPath = sfd.FileName;
        if (string.IsNullOrEmpty(destPath)) 
        { 
            ShowWarning("Chưa chọn nơi lưu!"); 
            return; 
        }

        try 
        {
            SetLoading(true);
            var p = new Progress<TransferProgress>(pr => {
                TransferProgressChanged?.Invoke((int)((pr.BytesTransferred * 100) / pr.TotalBytes), $"Đang mã hóa & lưu: {pr.BytesTransferred / 1024} KB / {pr.TotalBytes / 1024} KB");
            });

            AesKeySize keySize = cmbAesSize.SelectedIndex switch { 0 => AesKeySize.AES128, 1 => AesKeySize.AES192, _ => AesKeySize.AES256 };
            
            await _manager.EncryptLocalStorageAsync(_selectedFile, destPath, key, keySize, p);
            
            TransferProgressChanged?.Invoke(100, "Mã hóa và lưu cục bộ thành công.");
            MessageBox.Show($"Đã mã hóa và lưu thành công tại:\n{destPath}", "Hoàn tất", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { HandleError("Lỗi Mã Hóa Cục Bộ", ex); }
        finally { SetLoading(false); }
    }

    private bool ValidateInputs() {
        string key = txtKey.Text.Trim();
        if (string.IsNullOrEmpty(_selectedFile)) { ShowWarning("Chưa chọn tệp!"); return false; }
        if (string.IsNullOrEmpty(key) || key.Length < 8) { ShowWarning("Mật mã phải có ít nhất 8 ký tự!"); return false; }
        if (cmbUsers.SelectedItem == null) { ShowWarning("Chưa chọn người nhận!"); return false; }

        return true;
    }

    private void SetLoading(bool isLoading) {
        btnSend.Enabled = !isLoading;
        btnEncryptLocal.Enabled = !isLoading;
        btnSelectFile.Enabled = !isLoading;
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
        lblFileName.ForeColor = (_selectedFile != "") ? ThemeColors.TextAccent : ThemeColors.TextSecondary;
    }

    private void ApplyThemeToCard(ModernCard card)
    {
        foreach (Control c in card.Controls) {
            if (c is Label lbl) {
                if (lbl.Font == ThemeColors.LabelFont) lbl.ForeColor = ThemeColors.TextAccent;
                else lbl.ForeColor = ThemeColors.TextSecondary;
            }
            if (c is TextBox txt) { txt.BackColor = ThemeColors.InputBackground; txt.ForeColor = ThemeColors.TextPrimary; }
            if (c is ComboBox cmb) { cmb.BackColor = ThemeColors.InputBackground; cmb.ForeColor = ThemeColors.TextPrimary; }
        }
    }

    private void ShowWarning(string msg) => MessageBox.Show(msg, "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    private void HandleError(string prefix, Exception ex) {
        Logger.Log($"{prefix}: {ex.Message}");
        MessageBox.Show($"{prefix}: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        TransferProgressChanged?.Invoke(0, prefix + " thất bại.");
    }
}
