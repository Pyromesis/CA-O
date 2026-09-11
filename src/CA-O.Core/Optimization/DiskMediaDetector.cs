namespace CAO.Core.Optimization;

/// <summary>Medio físico de un disco (MSFT_PhysicalDisk.MediaType).</summary>
public enum DiskMedia
{
    Unknown = 0,
    Hdd = 1,
    Ssd = 2,
    Scm = 3,
}

/// <summary>
/// Resuelve el medio (HDD/SSD) de un volumen. La regla de oro de CA-O:
/// solo se desfragmenta un HDD confirmado; SSD y desconocidos se omiten.
/// Nunca lanza: lo desconocido es <see cref="DiskMedia.Unknown"/>.
/// Público para que la UI también muestre el medio (Panel, Analizar).
/// </summary>
public static class DiskMediaDetector
{
    /// <summary>MSFT_PhysicalDisk.MediaType: 3=HDD, 4=SSD, 5=SCM.</summary>
    public static DiskMedia MapMediaType(object? mediaType) => mediaType switch
    {
        byte b => MapMediaType((long)b),
        short s => MapMediaType((long)s),
        ushort us => MapMediaType((long)us),
        int i => MapMediaType((long)i),
        uint ui => MapMediaType((long)ui),
        long l => l switch
        {
            3 => DiskMedia.Hdd,
            4 => DiskMedia.Ssd,
            5 => DiskMedia.Scm,
            _ => DiskMedia.Unknown,
        },
        _ => DiskMedia.Unknown,
    };

    /// <summary>
    /// Volumen ("C:") → disco físico → medio, vía asociadores WMI
    /// (LogicalDisk→Partition→DiskDrive→MSFT_PhysicalDisk por índice).
    /// </summary>
    public static DiskMedia ResolveVolumeMedia(string driveVolume)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(driveVolume) || driveVolume.Length < 2) return DiskMedia.Unknown;
            var letter = char.ToUpperInvariant(driveVolume[0]);
            if (letter is < 'A' or > 'Z') return DiskMedia.Unknown;

            var scope = new System.Management.ManagementScope(@"root\cimv2");
            var diskIndex = FindDiskIndex(scope, letter);
            if (diskIndex is null) return DiskMedia.Unknown;
            return FindPhysicalMedia(diskIndex.Value);
        }
        catch { return DiskMedia.Unknown; }
    }

    private static int? FindDiskIndex(System.Management.ManagementScope scope, char letter)
    {
        try
        {
            var diskQuery = new System.Management.ObjectQuery(
                $"ASSOCIATORS OF {{Win32_LogicalDisk.DeviceID=\"{letter}:\"}} WHERE AssocClass=Win32_LogicalDiskToPartition");
            using var disks = new System.Management.ManagementObjectSearcher(scope, diskQuery);
            foreach (var partition in disks.Get().Cast<System.Management.ManagementObject>())
            {
                var partitionId = partition["DeviceID"]?.ToString();
                if (string.IsNullOrWhiteSpace(partitionId)) continue;
                var driveQuery = new System.Management.ObjectQuery(
                    $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID=\"{EscapeWmi(partitionId)}\"}} WHERE AssocClass=Win32_DiskDriveToDiskPartition");
                using var drives = new System.Management.ManagementObjectSearcher(scope, driveQuery);
                foreach (var drive in drives.Get().Cast<System.Management.ManagementObject>())
                {
                    var index = drive["Index"];
                    if (index is not null && int.TryParse(index.ToString(), out var number))
                        return number;
                }
            }
        }
        catch { }
        return null;
    }

    private static DiskMedia FindPhysicalMedia(int diskIndex)
    {
        try
        {
            var scope = new System.Management.ManagementScope(@"root\Microsoft\Windows\Storage");
            using var searcher = new System.Management.ManagementObjectSearcher(
                scope,
                new System.Management.ObjectQuery(
                    $"SELECT MediaType FROM MSFT_PhysicalDisk WHERE DeviceId=\"{diskIndex}\""));
            foreach (var disk in searcher.Get().Cast<System.Management.ManagementObject>())
            {
                var media = MapMediaType(disk["MediaType"]);
                if (media != DiskMedia.Unknown) return media;
            }
        }
        catch { }
        return DiskMedia.Unknown;
    }

    private static string EscapeWmi(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
