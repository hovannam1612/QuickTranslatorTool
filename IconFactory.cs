using System.Drawing.Drawing2D;
using System.Drawing.Text;
using static QuickTranslator.NativeMethods;

namespace QuickTranslator;

/// <summary>Draws the app icon at runtime so no binary assets are needed.</summary>
internal static class IconFactory
{
    public static Icon AppIcon { get; } = Create();

    private static Icon Create()
    {
        const int size = 64;
        using var bitmap = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            using var path = RoundedRect(new Rectangle(2, 2, size - 4, size - 4), 14);
            using var fill = new LinearGradientBrush(new Rectangle(0, 0, size, size), Color.FromArgb(66, 133, 244), Color.FromArgb(40, 90, 200), 90f);
            g.FillPath(fill, path);

            using var font = new Font("Microsoft YaHei UI", 24f, FontStyle.Bold, GraphicsUnit.Pixel);
            using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("文A", font, Brushes.White, new RectangleF(0, 2, size, size), format);
        }

        IntPtr handle = bitmap.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(handle);
            return (Icon)temp.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    private static GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        int d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.Left, r.Top, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
