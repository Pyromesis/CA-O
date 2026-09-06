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
            using var http = CreateClient();
            using var response = await http.GetAsync(LatestApiUrl, ct);
            response.EnsureSuccessStatusCode();
            using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
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

    /// <summary>Descarga con progreso 0..1. Lanza si la red falla.</summary>
    public static async Task DownloadAsync(string url, string destinationPath, IProgress<double>? progress, CancellationToken ct)
    {
        using var http = CreateClient();
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength;
        await using var network = await response.Content.ReadAsStreamAsync(ct);
        await using var file = File.Create(destinationPath);
        var buffer = new byte[81920];
        long read = 0;
        int n;
        while ((n = await network.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
        {
            await file.WriteAsync(buffer.AsMemory(0, n), ct);
            read += n;
            if (total.HasValue && total.Value > 0)
                progress?.Report((double)read / total.Value);
        }
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

    private static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"CA-O-App/{CurrentVersion}");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github.v3+json");
        return http;
    }
}
