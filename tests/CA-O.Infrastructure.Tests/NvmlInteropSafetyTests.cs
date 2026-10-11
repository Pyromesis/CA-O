using System.Runtime.InteropServices;
using CAO.Core.Overclock;
using CAO.Core.Overclock.OverclockCatalog;
using CAO.Infrastructure.Overclock;
using CAO.Shared;
using Xunit;

namespace CAO.Infrastructure.Tests.Overclock;

/// <summary>
/// Guards the native boundary that crashed the installed 2.2.5 with 0xc0000005
/// mid-probe: the old hand-written layer is gone, every NVIDIA call goes
/// through NVML with documented signatures, and the device pointer stays a
/// full-width pointer from the driver to every call.
/// </summary>
public sealed class NvmlInteropSafetyTests
{
    [Fact]
    public void NvmlClockOffset_MatchesTheDriverStruct()
    {
        Assert.Equal(24, Marshal.SizeOf<NvmlInterop.NvmlClockOffset>());
        Assert.Equal(0, (int)Marshal.OffsetOf<NvmlInterop.NvmlClockOffset>(nameof(NvmlInterop.NvmlClockOffset.Version)));
        Assert.Equal(4, (int)Marshal.OffsetOf<NvmlInterop.NvmlClockOffset>(nameof(NvmlInterop.NvmlClockOffset.Type)));
        Assert.Equal(8, (int)Marshal.OffsetOf<NvmlInterop.NvmlClockOffset>(nameof(NvmlInterop.NvmlClockOffset.Pstate)));
        Assert.Equal(12, (int)Marshal.OffsetOf<NvmlInterop.NvmlClockOffset>(nameof(NvmlInterop.NvmlClockOffset.ClockOffsetMHz)));
        Assert.Equal(16, (int)Marshal.OffsetOf<NvmlInterop.NvmlClockOffset>(nameof(NvmlInterop.NvmlClockOffset.MinClockOffsetMHz)));
        Assert.Equal(20, (int)Marshal.OffsetOf<NvmlInterop.NvmlClockOffset>(nameof(NvmlInterop.NvmlClockOffset.MaxClockOffsetMHz)));
    }

    [Fact]
    public void AdlxIntRange_MatchesTheDriverStruct()
    {
        Assert.Equal(12, Marshal.SizeOf<AmdAdlxInterop.AdlxIntRange>());
    }

    [Fact]
    public void NoRetiredTokens_RemainInOverclockSources()
    {
        string dir = Path.Combine(RepoRoot(), "src", "CA-O.Infrastructure", "Overclock");
        var files = Directory.EnumerateFiles(
            dir,
            "*.cs",
            new EnumerationOptions
            {
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint,
                RecurseSubdirectories = true,
            }).ToList();

        Assert.True(files.Count >= 12, $"Expected at least 12 overclock sources, found {files.Count}.");

        var offenders = new List<string>();
        foreach (string file in files)
        {
            string text = File.ReadAllText(file);
            if (text.Contains("nvapi64", StringComparison.OrdinalIgnoreCase)
                || text.Contains("NvInterop", StringComparison.Ordinal)
                || text.Contains("NvAPI_", StringComparison.Ordinal))
            {
                offenders.Add(Path.GetFileName(file));
            }
        }

        Assert.Empty(offenders);
    }

    [Fact]
    public async Task RealNvidiaHardware_ProbeAndDetailsCompleteWithoutFault()
    {
        var backend = new NvidiaGpuBackend();
        OverclockCapabilities probe = backend.Probe(CancellationToken.None);
        BackendAvailability? self = probe.Details.FirstOrDefault(d => d.BackendId == OverclockCatalog.NvapiBackendId);
        Assert.NotNull(self);

        if (!self!.Available && self.UnavailableReasonKey == "overclock-nvml-missing")
        {
            // No NVIDIA driver on this machine: nothing to exercise. The
            // struct-size and no-tokens tests above still guard the boundary
            // everywhere.
            return;
        }

        Assert.True(self.Available, self.UnavailableReasonKey);
        Assert.Equal(GpuBackendKind.NvidiaNvapi, probe.Gpu);

        IReadOnlyList<OverclockReading> details = await backend.ReadDetailsAsync(CancellationToken.None);
        Assert.NotEmpty(details);

        KnobRead clock = await backend.ReadAsync(NvKnobs.ClockReadout, CancellationToken.None);
        Assert.True(clock.Success, clock.FailureReasonKey ?? "clock readout failed");
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CA-O.sln")))
        {
            dir = dir.Parent;
        }

        return dir is null
            ? throw new InvalidOperationException("CA-O.sln not found above the test binaries.")
            : dir.FullName;
    }
}
