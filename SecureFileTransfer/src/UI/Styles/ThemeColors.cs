using System;
using System.Drawing;

namespace SecureFileTransfer.UI.Styles;

public enum ThemeMode { Dark, Light }

public static class ThemeColors
{
    public static ThemeMode CurrentMode { get; private set; } = ThemeMode.Dark;
    public static event Action? ThemeChanged;

    public static void ToggleTheme()
    {
        CurrentMode = CurrentMode == ThemeMode.Dark ? ThemeMode.Light : ThemeMode.Dark;
        ThemeChanged?.Invoke();
    }

    // --- Cấp độ Nền (Backgrounds) ---
    public static Color WindowBackground => CurrentMode == ThemeMode.Dark 
        ? Color.FromArgb(15, 17, 19) 
        : Color.FromArgb(240, 242, 245);

    public static Color PanelSurface => CurrentMode == ThemeMode.Dark 
        ? Color.FromArgb(24, 26, 28) 
        : Color.FromArgb(255, 255, 255);

    public static Color InputBackground => CurrentMode == ThemeMode.Dark 
        ? Color.FromArgb(33, 36, 39) 
        : Color.FromArgb(228, 230, 235);

    public static Color SidebarBackground => CurrentMode == ThemeMode.Dark 
        ? Color.FromArgb(12, 14, 16) 
        : Color.FromArgb(255, 255, 255);

    public static Color SidebarButtonHover => CurrentMode == ThemeMode.Dark 
        ? Color.FromArgb(30, 33, 36) 
        : Color.FromArgb(242, 242, 242);

    public static Color SidebarButtonActive => CurrentMode == ThemeMode.Dark 
        ? Color.FromArgb(38, 41, 44) 
        : Color.FromArgb(231, 243, 255);

    // --- Cấp độ Chữ (Text) ---
    public static Color TextPrimary => CurrentMode == ThemeMode.Dark 
        ? Color.FromArgb(225, 227, 230) 
        : Color.FromArgb(5, 5, 5);

    public static Color TextSecondary => CurrentMode == ThemeMode.Dark 
        ? Color.FromArgb(140, 145, 150) 
        : Color.FromArgb(101, 103, 107);

    public static Color TextAccent => CurrentMode == ThemeMode.Dark 
        ? Color.FromArgb(0, 164, 239) 
        : Color.FromArgb(0, 120, 212);

    public static Color TextSuccess => CurrentMode == ThemeMode.Dark 
        ? Color.FromArgb(120, 220, 120) 
        : Color.FromArgb(46, 160, 67);

    // --- Màu chủ đạo (Accents) ---
    public static Color Primary => CurrentMode == ThemeMode.Dark 
        ? Color.FromArgb(0, 103, 192) 
        : Color.FromArgb(0, 120, 212);

    public static Color AccentGlow => CurrentMode == ThemeMode.Dark 
        ? Color.FromArgb(30, 0, 164, 239) 
        : Color.FromArgb(20, 0, 120, 212);

    public static Color ShadowColor => CurrentMode == ThemeMode.Dark 
        ? Color.FromArgb(40, 0, 0, 0) 
        : Color.FromArgb(15, 0, 0, 0);

    public static Color Success => Color.FromArgb(35, 165, 90);
    public static Color Warning => Color.FromArgb(240, 180, 0);
    public static Color Danger => Color.FromArgb(220, 60, 60);

    // --- Cards & Flat Elements ---
    public static Color CardBackground => CurrentMode == ThemeMode.Dark 
        ? Color.FromArgb(30, 33, 36) 
        : Color.FromArgb(255, 255, 255);

    public static Color CardBorder => CurrentMode == ThemeMode.Dark 
        ? Color.FromArgb(45, 48, 52) 
        : Color.FromArgb(218, 220, 224);

    public static Color ButtonSecondary => CurrentMode == ThemeMode.Dark 
        ? Color.FromArgb(60, 63, 67) 
        : Color.FromArgb(228, 230, 235);

    // --- Fonts chuẩn Enterprise Elite ---
    public static readonly Font HeaderFont = new Font("Segoe UI Semibold", 18F);
    public static readonly Font TitleFont = new Font("Segoe UI Semibold", 11F);
    public static readonly Font BodyFont = new Font("Segoe UI", 10F);
    public static readonly Font LabelFont = new Font("Segoe UI Semibold", 9F);
    public static readonly Font CodeFont = new Font("Consolas", 9.5F);
}
