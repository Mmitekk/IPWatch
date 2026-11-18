using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace IpWatch;

internal static class TrayIconFactory
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);

    public static Icon Create(string caption, Color background, Color glyph)
    {
        const int size = 32;
        using var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(Color.Transparent);

            using var circle = new SolidBrush(background);
            g.FillEllipse(circle, 0, 0, size - 1, size - 1);

            var text = caption.Length > 2 ? caption[..2] : caption;
            using var font = new Font("Segoe UI", text.Length > 1 ? 12f : 15f, FontStyle.Bold, GraphicsUnit.Pixel);
            using var brush = new SolidBrush(glyph);
            using var format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            g.DrawString(text.ToUpperInvariant(), font, brush, new RectangleF(0, 0, size, size + 1), format);
        }

        IntPtr handle = bmp.GetHicon();
        using var temp = Icon.FromHandle(handle);
        var clone = (Icon)temp.Clone();
        DestroyIcon(handle);
        return clone;
    }
}
