namespace CAO.Shared;

public sealed record StorageVolumeReport(
    string Name,
    string DriveType,
    string FileSystem,
    long TotalBytes,
    long FreeBytes,
    bool IsSystemVolume,
    // Medio físico ("HDD"/"SSD"/"SCM"; vacío si desconocido).
    string Media = "");

public sealed record StorageDiagnosticsReport(
    IReadOnlyList<StorageVolumeReport> Volumes,
    DateTime TimestampUtc);