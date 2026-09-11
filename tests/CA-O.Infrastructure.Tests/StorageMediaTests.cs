using System.Text.Json;
using CAO.Core.Optimization;
using CAO.Infrastructure.Storage;
using CAO.Shared;
using Xunit;

namespace CAO.Infrastructure.Tests;

/// <summary>
/// Medio físico visible (HDD/SSD) en Panel y Analizar. Tests vivos tolerantes:
/// en VMs opacas el medio es "" y eso también es correcto.
/// </summary>
public sealed class StorageMediaTests
{
    private static readonly HashSet<string> ValidMedia =
        new(StringComparer.Ordinal) { "", "HDD", "SSD", "SCM" };

    [Fact]
    public void Measure_TagsEveryVolumeWithValidMedia()
    {
        var report = new StorageDiagnosticsProvider().Measure();

        Assert.NotEmpty(report.Volumes);
        Assert.All(report.Volumes, v => Assert.True(
            ValidMedia.Contains(v.Media ?? ""), $"Medio inesperado: {v.Media}"));
    }

    [Fact]
    public void SystemVolumeMedia_IsKnownOrHonestlyUnknown()
    {
        var media = DiskMediaDetector.ResolveVolumeMedia("C:");
        Assert.True(Enum.IsDefined(media));
    }

    [Fact]
    public void StorageRowSnapshot_OldJsonWithoutMediaStillReads()
    {
        var row = JsonSerializer.Deserialize<StorageRowSnapshot>(
            """{"Name":"C:","UsedPct":60.5,"FreeGb":385.1}""");
        Assert.NotNull(row);
        Assert.Equal("C:", row.Name);
        Assert.Equal(string.Empty, row.Media);
    }

    [Fact]
    public void StorageVolumeReport_MediaDefaultsToEmpty()
    {
        var volume = new StorageVolumeReport("C:\\", "Fixed", "NTFS", 100, 50, true);
        Assert.Equal(string.Empty, volume.Media);
    }
}
