namespace CAO.Shared;

public sealed record InputDiagnosticsReport(
    bool? MouseAccelerationEnabled,
    int HidDeviceCount,
    DateTime TimestampUtc);