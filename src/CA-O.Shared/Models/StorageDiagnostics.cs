namespace CAO.Shared;

public sealed record StorageVolumeReport(
    string Name,
    string DriveType,
    string FileSystem,
    long TotalBytes,
    long FreeBytes,
    bool IsSystemVolume);

public sealed record StorageDiagnosticsReport(
    IReadOnlyList<StorageVolumeReport> Volumes,
    DateTime TimestampUtc);