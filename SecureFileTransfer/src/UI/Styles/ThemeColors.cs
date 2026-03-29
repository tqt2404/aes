using System.Drawing;

namespace SecureFileTransfer.UI.Styles;

public static class ThemeColors
{
    // Cấp độ Nền (Backgrounds)
    public static readonly Color WindowBackground = Color.FromArgb(18, 18, 18);
    public static readonly Color PanelSurface = Color.FromArgb(30, 30, 30);
    public static readonly Color InputBackground = Color.FromArgb(45, 45, 45);
    public static readonly Color SidebarBackground = Color.FromArgb(25, 25, 25);
    public static readonly Color SidebarButtonHover = Color.FromArgb(40, 40, 40);
    public static readonly Color SidebarButtonActive = Color.FromArgb(45, 45, 45);

    // Cấp độ Chữ (Text)
    public static readonly Color TextPrimary = Color.FromArgb(240, 240, 240);
    public static readonly Color TextSecondary = Color.FromArgb(170, 170, 170);
    public static readonly Color TextAccent = Color.FromArgb(0, 174, 219);

    // Màu chính yếu (Brand / Primary Actions)
    public static readonly Color Primary = Color.FromArgb(0, 120, 212); // Fluent Blue
    public static readonly Color PrimaryHover = Color.FromArgb(25, 135, 225);
    public static readonly Color Success = Color.FromArgb(16, 124, 16);  // Green
    public static readonly Color Warning = Color.FromArgb(255, 185, 0);  // Yellow
    public static readonly Color Danger = Color.FromArgb(216, 59, 1);    // Red

    // Colors cho secondary buttons
    public static readonly Color ButtonSecondary = Color.FromArgb(70, 70, 70);

    // Fonts chuẩn Enterprise
    public static readonly Font HeaderFont = new Font("Segoe UI", 16F, FontStyle.Bold);
    public static readonly Font TitleFont = new Font("Segoe UI Semibold", 12F);
    public static readonly Font BodyFont = new Font("Segoe UI", 10F);
    public static readonly Font CodeFont = new Font("Consolas", 10F);
}
