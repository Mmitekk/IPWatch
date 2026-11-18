using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace IpWatch;

internal static class TrayIconFactory
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);

    /// <summary>Иконка-флаг страны в скруглённом квадрате + точка состояния связи.</summary>
    public static Icon CreateFlag(Image flag, Color statusColor)
    {
        const int size = 32;
        using var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            var rect = new RectangleF(0.5f, 0.5f, size - 1, size - 1);
            using var path = Rounded(rect, 7f);

            using (var clip = new Region(path))
            {
                g.SetClip(clip, CombineMode.Replace);

                // вписываем флаг целиком (contain), чтобы были видны вертикальные полосы
                float scale = Math.Min(size / (float)flag.Width, size / (float)flag.Height);
                float w = flag.Width * scale;
                float h = flag.Height * scale;
                g.DrawImage(flag, (size - w) / 2f, (size - h) / 2f, w, h);

                g.ResetClip();
            }

            using (var border = new Pen(Color.FromArgb(110, 255, 255, 255), 1f))
                g.DrawPath(border, path);

            // точка-индикатор качества связи в правом нижнем углу
            using (var ring = new SolidBrush(Color.FromArgb(22, 24, 29)))
                g.FillEllipse(ring, 19.5f, 19.5f, 12f, 12f);
            using (var dot = new SolidBrush(statusColor))
                g.FillEllipse(dot, 21.5f, 21.5f, 8f, 8f);
        }

        return ToIcon(bmp);
    }

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

        return ToIcon(bmp);
    }

    private static Icon ToIcon(Bitmap bmp)
    {
        IntPtr handle = bmp.GetHicon();
        using var temp = Icon.FromHandle(handle);
        var clone = (Icon)temp.Clone();
        DestroyIcon(handle);
        return clone;
    }

    private static GraphicsPath Rounded(RectangleF rect, float radius)
    {
        float d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
