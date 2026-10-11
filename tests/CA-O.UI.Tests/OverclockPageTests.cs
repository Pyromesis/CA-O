using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CAO.Core.Overclock;
using CAO.Core.Overclock.OverclockCatalog;
using CAO.Infrastructure.Overclock;
using CAO.Shared;
using CAO.UI.Navigation;
using CAO.UI.ViewModels;
using Xunit;

namespace CAO.UI.Tests;

/// <summary>
/// Guardas de la pestaña Overclock (spec Overclock, fase F1). La pagina es de
/// SOLO LECTURA en esta fase, asi que lo que se protege aqui es exactamente lo
/// que el diseno prohibe que se pierda:
///
/// 1. RUTA: 'overclock' existe, resuelve a un tipo real y va despues de
///    'benchmark' en el orden de navegacion. Y 'benchmark' NO se toco.
/// 2. REGISTRO: el .xaml esta declarado en el csproj. El proyecto tiene
///    EnableDefaultPageItems=false, asi que un .xaml sin listar no se compila:
///    la pagina simplemente no existiria en el binario y el fallo seria
///    silencioso en tiempo de ejecucion.
/// 3. SECCIONES: las 6 secciones numeradas siguen en el XAML.
/// 4. HONESTIDAD: ningun control se presenta como accionable cuando el backend
///    no esta o cuando el driver Writing esta bloqueado. Un 'Detect' que no
///    pudo inspeccionar NO puede responder 'disponible' (regla dura del repo).
/// </summary>
public sealed class OverclockPageTests
{
    private const string Route = "overclock";

    // ---------------------------------------------------------------- rutas

    [Fact]
    public void Overclock_Route_ResolvesToPageType()
    {
        var type = RouteTable.Resolve(Route);
        Assert.True(type is not null, $"la ruta '{Route}' no resuelve en RouteTable.");
        Assert.Equal("CAO.UI.Pages.OverclockPage", type!.FullName);
    }

    [Fact]
    public void Overclock_Route_IsInNavigationOrder()
    {
        Assert.Contains(Route, RouteTable.Order);
    }

    [Fact]
    public void Overclock_Route_IsOrderedAfterBenchmark()
    {
        var order = RouteTable.Order.ToList();
        var benchmark = order.IndexOf("benchmark");
        var overclock = order.IndexOf(Route);

        Assert.True(benchmark >= 0, "'benchmark' desaparecio del orden de navegacion.");
        Assert.True(overclock > benchmark,
            $"'overclock' ({overclock}) debe ir DESPUES de 'benchmark' ({benchmark}).");
    }

    [Fact]
    public void Benchmark_Route_IsUntouchedByTheOverclockSection()
    {
        Assert.Equal("CAO.UI.Pages.BenchmarkPage", RouteTable.Resolve("benchmark")?.FullName);
    }

    [Fact]
    public void EveryRoute_ResolvesToAnExistingPageType()
    {
        foreach (var tag in RouteTable.Routes.Keys)
        {
            Assert.True(RouteTable.Resolve(tag) is not null,
                $"la ruta '{tag}' esta en el diccionario pero no resuelve.");
        }
    }

    // ------------------------------------------------------------- ficheros

    [Fact]
    public void OverclockPage_Xaml_IsRegisteredInTheProjectFile()
    {
        var csproj = File.ReadAllText(Path.Combine(UiDir(), "CA-O.UI.csproj"));
        Assert.Contains("<Page Include=\"OverclockPage.xaml\" />", csproj, StringComparison.Ordinal);

        // El benchmark sigue registrado: la seccion nueva NO lo sustituye.
        Assert.Contains("<Page Include=\"BenchmarkPage.xaml\" />", csproj, StringComparison.Ordinal);
    }

