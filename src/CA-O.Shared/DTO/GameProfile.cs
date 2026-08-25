namespace CAO.Shared;

/// <summary>Anti-cheat posture of a game (FASE 22/23): drives conservative blocking.</summary>
public enum GameAntiCheatPolicy
{
    None,
    KernelLevel,
    UserMode,
}

/// <summary>
/// Per-game profile (FASE 22). Guidance-only: CA-O never mutates game files
/// or applies system-wide changes because of one game. Every entry must cite
/// vendor-documented features only — no forum folklore.
/// </summary>
public sealed record GameProfile
{
    public required string GameId { get; init; }

    public required string DisplayName { get; init; }

    /// <summary>Shipping executable name(s) used by detection.</summary>
    public required IReadOnlyList<string> Executables { get; init; }

    /// <summary>Launcher family, informational.</summary>
    public string Launcher { get; init; } = string.Empty;

    /// <summary>Vendor latency tech available in this game.</summary>
    public bool ReflexAvailable { get; init; }

    public bool AntiLagAvailable { get; init; }

    /// <summary>Anti-cheat posture for conservative gating.</summary>
    public required GameAntiCheatPolicy AntiCheatPolicy { get; init; }

    /// <summary>Guidance lines shown in the Gaming page (es-ES).</summary>
    public required IReadOnlyList<string> GuidanceEs { get; init; }
}
