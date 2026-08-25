namespace CAO.Shared;

/// <summary>DPC/ISR pressure observed on one logical processor.</summary>
public sealed record ProcessorInterruptSample(
    string Instance,
    double MaxDpcTimePercent,
    double MaxInterruptTimePercent);

/// <summary>
/// System-wide interrupt pressure over a sampling window. Per-driver
/// attribution requires an ETW kernel trace and is intentionally out of
/// scope (documented limitation); this answers "is there a DPC/ISR problem
/// at all, and how severe".
/// </summary>
public sealed record InterruptPressureReport(
    TimeSpan Window,
    IReadOnlyList<ProcessorInterruptSample> Samples,
    double TotalMaxDpcPercent,
    double TotalMaxInterruptPercent)
{
    /// <summary>Rough severity heuristic used by diagnostics UI.</summary>
    public string SeverityEs =>
        TotalMaxDpcPercent >= 25 ? "Alta" :
        TotalMaxDpcPercent >= 10 ? "Media" : "Baja";
}
