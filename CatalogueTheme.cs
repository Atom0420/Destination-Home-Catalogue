using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace DestinationHome.Catalogue;

internal static class CatalogueChrome
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    internal static void Apply(IntPtr window)
    {
        int enabled = 1;
        DwmSetWindowAttribute(window, 20, ref enabled, sizeof(int));
        int caption = ColorTranslator.ToWin32(Color.FromArgb(10, 17, 23));
        int border = ColorTranslator.ToWin32(Color.FromArgb(34, 51, 62));
        int text = ColorTranslator.ToWin32(Color.FromArgb(226, 238, 243));
        DwmSetWindowAttribute(window, 35, ref caption, sizeof(int));
        DwmSetWindowAttribute(window, 34, ref border, sizeof(int));
        DwmSetWindowAttribute(window, 36, ref text, sizeof(int));
    }

    internal static GraphicsPath Rounded(RectangleF bounds, float radius)
    {
        float diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class CatalogueCard : TableLayoutPanel
{
    internal Color BorderColor { get; set; } = Color.FromArgb(31, 46, 57);

    internal CatalogueCard()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw, true);
        Margin = Padding.Empty;
        BackColor = Color.FromArgb(14, 23, 29);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (Width < 4 || Height < 4) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = CatalogueChrome.Rounded(new RectangleF(0.5F, 0.5F, Width - 1F, Height - 1F), 10F);
        using var border = new Pen(BorderColor);
        e.Graphics.DrawPath(border, path);
    }
}

internal sealed class CatalogueField : Panel
{
    private readonly Control input;

    internal CatalogueField(Control input)
    {
        this.input = input;
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw, true);
        BackColor = Color.FromArgb(18, 30, 38);
        Padding = new Padding(12, 0, 12, 0);
        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        input.Dock = DockStyle.None;
        Controls.Add(input);
        input.Enter += (_, _) => Invalidate();
        input.Leave += (_, _) => Invalidate();
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (input is null) return;
        input.SetBounds(Padding.Left, Math.Max(0, (Height - input.PreferredSize.Height) / 2),
            Math.Max(1, Width - Padding.Horizontal), input.PreferredSize.Height);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (Width < 4 || Height < 4) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = CatalogueChrome.Rounded(new RectangleF(0.5F, 0.5F, Width - 1F, Height - 1F), 6F);
        using var border = new Pen(input.Focused ? Color.FromArgb(22, 207, 244) : Color.FromArgb(44, 64, 76));
        e.Graphics.DrawPath(border, path);
    }
}
