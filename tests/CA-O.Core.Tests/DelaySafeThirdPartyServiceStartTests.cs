using CAO.Core.Abstractions;
using CAO.Core.Optimizations.Startup;
using CAO.Shared;
using CAO.Shared.Constants;
using Xunit;

namespace CAO.Core.Tests;

/// <summary>
/// El servicio privilegiado de CA-O es el canal SYSTEM que ejecuta todas las
/// mutaciones: ninguna optimización genérica puede retrasarlo. Estos tests
/// fijan que nunca aparece como candidato, ni por nombre ni por procedencia
/// del binario, y que un tercero normal sigue siendo candidato.
/// </summary>
public sealed class DelaySafeThirdPartyServiceStartTests
{
    private const string ServicesKey = @"SYSTEM\CurrentControlSet\Services";
    private const string OwnExe = @"C:\Program Files\CA-O\service\CA-O.Privileged.exe";
    private const string ThirdPartyExe = @"C:\Program Files\Contoso\agent.exe";

    private static MemoryRegistry RegistryWith(params (string Name, string ImagePath)[] services)
    {
        var registry = new MemoryRegistry();
        foreach (var (name, imagePath) in services)
        {
            registry.SetValue(RegistryHive2.LocalMachine, $@"{ServicesKey}\{name}", "ImagePath", imagePath, RegistryValueKind2.String);
            registry.SetValue(RegistryHive2.LocalMachine, $@"{ServicesKey}\{name}", "Start", 2, RegistryValueKind2.DWord);
        }
        return registry;
    }

    private static DelaySafeThirdPartyServiceStart Sut(
        params DelaySafeThirdPartyServiceStart.ServiceProbe[] probes) =>
        new(() => probes);

    private static DelaySafeThirdPartyServiceStart.ServiceProbe Probe(
        string name, string displayName = "", int dependents = 0) =>
        new(name, displayName, System.ServiceProcess.ServiceStartMode.Automatic, dependents);

    [Fact]
    public async Task OwnServiceByNameIsNeverTouched()
    {
        var registry = RegistryWith(
            (BuildConstants.ServiceName, OwnExe),
            ("ContosoAgent", ThirdPartyExe));
        var sut = Sut(
            Probe(BuildConstants.ServiceName, BuildConstants.ServiceDisplayName),
            Probe("ContosoAgent", "Contoso Agent"));

        var preview = await sut.PreviewAsync(registry);
        Assert.DoesNotContain(preview.Lines, l =>
            l.Target.Contains(BuildConstants.ServiceName, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(preview.Lines, l => l.Target.Contains("ContosoAgent", StringComparison.OrdinalIgnoreCase));

        var snapshot = sut.Capture(registry);
        Assert.DoesNotContain(snapshot.Registry, e =>
            e.KeyPath.Contains(BuildConstants.ServiceName, StringComparison.OrdinalIgnoreCase));

        await sut.ApplyAsync(new OptimizationContext { Registry = registry });
        Assert.Null(registry.GetValue(
            RegistryHive2.LocalMachine, $@"{ServicesKey}\{BuildConstants.ServiceName}", "DelayedAutostart"));
        Assert.Equal(1, registry.GetValue(
            RegistryHive2.LocalMachine, $@"{ServicesKey}\ContosoAgent", "DelayedAutostart"));
    }

    [Fact]
    public void RenamedServiceWhoseBinaryLivesUnderCaoIsExcluded()
    {
        var registry = RegistryWith(("TotallyNotCao", OwnExe));
        var sut = Sut(Probe("TotallyNotCao", "Totally Not CA-O"));

        Assert.Empty(sut.Capture(registry).Registry);
        Assert.Equal(OptimizationState.Unknown, sut.Detect(registry));
    }

    [Fact]
    public void AnyExecutableUnderCaoInstallRootIsExcluded()
    {
        var custom = @"C:\Program Files\CA-O\service\custom-host.exe";
        var registry = RegistryWith(("CustomHost", custom));
        var sut = Sut(Probe("CustomHost", "Custom Host"));

        Assert.Empty(sut.Capture(registry).Registry);
        Assert.Equal(OptimizationState.Unknown, sut.Detect(registry));
    }

    [Fact]
    public async Task OwnServiceIsNotAppliedEvenWhenItLooksLikeAPendingCandidate()
    {
        var registry = RegistryWith((BuildConstants.ServiceName, OwnExe));
        var sut = Sut(Probe(BuildConstants.ServiceName, BuildConstants.ServiceDisplayName));

        var result = await sut.ApplyAsync(new OptimizationContext { Registry = registry });

        Assert.True(result.Success);
        Assert.Contains("No quedaban", result.MessageEs, StringComparison.OrdinalIgnoreCase);
        Assert.Null(registry.GetValue(
            RegistryHive2.LocalMachine, $@"{ServicesKey}\{BuildConstants.ServiceName}", "DelayedAutostart"));
    }

    [Fact]
    public void ThirdPartyServiceWithDependencyIsStillExcluded()
    {
        var registry = RegistryWith(("ContosoAgent", ThirdPartyExe));
        var sut = Sut(Probe("ContosoAgent", "Contoso Agent", dependents: 1));

        Assert.Empty(sut.Capture(registry).Registry);
    }
}
