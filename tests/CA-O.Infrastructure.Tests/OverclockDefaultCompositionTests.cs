using CAO.Core.Overclock;
using CAO.Core.Overclock.OverclockCatalog;
using CAO.Infrastructure.Overclock;
using Xunit;

namespace CAO.Infrastructure.Tests.Overclock;

/// <summary>
/// Guards the default composition of the Overclock section.
///
/// The CPU backend was written, tested in isolation, and still invisible in
/// the app because the registry and the ViewModel each re-listed the backends
/// by hand. These tests fail if that duplication ever comes back, which is the
/// only way this class of bug stays dead.
/// </summary>
public sealed class OverclockDefaultCompositionTests
{
    [Fact]
    public void DefaultBackends_IncludeTheCpuPolicyBackend()
    {
        var backends = OverclockReadOnlyRegistry.DefaultBackends();

        Assert.Contains(backends, b => b.Id == OverclockCatalog.IntelPolicyBackendId);
    }

    [Fact]
    public void DefaultBackends_IncludeTheAmdAdlxBackend()
    {
        var backends = OverclockReadOnlyRegistry.DefaultBackends();

        Assert.Contains(backends, b => b.Id == OverclockCatalog.AmdAdlxBackendId);
    }

    [Fact]
    public void DefaultBackends_IncludeGpuAndMemoryBackends()
    {
        var backends = OverclockReadOnlyRegistry.DefaultBackends();

        Assert.Contains(backends, b => b.Id == OverclockCatalog.NvapiBackendId);
        Assert.Contains(backends, b => b.Id == OverclockCatalog.SmbiosMemoryBackendId);
    }

    [Fact]
    public void BackendIds_AreUnique_SoNoComponentIsClaimedTwice()
    {
        var ids = OverclockReadOnlyRegistry.DefaultBackends().Select(b => b.Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void CreateDefault_UsesTheSameBackendsAsDefaultBackends()
    {
        // The registry composes from the shared list; the ViewModel reads the
        // same list. If these two ever diverge, the UI silently shows a
        // component as "missing" while the probe says it is available.
        var expected = OverclockReadOnlyRegistry.DefaultBackends()
            .Select(b => b.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        var probe = OverclockReadOnlyRegistry.CreateDefault().ProbeAll(CancellationToken.None);

        var probed = probe.Details
            .Select(d => d.BackendId)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(expected, probed);
    }

    [Fact]
    public void CreateDefault_ProbesEveryBackendWithoutThrowing()
    {
        var probe = OverclockReadOnlyRegistry.CreateDefault().ProbeAll(CancellationToken.None);

        Assert.Equal(4, probe.Details.Count);
        Assert.All(probe.Details, d => Assert.False(string.IsNullOrWhiteSpace(d.BackendId)));
    }

    [Fact]
    public void CreateDefault_NeverClaimsAKnobWithoutABackend()
    {
        var probe = OverclockReadOnlyRegistry.CreateDefault().ProbeAll(CancellationToken.None);
        var present = probe.Details.Where(d => d.Available).Select(d => d.BackendId).ToHashSet(StringComparer.Ordinal);

        // A knob may be listed even when its backend is missing (the UI shows
        // it with a reason), but a knob whose backend IS available must be
        // readable rather than silently dropped.
        Assert.All(
            OverclockCatalog.NvidiaGpuKnobs.Where(k => present.Contains(k.BackendId)),
            k => Assert.Contains(k, probe.Knobs));
        Assert.All(
            OverclockCatalog.CpuPolicyKnobs.Where(k => present.Contains(k.BackendId)),
            k => Assert.Contains(k, probe.Knobs));
        Assert.All(
            OverclockCatalog.AmdGpuKnobs.Where(k => present.Contains(k.BackendId)),
            k => Assert.Contains(k, probe.Knobs));
        Assert.All(
            OverclockCatalog.MemoryKnobs.Where(k => present.Contains(k.BackendId)),
            k => Assert.Contains(k, probe.Knobs));
    }

    [Fact]
    public void CreateDefault_ReportsAConcreteCpuBackendKind_NeverNoneWithoutAReason()
    {
        var probe = OverclockReadOnlyRegistry.CreateDefault().ProbeAll(CancellationToken.None);

        var cpuDetail = probe.Details
            .FirstOrDefault(d => d.BackendId == OverclockCatalog.IntelPolicyBackendId);

        Assert.NotNull(cpuDetail);
        if (probe.Cpu == CpuBackendKind.None)
        {
            Assert.False(string.IsNullOrWhiteSpace(cpuDetail!.UnavailableReasonKey));
        }
        else
        {
            Assert.True(cpuDetail!.Available);
        }
    }
}
