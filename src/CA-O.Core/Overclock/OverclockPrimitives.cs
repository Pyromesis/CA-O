namespace CAO.Core.Overclock;

/// <summary>Physical component a knob belongs to.</summary>
public enum OverclockComponent
{
    Cpu,
    Gpu,
    Memory,
}

/// <summary>Unit a knob value is expressed in.</summary>
public enum KnobUnit
{
    Hertz,
    Millivolt,
    Milliwatt,
    Percent,
    Bits,
    Ratio,
    Enumerated,
    Celsius,
}

/// <summary>Shape of the value a knob accepts.</summary>
public enum KnobKind
{
    /// <summary>A continuous numeric value within <see cref="KnobRange"/>.</summary>
    Scalar,

    /// <summary>A discrete choice from a fixed set of allowed values.</summary>
    Enum,

    /// <summary>A per-index table (fan curves, per-pstate offsets).</summary>
    Curve,
}

/// <summary>How two knobs relate to each other.</summary>
public enum DependencyKind
{
    /// <summary>Only meaningful together; applying one alone wastes risk budget.</summary>
    PairsWith,

    /// <summary>This knob cannot be offered unless the other one is available.</summary>
    Requires,

    /// <summary>These two cannot be active at the same time.</summary>
    IncompatibleWith,
}

/// <summary>
/// Whether a knob change survives closing the user session. GPU tuning state
/// is session-scoped by design (design D3): NVAPI setters are tied to the
/// interactive session, which is exactly why they are not driven from the
/// SYSTEM service.
/// </summary>
public enum KnobScope
{
    Session,
    Persistent,
}

/// <summary>Which backend serves a knob on the detected hardware.</summary>
public enum CpuBackendKind
{
    None,
    XtuGuided,
    PolicyOnly,
    MsrSigned,
}

public enum GpuBackendKind
{
    None,
    NvidiaNvapi,
    AmdAdlx,
}

/// <summary>
/// Memory has no writable backend in user mode (design 6.7). SMBIOS reports
/// nominal vs configured speed; the XMP/EXPO profile lives in the module's
/// SPD EEPROM and is only exposed to firmware during boot.
/// </summary>
public enum MemoryBackendKind
{
    None,
    SmbiosReportOnly,
}

/// <summary>Named tuning profiles the user picks; the engine picks the values.</summary>
public enum OverclockProfile
{
    Conservative,
    Balanced,
    Performance,
    Custom,
}