using System.Drawing.Drawing2D;
using Microsoft.Win32;

namespace QuickTranslator;

/// <summary>Colour palette that follows the Windows light/dark app theme.</summary>
internal sealed record Theme(
    Color Window, Color Card, Color Border, Color Text, Color Muted,
    Color Accent, Color AccentHover, Color AccentText, Color Subtle, Color SubtleHover, Color Error)
{
    public static readonly Theme Dark = new(
        Window: Color.FromArgb(0x1B, 0x1C, 0x20), Card: Color.FromArgb(0x25, 0x26, 0x2B), Border: Color.FromArgb(0x36, 0x38, 0x40),
        Text: Color.FromArgb(0xEC, 0xED, 0xF1), Muted: Color.FromArgb(0x8F, 0x93, 0x9E),
        Accent: Color.FromArgb(0x4C, 0x8D, 0xF6), AccentHover: Color.FromArgb(0x6B, 0xA2, 0xFA), AccentText: Color.White,
        Subtle: Color.FromArgb(0x33, 0x35, 0x3D), SubtleHover: Color.FromArgb(0x3F, 0x41, 0x4A), Error: Color.FromArgb(0xF2, 0x77, 0x7A));

    public static readonly Theme Light = new(
        Window: Color.FromArgb(0xF2, 0xF4, 0xF8), Card: Color.White, Border: Color.FromArgb(0xDD, 0xE0, 0xE8),
        Text: Color.FromArgb(0x1F, 0x23, 0x28), Muted: Color.FromArgb(0x6B, 0x72, 0x80),
        Accent: Color.FromArgb(0x2F, 0x6F, 0xED), AccentHover: Color.FromArgb(0x4A, 0x84, 0xF5), AccentText: Color.White,
        Subtle: Color.FromArgb(0xEA, 0xEC, 0xF2), SubtleHover: Color.FromArgb(0xDD, 0xE1, 0xEA), Error: Color.FromArgb(0xD1, 0x43, 0x43));

    public static Theme Current => IsDarkMode() ? Dark : Light;

    private static bool IsDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>Panel drawn as a rounded, bordered card.</summary>
internal sealed class Card : Panel
{
    public Color Fill { get; set; } = Color.White;
    public Color BorderColor { get; set; } = Color.Gainsboro;
    public int Radius { get; set; } = 12;

    public Card()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new RectangleF(0.5f, 0.5f, Width - 2, Height - 2);
        using var path = RoundedPath(rect, Radius);
        using var fill = new SolidBrush(Fill);
        using var pen = new Pen(BorderColor);
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(pen, path);
    }

    public static GraphicsPath RoundedPath(RectangleF r, float radius)
    {
        float d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.Left, r.Top, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

/// <summary>Flat button with rounded corners and a hover colour.</summary>
internal sealed class RoundedButton : Button
{
    private bool _hover;
    private bool _pressed;

    public Color NormalColor { get; set; } = Color.Gainsboro;
    public Color HoverColor { get; set; } = Color.Silver;
    public Color TextColor { get; set; } = Color.Black;
    public int Radius { get; set; } = 8;

    public RoundedButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
        TabStop = false;
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new RectangleF(0.5f, 0.5f, Width - 2, Height - 2);
        using var path = Card.RoundedPath(rect, Radius);
        Color fill = _pressed || _hover ? HoverColor : NormalColor;
        using var brush = new SolidBrush(fill);
        g.FillPath(brush, path);

        TextRenderer.DrawText(g, Text, Font, ClientRectangle, TextColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
    }
}
