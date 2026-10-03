using System.Text.Json.Serialization;

namespace DestinationHome.Catalogue;

internal sealed class CatalogueSnapshot
{
    public int Version { get; set; } = 1;
    public DateTime DownloadedAtUtc { get; set; }
    public string Source { get; set; } = "https://destinationhome.online/catalogue";
    public List<CatalogueItem> Items { get; set; } = new();
}

internal sealed class CatalogueItem
{
    private string? displayName;
    private string? displayDescription;
    private string? typeLabel;
    public string Uuid { get; set; } = string.Empty;
    public VersionInfo Version { get; set; } = new();
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Maker { get; set; }
    public ImagesInfo? Images { get; set; }
    public AgeRatingInfo? Legal { get; set; }
    public string? Timestamp { get; set; }
    public MetadataInfo? Metadata { get; set; }

    [JsonIgnore] public string DisplayName => displayName ??= string.IsNullOrWhiteSpace(Name) ? "Unnamed item" : Name.Trim();
    [JsonIgnore] public string DisplayDescription => displayDescription ??= Description?.Replace("\\\\n", "\n").Replace("\\n", "\n").Replace("[#Legal]", "").Trim() ?? "No description available.";
    [JsonIgnore] public string TypeLabel => typeLabel ??= CatalogueLabels.Type(Metadata);
}

internal sealed class VersionInfo
{
    public string? Hdk { get; set; }
    public string? Object { get; set; }
    public string? Odc { get; set; }
}

internal sealed class NamesInfo
{
    public string? Default { get; set; }
    public List<LocalizedName> Localized { get; set; } = new();
}

internal sealed class LocalizedName
{
    public string? Locale { get; set; }
    public string? Name { get; set; }
}

internal sealed class DescriptionsInfo
{
    public string? Default { get; set; }
    public List<LocalizedDescription> Localized { get; set; } = new();
}

internal sealed class LocalizedDescription
{
    public string? Locale { get; set; }
    public string? Description { get; set; }
}

internal sealed class ImagesInfo
{
    public string? Large { get; set; }
    public string? Small { get; set; }
    public string? Maker { get; set; }
}

internal sealed class LegalInfo
{
    [JsonPropertyName("age_rating")]
    public AgeRatingInfo? AgeRating { get; set; }
}

internal sealed class AgeRatingInfo
{
    [JsonPropertyName("minimum_age")]
    public int? MinimumAge { get; set; }
    [JsonPropertyName("parental_control_level")]
    public int? ParentalControlLevel { get; set; }
}

internal sealed class MetadataInfo
{
    public string? Type { get; set; }
    [JsonPropertyName("clothing_type")] public string? ClothingType { get; set; }
    public List<string> Genders { get; set; } = new();
    [JsonPropertyName("furniture_type")] public string? FurnitureType { get; set; }
    [JsonPropertyName("scene_type")] public string? SceneType { get; set; }
}

internal static class CatalogueLabels
{
    public static string Type(MetadataInfo? metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata?.Type)) return "Unknown";
        string main = Friendly(metadata.Type);
        string? subtype = metadata.ClothingType ?? metadata.FurnitureType ?? metadata.SceneType;
        if (subtype is not null) subtype = Friendly(subtype);
        string result = subtype is null ? main : $"{main} / {subtype}";
        if (metadata.Type == "CLOTHING" && metadata.Genders is { Count: > 0 })
        {
            result += " / " + string.Join('+', metadata.Genders.Distinct());
        }
        return result;
    }

    private static string Friendly(string value) => System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value.Replace('_', ' ').ToLowerInvariant());
}

internal readonly record struct CatalogueSyncProgress(int Pages, int Items, string Message);
