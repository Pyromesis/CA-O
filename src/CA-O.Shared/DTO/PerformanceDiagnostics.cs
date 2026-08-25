namespace CAO.Shared;

public sealed record ProcessorPerformance(
    string Name,
    int? LoadPercent,
    int? CurrentClockMHz,
    int? MaxClockMHz);

public sealed record GraphicsAdapterDiagnostic(
    string Name,
    string DriverVersion,
    ulong? AdapterRamBytes,
    string Status);

public sealed record PerformanceDiagnosticsReport(
    IReadOnlyList<ProcessorPerformance> Processors,
    IReadOnlyList<GraphicsAdapterDiagnostic> GraphicsAdapters,
    DateTime TimestampUtc);