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
    string Provider = "",
    // Visible en Administrador de dispositivos (presente) u oculto/fantasma.
    bool IsPresent = true);

public sealed record DriverDiagnosticsReport(
    IReadOnlyList<DriverDiagnostic> Drivers,
    DateTime TimestampUtc);

/// <summary>Un driver ofertado por Windows Update (DTO de ida y vuelta).</summary>
public sealed record DriverUpdateInfo(
    string UpdateId,
    string Title,
    string Kb,
    long SizeBytes,
    bool RebootRequired);

/// <summary>
/// Identidad del equipo (fase drivers 3: originales del fabricante).
/// Los equipos clónicos/VM devuelven "Default string" en sistema: por eso
/// también se lee placa base y BIOS como respaldo.
/// </summary>
public sealed record ComputerInfo(
    string Manufacturer,
    string Model,
    string SerialNumber,
    string BoardManufacturer = "",
    string BoardProduct = "",
    string BiosManufacturer = "");