    [Fact]
    public void OverclockPage_Xaml_KeepsTheSixNumberedSections()
    {
        var xaml = File.ReadAllText(PageXamlPath());

        foreach (var section in new[]
                 {
                     "01  PROCESADOR",
                     "02  GRAFICOS",
                     "03  MEMORIA",
                     "04  PERFILES DE JUEGO",
                     "05  ESTABILIDAD",
                     "06  RESULTADO",
                 })
        {
            Assert.Contains(section, xaml, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void OverclockPage_Xaml_CodeBehind_IsCompiled()
    {
        Assert.True(File.Exists(PageCodeBehindPath()),
            $"falta el code-behind '{PageCodeBehindPath()}'.");
    }

    // ----------------------------------------------------------- honestidad

    [Fact]
    public async Task Overclock_NoControl_IsActionable_WhenNoBackendIsPresent()
    {
        var vm = OverclockTestHarness.Create(
            new EmptyRegistry(),
            Array.Empty<IOverclockBackend>());

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Empty(vm.CpuKnobs);
        Assert.Empty(vm.GpuKnobs);
        Assert.Empty(vm.MemoryKnobs);
        Assert.NotEmpty(vm.StatusLine);
    }

    [Fact]
    public async Task Overclock_ReadOnlyPhase_NeverOffersAnActionableControl()
    {
        // Backends presentes pero en modo solo lectura: es el estado REAL de la
        // fase F1, y es el que la UI debe pintar como bloqueado, no como
        // disponible. Si alguien abre el write gate antes de tiempo, este
        // test falla y lo dice.
var gpu = new FakeBackend(OverclockCatalog.NvapiBackendId, OverclockComponent.Gpu, writeAllowed: false);
        var memory = new FakeBackend(OverclockCatalog.SmbiosMemoryBackendId, OverclockComponent.Memory, writeAllowed: false);
        var registry = new OverclockReadOnlyRegistry(gpu, memory);

        var vm = OverclockTestHarness.Create(
            registry,
            new IOverclockBackend[] { gpu, memory });

        await vm.RefreshCommand.ExecuteAsync(null);

        var rows = vm.CpuKnobs.Concat(vm.GpuKnobs).Concat(vm.MemoryKnobs).ToList();
        Assert.NotEmpty(rows);

        Assert.DoesNotContain(rows, r => r.IsActionable);
        Assert.DoesNotContain(rows, r => r.State == OverclockControlState.Available);
    }

    [Fact]
    public async Task Overclock_MissingBackend_ReportsDependencyMissingWithAReason()
    {
        var gpu = new FakeBackend(OverclockCatalog.NvapiBackendId, OverclockComponent.Gpu, writeAllowed: false);

        // Registry REAL con un backend inyectado: la memoria no aporta ninguno,
        // y aun asi la UI debe mostrar sus controles con el motivo en vez de
        // dejar la seccion vacia en silencio.
        var registry = new OverclockReadOnlyRegistry(gpu);

        var vm = OverclockTestHarness.Create(
            registry,
            new IOverclockBackend[] { gpu });

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.NotEmpty(vm.MemoryKnobs);
        Assert.All(vm.MemoryKnobs, r =>
        {
            Assert.Equal(OverclockControlState.DependencyMissing, r.State);
            Assert.False(r.IsActionable);
            Assert.False(string.IsNullOrWhiteSpace(r.Reason));
        });
    }

    // ------------------------------------------------------------- helpers

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CA-O.sln")))
        {
            dir = dir.Parent;
        }

        Assert.True(dir is not null,
            $"No se encontro 'CA-O.sln' subiendo desde '{AppContext.BaseDirectory}'. " +
            "Una guarda que pasa por vacio es exactamente el fallo que prohibe este test.");
        return dir!.FullName;
    }

    private static string UiDir() => Path.Combine(RepoRoot(), "src", "CA-O.UI");

    private static string PageXamlPath() => Path.Combine(UiDir(), "OverclockPage.xaml");

    private static string PageCodeBehindPath() => Path.Combine(UiDir(), "OverclockPage.xaml.cs");

    // --------------------------------------------------------------- fakes

    private sealed class EmptyRegistry : IOverclockBackendRegistry
    {
        public OverclockCapabilities ProbeAll(CancellationToken ct) => OverclockCapabilities.Empty;
    }

    private sealed class FakeBackend : IOverclockBackend, IOverclockWriteGate
    {
        private readonly string _id;
        private readonly OverclockComponent _component;
        private readonly bool _writeAllowed;

        public FakeBackend(string id, OverclockComponent component, bool writeAllowed)
        {
            _id = id;
            _component = component;
            _writeAllowed = writeAllowed;
        }

        public string Id => _id;

        public OverclockCapabilities Probe(CancellationToken ct) => new(
            CpuBackendKind.None,
            _component == OverclockComponent.Gpu ? GpuBackendKind.NvidiaNvapi : GpuBackendKind.None,
            _component == OverclockComponent.Memory ? MemoryBackendKind.SmbiosReportOnly : MemoryBackendKind.None,
            new[] { new BackendAvailability(_id, true, string.Empty, null, false, "fake", EvidenceLevel.Vendor) },
            CatalogKnobsFor(_component, _id));

        public bool CanWrite(KnobId knob) => _writeAllowed;

        public string? BlockReasonKey(KnobId knob) => _writeAllowed ? null : "overclock-fake-write-blocked";

        public ValueTask<KnobRead> ReadAsync(KnobId id, CancellationToken ct) =>
            new(KnobRead.Fail("overclock-fake-read-disabled"));

        public ValueTask<KnobWriteResult> WriteAsync(KnobId id, KnobValue value, CancellationToken ct) =>
            new(_writeAllowed ? KnobWriteResult.Ok(value) : KnobWriteResult.Blocked(BlockReasonKey(id) ?? "overclock-fake-write-blocked"));

        public ValueTask<UnitSnapshot> SnapshotAsync(CancellationToken ct) =>
            new(new UnitSnapshot(_component, Array.Empty<KeyValuePair<KnobId, KnobValue>>(), DateTimeOffset.UnixEpoch, "fake"));

        public ValueTask<RestoreOutcome> RestoreAsync(UnitSnapshot snapshot, CancellationToken ct) =>
            new(RestoreOutcome.Ok(snapshot.Count, verified: true));

        private static IReadOnlyList<OverclockKnob> CatalogKnobsFor(OverclockComponent component, string backendId)
        {
            var source = component switch
            {
                OverclockComponent.Gpu => OverclockCatalog.NvidiaGpuKnobs,
                OverclockComponent.Memory => OverclockCatalog.MemoryKnobs,
                _ => OverclockCatalog.CpuPolicyKnobs,
            };

            return source.Where(k => k.BackendId == backendId).ToList();
        }
    }
}