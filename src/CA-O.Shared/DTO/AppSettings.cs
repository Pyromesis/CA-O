using System.Text.Json.Serialization;

namespace CAO.Shared;

/// <summary>Persisted per-optimization entry inside settings.json.</summary>
public sealed class OptimizationSettingsEntry
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("last_run")]
    public DateTime? LastRunUtc { get; set; }

    [JsonPropertyName("impact")]
    public string Impact { get; set; } = "low";
}

/// <summary>UI preferences.</summary>
public sealed class UiSettings
{
    [JsonPropertyName("theme")]
    public string Theme { get; set; } = "system";

    [JsonPropertyName("language")]
    public string Language { get; set; } = "es-ES";

    [JsonPropertyName("expert_mode")]
    public bool ExpertMode { get; set; }
}

/// <summary>Cached system information from the last scan.</summary>
public sealed class SystemInfoCache
{
    [JsonPropertyName("last_scan")]
    public DateTime? LastScanUtc { get; set; }

    [JsonPropertyName("windows_version")]
    public string WindowsVersion { get; set; } = string.Empty;

    [JsonPropertyName("ram_gb")]
    public int RamGb { get; set; }

    [JsonPropertyName("disk_type")]
    public string DiskType { get; set; } = "SSD";
}

/// <summary>
/// Root document persisted at %AppData%\CA-O\settings.json.
/// Schema matches the product spec (v2).
/// </summary>
public sealed class AppSettings
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "2.0.0";

    [JsonPropertyName("optimizations")]
    public Dictionary<string, OptimizationSettingsEntry> Optimizations { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("ui_settings")]
    public UiSettings Ui { get; set; } = new();

    [JsonPropertyName("system_info")]
    public SystemInfoCache SystemInfo { get; set; } = new();

    public OptimizationSettingsEntry Entry(string optimizationId) =>
        Optimizations.TryGetValue(optimizationId, out var entry)
            ? entry
            : Optimizations[optimizationId] = new OptimizationSettingsEntry();
}
