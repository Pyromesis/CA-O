namespace CAO.Shared;

public sealed record ThermalZoneDiagnostic(
    string Name,
    double? TemperatureCelsius,
    string Source);

public sealed record ThermalDiagnosticsReport(
    IReadOnlyList<ThermalZoneDiagnostic> Zones,
    bool IsAvailable,
    DateTime TimestampUtc);