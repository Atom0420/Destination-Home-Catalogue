using System.Drawing.Drawing2D;

namespace DestinationHome.Catalogue;

internal sealed class CatalogueButton : Button
{
    private readonly CatalogueAppearance appearance;
    private readonly System.Windows.Forms.Timer animation = new() { Interval = 16 };
    private bool hovered;
    private bool pressed;
    private float hover;
    private float ripple = 1;
    private Point clickPoint;
    internal bool Primary { get; set; }

    internal CatalogueButton(CatalogueAppearance appearance)
    {
        this.appearance = appearance;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Cursor = Cursors.Hand;
        Font = new Font("Segoe UI Semibold", 8.5F);
        animation.Tick += (_, _) =>
        {
            float target = hovered && appearance.Reactive ? 1 : 0;
            hover += (target - hover) * 0.22F;
            ripple = Math.Min(1, ripple + 0.07F);
            if (Math.Abs(target - hover) < 0.015F && ripple >= 1) { hover = target; animation.Stop(); }
            Invalidate();
        };
        appearance.Changed += AppearanceChanged;
    }

    private void AppearanceChanged(object? sender, EventArgs e) { animation.Stop(); hover = 0; ripple = 1; Invalidate(); }
    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hovered = true; Animate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hovered = false; pressed = false; Animate(); }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); pressed = true; clickPoint = e.Location; ripple = appearance.Animated && appearance.Reactive ? 0 : 1; Animate(); }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); pressed = false; Animate(); }
    private void Animate()
    {
        if (appearance.Animated && appearance.Reactive) animation.Start();
        else { animation.Stop(); hover = hovered && appearance.Reactive ? 1 : 0; ripple = 1; }
        Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width < 4 || Height < 4) return;
        var palette = appearance.Palette;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Parent?.BackColor ?? palette.Background);
        var bounds = new RectangleF(0.5F, 0.5F, Width - 1, Height - 1);
        using var path = CatalogueChrome.Rounded(bounds, 7F);
        Color fill = Primary ? CatalogueAppearance.Blend(palette.Input, palette.Accent, 0.12F) : palette.Input;
        fill = CatalogueAppearance.Blend(fill, palette.Hover, pressed && appearance.Reactive ? 1 : hover);
        using var brush = new SolidBrush(fill);
        e.Graphics.FillPath(brush, path);
        if (ripple < 1 && appearance.Reactive)
        {
            var state = e.Graphics.Save();
            e.Graphics.SetClip(path);
            float radius = Math.Max(Width, Height) * ripple;
            using var rippleBrush = new SolidBrush(Color.FromArgb((int)(30 * (1 - ripple)), palette.Accent));
            e.Graphics.FillEllipse(rippleBrush, clickPoint.X - radius, clickPoint.Y - radius, radius * 2, radius * 2);
            e.Graphics.Restore(state);
        }
        using var border = new Pen(CatalogueAppearance.Blend(Primary ? palette.Accent : palette.Border, palette.Accent, hover * 0.65F));
        e.Graphics.DrawPath(border, path);
        TextRenderer.DrawText(e.Graphics, Text, Font, Rectangle.Inflate(ClientRectangle, -8, -2),
            !Enabled ? palette.Muted : Primary ? palette.Accent : palette.Text,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -5, -5), palette.Accent, fill);
    }
    protected override void Dispose(bool disposing) { if (disposing) { appearance.Changed -= AppearanceChanged; animation.Dispose(); } base.Dispose(disposing); }
}

