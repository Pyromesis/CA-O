namespace CAO.Shared;

/// <summary>
/// Immutable snapshot of everything the recommendation and profile engines
/// are allowed to consider: hardware, Windows build, security posture,
/// anti-cheat presence and power context (spec 104-106). Diagnostics-first:
/// recommendations are only computed against a filled context.
/// </summary>
public sealed record SystemContext
{
    // ---- Windows ----
    public required int WindowsBuild { get; init; }

    public required string WindowsEdition { get; init; }

    public required string Architecture { get; init; }

    public WindowsServicingState ServicingState { get; init; } = WindowsServicingState.Unknown;

    // ---- Hardware ----
    public required string CpuName { get; init; }

    public required int CpuCores { get; init; }

    public required int CpuLogicalProcessors { get; init; }

    public required int RamGb { get; init; }

    public bool HasSsd { get; init; }

    public bool HasNvme { get; init; }

    public bool IsLaptop { get; init; }

    public bool OnBattery { get; init; }

    // ---- GPU / display ----
    public string GpuName { get; init; } = string.Empty;

    public string GpuVendor { get; init; } = string.Empty;

    public string GpuDriverVersion { get; init; } = string.Empty;

    public int DisplayRefreshHz { get; init; }

    public bool VrrSupported { get; init; }

    // ---- Security posture ----
    public bool? SecureBootEnabled { get; init; }

    public bool? TpmPresent { get; init; }

    public bool? VbsEnabled { get; init; }

    public bool? HvciEnabled { get; init; }

    // ---- Anti-cheat ----
    public IReadOnlyList<AntiCheatInfo> AntiCheats { get; init; } = Array.Empty<AntiCheatInfo>();

    public bool VanguardDetected => AntiCheats.Any(a => a.Kind == AntiCheatKind.Vanguard);

    // ---- Games (spec 93) ----
    public IReadOnlyList<string> GamesDetected { get; init; } = Array.Empty<string>();

    // ---- Thermal ----
    public ThermalState ThermalState { get; init; } = ThermalState.Unknown;

    /// <summary>UTC timestamp of when this context was measured.</summary>
    public required DateTime MeasuredUtc { get; init; }
}

public enum WindowsServicingState
{
    Unknown,
    Supported,
    EndOfService,
}

public enum ThermalState
{
    Unknown,
    Nominal,
    Warm,
    Throttling,
}

public enum AntiCheatKind
{
    Unknown,
    Vanguard,
    EasyAntiCheat,
    BattlEye,
    Faceit,
    Ricochet,
}

public sealed record AntiCheatInfo(AntiCheatKind Kind, string Source, IReadOnlyList<string> Components);

/// <summary>Empty context for unit tests.</summary>
public static class SystemContextFactory
{
    public static SystemContext Default() => new()
    {
        WindowsBuild = 26200,
        WindowsEdition = "Windows 11",
        Architecture = "x64",
        CpuName = "Test CPU",
        CpuCores = 8,
        CpuLogicalProcessors = 16,
        RamGb = 16,
        MeasuredUtc = DateTime.UtcNow,
    };
}
