using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace DestinationHome.Catalogue;

internal sealed class CatalogueService : IDisposable
{
    private const string Endpoint = "https://destinationhome.online/api/catalog";
    private const string Query = """
        query OfflineCatalogue($first: Int, $after: String) {
          objects(first: $first, after: $after) {
            edges { node {
              uuid
              name description maker
              version { hdk object odc }
              images { large small maker }
              legal { minimum_age parental_control_level }
              timestamp
              metadata { type clothing_type genders furniture_type scene_type }
            } }
            pageInfo { hasNextPage endCursor }
          }
        }
        """;
    private readonly HttpClient client;
    private readonly JsonSerializerOptions jsonOptions = new() { PropertyNameCaseInsensitive = true, WriteIndented = false };

    public CatalogueService()
    {
        client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("DestinationHome-Catalogue/1.0");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<CatalogueSnapshot> SyncAsync(string dataDirectory, IProgress<CatalogueSyncProgress>? progress, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(dataDirectory);
        var items = new Dictionary<string, CatalogueItem>(StringComparer.OrdinalIgnoreCase);
        string? after = null;
        int pages = 0;
        string checkpointPath = Path.Combine(dataDirectory, "catalogue-checkpoint.json");
        if (File.Exists(checkpointPath))
        {
            try
            {
                SyncCheckpoint? checkpoint = JsonSerializer.Deserialize<SyncCheckpoint>(await File.ReadAllTextAsync(checkpointPath, cancellationToken), jsonOptions);
                if (checkpoint is not null && checkpoint.Endpoint == Endpoint && checkpoint.SavedAtUtc > DateTime.UtcNow.AddDays(-1))
                {
                    foreach (CatalogueItem item in checkpoint.Items) items[item.Uuid] = item;
                    after = checkpoint.After;
                    pages = checkpoint.Pages;
                    progress?.Report(new CatalogueSyncProgress(pages, items.Count, "Resuming metadata download"));
                }
            }
            catch (JsonException) { /* An incomplete checkpoint must not prevent a fresh download. */ }
        }
        bool hasMore;
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            CataloguePage page = await GetPageWithRetryAsync(after, cancellationToken);
            pages++;
            foreach (CatalogueItem item in page.Edges.Select(edge => edge.Node))
            {
                items[item.Uuid] = item;
            }

            progress?.Report(new CatalogueSyncProgress(pages, items.Count, page.PageInfo.HasNextPage ? "Downloading metadata" : "Saving offline snapshot"));
            hasMore = page.PageInfo.HasNextPage;
            if (hasMore && (string.IsNullOrWhiteSpace(page.PageInfo.EndCursor) || page.PageInfo.EndCursor.Equals(after, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException("The catalogue returned an invalid pagination cursor.");
            }
            after = page.PageInfo.EndCursor;
            if (hasMore && pages % 25 == 0)
            {
                var checkpoint = new SyncCheckpoint { Endpoint = Endpoint, SavedAtUtc = DateTime.UtcNow, After = after, Pages = pages, Items = items.Values.ToList() };
                await File.WriteAllTextAsync(checkpointPath + ".tmp", JsonSerializer.Serialize(checkpoint, jsonOptions), cancellationToken);
                File.Move(checkpointPath + ".tmp", checkpointPath, overwrite: true);
            }
            if (hasMore) await Task.Delay(60, cancellationToken);
        } while (hasMore);

        var snapshot = new CatalogueSnapshot
        {
            DownloadedAtUtc = DateTime.UtcNow,
            Items = items.Values.OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.Uuid, StringComparer.Ordinal).ToList()
        };
        string destination = Path.Combine(dataDirectory, "catalogue.json");
        string temporary = destination + ".tmp";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(snapshot, jsonOptions), cancellationToken);
        File.Move(temporary, destination, overwrite: true);
        if (File.Exists(checkpointPath)) File.Delete(checkpointPath);
        progress?.Report(new CatalogueSyncProgress(pages, snapshot.Items.Count, "Metadata snapshot complete"));
        return snapshot;
    }

    private async Task<CataloguePage> GetPageWithRetryAsync(string? after, CancellationToken cancellationToken)
    {
        Exception? last = null;
        for (int attempt = 1; attempt <= 4; attempt++)
        {
            try
            {
                string body = JsonSerializer.Serialize(new { query = Query, variables = new { first = 100, after } });
                using var response = await client.PostAsync(Endpoint, new StringContent(body, Encoding.UTF8, "application/json"), cancellationToken);
                response.EnsureSuccessStatusCode();
                await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                if (document.RootElement.TryGetProperty("errors", out JsonElement errors) && errors.GetArrayLength() > 0)
                    throw new InvalidOperationException(errors.ToString());
                if (!document.RootElement.TryGetProperty("data", out JsonElement data) || data.ValueKind != JsonValueKind.Object ||
                    !data.TryGetProperty("objects", out JsonElement pageElement) || pageElement.ValueKind != JsonValueKind.Object)
                {
                    throw new InvalidOperationException(document.RootElement.ToString());
                }
                return JsonSerializer.Deserialize<CataloguePage>(pageElement.GetRawText(), jsonOptions)
                    ?? throw new InvalidOperationException("The catalogue returned an empty page.");
            }
            catch (Exception exception) when (exception is HttpRequestException or IOException or JsonException or InvalidOperationException or TaskCanceledException)
            {
                if (exception is TaskCanceledException && cancellationToken.IsCancellationRequested) throw;
                last = exception;
                if (attempt < 4) await Task.Delay(TimeSpan.FromMilliseconds(500 * attempt), cancellationToken);
            }
        }
        throw last ?? new HttpRequestException("Catalogue metadata download failed.");
    }

    public void Dispose() => client.Dispose();

    private sealed class CataloguePage
    {
        public List<CatalogueEdge> Edges { get; set; } = new();
        public PageInfo PageInfo { get; set; } = new();
    }

    private sealed class CatalogueEdge { public CatalogueItem Node { get; set; } = new(); }
    private sealed class PageInfo { public bool HasNextPage { get; set; } public string? EndCursor { get; set; } }
    private sealed class SyncCheckpoint
    {
        public string Endpoint { get; set; } = "";
        public DateTime SavedAtUtc { get; set; }
        public string? After { get; set; }
        public int Pages { get; set; }
        public List<CatalogueItem> Items { get; set; } = new();
    }
}