internal sealed class CatalogueDropdown : Control
{
    private readonly CatalogueAppearance appearance;
    private ToolStripDropDown? popup;
    private int selectedIndex = -1;
    private bool hovered;
    internal List<string> Items { get; } = new();
    internal event EventHandler? SelectedIndexChanged;
    internal Control? PopupContent => popup?.Items.OfType<ToolStripControlHost>().FirstOrDefault()?.Control;
    internal bool PopupVisible => popup?.Visible == true;
    internal void CloseChoices() => popup?.Close();
    internal void Navigate(Keys key) => OnKeyDown(new KeyEventArgs(key));
    internal object? SelectedItem { get => selectedIndex >= 0 && selectedIndex < Items.Count ? Items[selectedIndex] : null; set => SelectedIndex = value is null ? -1 : Items.IndexOf(value.ToString()!); }
    internal int SelectedIndex
    {
        get => selectedIndex;
        set
        {
            int next = Math.Clamp(value, -1, Items.Count - 1);
            if (next == selectedIndex) return;
            selectedIndex = next;
            Invalidate();
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    internal CatalogueDropdown(CatalogueAppearance appearance)
    {
        this.appearance = appearance;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        TabStop = true;
        Cursor = Cursors.Hand;
        AccessibleRole = AccessibleRole.ComboBox;
        Font = new Font("Segoe UI", 10F);
        appearance.Changed += AppearanceChanged;
    }
    private void AppearanceChanged(object? sender, EventArgs e) { Invalidate(); popup?.Invalidate(true); }
    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hovered = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hovered = false; Invalidate(); }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    protected override void OnClick(EventArgs e) { base.OnClick(e); ShowChoices(); }
    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.Home or Keys.End || base.IsInputKey(keyData);
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is Keys.Space or Keys.Enter or Keys.F4 || e.Alt && e.KeyCode == Keys.Down) { ShowChoices(); e.Handled = true; }
        else if (e.KeyCode == Keys.Down) { SelectedIndex = Math.Min(Items.Count - 1, selectedIndex + 1); e.Handled = true; }
        else if (e.KeyCode == Keys.Up) { SelectedIndex = Math.Max(0, selectedIndex - 1); e.Handled = true; }
        else if (e.KeyCode == Keys.Home) { SelectedIndex = 0; e.Handled = true; }
        else if (e.KeyCode == Keys.End) { SelectedIndex = Items.Count - 1; e.Handled = true; }
    }
    protected override void OnMouseWheel(MouseEventArgs e) { if (Focused) SelectedIndex = Math.Clamp(selectedIndex - Math.Sign(e.Delta), 0, Items.Count - 1); else base.OnMouseWheel(e); }
    internal void ShowChoices()
    {
        if (Items.Count == 0) return;
        if (popup?.Visible == true) { popup.Close(); return; }
        Focus();
        popup?.Dispose();
        var choices = new ChoiceList(appearance, Items, selectedIndex, Width, DeviceDpi);
        popup = new ToolStripDropDown { AutoSize = true, Padding = new Padding(1), BackColor = appearance.Palette.Border, DropShadowEnabled = true };
        // Register the nested menu with its containing drawer, so Windows keeps both open.
        for (Control? ancestor = Parent; ancestor is not null; ancestor = ancestor.Parent)
            if (ancestor is ToolStripDropDown enclosing)
            {
                popup.OwnerItem = enclosing.Items.OfType<ToolStripControlHost>().FirstOrDefault();
                break;
            }
        var host = new ToolStripControlHost(choices) { Margin = Padding.Empty, Padding = Padding.Empty, AutoSize = false, Size = choices.Size };
        popup.Items.Add(host);
        choices.Chosen += index => { SelectedIndex = index; popup.Close(); Focus(); };
        choices.Cancelled += () => { popup.Close(); Focus(); };
        popup.Show(this, new Point(0, Height + 4));
        choices.Focus();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width < 4 || Height < 4) return;
        var p = appearance.Palette;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Parent?.BackColor ?? p.Surface);
        using var shape = CatalogueChrome.Rounded(new RectangleF(0.5F, 0.5F, Width - 1, Height - 1), 6);
        using var fill = new SolidBrush(hovered && appearance.Reactive ? p.Hover : p.Input);
        using var border = new Pen(Focused || hovered && appearance.Reactive ? p.Accent : p.Border);
        e.Graphics.FillPath(fill, shape); e.Graphics.DrawPath(border, shape);
        int inset = Math.Max(10, (int)(12 * DeviceDpi / 96F));
        TextRenderer.DrawText(e.Graphics, SelectedItem?.ToString() ?? "Choose…", Font,
            new Rectangle(inset, 0, Math.Max(1, Width - inset - 30), Height), p.Text,
            TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        using var arrow = new Pen(p.Muted, 1.5F) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        int x = Width - inset - 5, y = Height / 2;
        e.Graphics.DrawLines(arrow, new[] { new Point(x - 4, y - 2), new Point(x, y + 2), new Point(x + 4, y - 2) });
    }
    protected override void Dispose(bool disposing) { if (disposing) { appearance.Changed -= AppearanceChanged; popup?.Dispose(); } base.Dispose(disposing); }

    private sealed class ChoiceList : Control
    {
        private readonly CatalogueAppearance appearance;
        private readonly List<string> items;
        private readonly int rowHeight;
        private readonly int selected;
        private int hot;
        private int first;
        internal event Action<int>? Chosen;
        internal event Action? Cancelled;
        internal void Navigate(Keys key) => OnKeyDown(new KeyEventArgs(key));
        internal ChoiceList(CatalogueAppearance appearance, List<string> items, int selected, int width, int dpi)
        {
            this.appearance = appearance; this.items = items; this.selected = selected; hot = Math.Max(0, selected);
            Font = new Font("Segoe UI", 10F);
            rowHeight = Math.Max(Font.Height + 16, (int)(36 * dpi / 96F));
            int maxHeight = Math.Max(rowHeight * 2, Screen.FromPoint(Cursor.Position).WorkingArea.Height - 100);
            Size = new Size(Math.Max(180, width - 2), Math.Min(items.Count * rowHeight + 8, maxHeight));
            TabStop = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }
        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); hot = Math.Clamp(first + (e.Y - 4) / rowHeight, 0, items.Count - 1); Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) Chosen?.Invoke(Math.Clamp(first + (e.Y - 4) / rowHeight, 0, items.Count - 1)); }
        protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.Home or Keys.End || base.IsInputKey(keyData);
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Down) hot = Math.Min(items.Count - 1, hot + 1);
            if (e.KeyCode == Keys.Up) hot = Math.Max(0, hot - 1);
            if (e.KeyCode == Keys.Home) hot = 0;
            if (e.KeyCode == Keys.End) hot = items.Count - 1;
            if (e.KeyCode is Keys.Enter or Keys.Space) Chosen?.Invoke(hot);
            if (e.KeyCode == Keys.Escape) Cancelled?.Invoke();
            if (e.KeyCode is Keys.Up or Keys.Down or Keys.Home or Keys.End or Keys.Enter or Keys.Space or Keys.Escape) e.SuppressKeyPress = true;
            EnsureVisible(); Invalidate();
        }
        protected override void OnMouseWheel(MouseEventArgs e) { hot = Math.Clamp(hot - Math.Sign(e.Delta), 0, items.Count - 1); EnsureVisible(); Invalidate(); }
        private void EnsureVisible() { int visible = Math.Max(1, (Height - 8) / rowHeight); if (hot < first) first = hot; if (hot >= first + visible) first = hot - visible + 1; }
        protected override void OnPaint(PaintEventArgs e)
        {
            var p = appearance.Palette;
            e.Graphics.Clear(p.Input);
            for (int index = first; index < items.Count; index++)
            {
                var row = new Rectangle(4, 4 + (index - first) * rowHeight, Width - 8, rowHeight);
                if (row.Bottom > Height - 4) break;
                if (index == hot) { using var fill = new SolidBrush(p.Hover); e.Graphics.FillRectangle(fill, row); }
                TextRenderer.DrawText(e.Graphics, items[index], Font, new Rectangle(row.X + 10, row.Y, row.Width - 38, row.Height), index == selected ? p.Accent : p.Text,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
                if (index == selected) TextRenderer.DrawText(e.Graphics, "✓", Font, new Rectangle(row.Right - 28, row.Y, 24, row.Height), p.Accent, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
            }
        }
    }

    internal void NavigateChoices(Keys key)
    {
        if (PopupContent is ChoiceList choices) choices.Navigate(key);
    }
}

