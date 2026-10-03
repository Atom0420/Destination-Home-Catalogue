namespace DestinationHome.Catalogue;

internal static class Program
{
    [STAThread]
    private static async Task<int> Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Contains("--sync-only", StringComparer.OrdinalIgnoreCase))
        {
            string dataDirectory = GetArgument(args, "--data-dir") ?? Path.Combine(AppContext.BaseDirectory, "Data");
            Directory.CreateDirectory(dataDirectory);
            string logPath = Path.Combine(dataDirectory, "sync.log");
            try
            {
                using var service = new CatalogueService();
                var progress = new InlineProgress<CatalogueSyncProgress>(value =>
                {
                    string line = $"[{DateTime.Now:HH:mm:ss}] Page {value.Pages:N0} • {value.Items:N0} items • {value.Message}";
                    Console.WriteLine(line);
                    File.AppendAllText(logPath, line + Environment.NewLine);
                });
                await service.SyncAsync(dataDirectory, progress, CancellationToken.None);
                return 0;
            }
            catch (Exception exception)
            {
                File.AppendAllText(logPath, $"[{DateTime.Now:O}] FAILED: {exception}{Environment.NewLine}");
                return 1;
            }
        }

        if (args.Contains("--verify-offline", StringComparer.OrdinalIgnoreCase))
        {
            string reportDirectory = GetArgument(args, "--report-dir") ?? AppContext.BaseDirectory;
            int result = 1;
            using var form = new CatalogueViewerForm();
            form.Shown += (_, _) => form.BeginInvoke(new Action(() =>
            {
                try { form.VerifyOffline(reportDirectory); result = 0; }
                catch (Exception exception) { Directory.CreateDirectory(reportDirectory); File.WriteAllText(Path.Combine(reportDirectory, "offline-verification-error.txt"), exception.ToString()); }
                finally { form.Close(); }
            }));
            Application.Run(form);
            return result;
        }
        Application.Run(new CatalogueViewerForm());
        return 0;
    }

    private static string? GetArgument(string[] args, string name)
    {
        int index = Array.FindIndex(args, value => value.Equals(name, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
