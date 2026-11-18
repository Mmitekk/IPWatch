using System.Drawing.Drawing2D;

namespace IpWatch;

/// <summary>Чекбокс, который рисуется сам — системный на тёмном фоне выглядит белым квадратом.</summary>
internal sealed class DarkCheckBox : CheckBox
{
    private static readonly Color BoxBack = Color.FromArgb(40, 44, 52);
    private static readonly Color BoxBorder = Color.FromArgb(80, 86, 98);
    private static readonly Color BoxChecked = Color.FromArgb(88, 166, 255);
    private static readonly Color TextColor = Color.FromArgb(168, 175, 188);
    private static readonly Color TextHover = Color.FromArgb(225, 229, 236);

    private const int BoxSize = 15;
    private const int Gap = 7;

    private bool _hover;

    public DarkCheckBox()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        ForeColor = TextColor;
        Font = new Font("Segoe UI", 8.5F);
        Cursor = Cursors.Hand;
        AutoSize = false;
        Height = 20;
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var textSize = TextRenderer.MeasureText(Text, Font);
        return new Size(BoxSize + Gap + textSize.Width + 2, Math.Max(BoxSize, textSize.Height) + 4);
    }

    protected override void OnMouseEnter(EventArgs eventargs)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(eventargs);
    }

    protected override void OnMouseLeave(EventArgs eventargs)
    {
        _hover = false;
        Invalidate();
        base.OnMouseLeave(eventargs);
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var g = pevent.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Color.FromArgb(22, 24, 29));

        var box = new Rectangle(1, (Height - BoxSize) / 2, BoxSize, BoxSize);

        using (var path = Rounded(box, 4))
        {
            using var fill = new SolidBrush(Checked ? BoxChecked : BoxBack);
            g.FillPath(fill, path);
            using var pen = new Pen(Checked ? BoxChecked : _hover ? TextColor : BoxBorder, 1.2f);
            g.DrawPath(pen, path);
        }

        if (Checked)
        {
            using var pen = new Pen(Color.FromArgb(16, 22, 32), 2f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };
            g.DrawLines(pen, new[]
            {
                new PointF(box.Left + 3.5f, box.Top + box.Height / 2f + 0.5f),
                new PointF(box.Left + box.Width / 2f - 0.5f, box.Bottom - 4f),
                new PointF(box.Right - 3f, box.Top + 4f)
            });
        }

        var textRect = new Rectangle(box.Right + Gap, 0, Width - box.Right - Gap, Height);
        TextRenderer.DrawText(g, Text, Font, textRect,
            _hover ? TextHover : TextColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    private static GraphicsPath Rounded(Rectangle rect, float radius)
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
