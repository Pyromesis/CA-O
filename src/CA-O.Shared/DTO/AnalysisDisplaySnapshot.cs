namespace CAO.Shared;

/// <summary>
/// Foto de lo RENDERIZADO en Analizar: textos, resolvers DNS, filas de
/// barras y escaneos que no tienen estado persistente propio (DNS, DPC,
/// gaming-scan, diagnósticos). Vive en UiState (sesión) y en
/// PersistedAnalysis.Display (disco, campo opcional: ficheros viejos
/// sin él se leen igual, sin bump de schema).
/// </summary>
public sealed record DnsRowSnapshot(string Resolver, double Ms);

public sealed record StorageRowSnapshot(string Name, double UsedPct, double FreeGb, string Media = "");

public sealed record AnalysisDisplaySnapshot(
    string? Network,
    string? Security,
    string? Storage,
    string? Drivers,
    string? Input,
    string? Thermal,
    string? Perf,
    string? DnsBest,
    string? DnsPrimary,
    double? DnsPrimaryMs,
    string? DnsSecondary,
    double? DnsSecondaryMs,
    List<DnsRowSnapshot>? DnsRows,
    List<StorageRowSnapshot>? StorageRows,
    string? Interrupts,
    string? DpcStatus,
    double? DpcMax,
    string? GamingScan,
    string? GamingBlocked,
    string? GamingGames);
