using System.IO;
using CAO.Core.Optimization;
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
                string.Equals(drive.Name, systemRoot, StringComparison.OrdinalIgnoreCase),
                MediaLabel(drive)))
            .ToArray();

        return new StorageDiagnosticsReport(volumes, DateTime.UtcNow);
    }

    /// <summary>
    /// Medio físico para volúmenes fijos ("HDD"/"SSD"/"SCM"); vacío si no se
    /// puede determinar (extraíbles, red, VMs opacas). Nunca lanza.
    /// </summary>
    internal static string MediaLabel(DriveInfo drive)
    {
        try
        {
            if (drive.DriveType != DriveType.Fixed) return string.Empty;
            return DiskMediaDetector.ResolveVolumeMedia(drive.Name[..2].ToUpperInvariant()) switch
            {
                DiskMedia.Hdd => "HDD",
                DiskMedia.Ssd => "SSD",
                DiskMedia.Scm => "SCM",
                _ => string.Empty,
            };
        }
        catch { return string.Empty; }
    }
}