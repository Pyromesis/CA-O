using CAO.Core.Abstractions;
using CAO.Core.Optimizations.Storage;
using CAO.Shared;
using Xunit;

namespace CAO.Core.Tests;

/// <summary>
/// disable-hibernate debe leer el estado VIVO del registro
/// (HKLM\SYSTEM\CurrentControlSet\Control\Power\HibernateEnabled).
/// Antes usaba solo el valor inyectado `HibernateAvailable` (siempre `true`
/// en el servicio), la verificación fallaba con CAO-TXN-003 y la reversión
/// reactivaba la hibernación.
/// </summary>
public sealed class DisableHibernateLiveStateTests
{
    private const string KeyPath = @"SYSTEM\CurrentControlSet\Control\Power";
    private const string ValueName = "HibernateEnabled";

    private static MemoryRegistry RegistryWith(object? value)
    {
        var registry = new MemoryRegistry();
        if (value is not null)
            registry.SetValue(RegistryHive2.LocalMachine, KeyPath, ValueName, value, RegistryValueKind2.DWord);
        return registry;
    }

    private static OptimizationContext Context(MemoryRegistry registry) =>
        new() { Registry = registry };

    [Fact]
    public void Detect_OffInRegistry_IsApplied()
    {
        var optimization = new DisableHibernate();
        Assert.Equal(OptimizationState.AppliedByCao, optimization.Detect(RegistryWith(0)));
    }

    [Fact]
    public void Detect_OnInRegistry_IsNotApplied()
    {
        var optimization = new DisableHibernate();
        Assert.Equal(OptimizationState.NotApplied, optimization.Detect(RegistryWith(1)));
    }

    [Fact]
    public void Detect_AbsentValue_FallsBackToInjected()
    {
        Assert.Equal(OptimizationState.NotApplied,
            new DisableHibernate { HibernateAvailable = true }.Detect(RegistryWith(null)));
        Assert.Equal(OptimizationState.AppliedByCao,
            new DisableHibernate { HibernateAvailable = false }.Detect(RegistryWith(null)));
    }

    [Fact]
    public async Task Verify_OffInRegistry_Passes()
    {
        var registry = RegistryWith(0);
        var verification = await new DisableHibernate().VerifyAsync(Context(registry));
        Assert.Equal(VerificationStatus.Passed, verification.Status);
    }

    [Fact]
    public async Task Verify_OnInRegistry_Fails()
    {
        var registry = RegistryWith(1);
        var verification = await new DisableHibernate().VerifyAsync(Context(registry));
        Assert.Equal(VerificationStatus.Failed, verification.Status);
    }
}
