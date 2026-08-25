namespace CAO.Shared;

public sealed record DriverDiagnostic(
    string Name,
    string DeviceClass,
    string Manufacturer,
    string Version,
    string Date,
    bool? IsSigned,
    string Status,
    int ProblemCode);

public sealed record DriverDiagnosticsReport(
    IReadOnlyList<DriverDiagnostic> Drivers,
    DateTime TimestampUtc);