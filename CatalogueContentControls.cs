using System.Drawing.Drawing2D;

namespace DestinationHome.Catalogue;

internal sealed class CatalogueGrid : DataGridView
{
    internal event Action<int>? WheelScrolled;
    internal CatalogueGrid() { DoubleBuffered = true; ScrollBars = ScrollBars.None; }
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        // The painted scrollbar owns scrolling; don't let the native grid restore white bars.
        WheelScrolled?.Invoke(-Math.Sign(e.Delta) * Math.Max(1, SystemInformation.MouseWheelScrollLines));
    }
}

internal sealed class CatalogueDetailsViewport : Panel
{
    private readonly CatalogueScrollBar scrollbar;
    internal TableLayoutPanel Content { get; }
    internal CatalogueDetailsViewport(CatalogueScrollBar scrollbar, TableLayoutPanel content)
    {
        this.scrollbar = scrollbar;
        Content = content;
        DoubleBuffered = true;
        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Controls.Add(content);
        scrollbar.ValueChanged += (_, _) => PositionContent();
        Resize += (_, _) => PositionContent();
    }
    internal void PositionContent()
    {
        scrollbar.Total = Content.Height;
        scrollbar.Viewport = ClientSize.Height;
        scrollbar.Value = scrollbar.Value;
        Content.Location = new Point(0, -scrollbar.Value);
        Content.Width = ClientSize.Width;
        scrollbar.Invalidate();
    }
    internal void ScrollBy(int amount) => scrollbar.Value += amount;
    protected override void OnMouseWheel(MouseEventArgs e) => ScrollBy(-Math.Sign(e.Delta) * Math.Max(36, Font.Height * 3));
}

internal sealed class CatalogueDescription : RichTextBox
{
    internal Action<int>? ScrollRequested { get; set; }
    protected override void OnMouseWheel(MouseEventArgs e) => ScrollRequested?.Invoke(-Math.Sign(e.Delta) * Math.Max(36, Font.Height * 3));
}

internal sealed class CataloguePreview : Control
{
    private readonly CatalogueAppearance appearance;
    private readonly System.Windows.Forms.Timer animation = new() { Interval = 16 };
    private Image? image;
    private float reveal = 1;
    private bool hovered;
    internal Image? Image { get => image; set { image = value; reveal = appearance.Animated && appearance.Reactive ? 0 : 1; if (reveal < 1) animation.Start(); Invalidate(); } }
    internal CataloguePreview(CatalogueAppearance appearance)
    {
        this.appearance = appearance;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        animation.Tick += (_, _) => { reveal = Math.Min(1, reveal + 0.09F); if (reveal >= 1) animation.Stop(); Invalidate(); };
        appearance.Changed += AppearanceChanged;
    }
    private void AppearanceChanged(object? sender, EventArgs e) { animation.Stop(); reveal = 1; Invalidate(); }
    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hovered = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hovered = false; Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var p = appearance.Palette;
        e.Graphics.Clear(p.Background);
        if (Width < 24 || Height < 24) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var shape = CatalogueChrome.Rounded(new RectangleF(.5F, .5F, Width - 1, Height - 1), 8);
        using var border = new Pen(hovered && appearance.Reactive ? p.Accent : p.Border);
        e.Graphics.DrawPath(border, shape);
        if (image is null)
        {
            TextRenderer.DrawText(e.Graphics, "No public picture available", Font, Rectangle.Inflate(ClientRectangle, -12, -12), p.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
            return;
        }
        // Hover never zooms/crops the picture: the entire item stays visible.
        float ratio = Math.Min((Width - 24F) / image.Width, (Height - 24F) / image.Height);
        int w = Math.Max(1, (int)(image.Width * ratio)), h = Math.Max(1, (int)(image.Height * ratio));
        e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        e.Graphics.DrawImage(image, new Rectangle((Width - w) / 2, (Height - h) / 2, w, h));
        if (reveal < 1) { using var veil = new SolidBrush(Color.FromArgb((int)(255 * (1 - reveal)), p.Background)); e.Graphics.FillRectangle(veil, Rectangle.Inflate(ClientRectangle, -1, -1)); }
    }
    protected override void Dispose(bool disposing) { if (disposing) { animation.Dispose(); appearance.Changed -= AppearanceChanged; } base.Dispose(disposing); }
}
