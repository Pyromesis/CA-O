using CAO.Core.Abstractions;
using CAO.Core.Rollback;
using CAO.Shared;
using Xunit;

namespace CAO.Core.Tests;

/// <summary>
/// El motor calcula el estado real de una optimizacion ANTES de construir la
/// transaccion (comprobacion de idempotencia) y la transaccion lo volvia a
/// calcular por su cuenta. Para las limpiezas de ficheros eso significa un
/// segundo recorrido completo del arbol de directorios en cada apply: con mas de
/// 15.000 ficheros en el TEMP del usuario la deteccion tarda segundos.
/// La transaccion debe reutilizar el estado que ya le entregan.
/// </summary>
public sealed class TransactionPreDetectedStateTests
{
    private static readonly SystemContext Context = SystemContextFactory.Default();

    private static OptimizationDefinition Definition(string id) => new()
    {
        Id = id, NameEs = "Prueba de pre-deteccion", NameEn = "Pre-detect test",
        DescriptionEs = "Comprobacion de reutilizacion del estado detectado", DescriptionEn = "Pre-detected state reuse",
        Evidence = EvidenceLevel.Benchmark, Risk = RiskLevel.Safe,
        Compatibility = CompatibilityStatus.Compatible, SecurityImpact = SecurityImpact.None,
        Reversible = true, Flags = OptimizationFlags.None,
    };

    private static MemoryRegistry RegistrySayingApplied()
    {
        var registry = new MemoryRegistry();
        registry.SetValue(RegistryHive2.CurrentUser, StubOptimization.TestKey,
            StubOptimization.TestValue, 1, RegistryValueKind2.DWord);
        return registry;
    }

    [Fact]
    public async Task SuppliedPreDetectedState_IsUsed_InsteadOfDetectingAgain()
    {
        // El registro dice "ya aplicado", pero el estado que recibe la transaccion
        // dice que no. Debe ganar el estado recibido: si la transaccion se
        // detectara por su cuenta, veria "aplicado" y devolveria exito sin
        // mutar nada (ApplyCalls == 0).
        var stub = new StubOptimization(Definition("t2-precheck-opt"));

        var report = await new OptimizationTransaction(
            stub, RegistrySayingApplied(), Context,
            preDetectedState: OptimizationState.NotApplied).RunAsync();

        Assert.True(report.Success, report.MessageEs);
        Assert.Equal(1, stub.ApplyCalls);
    }

    [Fact]
    public async Task AnUnknownPreDetectedState_IsNotTrusted_TheTransactionDetectsItself()
    {
        // Contracaso: "desconocido" no es una respuesta en la que se pueda
        // decidir. La transaccion debe inspeccionar ella misma.
        var stub = new StubOptimization(Definition("t2-unknown-opt"));

        var report = await new OptimizationTransaction(
            stub, RegistrySayingApplied(), Context,
            preDetectedState: OptimizationState.Unknown).RunAsync();

        Assert.True(report.Success, report.MessageEs);
        Assert.Equal(0, stub.ApplyCalls);
    }
}