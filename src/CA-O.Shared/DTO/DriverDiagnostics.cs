namespace CAO.Shared;

public sealed record DriverDiagnostic(
    string Name,
    string DeviceClass,
    string Manufacturer,
    string Version,
    string Date,
    bool? IsSigned,
    string Status,
    int ProblemCode,
    // Identidad accionable (fase 2): vacíos en datos antiguos, siempre
    // presentes en escaneos nuevos. Defaults para no romper constructores.
    string PnpDeviceId = "",
    string HardwareId = "",
    string InfName = "",
    string Provider = "");

public sealed record DriverDiagnosticsReport(
    IReadOnlyList<DriverDiagnostic> Drivers,
    DateTime TimestampUtc);

/// <summary>Identidad del equipo (fase drivers 3: originales del fabricante).</summary>
public sealed record ComputerInfo(
    string Manufacturer,
    string Model,
    string SerialNumber);