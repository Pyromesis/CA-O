namespace CAO.Shared;

/// <summary>Top-level UI categories shown in the navigation.</summary>
public enum OptimizationCategory
{
    Performance,
    PrivacySecurity,
    Gaming,
    Storage,
    Network,
    Cleanup,
}

/// <summary>User-facing impact label. Deliberately coarse; no FPS numbers without measurement.</summary>
public enum ImpactLevel
{
    Low,
    Medium,
    High,
}

public enum PerformanceImpact
{
    None,
    Tiny,
    Small,
    Moderate,
    Large,
    WorkloadDependent,
    DiagnosticOnly,
}

public enum EvidenceLevel
{
    Official,
    Vendor,
    Benchmark,
    Empirical,
    Heuristic,
    Unknown,
}

public enum RiskLevel
{
    Safe,
    Low,
    Moderate,
    High,
    Critical,
}

public enum CompatibilityStatus
{
    Compatible,
    NoKnownConflict,
    Conditional,
    PotentialConflict,
    Incompatible,
    Unknown,
}

public enum SecurityImpact
{
    None,
    PrivacyOnly,
    ReducedProtection,
    IncreasedProtection,
    Unknown,
}

/// <summary>Extra handling an optimization requires before Apply is offered.</summary>
[Flags]
public enum OptimizationFlags
{
    None = 0,

    /// <summary>Only visible when the user enables Expert Mode.</summary>
    ExpertOnly = 1 << 0,

    /// <summary>Reduces a security feature; shows a critical warning and double confirmation.</summary>
    SecurityTradeoff = 1 << 1,

    /// <summary>A reboot is required for the change to take effect.</summary>
    RequiresReboot = 1 << 2,

    /// <summary>Recommended only on SSDs (e.g. search indexing).</summary>
    RecommendedOnSsd = 1 << 3,

    /// <summary>Maintenance action; not reversible by design (e.g. temp cleanup).</summary>
    NotReversible = 1 << 4,
}

/// <summary>Current machine-detected/user-chosen state of one optimization.</summary>
public enum OptimizationState
{
    Unknown,
    AppliedByCao,
    NotApplied,
    AppliedManually,
    PendingReboot,
}
