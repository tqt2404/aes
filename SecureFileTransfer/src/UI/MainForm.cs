using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.Options;
using SecureFileTransfer.Models;
using SecureFileTransfer.Services;
using SecureFileTransfer.Network;
using SecureFileTransfer.UI.Styles;
using SecureFileTransfer.UI.UserControls;
using SecureFileTransfer.Utils;

namespace SecureFileTransfer.UI;

public partial class MainForm : Form
{
    private readonly FileTransferManager _manager;
    private readonly HubTcpClient _hubClient;
    private readonly CentralHubServer _hubServer;
    private readonly AppConfig _config;

    private readonly SenderView _senderView;
    private readonly ReceiverView _receiverView;

    private Panel pnlSidebar = default!;
    private Panel pnlContent = default!;
    private Panel pnlBottom = default!;
    private TextBox txtLogs = default!;
    private ModernProgressBar prgTransfer = default!;
    private Label lblStatus = default!;
    private ModernButton btnOpenFolder = default!;
    private ModernButton btnThemeToggle = default!;
    private ModernButton btnStartServer = default!;
    private string _lastSavedPath = "";
    private Panel pnlActiveIndicator = default!;
    private System.Windows.Forms.Timer _fadeTimer = default!;
    private float _opacity = 0;

    private bool _isServerRunning = false;

    // Drag move support
    public const int WM_NCLBUTTONDOWN = 0xA1;
    public const int HT_CAPTION = 0x2;
    [DllImportAttribute("user32.dll")]
    public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
    [DllImportAttribute("user32.dll")]
    public static extern bool ReleaseCapture();

    public MainForm(FileTransferManager manager, HubTcpClient hubClient, CentralHubServer hubServer, IOptions<AppConfig> config)
    {
        _manager = manager;
        _hubClient = hubClient;
        _hubServer = hubServer;
        _config = config.Value;

        _manager.AttachHubClient(_hubClient);

        _senderView = new SenderView(_manager, _hubClient, _config);
        _receiverView = new ReceiverView(_manager, _config);

        BindEvents(_senderView);
        BindEvents(_receiverView);

        // Bind global receiver events explicitly
        _manager.OnReceiveProgress += (pr) => {
            Invoke(() => {
                prgTransfer.Maximum = 100;
                int percent = (int)(pr.BytesTransferred * 100 / (pr.TotalBytes > 0 ? pr.TotalBytes : 1));
                prgTransfer.Value = percent > 100 ? 100 : percent;
                lblStatus.Text = $"Đang nhận: {percent}%";
            });
        };

        _fadeTimer = new System.Windows.Forms.Timer { Interval = 20 };
        _fadeTimer.Tick += (s, e) => {
            _opacity += 0.1f;
            if (_opacity >= 1.0f) { _opacity = 1.0f; _fadeTimer.Stop(); }
        };

        ThemeColors.ThemeChanged += ApplyTheme;

        InitializeComponent();
        this.Load += MainForm_Load;
    }

    private async void MainForm_Load(object? sender, EventArgs e)
    {
        bool connected = await RequestLoginAsync();
        if (!connected)
        {
            Application.Exit();
            return;
        }
    }

