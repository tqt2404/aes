 using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Extensions.Options;
using SecureFileTransfer.Models;
using SecureFileTransfer.Services;
using SecureFileTransfer.UI.Styles;
using SecureFileTransfer.UI.UserControls;
using SecureFileTransfer.Utils;

namespace SecureFileTransfer.UI;

public partial class MainForm : Form
{
    private readonly FileTransferManager _manager;
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
    private string _lastSavedPath = "";
    private Panel pnlActiveIndicator = default!;
    private System.Windows.Forms.Timer _fadeTimer = default!;
    private float _opacity = 0;

    // Drag move support
    public const int WM_NCLBUTTONDOWN = 0xA1;
    public const int HT_CAPTION = 0x2;
    [DllImportAttribute("user32.dll")]
    public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
    [DllImportAttribute("user32.dll")]
    public static extern bool ReleaseCapture();

    public MainForm(FileTransferManager manager, IOptions<AppConfig> config)
    {
        _manager = manager;
        _config = config.Value;

        // Create Views
        _senderView = new SenderView(_manager, _config);
        _receiverView = new ReceiverView(_manager, _config);

        // Bind Events
        BindEvents(_senderView);
        BindEvents(_receiverView);

        // Notify Sender if Receiver is listening locally
        _receiverView.ListeningStateChanged += (isListening) => {
            _senderView.SetReceiverReadyStatus(isListening);
        };

        _fadeTimer = new System.Windows.Forms.Timer { Interval = 20 };
        _fadeTimer.Tick += (s, e) => {
            _opacity += 0.1f;
            if (_opacity >= 1.0f) {
                _opacity = 1.0f;
                _fadeTimer.Stop();
            }
        };

        ThemeColors.ThemeChanged += ApplyTheme;

        InitializeComponent();
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
        var lblTitle = new Label { Text = "ENTERPRISE SECURE DATA GATEWAY", ForeColor = ThemeColors.TextAccent, Font = ThemeColors.TitleFont, AutoSize = true, Location = new Point(15, 8) };
        header.Controls.Add(lblTitle);
        
        // Window Control Buttons
        var btnClose = new Button { Text = "✕", Dock = DockStyle.Right, Width = 40, FlatStyle = FlatStyle.Flat, ForeColor = Color.Gray, Cursor = Cursors.Hand };
        btnClose.FlatAppearance.BorderSize = 0;
        btnClose.Click += (s, e) => Application.Exit();
        
        var btnMinimize = new Button { Text = "―", Dock = DockStyle.Right, Width = 40, FlatStyle = FlatStyle.Flat, ForeColor = Color.Gray, Cursor = Cursors.Hand };
        btnMinimize.FlatAppearance.BorderSize = 0;
        btnMinimize.Click += (s, e) => this.WindowState = FormWindowState.Minimized;

        btnThemeToggle = new ModernButton { Text = "🌙", Dock = DockStyle.Right, Width = 40, BackColor = Color.Transparent };
        btnThemeToggle.Click += (s, e) => ThemeColors.ToggleTheme();
        
        header.Controls.Add(btnThemeToggle);
        header.Controls.Add(btnMinimize);
        header.Controls.Add(btnClose);
        
        // Drag to move
        header.MouseDown += (s, e) => {
            if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0); }
        };
        lblTitle.MouseDown += (s, e) => {
            if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0); }
        };

        this.Controls.Add(header);

        // Sidebar
        pnlSidebar = new Panel { Dock = DockStyle.Left, Width = 250, BackColor = ThemeColors.SidebarBackground, Padding = new Padding(0, 20, 0, 0) };
        
        var btnNavSend = CreateSidebarButton("Gửi Dữ Liệu", "");
        var btnNavReceive = CreateSidebarButton("Nhận Dữ Liệu", "");
        
        btnNavSend.Click += (s, e) => SwitchView(_senderView, btnNavSend);
        btnNavReceive.Click += (s, e) => SwitchView(_receiverView, btnNavReceive);

        pnlSidebar.Controls.Add(btnNavReceive);
        pnlSidebar.Controls.Add(btnNavSend);
        this.Controls.Add(pnlSidebar);

        // Bottom Panel
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

        // Main Content Area
        pnlContent = new Panel { Dock = DockStyle.Fill, BackColor = ThemeColors.PanelSurface };
        this.Controls.Add(pnlContent);
        pnlContent.BringToFront();

        // Sidebar Indicator
        pnlActiveIndicator = new Panel { Width = 4, Height = 60, BackColor = ThemeColors.TextAccent, Visible = false };
        pnlSidebar.Controls.Add(pnlActiveIndicator);

        // Default View
        SwitchView(_senderView, (ModernButton)btnNavSend);
    }

    private ModernButton CreateSidebarButton(string text, string icon)
    {
        var btn = new ModernButton { 
            Text = text, 
            Dock = DockStyle.Top, 
            Height = 60, 
            ForeColor = ThemeColors.TextSecondary, 
            BackColor = ThemeColors.SidebarBackground, 
            Font = ThemeColors.TitleFont,
            Padding = new Padding(25, 0, 0, 0) 
        };
        return btn;
    }

    private void SwitchView(UserControl view, ModernButton activeButton)
    {
        // View Transition Animation logic
        _opacity = 0;
        _fadeTimer.Start();

        // Update sidebar buttons visual state
        ApplyThemeSidebar();

        // Active style over the themed base
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
        
        // Header & Sidebar
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
            } else {
                MessageBox.Show("Không tìm thấy đường dẫn tại đĩa!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
