using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using SecureFileTransfer.UI.Styles;

namespace SecureFileTransfer.UI.UserControls;

public class ModernButton : Button
{
    private bool _isHovered = false;
    private bool _isPressed = false;

    public ModernButton()
    {
        this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        this.Cursor = Cursors.Hand;
        this.FlatAppearance.BorderSize = 0;
        this.FlatStyle = FlatStyle.Flat;
        ThemeColors.ThemeChanged += () => { if (!IsDisposed) Invalidate(); };
    }

    protected override void OnMouseEnter(EventArgs e) { _isHovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _isHovered = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs mevent) { _isPressed = true; Invalidate(); base.OnMouseDown(mevent); }
    protected override void OnMouseUp(MouseEventArgs mevent) { _isPressed = false; Invalidate(); base.OnMouseUp(mevent); }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var g = pevent.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        Rectangle rect = new Rectangle(0, 0, Width, Height);
        float radius = 12f;

        Color baseColor = (this.BackColor == ThemeColors.Primary) ? ThemeColors.Primary : this.BackColor;
        if (_isPressed) baseColor = ControlPaint.Dark(baseColor, 0.05f);
        else if (_isHovered) baseColor = ControlPaint.Light(baseColor, 0.05f);

        using (GraphicsPath path = GetRoundedPath(rect, radius))
        {
            // Flat Solid Fill
            using (SolidBrush brush = new SolidBrush(baseColor))
            {
                g.FillPath(brush, path);
            }

            // Subtle border for flat design
            using (Pen borderPen = new Pen(Color.FromArgb(20, 255, 255, 255), 1f))
            {
                g.DrawPath(borderPen, path);
            }
        }

        // Text
        TextRenderer.DrawText(g, Text, Font, rect, ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
    }

    private GraphicsPath GetRoundedPath(Rectangle rect, float radius)
    {
        GraphicsPath path = new GraphicsPath();
        float diameter = radius * 2;
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseAllFigures();
        return path;
    }
}

public class ModernProgressBar : Control
{
    private int _value = 0;
    private int _maximum = 100;

    public int Value { get => _value; set { _value = Math.Clamp(value, 0, _maximum); Invalidate(); } }
    public int Maximum { get => _maximum; set { _maximum = value; Invalidate(); } }

    public ModernProgressBar()
    {
        this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        this.Height = 8;
        ThemeColors.ThemeChanged += () => { if (!IsDisposed) Invalidate(); };
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        Rectangle rect = new Rectangle(0, 0, Width, Height);
        float radius = Height / 2f;

        // Background Track
        using (GraphicsPath trackPath = GetRoundedPath(rect, radius))
        {
            using (SolidBrush trackBrush = new SolidBrush(Color.FromArgb(40, 40, 40)))
            {
                g.FillPath(trackBrush, trackPath);
            }
        }

        // Progress Fill
        if (_value > 0)
        {
            float progressWidth = (float)_value / _maximum * Width;
            if (progressWidth < radius * 2) progressWidth = radius * 2;
            
            Rectangle progressRect = new Rectangle(0, 0, (int)progressWidth, Height);
            using (GraphicsPath progressPath = GetRoundedPath(progressRect, radius))
            {
                using (SolidBrush pbBrush = new SolidBrush(ThemeColors.Primary))
                {
                    g.FillPath(pbBrush, progressPath);
                }
            }
        }
    }

    private GraphicsPath GetRoundedPath(Rectangle rect, float radius)
    {
        GraphicsPath path = new GraphicsPath();
        float diameter = radius * 2;
        if (rect.Width < diameter) return path;
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseAllFigures();
        return path;
    }
}

public class ModernCard : Panel
{
    public ModernCard()
    {
        this.BackColor = ThemeColors.CardBackground;
        this.Padding = new Padding(20);
        this.DoubleBuffered = true;
        ThemeColors.ThemeChanged += () => { 
            if (!IsDisposed) {
                this.BackColor = ThemeColors.CardBackground;
                Invalidate(); 
            }
        };
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using (GraphicsPath path = GetRoundedPath(rect, 12f))
        {
            // Subtle Border
            using (Pen borderPen = new Pen(ThemeColors.CardBorder, 1.5f))
            {
                g.DrawPath(borderPen, path);
            }
        }
    }

    private GraphicsPath GetRoundedPath(Rectangle rect, float radius)
    {
        GraphicsPath path = new GraphicsPath();
        float diameter = radius * 2;
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseAllFigures();
        return path;
    }
}