    private async Task<bool> RequestLoginAsync()
    {
        using var tempForm = new Form 
        { 
            Width = 400, Height = 300, 
            Text = "Kết nối Hub Server", 
            StartPosition = FormStartPosition.CenterScreen,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false,
            BackColor = ThemeColors.WindowBackground,
            ForeColor = ThemeColors.TextPrimary
        };

        var lblIp = new Label { Text = "Hub IP:", Location = new Point(20, 20), AutoSize = true, ForeColor = ThemeColors.TextPrimary };
        var txtIp = new TextBox { Text = "127.0.0.1", Location = new Point(20, 45), Width = 340, BackColor = ThemeColors.InputBackground, ForeColor = ThemeColors.TextPrimary };

        var lblName = new Label { Text = "Tên hiển thị:", Location = new Point(20, 80), AutoSize = true, ForeColor = ThemeColors.TextPrimary };
        var txtName = new TextBox { Text = $"User_{new Random().Next(1000, 9999)}", Location = new Point(20, 105), Width = 340, BackColor = ThemeColors.InputBackground, ForeColor = ThemeColors.TextPrimary };

        var btnConnect = new Button { Text = "KẾT NỐI", Location = new Point(20, 160), Width = 340, Height = 40, BackColor = ThemeColors.Primary, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        btnConnect.FlatAppearance.BorderSize = 0;

        var chkHost = new CheckBox { Text = "Chạy kèm Hub Server ngầm (Port 5000)", Location = new Point(20, 210), AutoSize = true, ForeColor = ThemeColors.TextSecondary };

        tempForm.Controls.AddRange(new Control[] { lblIp, txtIp, lblName, txtName, btnConnect, chkHost });

        bool isConnected = false;

        btnConnect.Click += async (s, e) =>
        {
            btnConnect.Enabled = false;
            btnConnect.Text = "Đang kết nối...";
            try
            {
                if (chkHost.Checked) 
                {
                    _isServerRunning = true;
                    _ = _hubServer.StartAsync();
                    await _hubServer.WaitForStartAsync(); // Đợi Server thực sự mở port
                    txtIp.Text = "127.0.0.1"; // auto connect to self
                    btnStartServer.Text = "DỪNG SERVER";
                    btnStartServer.BackColor = ThemeColors.Danger;
                }

                await _hubClient.ConnectAsync(txtIp.Text.Trim(), 5000, txtName.Text.Trim());
                isConnected = true;
                this.Text = $"ENTERPRISE SECURE DATA GATEWAY - [{txtName.Text}]";
                tempForm.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thể kết nối Hub: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnConnect.Enabled = true;
                btnConnect.Text = "KẾT NỐI";
            }
        };

        tempForm.ShowDialog();
        return isConnected;
    }

    private void BindEvents(dynamic view)
    {
        view.TransferProgressChanged += new Action<int, string>((progress, status) => {
            Invoke(() => {
                prgTransfer.Value = progress;
                lblStatus.Text = status;
            });
        });
        
        view.RequestOpenFolder += new Action<string>((path) => {
            Invoke(() => {
                _lastSavedPath = path;
                btnOpenFolder.Visible = true;
                Logger.Log($"[Hệ thống] Tệp tin thực tế đã nằm tại: {path}");
            });
        });
    }

    private void InitializeComponent()
    {
        this.Text = "ENTERPRISE SECURE DATA GATEWAY";
        this.Size = new Size(1200, 850);
        this.BackColor = ThemeColors.WindowBackground;
        this.ForeColor = ThemeColors.TextPrimary;
        this.Font = ThemeColors.BodyFont;
        this.StartPosition = FormStartPosition.CenterScreen;
        this.FormBorderStyle = FormBorderStyle.None; 
        this.DoubleBuffered = true;

        var header = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = ThemeColors.SidebarBackground };
        var lblTitle = new Label { Text = "ENTERPRISE SECURE DATA GATEWAY - HUB & SPOKE", ForeColor = ThemeColors.TextAccent, Font = ThemeColors.TitleFont, AutoSize = true, Location = new Point(15, 8) };
        header.Controls.Add(lblTitle);
        
        var btnClose = new Button { Text = "✕", Dock = DockStyle.Right, Width = 40, FlatStyle = FlatStyle.Flat, ForeColor = Color.Gray, Cursor = Cursors.Hand };
        btnClose.FlatAppearance.BorderSize = 0;
        btnClose.Click += (s, e) => { _hubServer.Stop(); Application.Exit(); };
        
        var btnMinimize = new Button { Text = "―", Dock = DockStyle.Right, Width = 40, FlatStyle = FlatStyle.Flat, ForeColor = Color.Gray, Cursor = Cursors.Hand };
        btnMinimize.FlatAppearance.BorderSize = 0;
        btnMinimize.Click += (s, e) => this.WindowState = FormWindowState.Minimized;

        btnThemeToggle = new ModernButton { Text = "🌙", Dock = DockStyle.Right, Width = 40, BackColor = Color.Transparent };
        btnThemeToggle.Click += (s, e) => ThemeColors.ToggleTheme();
        
        header.Controls.Add(btnThemeToggle);
        header.Controls.Add(btnMinimize);
        header.Controls.Add(btnClose);
        
        header.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0); } };
        lblTitle.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0); } };

        this.Controls.Add(header);

        // Sidebar
        pnlSidebar = new Panel { Dock = DockStyle.Left, Width = 250, BackColor = ThemeColors.SidebarBackground, Padding = new Padding(0, 20, 0, 0) };
        
        var btnNavSend = CreateSidebarButton("Gửi Dữ Liệu", "");
        var btnNavReceive = CreateSidebarButton("Nhận Dữ Liệu", "");
        
        btnStartServer = CreateModernButton("CHẠY SERVER NGẦM", ThemeColors.Success, 210, 40);
        btnStartServer.Dock = DockStyle.Bottom;
        btnStartServer.Margin = new Padding(20);
        btnStartServer.Click += BtnStartServer_Click;

        btnNavSend.Click += (s, e) => SwitchView(_senderView, btnNavSend);
        btnNavReceive.Click += (s, e) => SwitchView(_receiverView, btnNavReceive);

        pnlSidebar.Controls.Add(btnNavReceive);
        pnlSidebar.Controls.Add(btnNavSend);
        
        var bottomSidebar = new Panel { Dock = DockStyle.Bottom, Height = 80, Padding = new Padding(20) };
        bottomSidebar.Controls.Add(btnStartServer);
        pnlSidebar.Controls.Add(bottomSidebar);

        this.Controls.Add(pnlSidebar);

        pnlBottom = new Panel { Dock = DockStyle.Bottom, Height = 160, BackColor = ThemeColors.WindowBackground, Padding = new Padding(15) };
        var pnlStatus = new Panel { Dock = DockStyle.Top, Height = 60, Padding = new Padding(0, 5, 0, 5) };
        prgTransfer = new ModernProgressBar { Dock = DockStyle.Top, Height = 6, Maximum = 100 };
        lblStatus = new Label { Text = "Hệ thống sẵn sàng.", Dock = DockStyle.Left, ForeColor = ThemeColors.TextSecondary, Font = new Font("Segoe UI", 9F, FontStyle.Italic), AutoSize = true, Padding = new Padding(0, 15, 0, 0) };
        btnOpenFolder = new ModernButton { Text = "MỞ THƯ MỤC LƯU", Dock = DockStyle.Right, Width = 180, Height = 35, BackColor = ThemeColors.Primary, ForeColor = Color.White, Visible = false };
        btnOpenFolder.Click += (s, e) => OpenExplorer(_lastSavedPath);
        
        pnlStatus.Controls.Add(lblStatus);
        pnlStatus.Controls.Add(btnOpenFolder);
        pnlStatus.Controls.Add(prgTransfer);

        txtLogs = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, BackColor = Color.FromArgb(10, 10, 10), ForeColor = ThemeColors.TextSecondary, Font = ThemeColors.CodeFont, BorderStyle = BorderStyle.None, ScrollBars = ScrollBars.Vertical };
        
        pnlBottom.Controls.Add(txtLogs);
        pnlBottom.Controls.Add(pnlStatus);
        this.Controls.Add(pnlBottom);

        pnlContent = new Panel { Dock = DockStyle.Fill, BackColor = ThemeColors.PanelSurface };
        this.Controls.Add(pnlContent);
        pnlContent.BringToFront();

        pnlActiveIndicator = new Panel { Width = 4, Height = 60, BackColor = ThemeColors.TextAccent, Visible = false };
        pnlSidebar.Controls.Add(pnlActiveIndicator);

        SwitchView(_senderView, (ModernButton)btnNavSend);
    }

    private void BtnStartServer_Click(object? sender, EventArgs e)
    {
        if (_isServerRunning)
        {
            _hubServer.Stop();
            _isServerRunning = false;
            btnStartServer.Text = "CHẠY SERVER NGẦM";
            btnStartServer.BackColor = ThemeColors.Success;
        }
        else
        {
            _ = _hubServer.StartAsync();
            _isServerRunning = true;
            btnStartServer.Text = "DỪNG SERVER";
            btnStartServer.BackColor = ThemeColors.Danger;
        }
    }

    private ModernButton CreateSidebarButton(string text, string icon)
    {
        return new ModernButton { 
            Text = text, Dock = DockStyle.Top, Height = 60, 
            ForeColor = ThemeColors.TextSecondary, BackColor = ThemeColors.SidebarBackground, 
            Font = ThemeColors.TitleFont, Padding = new Padding(25, 0, 0, 0) 
        };
    }
    
    private ModernButton CreateModernButton(string text, Color backColor, int width, int height) {
        return new ModernButton { 
            Text = text, Width = width, Height = height, 
            BackColor = backColor, ForeColor = Color.White, Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold)
        };
    }

    private void SwitchView(UserControl view, ModernButton activeButton)
    {
        _opacity = 0;
        _fadeTimer.Start();
        ApplyThemeSidebar();
        activeButton.BackColor = ThemeColors.SidebarButtonActive;
        activeButton.ForeColor = ThemeColors.TextAccent;
        
        pnlActiveIndicator.Location = new Point(0, activeButton.Location.Y);
        pnlActiveIndicator.Visible = true;
        pnlActiveIndicator.BringToFront();

        pnlContent.Controls.Clear();
        pnlContent.Controls.Add(view);
    }

    private void ApplyTheme()
    {
        this.BackColor = ThemeColors.WindowBackground;
        this.ForeColor = ThemeColors.TextPrimary;
        
        foreach (Control c in this.Controls) {
            if (c is Panel p && p.Dock == DockStyle.Top) p.BackColor = ThemeColors.SidebarBackground;
        }
        
        pnlSidebar.BackColor = ThemeColors.SidebarBackground;
        pnlActiveIndicator.BackColor = ThemeColors.TextAccent;
        pnlContent.BackColor = ThemeColors.PanelSurface;
        pnlBottom.BackColor = ThemeColors.WindowBackground;
        
        ApplyThemeSidebar();
        
        lblStatus.ForeColor = ThemeColors.TextSecondary;
        txtLogs.BackColor = ThemeColors.CurrentMode == ThemeMode.Dark ? Color.FromArgb(10, 10, 10) : Color.FromArgb(245, 245, 245);
        txtLogs.ForeColor = ThemeColors.TextPrimary;

        btnThemeToggle.Text = ThemeColors.CurrentMode == ThemeMode.Dark ? "🌙" : "☀️";
    }

    private void ApplyThemeSidebar()
    {
        foreach (Control ctrl in pnlSidebar.Controls) {
            if (ctrl is ModernButton b) {
                b.BackColor = ThemeColors.SidebarBackground;
                b.ForeColor = ThemeColors.TextSecondary;
            }
        }
    }

    private void OpenExplorer(string path)
    {
        if (!string.IsNullOrEmpty(path)) {
            if (File.Exists(path)) {
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
            } else if (Directory.Exists(path)) {
                System.Diagnostics.Process.Start("explorer.exe", $"\"{path}\"");
            }
        }
    }
}