internal sealed class CatalogueToggle : Control
{
    private readonly CatalogueAppearance appearance;
    private bool value;
    internal bool Checked { get => value; set { if (this.value == value) return; this.value = value; Invalidate(); CheckedChanged?.Invoke(this, EventArgs.Empty); } }
    internal event EventHandler? CheckedChanged;
    internal CatalogueToggle(CatalogueAppearance appearance)
    {
        this.appearance = appearance;
        Dock = DockStyle.Fill; Margin = Padding.Empty; Cursor = Cursors.Hand; TabStop = true;
        AccessibleRole = AccessibleRole.CheckButton;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
        appearance.Changed += AppearanceChanged;
    }
    private void AppearanceChanged(object? sender, EventArgs e) => Invalidate();
    protected override void OnClick(EventArgs e) { base.OnClick(e); Checked = !Checked; }
    protected override void OnKeyDown(KeyEventArgs e) { base.OnKeyDown(e); if (e.KeyCode is Keys.Space or Keys.Enter) { Checked = !Checked; e.Handled = true; } }
    protected override void OnPaint(PaintEventArgs e)
    {
        var p = appearance.Palette;
        e.Graphics.Clear(p.Surface); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(0, 0, Width - 64, Height), p.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        var track = new RectangleF(Width - 48, (Height - 22) / 2F, 44, 22);
        using var shape = CatalogueChrome.Rounded(track, 11);
        using var fill = new SolidBrush(value ? p.Accent : p.Border); e.Graphics.FillPath(fill, shape);
        using var knob = new SolidBrush(value ? p.Background : p.Text);
        e.Graphics.FillEllipse(knob, value ? track.Right - 19 : track.Left + 3, track.Top + 3, 16, 16);
        if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -2, -2), p.Accent, p.Surface);
    }
    protected override void Dispose(bool disposing) { if (disposing) appearance.Changed -= AppearanceChanged; base.Dispose(disposing); }
}

