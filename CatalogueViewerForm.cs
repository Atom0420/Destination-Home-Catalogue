using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;

namespace DestinationHome.Catalogue;

internal sealed class CatalogueViewerForm : Form
{
    private static readonly Color Background = Color.FromArgb(8, 14, 18);
    private static readonly Color Surface = Color.FromArgb(14, 23, 29);
    private static readonly Color Input = Color.FromArgb(18, 30, 38);
    private static readonly Color TextPrimary = Color.FromArgb(226, 238, 243);
    private static readonly Color TextMuted = Color.FromArgb(126, 151, 162);
    private static readonly Color Cyan = Color.FromArgb(22, 207, 244);
    private static readonly Color Amber = Color.FromArgb(255, 181, 35);
    private static readonly Color Green = Color.FromArgb(69, 214, 132);

    private readonly string dataDirectory = Path.Combine(AppContext.BaseDirectory, "Data");
    private readonly JsonSerializerOptions jsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly System.Windows.Forms.Timer filterTimer = new() { Interval = 220 };
    private List<CatalogueItem> items = new();
    private CancellationTokenSource? syncCancellation;
    private Image? displayedImage;
    private readonly Dictionary<string, Image?> thumbnails = new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<string> thumbnailOrder = new();

    private TextBox searchBox = null!;
    private ComboBox typeFilter = null!;
    private Label countLabel = null!;
    private Label statusLabel = null!;
    private Button syncButton = null!;
    private DataGridView grid = null!;
    private PictureBox preview = null!;
    private Label nameLabel = null!;
    private TextBox uuidBox = null!;
    private Label typeLabel = null!;
    private Label versionLabel = null!;
    private RichTextBox descriptionBox = null!;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        CatalogueChrome.Apply(Handle);
    }

    public CatalogueViewerForm()
    {
        InitializeUi();
        filterTimer.Tick += (_, _) => { filterTimer.Stop(); ApplyFilter(); };
        Shown += (_, _) => LoadSnapshot();
        FormClosing += (_, _) => { syncCancellation?.Cancel(); displayedImage?.Dispose(); foreach (Image? image in thumbnails.Values) image?.Dispose(); filterTimer.Dispose(); };
    }

    private void InitializeUi()
    {
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
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, BackColor = Background, Padding = new Padding(0, 6, 0, 12), Margin = Padding.Empty };
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160F));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 168F));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160F));
        var heading = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty, BackColor = Background };
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        heading.RowStyles.Add(new RowStyle(SizeType.Percent, 68F));
        heading.RowStyles.Add(new RowStyle(SizeType.Percent, 32F));
        heading.Controls.Add(new Label { Text = "UUID Catalogue", Dock = DockStyle.Fill, Margin = Padding.Empty, ForeColor = TextPrimary, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI Semibold", 21F), AutoEllipsis = true }, 0, 0);
        heading.Controls.Add(new Label { Text = "DESTINATION HOME  /  OFFLINE LIBRARY", Dock = DockStyle.Fill, Margin = Padding.Empty, ForeColor = TextMuted, Font = new Font("Segoe UI", 8F), AutoEllipsis = true }, 0, 1);
        panel.Controls.Add(heading, 0, 0);
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
        return panel;
    }

    private Control BuildSearchBar()
    {
        var panel = new CatalogueCard { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(16, 10, 16, 14), Margin = new Padding(0, 0, 0, 14) };
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
        panel.Controls.Add(new CatalogueField(searchBox), 0, 1);
        typeFilter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DrawMode = DrawMode.OwnerDrawFixed, BackColor = Input, ForeColor = TextPrimary, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10F), AccessibleName = "Item type" };
        typeFilter.DrawItem += (_, e) =>
        {
            using var brush = new SolidBrush((e.State & DrawItemState.Selected) != 0 ? Color.FromArgb(24, 56, 68) : Input);
            e.Graphics.FillRectangle(brush, e.Bounds);
            string text = e.Index >= 0 ? typeFilter.Items[e.Index]?.ToString() ?? "" : typeFilter.SelectedItem?.ToString() ?? "All types";
            TextRenderer.DrawText(e.Graphics, text, e.Font, e.Bounds, TextPrimary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        };
        typeFilter.Items.AddRange(new object[] { "All types", "Clothing", "Furniture", "Portable", "Scene", "Minigame", "Other", "Unknown" });
        typeFilter.SelectedIndex = 0;
        typeFilter.SelectedIndexChanged += FilterChanged;
        panel.Controls.Add(new CatalogueField(typeFilter) { Margin = new Padding(16, 0, 0, 0), Padding = new Padding(10, 0, 6, 0) }, 1, 1);
        return panel;
    }

    private Control BuildWorkspace()
    {
        var split = new SplitContainer { Size = new Size(1280, 600), Dock = DockStyle.Fill, Margin = Padding.Empty, SplitterWidth = 16, SplitterDistance = 852, Panel1MinSize = 430, Panel2MinSize = 360, BackColor = Background, FixedPanel = FixedPanel.Panel2 };
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
            if (e.ColumnIndex == 0 && e.RowIndex >= 0 && grid.Rows[e.RowIndex].DataBoundItem is CatalogueItem item)
            {
                e.Value = Thumbnail(item);
                e.FormattingApplied = true;
            }
        };
        grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0 && grid.Rows[e.RowIndex].DataBoundItem is CatalogueItem item) Clipboard.SetText(item.Uuid); };
        var listCard = new CatalogueCard { Dock = DockStyle.Fill, Padding = new Padding(1), ColumnCount = 1, RowCount = 1 };
        listCard.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        listCard.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        listCard.Controls.Add(grid, 0, 0);
        split.Panel1.Controls.Add(listCard);
        split.Panel2.Controls.Add(BuildDetails());
        return split;
    }

    private Control BuildDetails()
    {
        var panel = new CatalogueCard { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 9, Padding = new Padding(18, 14, 18, 16) };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 54F));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 46F));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
        panel.Controls.Add(Caption("ITEM DETAILS"), 0, 0);
        preview = new PictureBox { Dock = DockStyle.Fill, BackColor = Background, SizeMode = PictureBoxSizeMode.Zoom, Margin = new Padding(0, 0, 0, 12) };
        preview.Paint += (_, e) => { if (preview.Image is null) TextRenderer.DrawText(e.Graphics, "No public picture available", Font, preview.ClientRectangle, TextMuted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter); };
        panel.Controls.Add(preview, 0, 1);
        nameLabel = new Label { Text = "Select an item", Dock = DockStyle.Fill, ForeColor = TextPrimary, Font = new Font("Segoe UI Semibold", 13F), AutoEllipsis = true, Margin = Padding.Empty, Padding = new Padding(0, 3, 0, 3) };
        panel.Controls.Add(nameLabel, 0, 2);
        panel.Controls.Add(Caption("UUID"), 0, 3);
        uuidBox = CreateTextBox(readOnly: true);
        panel.Controls.Add(new CatalogueField(uuidBox), 0, 4);
        typeLabel = new Label { Dock = DockStyle.Fill, ForeColor = Cyan, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, Margin = Padding.Empty };
        versionLabel = new Label { Dock = DockStyle.Fill, ForeColor = TextMuted, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, Margin = Padding.Empty };
        panel.Controls.Add(typeLabel, 0, 5);
        panel.Controls.Add(versionLabel, 0, 6);
        descriptionBox = new RichTextBox { Dock = DockStyle.Fill, BackColor = Surface, ForeColor = TextPrimary, BorderStyle = BorderStyle.None, ReadOnly = true, DetectUrls = false, Margin = new Padding(0, 0, 0, 6), Font = new Font("Segoe UI", 9F) };
        panel.Controls.Add(descriptionBox, 0, 7);
        var copy = CreateButton("COPY UUID", Cyan);
        copy.Margin = new Padding(0, 8, 0, 0);
        copy.Click += (_, _) => { if (uuidBox.TextLength > 0) Clipboard.SetText(uuidBox.Text); };
        panel.Controls.Add(copy, 0, 8);
        return panel;
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
        Image? thumbnail = source is null ? null : new Bitmap(source, new Size(56, 56));
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
        var layoutChecks = new List<string>();
        Size originalSize = Size;
        foreach (Size size in new[] { originalSize, MinimumSize, new Size(1616, 1019) })
        {
            Size = size;
            PerformLayout();
            VerifyLayout(this);
            layoutChecks.Add($"{ClientSize.Width}x{ClientSize.Height}");
        }
        Size = originalSize;
        PerformLayout();
        Directory.CreateDirectory(reportDirectory);
        using var screenshot = new Bitmap(Width, Height);
        DrawToBitmap(screenshot, new Rectangle(Point.Empty, Size));
        screenshot.Save(Path.Combine(reportDirectory, "catalogue-preview.png"), System.Drawing.Imaging.ImageFormat.Png);
        File.WriteAllText(Path.Combine(reportDirectory, "offline-verification.json"), JsonSerializer.Serialize(new
        {
            success = true, itemCount = items.Count, uuidSearch = true, typeFilter = true,
            localImage = illustrated.Uuid, internetRequiredForViewing = false, layoutChecks
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void VerifyLayout(Control parent)
    {
        Control[] children = parent.Controls.Cast<Control>().Where(control => control.Visible).ToArray();
        foreach (Control child in children)
        {
            if (child.Bounds.Left < 0 || child.Bounds.Top < 0 || child.Bounds.Right > parent.ClientSize.Width + 1 || child.Bounds.Bottom > parent.ClientSize.Height + 1)
                throw new InvalidOperationException($"Clipped {child.GetType().Name} ({child.Text}) in {parent.GetType().Name}: {child.Bounds} / {parent.ClientSize}");
            VerifyLayout(child);
        }
        if (parent is TableLayoutPanel)
            for (int first = 0; first < children.Length; first++)
                for (int second = first + 1; second < children.Length; second++)
                    if (children[first].Bounds.IntersectsWith(children[second].Bounds))
                        throw new InvalidOperationException($"Overlapping catalogue controls: {children[first].Text} / {children[second].Text}");
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
    private static Label Caption(string text) => new() { Text = text, Dock = DockStyle.Fill, ForeColor = TextMuted, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI Semibold", 8F), Margin = Padding.Empty };
    private static TextBox CreateTextBox(bool readOnly = false) => new() { BackColor = Input, ForeColor = readOnly ? TextMuted : TextPrimary, BorderStyle = BorderStyle.None, ReadOnly = readOnly, Font = new Font("Consolas", 9.5F), Margin = Padding.Empty };
    private static Button CreateButton(string text, Color accent) { var button = new Button { Text = text, Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, BackColor = Input, ForeColor = TextPrimary, Font = new Font("Segoe UI Semibold", 8F), Cursor = Cursors.Hand, Margin = Padding.Empty }; button.FlatAppearance.BorderColor = Color.FromArgb(46, 68, 81); button.FlatAppearance.MouseOverBackColor = Color.FromArgb(24, 47, 58); button.FlatAppearance.MouseDownBackColor = Color.FromArgb(29, 58, 69); if (accent == Cyan) { button.BackColor = Color.FromArgb(15, 46, 58); button.FlatAppearance.BorderColor = Color.FromArgb(34, 98, 115); button.ForeColor = Cyan; } return button; }
    private static DataGridViewTextBoxColumn Column(string header, string property, int width, bool fill = false) => new() { HeaderText = header, DataPropertyName = property, Width = width, AutoSizeMode = fill ? DataGridViewAutoSizeColumnMode.Fill : DataGridViewAutoSizeColumnMode.None, SortMode = DataGridViewColumnSortMode.Automatic };
    private static DataGridView CreateGrid()
    {
        var grid = new DataGridView { Dock = DockStyle.Fill, Margin = Padding.Empty, BackgroundColor = Surface, BorderStyle = BorderStyle.None, CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal, ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None, GridColor = Color.FromArgb(25, 38, 47), ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, RowTemplate = { Height = 72 }, EnableHeadersVisualStyles = false, AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None };
        grid.DefaultCellStyle.BackColor = Surface; grid.DefaultCellStyle.ForeColor = TextPrimary; grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(24, 56, 68); grid.DefaultCellStyle.SelectionForeColor = Color.White;
        grid.DefaultCellStyle.Padding = new Padding(8, 0, 8, 0);
        grid.ColumnHeadersDefaultCellStyle.BackColor = Surface; grid.ColumnHeadersDefaultCellStyle.ForeColor = TextMuted; grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Surface; grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 0, 8, 0); grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 8F); grid.ColumnHeadersHeight = 42; grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        return grid;
    }
}
