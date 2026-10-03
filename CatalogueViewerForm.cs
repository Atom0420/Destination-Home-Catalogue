using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;

namespace DestinationHome.Catalogue;

internal sealed class CatalogueViewerForm : Form
{
    private readonly CatalogueAppearance appearance = new();
    private Color Background => appearance.Palette.Background;
    private Color Surface => appearance.Palette.Surface;
    private Color Input => appearance.Palette.Input;
    private Color TextPrimary => appearance.Palette.Text;
    private Color TextMuted => appearance.Palette.Muted;
    private Color Cyan => appearance.Palette.Accent;
    private Color Amber => appearance.Palette.Secondary;
    private Color Green => appearance.Palette.Green;

    private readonly string dataDirectory = Path.Combine(AppContext.BaseDirectory, "Data");
    private readonly JsonSerializerOptions jsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly System.Windows.Forms.Timer filterTimer = new() { Interval = 220 };
    private List<CatalogueItem> items = new();
    private CancellationTokenSource? syncCancellation;
    private Image? displayedImage;
    private readonly Dictionary<string, Image?> thumbnails = new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<string> thumbnailOrder = new();

    private TextBox searchBox = null!;
    private CatalogueDropdown typeFilter = null!;
    private Label countLabel = null!;
    private Label statusLabel = null!;
    private Button syncButton = null!;
    private CatalogueGrid grid = null!;
    private CataloguePreview preview = null!;
    private Label nameLabel = null!;
    private TextBox uuidBox = null!;
    private Label typeLabel = null!;
    private Label versionLabel = null!;
    private CatalogueDescription descriptionBox = null!;
    private CatalogueScrollBar listScroll = null!, detailScroll = null!;
    private CatalogueDetailsViewport detailViewport = null!;
    private TableLayoutPanel detailContent = null!;
    private ToolStripDropDown? appearancePopup;
    private Control? appearancePanel;
    private int hoveredRow = -1;
    private bool updatingDetails;
    private float verificationScale = 1F;
    private Icon? catalogueIcon;
    private Image? catalogueArtwork;
    private bool resourcesReleased;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        CatalogueChrome.Apply(Handle, appearance.Palette);
    }

    public CatalogueViewerForm()
    {
        CataloguePreferences preferences = CataloguePreferences.Load(Path.Combine(dataDirectory, "viewer-settings.json"));
        appearance.Set(preferences.Theme, preferences.Animated, preferences.Reactive);
        InitializeUi();
        appearance.Changed += (_, _) => ApplyPalette();
        ApplyPalette();
        filterTimer.Tick += (_, _) => { filterTimer.Stop(); ApplyFilter(); };
        Shown += (_, _) => LoadSnapshot();
        FormClosing += (_, _) => syncCancellation?.Cancel();
    }

    private void InitializeUi()
    {
        using (Stream stream = typeof(CatalogueViewerForm).Assembly.GetManifestResourceStream("DestinationHome.Catalogue.CatalogueIcon")!)
        using (var source = new Icon(stream)) catalogueIcon = (Icon)source.Clone();
        Icon = catalogueIcon;
        using (Stream stream = typeof(CatalogueViewerForm).Assembly.GetManifestResourceStream("DestinationHome.Catalogue.CatalogueArtwork")!)
        using (Image source = Image.FromStream(stream)) catalogueArtwork = new Bitmap(source);
        Text = "UUID Catalogue · Destination Home";
        ClientSize = new Size(1320, 820);
        MinimumSize = new Size(1100, 680);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        BackColor = Background;
        ForeColor = TextPrimary;
        Font = new Font("Segoe UI", 9F);
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(20, 8, 20, 12), BackColor = Background };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildSearchBar(), 0, 1);
        root.Controls.Add(BuildWorkspace(), 0, 2);
        statusLabel = new Label { Text = "Loading offline catalogue…", Dock = DockStyle.Fill, ForeColor = TextMuted, TextAlign = ContentAlignment.MiddleLeft, Padding = Padding.Empty, AutoEllipsis = true, Margin = Padding.Empty };
        root.Controls.Add(statusLabel, 0, 3);
        Controls.Add(root);
    }

    private Control BuildHeader()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 1, BackColor = Background, Padding = new Padding(0, 6, 0, 12), Margin = Padding.Empty };
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125F));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 152F));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 148F));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 126F));
        var heading = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty, BackColor = Background };
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        heading.RowStyles.Add(new RowStyle(SizeType.Percent, 68F));
        heading.RowStyles.Add(new RowStyle(SizeType.Percent, 32F));
        heading.Controls.Add(new Label { Text = "UUID Catalogue", Dock = DockStyle.Fill, Margin = Padding.Empty, ForeColor = TextPrimary, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI Semibold", 21F), AutoEllipsis = true }, 0, 0);
        heading.Controls.Add(new Label { Text = "DESTINATION HOME  /  OFFLINE LIBRARY", Dock = DockStyle.Fill, Margin = Padding.Empty, ForeColor = TextMuted, Font = new Font("Segoe UI", 8F), AutoEllipsis = true }, 0, 1);
        var branding = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, BackColor = Background };
        branding.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58F));
        branding.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        branding.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        branding.Controls.Add(new PictureBox { Image = catalogueArtwork, SizeMode = PictureBoxSizeMode.Zoom, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 12, 0), AccessibleName = "Catalogue gateway icon" }, 0, 0);
        branding.Controls.Add(heading, 1, 0);
        panel.Controls.Add(branding, 0, 0);
        countLabel = new Label { Text = "0 ITEMS", Dock = DockStyle.Fill, ForeColor = Green, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Consolas", 10F, FontStyle.Bold), Margin = Padding.Empty };
        panel.Controls.Add(countLabel, 1, 0);
        syncButton = CreateButton("UPDATE METADATA", Amber);
        syncButton.Margin = new Padding(8, 11, 0, 11);
        syncButton.Click += async (_, _) => await SyncMetadataAsync();
        panel.Controls.Add(syncButton, 2, 0);
        var folderButton = CreateButton("OPEN DATA FOLDER", TextMuted);
        folderButton.Margin = new Padding(10, 11, 0, 11);
        folderButton.Click += (_, _) => OpenDataFolder();
        panel.Controls.Add(folderButton, 3, 0);
        var themeButton = CreateButton("APPEARANCE", Cyan);
        themeButton.Margin = new Padding(10, 11, 0, 11);
        themeButton.Click += (_, _) => ShowAppearance(themeButton);
        panel.Controls.Add(themeButton, 4, 0);
        return panel;
    }

    private Control BuildSearchBar()
    {
        var panel = new CatalogueCard(appearance) { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(16, 10, 16, 14), Margin = new Padding(0, 0, 0, 14) };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250F));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 18F));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        panel.Controls.Add(Caption("SEARCH"), 0, 0);
        var typeCaption = Caption("ITEM TYPE");
        typeCaption.Margin = new Padding(16, 0, 0, 0);
        panel.Controls.Add(typeCaption, 1, 0);
        searchBox = CreateTextBox();
        searchBox.AccessibleName = "Search catalogue";
        searchBox.PlaceholderText = "Name, UUID, description, or category…";
        searchBox.TextChanged += FilterChanged;
        panel.Controls.Add(new CatalogueField(searchBox, appearance), 0, 1);
        typeFilter = new CatalogueDropdown(appearance) { AccessibleName = "Item type", Margin = new Padding(16, 0, 0, 0) };
        typeFilter.Items.AddRange(new[] { "All types", "Clothing", "Furniture", "Portable", "Scene", "Minigame", "Other", "Unknown" });
        typeFilter.SelectedIndex = 0;
        typeFilter.SelectedIndexChanged += FilterChanged;
        panel.Controls.Add(typeFilter, 1, 1);
        return panel;
    }

    private Control BuildWorkspace()
    {
        var split = new SplitContainer { Size = new Size(1280, 600), Dock = DockStyle.Fill, Margin = Padding.Empty, SplitterWidth = 16, SplitterDistance = 852, Panel1MinSize = 620, Panel2MinSize = 360, BackColor = Background, FixedPanel = FixedPanel.Panel2 };
        grid = CreateGrid();
        grid.AutoGenerateColumns = false;
        grid.Columns.Add(new DataGridViewImageColumn { Name = "Thumbnail", HeaderText = "", Width = 64, ImageLayout = DataGridViewImageCellLayout.Zoom, DefaultCellStyle = new DataGridViewCellStyle { NullValue = null, Padding = new Padding(6) } });
        var nameColumn = Column("ITEM", "DisplayName", 260, true);
        nameColumn.MinimumWidth = 110;
        nameColumn.FillWeight = 44F;
        var uuidColumn = Column("UUID", "Uuid", 300, true);
        uuidColumn.MinimumWidth = 280;
        uuidColumn.FillWeight = 36F;
        uuidColumn.DefaultCellStyle.Font = new Font("Consolas", 9F);
        var typeColumn = Column("TYPE", "TypeLabel", 170, true);
        typeColumn.MinimumWidth = 135;
        typeColumn.FillWeight = 20F;
        grid.Columns.AddRange(nameColumn, uuidColumn, typeColumn);
        grid.SelectionChanged += (_, _) => ShowSelectedItem();
        grid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.CellStyle is not null)
                e.CellStyle.BackColor = appearance.Reactive && e.RowIndex == hoveredRow ? appearance.Palette.Hover : Surface;
            if (e.ColumnIndex == 0 && e.RowIndex >= 0 && grid.Rows[e.RowIndex].DataBoundItem is CatalogueItem item)
            {
                e.Value = Thumbnail(item);
                e.FormattingApplied = true;
            }
        };
        grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0 && grid.Rows[e.RowIndex].DataBoundItem is CatalogueItem item) Clipboard.SetText(item.Uuid); };
        grid.CellMouseEnter += (_, e) => { int previous = hoveredRow; hoveredRow = e.RowIndex; if (previous >= 0 && previous < grid.RowCount) grid.InvalidateRow(previous); if (hoveredRow >= 0) grid.InvalidateRow(hoveredRow); };
        grid.MouseLeave += (_, _) => { int previous = hoveredRow; hoveredRow = -1; if (previous >= 0 && previous < grid.RowCount) grid.InvalidateRow(previous); };
        grid.RowPostPaint += (_, e) => { if (appearance.Reactive && grid.Rows[e.RowIndex].Selected) { using var brush = new SolidBrush(Cyan); e.Graphics.FillRectangle(brush, e.RowBounds.Left, e.RowBounds.Top + 10, 3, Math.Max(1, e.RowBounds.Height - 20)); } };
        var listCard = new CatalogueCard(appearance) { Dock = DockStyle.Fill, Padding = new Padding(1), ColumnCount = 2, RowCount = 1 };
        listCard.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        listCard.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 18F));
        listCard.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        listCard.Controls.Add(grid, 0, 0);
        grid.Dock = DockStyle.Top;
        listCard.Layout += (_, _) =>
        {
            int available = listCard.ClientSize.Height - listCard.Padding.Vertical;
            int rowHeight = grid.RowCount > 0 ? grid.Rows[0].Height : grid.RowTemplate.Height;
            int rows = Math.Max(0, (available - grid.ColumnHeadersHeight) / Math.Max(1, rowHeight));
            int height = Math.Min(available, grid.ColumnHeadersHeight + rows * rowHeight);
            if (height > 0 && grid.Height != height) grid.Height = height;
        };
        listScroll = new CatalogueScrollBar(appearance) { AccessibleName = "Catalogue list scroll" };
        listScroll.ValueChanged += (_, _) => { if (grid.RowCount > 0 && grid.FirstDisplayedScrollingRowIndex != listScroll.Value) grid.FirstDisplayedScrollingRowIndex = listScroll.Value; };
        grid.WheelScrolled += rows => listScroll.Value += rows;
        grid.Scroll += (_, _) => UpdateListScroll();
        grid.SizeChanged += (_, _) => UpdateListScroll();
        grid.DataBindingComplete += (_, _) => UpdateListScroll();
        listCard.Controls.Add(listScroll, 1, 0);
        split.Panel1.Controls.Add(listCard);
        split.Panel2.Controls.Add(BuildDetails());
        return split;
    }

    private Control BuildDetails()
    {
        var panel = new CatalogueCard(appearance) { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(18, 14, 12, 16) };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 16F));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
        detailContent = new TableLayoutPanel { ColumnCount = 1, RowCount = 8, Margin = Padding.Empty, BackColor = Surface };
        detailContent.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (float height in new[] { 26F, 200F, 64F, 20F, 40F, 28F, 42F, 140F }) detailContent.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        detailScroll = new CatalogueScrollBar(appearance) { AccessibleName = "Item details scroll" };
        detailViewport = new CatalogueDetailsViewport(detailScroll, detailContent) { BackColor = Surface };
        detailViewport.SizeChanged += (_, _) => ResizeDetails();
        detailContent.Controls.Add(Caption("ITEM DETAILS"), 0, 0);
        preview = new CataloguePreview(appearance) { Dock = DockStyle.Fill, BackColor = Background, Margin = new Padding(0, 0, 0, 12) };
        detailContent.Controls.Add(preview, 0, 1);
        nameLabel = new Label { Text = "Select an item", Dock = DockStyle.Fill, ForeColor = TextPrimary, Font = new Font("Segoe UI Semibold", 13F), Margin = Padding.Empty, Padding = new Padding(0, 3, 0, 3) };
        detailContent.Controls.Add(nameLabel, 0, 2);
        detailContent.Controls.Add(Caption("UUID"), 0, 3);
        uuidBox = CreateTextBox(readOnly: true);
        detailContent.Controls.Add(new CatalogueField(uuidBox, appearance), 0, 4);
        typeLabel = new Label { Dock = DockStyle.Fill, ForeColor = Cyan, TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty };
        versionLabel = new Label { Dock = DockStyle.Fill, ForeColor = TextMuted, TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty };
        detailContent.Controls.Add(typeLabel, 0, 5);
        detailContent.Controls.Add(versionLabel, 0, 6);
        descriptionBox = new CatalogueDescription { Dock = DockStyle.Fill, BackColor = Surface, ForeColor = TextPrimary, BorderStyle = BorderStyle.None, ScrollBars = RichTextBoxScrollBars.None, ReadOnly = true, DetectUrls = false, Margin = new Padding(0, 0, 0, 6), Font = new Font("Segoe UI", 9F) };
        descriptionBox.ScrollRequested = detailViewport.ScrollBy;
        detailContent.Controls.Add(descriptionBox, 0, 7);
        // Labels and pictures forward wheel scrolling without needing to focus a text field.
        foreach (Control child in detailContent.Controls) child.MouseWheel += (_, e) => detailViewport.ScrollBy(-Math.Sign(e.Delta) * Math.Max(36, Font.Height * 3));
        panel.Controls.Add(detailViewport, 0, 0);
        panel.Controls.Add(detailScroll, 1, 0);
        var copy = CreateButton("COPY UUID", Cyan);
        copy.Margin = new Padding(0, 8, 0, 0);
        copy.Click += (_, _) => { if (uuidBox.TextLength > 0) Clipboard.SetText(uuidBox.Text); };
        panel.Controls.Add(copy, 0, 1);
        panel.SetColumnSpan(copy, 2);
        return panel;
    }

    private void ResizeDetails()
    {
        if (updatingDetails || descriptionBox is null || detailViewport.Width < 10) return;
        updatingDetails = true;
        try
        {
            float scale = DeviceDpi / 96F * verificationScale;
            int width = Math.Max(1, detailViewport.ClientSize.Width - 8);
            int Measure(string text, Font font) => TextRenderer.MeasureText(text, font, new Size(width, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
            float[] heights = { 26 * scale, 200 * scale, Math.Max(42 * scale, Measure(nameLabel.Text, nameLabel.Font) + 12 * scale), 20 * scale, 40 * scale,
                Math.Max(28 * scale, Measure(typeLabel.Text, typeLabel.Font) + 8 * scale), Math.Max(36 * scale, Measure(versionLabel.Text, versionLabel.Font) + 8 * scale),
                Math.Max(100 * scale, Measure(descriptionBox.Text, descriptionBox.Font) + descriptionBox.Font.Height * 3) };
            detailContent.SuspendLayout();
            for (int row = 0; row < heights.Length; row++) detailContent.RowStyles[row].Height = heights[row];
            detailContent.Size = new Size(detailViewport.ClientSize.Width, (int)Math.Ceiling(heights.Sum()));
            detailContent.ResumeLayout(true);
            detailViewport.PositionContent();
        }
        finally { updatingDetails = false; }
    }

    private void UpdateListScroll()
    {
        if (listScroll is null) return;
        listScroll.Total = grid.RowCount;
        listScroll.Viewport = Math.Max(1, grid.DisplayedRowCount(false));
        listScroll.Value = Math.Max(0, grid.FirstDisplayedScrollingRowIndex);
        listScroll.Invalidate();
    }

    private Control BuildAppearancePanel(bool savePreferences = true)
    {
        float scale = DeviceDpi / 96F * verificationScale;
        var panel = new CatalogueCard(appearance) { Size = new Size((int)(320 * scale), (int)(300 * scale)), Padding = new Padding((int)(20 * scale)), ColumnCount = 1, RowCount = 7 };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (float height in new[] { 30F, 22F, 42F, 16F, 44F, 44F, 62F }) panel.RowStyles.Add(new RowStyle(SizeType.Absolute, height * scale));
        panel.Controls.Add(new Label { Text = "Make it yours", Font = new Font("Segoe UI Semibold", 13F), ForeColor = TextPrimary, Dock = DockStyle.Fill, Margin = Padding.Empty }, 0, 0);
        panel.Controls.Add(Caption("COLOUR THEME"), 0, 1);
        var theme = new CatalogueDropdown(appearance) { AccessibleName = "Colour theme" };
        theme.Items.AddRange(CataloguePalette.All.Select(palette => palette.Name));
        theme.SelectedItem = appearance.Palette.Name;
        panel.Controls.Add(theme, 0, 2);
        var animated = new CatalogueToggle(appearance) { Text = "Animated transitions", Checked = appearance.Animated, AccessibleName = "Animated transitions" };
        var reactive = new CatalogueToggle(appearance) { Text = "Reactive interactions", Checked = appearance.Reactive, AccessibleName = "Reactive interactions" };
        panel.Controls.Add(animated, 0, 4);
        panel.Controls.Add(reactive, 0, 5);
        panel.Controls.Add(new Label { Text = "Hover highlights, click ripples and preview reveals. Switch both off for a quiet layout.\nSaved automatically on this PC.", Font = new Font("Segoe UI", 8.5F), ForeColor = TextMuted, Dock = DockStyle.Fill, Margin = Padding.Empty }, 0, 6);
        void Changed(object? sender, EventArgs e)
        {
            appearance.Set(theme.SelectedItem?.ToString() ?? "Midnight", animated.Checked, reactive.Checked);
            if (!savePreferences) return;
            try { new CataloguePreferences { Theme = appearance.Palette.Name, Animated = appearance.Animated, Reactive = appearance.Reactive }.Save(Path.Combine(dataDirectory, "viewer-settings.json")); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { statusLabel.Text = "Appearance changed; settings could not be saved to this folder."; }
        }
        theme.SelectedIndexChanged += Changed;
        animated.CheckedChanged += Changed;
        reactive.CheckedChanged += Changed;
        ApplyControlPalette(panel);
        return panel;
    }

    private void ShowAppearance(Control anchor, bool savePreferences = true)
    {
        if (appearancePopup?.Visible == true) { appearancePopup.Close(); return; }
        appearancePopup?.Dispose();
        appearancePanel = BuildAppearancePanel(savePreferences);
        appearancePopup = new ToolStripDropDown { Padding = new Padding(1), BackColor = appearance.Palette.Border, DropShadowEnabled = true };
        appearancePopup.Items.Add(new ToolStripControlHost(appearancePanel) { AutoSize = false, Size = appearancePanel.Size, Margin = Padding.Empty, Padding = Padding.Empty });
        appearancePopup.Show(anchor, new Point(anchor.Width - appearancePanel.Width, anchor.Height + 8));
    }

    private void ApplyPalette()
    {
        ApplyControlPalette(this);
        if (appearancePanel is not null && !appearancePanel.IsDisposed) ApplyControlPalette(appearancePanel);
        if (appearancePopup is not null) appearancePopup.BackColor = appearance.Palette.Border;
        var p = appearance.Palette;
        grid.BackgroundColor = Surface; grid.GridColor = p.Border;
        grid.DefaultCellStyle.BackColor = Surface; grid.DefaultCellStyle.ForeColor = TextPrimary;
        grid.DefaultCellStyle.SelectionBackColor = p.Selected; grid.DefaultCellStyle.SelectionForeColor = TextPrimary;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Surface; grid.ColumnHeadersDefaultCellStyle.ForeColor = TextMuted;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Surface; grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = TextPrimary;
        grid.Invalidate();
        if (IsHandleCreated) CatalogueChrome.Apply(Handle, p);
        Invalidate(true);
    }

    private sealed record ColourRoles(string Background, string Foreground);
    private void ApplyControlPalette(Control control)
    {
        string Role(Color colour, string fallback) => colour == TextPrimary ? "text" : colour == TextMuted ? "muted" : colour == Cyan ? "accent" : colour == Green ? "green" : colour == Input ? "input" : colour == Surface ? "surface" : colour == Background ? "background" : fallback;
        Color Colour(string role) => role switch { "text" => TextPrimary, "muted" => TextMuted, "accent" => Cyan, "green" => Green, "input" => Input, "surface" => Surface, _ => Background };
        // Roles are captured once, so changing palette cannot confuse similar colours.
        if (control.Tag is not ColourRoles) control.Tag = new ColourRoles(Role(control.BackColor, "background"), Role(control.ForeColor, "text"));
        var roles = (ColourRoles)control.Tag;
        control.BackColor = Colour(roles.Background); control.ForeColor = Colour(roles.Foreground);
        if (control is CatalogueCard card) { card.BackColor = Surface; card.BorderColor = appearance.Palette.Border; }
        if (control is CatalogueField) control.BackColor = Input;
        if (control is CatalogueDropdown) { control.BackColor = Input; control.ForeColor = TextPrimary; }
        foreach (Control child in control.Controls) ApplyControlPalette(child);
        control.Invalidate();
    }

    private void LoadSnapshot()
    {
        string path = Path.Combine(dataDirectory, "catalogue.json");
        if (!File.Exists(path)) { statusLabel.Text = "Offline snapshot not found. Use UPDATE METADATA while online."; return; }
        try
        {
            CatalogueSnapshot snapshot = JsonSerializer.Deserialize<CatalogueSnapshot>(File.ReadAllText(path), jsonOptions) ?? new();
            items = snapshot.Items;
            ApplyFilter();
            statusLabel.Text = $"Offline • snapshot {snapshot.DownloadedAtUtc.ToLocalTime():g} • images: {ImageArchiveStatus()}";
        }
        catch (Exception exception) when (exception is IOException or JsonException) { statusLabel.Text = $"Could not load offline snapshot: {exception.Message}"; }
    }

    private void FilterChanged(object? sender, EventArgs e) { filterTimer.Stop(); filterTimer.Start(); }

    private void ApplyFilter()
    {
        string search = searchBox.Text.Trim();
        string type = typeFilter.SelectedItem?.ToString() ?? "All types";
        List<CatalogueItem> filtered = items.Where(item =>
            (type == "All types" || item.TypeLabel.StartsWith(type, StringComparison.OrdinalIgnoreCase)) &&
            (search.Length == 0 || item.Uuid.Contains(search, StringComparison.OrdinalIgnoreCase) || item.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) || item.DisplayDescription.Contains(search, StringComparison.OrdinalIgnoreCase) || (item.Maker?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) || item.TypeLabel.Contains(search, StringComparison.OrdinalIgnoreCase))).ToList();
        grid.DataSource = new BindingList<CatalogueItem>(filtered);
        countLabel.Text = $"{filtered.Count:N0} ITEMS";
        UpdateListScroll();
    }

    private void ShowSelectedItem()
    {
        if (grid.SelectedRows.Count == 0 || grid.SelectedRows[0].DataBoundItem is not CatalogueItem item)
        {
            nameLabel.Text = "Select an item";
            uuidBox.Clear(); typeLabel.Text = ""; versionLabel.Text = ""; descriptionBox.Clear();
            preview.Image = null;
            displayedImage?.Dispose(); displayedImage = null;
            preview.Invalidate();
            ResizeDetails();
            return;
        }
        nameLabel.Text = item.DisplayName;
        uuidBox.Text = item.Uuid;
        typeLabel.Text = item.TypeLabel;
        versionLabel.Text = $"HDK {item.Version.Hdk ?? "—"}  •  Object {item.Version.Object ?? "—"}  •  ODC {item.Version.Odc ?? "—"}";
        descriptionBox.Text = item.DisplayDescription + Environment.NewLine + Environment.NewLine + "Maker: " + (item.Maker ?? "—") + Environment.NewLine + "Minimum age: " + (item.Legal?.MinimumAge?.ToString() ?? "—") + Environment.NewLine + "Parental control: " + (item.Legal?.ParentalControlLevel?.ToString() ?? "—");
        preview.Image = null;
        displayedImage?.Dispose();
        displayedImage = LoadLocalImage(item);
        preview.Image = displayedImage;
        detailScroll.Value = 0;
        ResizeDetails();
    }

    private Image? LoadLocalImage(CatalogueItem item)
    {
        string archiveRoot = Path.GetFullPath(Path.Combine(dataDirectory, "ImageArchive")) + Path.DirectorySeparatorChar;
        string itemDirectory = Path.GetFullPath(Path.Combine(archiveRoot, item.Uuid));
        if (!itemDirectory.StartsWith(archiveRoot, StringComparison.OrdinalIgnoreCase)) return null;
        var candidates = new List<string>();
        foreach (string? template in new[] { item.Images?.Large, item.Images?.Small, item.Images?.Maker })
        {
            if (string.IsNullOrWhiteSpace(template)) continue;
            string relative = template.Replace("[THUMBNAIL_ROOT]", string.Empty, StringComparison.Ordinal).Replace('/', Path.DirectorySeparatorChar);
            string candidate = Path.GetFullPath(Path.Combine(archiveRoot, item.Uuid, relative));
            if (!candidate.StartsWith(archiveRoot, StringComparison.OrdinalIgnoreCase)) continue;
            candidates.Add(candidate);
        }
        if (Directory.Exists(itemDirectory))
            candidates.AddRange(Directory.EnumerateFiles(itemDirectory, "*.png").OrderBy(path => Path.GetFileName(path).StartsWith("large", StringComparison.OrdinalIgnoreCase) ? 0 : 1));
        foreach (string candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(candidate)) continue;
            try { using var stream = new FileStream(candidate, FileMode.Open, FileAccess.Read, FileShare.Read); using var source = Image.FromStream(stream); return new Bitmap(source); }
            catch (Exception exception) when (exception is IOException or ArgumentException) { }
        }
        return null;
    }

    private Image? Thumbnail(CatalogueItem item)
    {
        if (thumbnails.TryGetValue(item.Uuid, out Image? cached)) return cached;
        using Image? source = LoadLocalImage(item);
        Image? thumbnail = null;
        if (source is not null)
        {
            var bitmap = new Bitmap(56, 56);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.Clear(Color.Transparent);
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            float ratio = Math.Min(56F / source.Width, 56F / source.Height);
            int width = Math.Max(1, (int)(source.Width * ratio)), height = Math.Max(1, (int)(source.Height * ratio));
            graphics.DrawImage(source, new Rectangle((56 - width) / 2, (56 - height) / 2, width, height));
            thumbnail = bitmap;
        }
        thumbnails[item.Uuid] = thumbnail;
        thumbnailOrder.Enqueue(item.Uuid);
        while (thumbnailOrder.Count > 128)
        {
            string oldest = thumbnailOrder.Dequeue();
            thumbnails[oldest]?.Dispose();
            thumbnails.Remove(oldest);
        }
        return thumbnail;
    }

    internal void VerifyOffline(string reportDirectory)
    {
        if (items.Count == 0) throw new InvalidOperationException("The local catalogue is empty.");
        CatalogueItem? illustrated = items.FirstOrDefault(item =>
        {
            using Image? image = LoadLocalImage(item);
            return image is not null;
        });
        if (illustrated is null) throw new InvalidOperationException("No local catalogue picture could be loaded.");
        searchBox.Text = illustrated.Uuid;
        ApplyFilter();
        if (grid.Rows.Count != 1) throw new InvalidOperationException("Offline UUID search did not return one item.");
        grid.Rows[0].Selected = true;
        ShowSelectedItem();
        if (preview.Image is null) throw new InvalidOperationException("The selected local image was not displayed.");
        searchBox.Text = "";
        typeFilter.SelectedItem = "Clothing";
        ApplyFilter();
        if (grid.Rows.Count == 0 || grid.Rows.Cast<DataGridViewRow>().Any(row => !((CatalogueItem)row.DataBoundItem).TypeLabel.StartsWith("Clothing")))
            throw new InvalidOperationException("Offline type filtering failed.");
        typeFilter.SelectedIndex = 0;
        searchBox.Text = "dress";
        ApplyFilter();
        if (grid.Rows.Count == 0) { searchBox.Text = illustrated.DisplayName; ApplyFilter(); }
        Directory.CreateDirectory(reportDirectory);
        var layoutChecks = new List<string>();
        Size originalSize = Size;
        string originalTheme = appearance.Palette.Name;
        bool originalAnimated = appearance.Animated, originalReactive = appearance.Reactive;
        foreach (CataloguePalette palette in CataloguePalette.All)
        {
            appearance.Set(palette.Name, false, false);
            if (BackColor != palette.Background || grid.DefaultCellStyle.ForeColor != palette.Text || typeFilter.BackColor != palette.Input)
                throw new InvalidOperationException($"Theme was not applied consistently: {palette.Name}");
            foreach (Size size in new[] { originalSize, MinimumSize, new Size(1616, 1019) })
            {
                Size = size;
                PerformLayout(); ResizeDetails();
                VerifyLayout(this);
                VerifyTextFits();
                if (grid.Columns.GetColumnsWidth(DataGridViewElementStates.Visible) > grid.ClientSize.Width)
                    throw new InvalidOperationException("Catalogue columns exceed the visible list.");
                detailScroll.Value = detailScroll.Maximum;
                if (detailContent.Bottom > detailViewport.Height + 1 && detailScroll.Maximum > 0)
                    throw new InvalidOperationException("The bottom of the item details cannot be reached.");
                detailScroll.Value = 0;
                layoutChecks.Add($"{palette.Name}: {ClientSize.Width}x{ClientSize.Height}");
            }
            Size = originalSize;
            PerformLayout();
            using var themedScreenshot = new Bitmap(Width, Height);
            DrawToBitmap(themedScreenshot, new Rectangle(Point.Empty, Size));
            themedScreenshot.Save(Path.Combine(reportDirectory, $"catalogue-{palette.Name.Replace(' ', '-')}.png"), System.Drawing.Imaging.ImageFormat.Png);
        }

        // Exercise the actual appearance controls without changing the user's saved preferences.
        using (Control settings = BuildAppearancePanel(savePreferences: false))
        {
            settings.CreateControl(); settings.PerformLayout(); VerifyLayout(settings);
            var theme = settings.Controls.OfType<CatalogueDropdown>().Single();
            var toggles = settings.Controls.OfType<CatalogueToggle>().ToArray();
            theme.SelectedItem = "Dracula";
            foreach (CatalogueToggle toggle in toggles) toggle.Checked = true;
            if (appearance.Palette.Name != "Dracula" || !appearance.Animated || !appearance.Reactive) throw new InvalidOperationException("Appearance controls did not enable the chosen settings.");
            ApplyControlPalette(settings);
            using var settingsImage = new Bitmap(settings.Width, settings.Height);
            settings.DrawToBitmap(settingsImage, settings.ClientRectangle);
            settingsImage.Save(Path.Combine(reportDirectory, "appearance-panel.png"));
            foreach (CatalogueToggle toggle in toggles) toggle.Checked = false;
            if (appearance.Animated || appearance.Reactive) throw new InvalidOperationException("Motion and reactive effects did not turn off.");
        }
        string preferenceFixture = Path.Combine(reportDirectory, "settings-fixture.json");
        new CataloguePreferences { Theme = "Dracula", Animated = false, Reactive = false }.Save(preferenceFixture);
        CataloguePreferences restored = CataloguePreferences.Load(preferenceFixture);
        if (restored.Theme != "Dracula" || restored.Animated || restored.Reactive) throw new InvalidOperationException("Appearance preferences did not round-trip.");

        typeFilter.Navigate(Keys.End);
        if (typeFilter.SelectedItem?.ToString() != "Unknown") throw new InvalidOperationException("Dropdown keyboard navigation failed.");
        typeFilter.Navigate(Keys.Home);
        typeFilter.ShowChoices();
        if (!typeFilter.PopupVisible || typeFilter.PopupContent is not Control choices) throw new InvalidOperationException("The themed dropdown did not open.");
        VerifyLayout(choices);
        using (var dropdownImage = new Bitmap(choices.Width, choices.Height))
        {
            choices.DrawToBitmap(dropdownImage, choices.ClientRectangle);
            dropdownImage.Save(Path.Combine(reportDirectory, "type-dropdown.png"));
        }
        typeFilter.NavigateChoices(Keys.End); typeFilter.NavigateChoices(Keys.Enter);
        if (typeFilter.SelectedItem?.ToString() != "Unknown" || typeFilter.PopupVisible) throw new InvalidOperationException("Popup selection did not update/close.");
        typeFilter.SelectedIndex = 0;
        ShowAppearance(typeFilter, savePreferences: false);
        var themePopupDropdown = appearancePanel!.Controls.OfType<CatalogueDropdown>().Single();
        themePopupDropdown.ShowChoices();
        if (appearancePopup?.Visible != true || !themePopupDropdown.PopupVisible) throw new InvalidOperationException("The appearance panel closed while choosing a theme.");
        themePopupDropdown.NavigateChoices(Keys.Home); themePopupDropdown.NavigateChoices(Keys.Down); themePopupDropdown.NavigateChoices(Keys.Enter);
        if (appearance.Palette.Name != "Dracula") throw new InvalidOperationException("The theme popup did not apply Dracula.");
        appearancePopup.Close();

        foreach (CatalogueItem extreme in new[] { items.MaxBy(item => item.DisplayName.Length)!, items.MaxBy(item => item.DisplayDescription.Length)! })
        {
            searchBox.Text = extreme.Uuid; ApplyFilter(); Size = MinimumSize; PerformLayout(); ResizeDetails();
            VerifyLayout(this); VerifyTextFits();
            detailScroll.Value = detailScroll.Maximum;
            if (detailContent.Bottom > detailViewport.Height + 1) throw new InvalidOperationException("Long item details cannot be scrolled to their end.");
        }
        var scaleChecks = new List<string>();
        foreach (float scale in new[] { 1.25F, 1.5F, 2F })
        {
            using var scaled = new CatalogueViewerForm();
            scaled.CreateControl();
            scaled.verificationScale = scale;
            static IEnumerable<Control> Descendants(Control parent) => new[] { parent }.Concat(parent.Controls.Cast<Control>().SelectMany(Descendants));
            var fonts = Descendants(scaled).Select(control => (Control: control, Font: control.Font)).ToArray();
            scaled.Scale(new SizeF(scale, scale));
            foreach (var entry in fonts) entry.Control.Font = new Font(entry.Font.FontFamily, entry.Font.Size * scale, entry.Font.Style);
            scaled.Size = new Size((int)(MinimumSize.Width * scale), (int)(MinimumSize.Height * scale));
            scaled.items = new List<CatalogueItem> { illustrated };
            scaled.ApplyFilter(); scaled.PerformLayout(); scaled.ResizeDetails();
            VerifyLayout(scaled); scaled.VerifyTextFits();
            using Control settings = scaled.BuildAppearancePanel(savePreferences: false);
            settings.PerformLayout(); VerifyLayout(settings);
            scaleChecks.Add($"{scale:P0} simulated layout and text scale");
        }
        searchBox.Text = "dress"; typeFilter.SelectedIndex = 0; ApplyFilter();
        Size = originalSize;
        PerformLayout();
        appearance.Set(originalTheme, originalAnimated, originalReactive);
        using var screenshot = new Bitmap(Width, Height);
        DrawToBitmap(screenshot, new Rectangle(Point.Empty, Size));
        screenshot.Save(Path.Combine(reportDirectory, "catalogue-preview.png"), System.Drawing.Imaging.ImageFormat.Png);
        File.WriteAllText(Path.Combine(reportDirectory, "offline-verification.json"), JsonSerializer.Serialize(new
        {
            success = true, itemCount = items.Count, uuidSearch = true, typeFilter = true,
            localImage = illustrated.Uuid, internetRequiredForViewing = false, layoutChecks,
            themes = CataloguePalette.All.Select(palette => palette.Name), preferencesRoundTrip = true,
            motionToggles = true, longDetailsScrollable = true, stockDropdowns = false, scaleChecks,
            applicationIcon = catalogueIcon is not null, headerArtwork = catalogueArtwork is not null,
            dropdownKeyboardAndPopup = true
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void VerifyLayout(Control parent)
    {
        Control[] children = parent.Controls.Cast<Control>().Where(control => control.Visible).ToArray();
        foreach (Control child in children)
        {
            // Only this deliberate scrolling viewport may have content extending past its edges.
            if (parent is not CatalogueDetailsViewport && (child.Bounds.Left < 0 || child.Bounds.Top < 0 || child.Bounds.Right > parent.ClientSize.Width + 1 || child.Bounds.Bottom > parent.ClientSize.Height + 1))
                throw new InvalidOperationException($"Clipped {child.GetType().Name} ({child.Text}) in {parent.GetType().Name}: {child.Bounds} / {parent.ClientSize}");
            VerifyLayout(child);
        }
        if (parent is TableLayoutPanel)
            for (int first = 0; first < children.Length; first++)
                for (int second = first + 1; second < children.Length; second++)
                    if (children[first].Bounds.IntersectsWith(children[second].Bounds))
                        throw new InvalidOperationException($"Overlapping catalogue controls: {children[first].Text} / {children[second].Text}");
    }

    private void VerifyTextFits()
    {
        foreach (Label label in new[] { nameLabel, typeLabel, versionLabel })
        {
            Size measured = TextRenderer.MeasureText(label.Text, label.Font, new Size(Math.Max(1, label.Width - 8), int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            if (measured.Height + label.Padding.Vertical > label.Height) throw new InvalidOperationException($"Item text is clipped: {label.Text}");
        }
        int lastLineBottom = descriptionBox.GetPositionFromCharIndex(Math.Max(0, descriptionBox.TextLength - 1)).Y + descriptionBox.Font.Height;
        if (lastLineBottom > descriptionBox.ClientSize.Height) throw new InvalidOperationException("Item description is clipped internally.");
    }

    private async Task SyncMetadataAsync()
    {
        if (syncCancellation is not null) { syncCancellation.Cancel(); return; }
        syncCancellation = new CancellationTokenSource();
        syncButton.Text = "CANCEL UPDATE";
        try
        {
            using var service = new CatalogueService();
            var progress = new Progress<CatalogueSyncProgress>(value => statusLabel.Text = $"Online sync • page {value.Pages:N0} • {value.Items:N0} items • {value.Message}");
            await service.SyncAsync(dataDirectory, progress, syncCancellation.Token);
            LoadSnapshot();
        }
        catch (OperationCanceledException) { statusLabel.Text = "Metadata update cancelled; previous offline snapshot was preserved."; }
        catch (Exception exception) { statusLabel.Text = $"Update failed: {exception.Message}"; MessageBox.Show(exception.Message, "Catalogue Update Failed", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { syncCancellation.Dispose(); syncCancellation = null; syncButton.Text = "UPDATE METADATA"; }
    }

    private string ImageArchiveStatus() => Directory.Exists(Path.Combine(dataDirectory, "ImageArchive")) ? "local" : "not installed";
    private void OpenDataFolder() { Directory.CreateDirectory(dataDirectory); Process.Start(new ProcessStartInfo { FileName = dataDirectory, UseShellExecute = true }); }
    private Label Caption(string text) => new() { Text = text, Dock = DockStyle.Fill, ForeColor = TextMuted, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI Semibold", 8F), Margin = Padding.Empty };
    private TextBox CreateTextBox(bool readOnly = false) => new() { BackColor = Input, ForeColor = readOnly ? TextMuted : TextPrimary, BorderStyle = BorderStyle.None, ReadOnly = readOnly, Font = new Font("Consolas", 9.5F), Margin = Padding.Empty };
    private Button CreateButton(string text, Color accent) => new CatalogueButton(appearance) { Text = text, Primary = accent == Cyan };
    private static DataGridViewTextBoxColumn Column(string header, string property, int width, bool fill = false) => new() { HeaderText = header, DataPropertyName = property, Width = width, AutoSizeMode = fill ? DataGridViewAutoSizeColumnMode.Fill : DataGridViewAutoSizeColumnMode.None, SortMode = DataGridViewColumnSortMode.Automatic };
    private CatalogueGrid CreateGrid()
    {
        var grid = new CatalogueGrid { Dock = DockStyle.Fill, Margin = Padding.Empty, BackgroundColor = Surface, BorderStyle = BorderStyle.None, CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal, ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None, GridColor = Color.FromArgb(25, 38, 47), ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false, AllowUserToResizeColumns = false, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, RowTemplate = { Height = 72 }, EnableHeadersVisualStyles = false, AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None };
        grid.DefaultCellStyle.BackColor = Surface; grid.DefaultCellStyle.ForeColor = TextPrimary; grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(24, 56, 68); grid.DefaultCellStyle.SelectionForeColor = Color.White;
        grid.DefaultCellStyle.Padding = new Padding(8, 0, 8, 0);
        grid.ColumnHeadersDefaultCellStyle.BackColor = Surface; grid.ColumnHeadersDefaultCellStyle.ForeColor = TextMuted; grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Surface; grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 0, 8, 0); grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 8F); grid.ColumnHeadersHeight = 42; grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        return grid;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !resourcesReleased)
        {
            resourcesReleased = true;
            syncCancellation?.Cancel(); filterTimer.Dispose(); appearancePopup?.Dispose();
            displayedImage?.Dispose(); foreach (Image? image in thumbnails.Values) image?.Dispose();
            catalogueIcon?.Dispose(); catalogueArtwork?.Dispose();
        }
        base.Dispose(disposing);
    }
}
