using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;

namespace CAO.UI.Helpers;

/// <summary>Release publicado en GitHub con su paquete completo.</summary>
public sealed record AvailableRelease(string Tag, string Name, string? ZipUrl, long ZipBytes);

/// <summary>
/// Actualizaciones contra GitHub Releases: comparar versión, elegir paquete
/// completo y descargarlo con progreso. Sin estado: todo lo persistente vive
/// en UiState (sesión). Testeable: la lógica pura no toca red.
/// </summary>
public static class AppUpdater
{
    public const string Owner = "Pyromesis";
    public const string Repo = "CA-O";
    public const string LatestApiUrl = "https://api.github.com/repos/Pyromesis/CA-O/releases/latest";

    public static string CurrentVersion => CAO.Shared.Constants.BuildConstants.ProductVersion;

    /// <summary>¿Es el tag más nuevo que la versión actual? ("v2.1.6" > "2.1.5").</summary>
    public static bool IsNewer(string currentVersion, string? tag)
    {
        if (!TryParseVersion(tag, out var latest)) return false;
        if (!TryParseVersion(currentVersion, out var current)) return false;
        return latest > current;
    }

    public static bool TryParseVersion(string? text, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(text)) return false;
        var cleaned = text.Trim().TrimStart('v', 'V');
        var dash = cleaned.IndexOfAny(['-', '+']);
        if (dash >= 0) cleaned = cleaned[..dash];
        return Version.TryParse(cleaned, out version!) && version.Major > 0;
    }

    /// <summary>Elige el ZIP completo (no el GUI online) de la lista de assets.</summary>
    public static (string Name, string Url, long Bytes)? SelectFullPackageAsset(
        IEnumerable<(string Name, string Url, long Bytes)> assets)
    {
        foreach (var asset in assets)
        {
            if (asset.Name.EndsWith("-win-x64.zip", StringComparison.OrdinalIgnoreCase) &&
                !asset.Name.StartsWith("CA-O-Setup-GUI", StringComparison.OrdinalIgnoreCase) &&
                Uri.TryCreate(asset.Url, UriKind.Absolute, out _))
            {
                return asset;
            }
        }
        return null;
    }

    /// <summary>Consulta el último release; null si no hay nada nuevo o falla la red.</summary>
    public static async Task<AvailableRelease?> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            using var http = CreateClient(TimeSpan.FromSeconds(10));
            using var response = await http.GetAsync(LatestApiUrl, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            var root = document.RootElement;
            var tag = root.TryGetProperty("tag_name", out var tagProp) ? tagProp.GetString() : null;
            if (!IsNewer(CurrentVersion, tag)) return null;

            string? name = null;
            if (root.TryGetProperty("name", out var nameProp)) name = nameProp.GetString();

            (string Name, string Url, long Bytes)? picked = null;
            if (root.TryGetProperty("assets", out var assetsProp) && assetsProp.ValueKind == JsonValueKind.Array)
            {
                var assets = new List<(string, string, long)>();
                foreach (var asset in assetsProp.EnumerateArray())
                {
                    var assetName = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    var url = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() ?? "" : "";
                    var size = asset.TryGetProperty("size", out var s) && s.TryGetInt64(out var bytes) ? bytes : 0L;
                    assets.Add((assetName, url, size));
                }
                picked = SelectFullPackageAsset(assets);
            }

            return new AvailableRelease(tag!, name ?? tag!, picked?.Url, picked?.Bytes ?? 0L);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Descarga con progreso 0..1. Nunca continúa en el hilo de UI
    /// (<c>ConfigureAwait(false)</c>) y limita los reportes de progreso
    /// (cada ≥0,5 % o ≥500 ms) para no inundar el dispatcher con los
    /// ~5000 trozos de un paquete de ~400 MB — eso congelaba la app.
    /// Lanza si la red falla.
    /// </summary>
    public static async Task DownloadAsync(string url, string destinationPath, IProgress<double>? progress, CancellationToken ct)
    {
        using var http = CreateClient(TimeSpan.FromMinutes(30));
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength;
        await using var network = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var file = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var buffer = new byte[81920];
        long read = 0;
        var lastReported = 0.0;
        var lastReportAt = Environment.TickCount64;
        int n;
        while ((n = await network.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false)) > 0)
        {
            await file.WriteAsync(buffer.AsMemory(0, n), ct).ConfigureAwait(false);
            read += n;
            if (total.HasValue && total.Value > 0 && progress is not null)
            {
                var ratio = (double)read / total.Value;
                var now = Environment.TickCount64;
                if (ratio - lastReported >= 0.005 || now - lastReportAt >= 500 || ratio >= 1.0)
                {
                    lastReported = ratio;
                    lastReportAt = now;
                    progress.Report(Math.Min(ratio, 1.0));
                }
            }
        }
        if (total.HasValue && total.Value > 0)
            progress?.Report(1.0);
    }

    /// <summary>
    /// Extrae el ZIP entrada por entrada con progreso 0..1 (limitado a ≥0,5 % o
    /// ≥500 ms) para que la UI muestre porcentaje real en vez de una espera
    /// indeterminada de varios minutos. Corre fuera del hilo UI, protege
    /// contra Zip-Slip y propaga cancelación. Lanza si falla.
    /// </summary>
    public static async Task ExtractWithProgressAsync(
        string zipPath, string destinationDir,
        IProgress<(double Ratio, int Done, int Total)>? progress, CancellationToken ct)
    {
        await Task.Run(() =>
        {
            using var archive = ZipFile.OpenRead(zipPath);
            var entries = archive.Entries
                .Where(e => !string.IsNullOrEmpty(e.Name))
                .ToList();
            var total = Math.Max(entries.Count, 1);
            var root = Path.GetFullPath(destinationDir) + Path.DirectorySeparatorChar;
            var done = 0;
            var lastReported = 0.0;
            var lastReportAt = Environment.TickCount64;
            foreach (var entry in entries)
            {
                ct.ThrowIfCancellationRequested();
                var dest = Path.GetFullPath(Path.Combine(destinationDir, entry.FullName));
                if (!dest.StartsWith(root, StringComparison.Ordinal))
                    throw new IOException($"Entrada ZIP fuera del destino: {entry.FullName}");
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                entry.ExtractToFile(dest, overwrite: true);
                done++;
                if (progress is not null)
                {
                    var ratio = (double)done / total;
                    var now = Environment.TickCount64;
                    if (ratio - lastReported >= 0.005 || now - lastReportAt >= 500 || done >= total)
                    {
                        lastReported = ratio;
                        lastReportAt = now;
                        progress.Report((Math.Min(ratio, 1.0), done, total));
                    }
                }
            }
            progress?.Report((1.0, done, total));
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Quita Mark-of-the-Web (Zone.Identifier) de un árbol descargado. Los
    /// archivos extraídos de un ZIP de internet heredan la marca y SmartScreen
    /// puede frenar el instalador auto-lanzado en silencio (el proceso vive
    /// pero su ventana nunca aparece). Best-effort: nunca lanza.
    /// Devuelve cuántos ADS eliminó.
    /// </summary>
    public static int UnblockTree(string directory)
    {
        var removed = 0;
        try
        {
            if (File.Exists(directory))
                return TryDeleteAds(directory) ? 1 : 0;
            if (!Directory.Exists(directory)) return 0;
            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                if (TryDeleteAds(file)) removed++;
            }
        }
        catch { }
        return removed;
    }

    private static bool TryDeleteAds(string file)
    {
        try
        {
            File.Delete(file + ":Zone.Identifier");
            return true;
        }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
        catch (IOException) { return false; }
        catch (NotSupportedException) { return false; }
    }

    /// <summary>
    /// Reintenta una operación ante bloqueos transitorios del archivo (p. ej. el antivirus
    /// escaneando el ZIP recién descargado). Solo reintenta IOException; cualquier otro
    /// error se propaga de inmediato.
    /// </summary>
    public static async Task ExecuteWithRetryAsync(Func<Task> operation, int maxAttempts = 5, int baseDelayMs = 500, CancellationToken ct = default)
    {
        var attempt = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await operation().ConfigureAwait(false);
                return;
            }
            catch (IOException) when (attempt < maxAttempts - 1)
            {
                attempt++;
                await Task.Delay(baseDelayMs * attempt, ct).ConfigureAwait(false);
            }
        }
    }

    private static HttpClient CreateClient(TimeSpan? timeout = null)
    {
        var http = new HttpClient { Timeout = timeout ?? TimeSpan.FromSeconds(10) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"CA-O-App/{CurrentVersion}");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github.v3+json");
        return http;
    }
}
