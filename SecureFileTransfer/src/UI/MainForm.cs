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

    private Panel pnlSidebar;
    private Panel pnlContent;
    private Panel pnlBottom;
    private TextBox txtLogs;
    private ProgressBar prgTransfer;
    private Label lblStatus;
    private Button btnOpenFolder;
    private string _lastSavedPath = "";

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
        var btnClose = new Button { Text = "✕", Dock = DockStyle.Right, Width = 40, FlatStyle = FlatStyle.Flat, ForeColor = Color.Gray, BackColor = ThemeColors.SidebarBackground, Cursor = Cursors.Hand };
        btnClose.FlatAppearance.BorderSize = 0;
        btnClose.Click += (s, e) => Application.Exit();
        
        var btnMinimize = new Button { Text = "―", Dock = DockStyle.Right, Width = 40, FlatStyle = FlatStyle.Flat, ForeColor = Color.Gray, BackColor = ThemeColors.SidebarBackground, Cursor = Cursors.Hand };
        btnMinimize.FlatAppearance.BorderSize = 0;
        btnMinimize.Click += (s, e) => this.WindowState = FormWindowState.Minimized;
        
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
        
        var pnlStatus = new Panel { Dock = DockStyle.Top, Height = 50 };
        prgTransfer = new ProgressBar { Dock = DockStyle.Top, Height = 10, Value = 0, Style = ProgressBarStyle.Continuous };
        lblStatus = new Label { Text = "Hệ thống sẵn sàng.", Dock = DockStyle.Left, ForeColor = ThemeColors.Success, Font = new Font("Segoe UI", 9F, FontStyle.Italic), AutoSize = true, Padding = new Padding(0, 10, 0, 0) };
        btnOpenFolder = new Button { Text = "MỞ THƯ MỤC LƯU", Dock = DockStyle.Right, Width = 180, Height = 30, BackColor = ThemeColors.Primary, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Visible = false, Margin = new Padding(0, 10, 0, 0), Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
        btnOpenFolder.FlatAppearance.BorderSize = 0;
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

        // Handle System Logs
        Logger.OnLog += m => Invoke(() => { 
            txtLogs.AppendText($"[{DateTime.Now:HH:mm:ss}] {m}{Environment.NewLine}"); 
            txtLogs.ScrollToCaret(); 
        });

        // Default View
        SwitchView(_senderView, btnNavSend);
    }

    private Button CreateSidebarButton(string text, string icon)
    {
        var btn = new Button { 
            Text = text, 
            Dock = DockStyle.Top, 
            Height = 60, 
            FlatStyle = FlatStyle.Flat, 
            ForeColor = ThemeColors.TextSecondary, 
            BackColor = ThemeColors.SidebarBackground, 
            TextAlign = ContentAlignment.MiddleLeft, 
            Font = ThemeColors.TitleFont,
            Cursor = Cursors.Hand,
            Padding = new Padding(25, 0, 0, 0) 
        };
        btn.FlatAppearance.BorderSize = 0;
        return btn;
    }

    private void SwitchView(UserControl view, Button activeButton)
    {
        // Reset styles
        foreach (Control ctrl in pnlSidebar.Controls) {
            if (ctrl is Button b) {
                b.BackColor = ThemeColors.SidebarBackground;
                b.ForeColor = ThemeColors.TextSecondary;
                b.Font = new Font(b.Font, FontStyle.Regular);
            }
        }

        // Active style
        activeButton.BackColor = ThemeColors.SidebarButtonActive;
        activeButton.ForeColor = ThemeColors.Primary;
        activeButton.Font = new Font(activeButton.Font, FontStyle.Bold);

        pnlContent.Controls.Clear();
        pnlContent.Controls.Add(view);
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
