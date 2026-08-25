using System.IO;
using CAO.Shared;

namespace CAO.Infrastructure.Storage;

public sealed class StorageDiagnosticsProvider
{
    public StorageDiagnosticsReport Measure()
    {
        var systemRoot = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
        var volumes = DriveInfo.GetDrives()
            .Where(drive => drive.IsReady)
            .Select(drive => new StorageVolumeReport(
                drive.Name,
                drive.DriveType.ToString(),
                drive.DriveFormat,
                drive.TotalSize,
                drive.AvailableFreeSpace,
                string.Equals(drive.Name, systemRoot, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        return new StorageDiagnosticsReport(volumes, DateTime.UtcNow);
    }
}