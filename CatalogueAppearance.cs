using System.Text.Json;

namespace DestinationHome.Catalogue;

internal sealed record CataloguePalette(string Name, Color Background, Color Surface, Color Input,
    Color Text, Color Muted, Color Accent, Color Secondary, Color Green, Color Border, Color Hover, Color Selected)
{
    private static Color Hex(string value) => ColorTranslator.FromHtml(value);
    internal static readonly CataloguePalette[] All =
    {
        new("Midnight", Hex("#080E12"), Hex("#0E171D"), Hex("#121E26"), Hex("#E2EEF3"), Hex("#93AAB6"), Hex("#16CFF4"), Hex("#FFB523"), Hex("#45D684"), Hex("#293E4B"), Hex("#182D39"), Hex("#183C4B")),
        new("Dracula", Hex("#21222C"), Hex("#282A36"), Hex("#303341"), Hex("#F8F8F2"), Hex("#B3AFC8"), Hex("#BD93F9"), Hex("#FF79C6"), Hex("#50FA7B"), Hex("#44475A"), Hex("#37384C"), Hex("#49415F")),
        new("Tokyo Night", Hex("#16161E"), Hex("#1A1B26"), Hex("#24283B"), Hex("#C0CAF5"), Hex("#9AA5CE"), Hex("#7AA2F7"), Hex("#BB9AF7"), Hex("#9ECE6A"), Hex("#39415C"), Hex("#292E45"), Hex("#334368")),
        new("Nord", Hex("#242933"), Hex("#2E3440"), Hex("#3B4252"), Hex("#ECEFF4"), Hex("#BBC6D7"), Hex("#88C0D0"), Hex("#B48EAD"), Hex("#A3BE8C"), Hex("#4C566A"), Hex("#414C60"), Hex("#445C70")),
        new("Rosé Pine", Hex("#191724"), Hex("#1F1D2E"), Hex("#26233A"), Hex("#E0DEF4"), Hex("#AFAACB"), Hex("#C4A7E7"), Hex("#EBBCBA"), Hex("#9CCFD8"), Hex("#44415A"), Hex("#302B46"), Hex("#433A59")),
        new("Solarized", Hex("#002B36"), Hex("#073642"), Hex("#103E49"), Hex("#EEE8D5"), Hex("#A7B9B6"), Hex("#2AA198"), Hex("#B58900"), Hex("#B4C66F"), Hex("#31545D"), Hex("#174955"), Hex("#1E5A62"))
    };
}

internal sealed class CatalogueAppearance
{
    internal CataloguePalette Palette { get; private set; } = CataloguePalette.All[0];
    internal bool Animated { get; private set; } = true;
    internal bool Reactive { get; private set; } = true;
    internal event EventHandler? Changed;

    internal void Set(string theme, bool animated, bool reactive)
    {
        Palette = CataloguePalette.All.FirstOrDefault(palette => palette.Name == theme) ?? CataloguePalette.All[0];
        Animated = animated;
        Reactive = reactive;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    internal static Color Blend(Color from, Color to, float amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        return Color.FromArgb((int)(from.R + (to.R - from.R) * amount), (int)(from.G + (to.G - from.G) * amount), (int)(from.B + (to.B - from.B) * amount));
    }
}

internal sealed class CataloguePreferences
{
    public string Theme { get; set; } = "Midnight";
    public bool Animated { get; set; } = true;
    public bool Reactive { get; set; } = true;

    internal static CataloguePreferences Load(string path)
    {
        try { return File.Exists(path) ? JsonSerializer.Deserialize<CataloguePreferences>(File.ReadAllText(path)) ?? new() : new(); }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException) { return new(); }
    }

    internal void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(path + ".tmp", path, true);
    }
}