internal sealed class CatalogueScrollBar : Control
{
    private readonly CatalogueAppearance appearance;
    private int current;
    private bool hovered;
    private bool dragging;
    private int dragStart, valueStart;
    internal int Total { get; set; }
    internal int Viewport { get; set; }
    internal int Maximum => Math.Max(0, Total - Viewport);
    internal int Value { get => current; set { int next = Math.Clamp(value, 0, Maximum); if (current == next) return; current = next; Invalidate(); ValueChanged?.Invoke(this, EventArgs.Empty); } }
    internal event EventHandler? ValueChanged;
    internal CatalogueScrollBar(CatalogueAppearance appearance)
    {
        this.appearance = appearance;
        Dock = DockStyle.Fill; Margin = Padding.Empty; Cursor = Cursors.Hand; TabStop = true; AccessibleRole = AccessibleRole.ScrollBar;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        appearance.Changed += AppearanceChanged;
    }
    private void AppearanceChanged(object? sender, EventArgs e) => Invalidate();
    private Rectangle Thumb
    {
        get
        {
            int trackHeight = Math.Max(1, Height - 12);
            int length = Math.Clamp((int)(trackHeight * (double)Viewport / Math.Max(1, Total)), Math.Min(28, trackHeight), trackHeight);
            int top = 6 + (Maximum == 0 ? 0 : (int)((trackHeight - length) * (double)current / Maximum));
            return new Rectangle(4, top, Math.Max(2, Width - 8), length);
        }
    }
    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hovered = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hovered = false; Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        Focus();
        if (Thumb.Contains(e.Location)) { dragging = true; Capture = true; dragStart = e.Y; valueStart = current; }
        else Value += e.Y < Thumb.Top ? -Math.Max(1, Viewport) : Math.Max(1, Viewport);
    }
    protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (dragging) Value = valueStart + (int)((e.Y - dragStart) * (double)Maximum / Math.Max(1, Height - 12 - Thumb.Height)); }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); dragging = false; Capture = false; Invalidate(); }
    protected override void OnMouseCaptureChanged(EventArgs e) { base.OnMouseCaptureChanged(e); if (!Capture) dragging = false; }
    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.PageUp or Keys.PageDown or Keys.Home or Keys.End || base.IsInputKey(keyData);
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        Value = e.KeyCode switch { Keys.Up => current - Math.Max(1, Viewport / 10), Keys.Down => current + Math.Max(1, Viewport / 10), Keys.PageUp => current - Viewport, Keys.PageDown => current + Viewport, Keys.Home => 0, Keys.End => Maximum, _ => current };
        e.Handled = true;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var p = appearance.Palette;
        e.Graphics.Clear(Parent?.BackColor ?? p.Surface);
        if (Maximum == 0) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var shape = CatalogueChrome.Rounded(Thumb, Math.Max(1, Thumb.Width / 2F));
        using var fill = new SolidBrush(appearance.Reactive && (hovered || dragging) ? p.Accent : p.Border);
        e.Graphics.FillPath(fill, shape);
    }
    protected override void Dispose(bool disposing) { if (disposing) appearance.Changed -= AppearanceChanged; base.Dispose(disposing); }
}
