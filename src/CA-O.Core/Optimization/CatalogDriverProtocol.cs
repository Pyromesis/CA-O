using System.Text.RegularExpressions;
using CAO.Shared;

namespace CAO.Core.Optimization;

/// <summary>
/// Protocolo puro del Catálogo de Microsoft Update (probado en vivo):
/// Search.aspx?q=HWID → GUIDs+títulos; DownloadDialog.aspx → URLs del .cab.
/// Sin red aquí: testeable con HTML enlatado. Público para la UI (rank de
/// INF y comparación de versiones del flujo automático).
/// </summary>
public static partial class CatalogDriverProtocol
{
    public const string CatalogBaseUrl = "https://www.catalog.update.microsoft.com";
    public const string BrowserUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0 Safari/537.36";
    public const int MaxOffers = 25;
    public const int MaxInfs = 50;
    public const long MaxCabBytes = 1536L * 1024 * 1024; // 1.5 GB

    [GeneratedRegex(@"VEN_[0-9A-Fa-f]{4}&DEV_[0-9A-Fa-f]{4}", RegexOptions.CultureInvariant)]
    private static partial Regex VenDevFragment();

    [GeneratedRegex(@"(?:\((\d+(?:\.\d+){1,3})\)| - (\d+(?:\.\d+){1,3}))\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex TrailingVersion();

    private static readonly Regex OfferAnchor = new(
        @"onclick='goToDetails\(""(?<id>[0-9a-fA-F-]{36})""\);' class=""contentTextItemSpacerNoBreakLink"">\s*(?<title>[^<]+?)\s*</a>",
        RegexOptions.Singleline | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    private static readonly Regex DownloadUrl = new(
        @"(https?://(?:dl\.delivery\.mp\.microsoft\.com|(?:catalog\.s\.)?download\.windowsupdate\.com|download\.microsoft\.com)/[^\s'""<>]+)",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
        TimeSpan.FromSeconds(5));

    public static string SearchUrl(string query) =>
        CatalogBaseUrl + "/Search.aspx?q=" + Uri.EscapeDataString(query);

    /// <summary>Fragmento VEN_xxxx&amp;DEV_yyyy de un HWID (vacío si no hay).</summary>
    public static string ExtractVenDev(string hardwareId)
    {
        if (string.IsNullOrWhiteSpace(hardwareId)) return string.Empty;
        var m = VenDevFragment().Match(hardwareId);
        return m.Success ? m.Value.ToUpperInvariant() : string.Empty;
    }

    /// <summary>
    /// Consultas en orden: HWID completo → fragmento VEN&amp;DEV → palabras
    /// del nombre. El catálogo rara vez matchea el HWID largo.
    /// </summary>
    public static IReadOnlyList<string> BuildQueries(string hardwareId, string deviceName)
    {
        var queries = new List<string>();
        void Add(string q)
        {
            q = q.Trim();
            if (q.Length >= 3 && !queries.Contains(q, StringComparer.OrdinalIgnoreCase))
                queries.Add(q);
        }
        Add(hardwareId);
        var venDev = ExtractVenDev(hardwareId);
        if (venDev.Length > 0) Add(venDev);
        if (!string.IsNullOrWhiteSpace(deviceName))
        {
            var tokens = Regex.Split(deviceName, @"[^A-Za-z0-9]+")
                .Where(t => t.Length > 2)
                .Take(4);
            Add(string.Join(" ", tokens));
        }
        return queries;
    }

    /// <summary>Parsea ofertas (id+título+versión), la más nueva primero.</summary>
    public static IReadOnlyList<CatalogDriverOffer> ParseOffers(string html)
    {
        if (string.IsNullOrEmpty(html)) return Array.Empty<CatalogDriverOffer>();
        var offers = new List<CatalogDriverOffer>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in OfferAnchor.Matches(html))
        {
            var id = m.Groups["id"].Value;
            if (!seen.Add(id)) continue;
            var title = System.Net.WebUtility.HtmlDecode(m.Groups["title"].Value).Trim();
            if (title.Length == 0) continue;
            offers.Add(new CatalogDriverOffer(id, title, ExtractVersion(title)));
            if (offers.Count >= MaxOffers) break;
        }
        offers.Sort(static (a, b) =>
        {
            var va = TryParseVersion(a.Version);
            var vb = TryParseVersion(b.Version);
            if (va is null && vb is null) return string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase);
            if (va is null) return 1;
            if (vb is null) return -1;
            return vb.CompareTo(va);
        });
        return offers;
    }

    public static string ExtractVersion(string title)
    {
        var m = TrailingVersion().Match(title);
        if (!m.Success) return string.Empty;
        return m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
    }

    public static Version? TryParseVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version)) return null;
        var parts = version.Split('.');
        while (parts.Length < 2) parts = [.. parts, "0"];
        if (parts.Length > 4) parts = parts[..4];
        return Version.TryParse(string.Join(".", parts), out var v) ? v : null;
    }

    public static string DownloadDialogBody(string updateId) =>
        "updateIDs=" + Uri.EscapeDataString($"[{{\"size\":0,\"updateID\":\"{updateId}\",\"uidInfo\":\"{updateId}\"}}]");

    /// <summary>URLs de descarga del diálogo (solo hosts Microsoft).</summary>
    public static IReadOnlyList<string> ParseDownloadUrls(string content)
    {
        if (string.IsNullOrEmpty(content)) return Array.Empty<string>();
        return DownloadUrl.Matches(content)
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(CAO.Shared.Security.CommandPolicy.IsAllowedCatalogHost)
            .ToList();
    }

    public static string? PickCabUrl(IReadOnlyList<string> urls) =>
        urls.FirstOrDefault(u => u.EndsWith(".cab", StringComparison.OrdinalIgnoreCase)) ?? urls.FirstOrDefault();

    /// <summary>
    /// ¿Merece la pena intentar la oferta? Solo se omite si es la MISMA
    /// versión instalada: entre esquemas distintos (inbox vs fabricante) el
    /// "mayor que" miente, así que ante la duda se intenta.
    /// </summary>
    public static bool ShouldTryOffer(string installedVersion, string offeredVersion)
    {
        var installed = TryParseVersion(installedVersion);
        var offered = TryParseVersion(offeredVersion);
        if (installed is null || offered is null) return true;
        return !offered.Equals(installed);
    }

    /// <summary>
    /// Elige el .inf que menciona el HWID (2 pts) o su VEN&amp;DEV (1 pto).
    /// null = ninguno menciona al dispositivo (pedir al usuario).
    /// </summary>
    public static string? PickBestInfMatch(IReadOnlyList<string> infPaths, string hardwareId)
    {
        if (infPaths.Count == 0 || string.IsNullOrWhiteSpace(hardwareId)) return null;
        var venDev = ExtractVenDev(hardwareId);
        string? best = null;
        int bestScore = 0;
        foreach (var path in infPaths.Take(MaxInfs))
        {
            int score = 0;
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists || info.Length == 0 || info.Length > 512 * 1024) continue;
                var text = File.ReadAllText(path);
                if (text.Contains(hardwareId, StringComparison.OrdinalIgnoreCase)) score = 2;
                else if (venDev.Length > 0 && text.Contains(venDev, StringComparison.OrdinalIgnoreCase)) score = 1;
            }
            catch { continue; }
            if (score > bestScore)
            {
                bestScore = score;
                best = path;
            }
        }
        return bestScore > 0 ? best : null;
    }

    public static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024L * 1024 => $"{bytes / 1024.0:0.#} KB",
        < 1024L * 1024 * 1024 => $"{bytes / 1024.0 / 1024:0.#} MB",
        _ => $"{bytes / 1024.0 / 1024 / 1024:0.##} GB",
    };
}
