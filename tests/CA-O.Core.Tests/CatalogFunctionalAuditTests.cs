using CAO.Core.Abstractions;
using CAO.Core.Catalog;
using CAO.Core.Interfaces;
using CAO.Core.Optimizations;
using CAO.Core.Optimizations.Storage;
using CAO.Shared;
using CAO.Shared.Security;
using Xunit;

namespace CAO.Core.Tests;

/// <summary>
/// Auditoría funcional del catálogo completo (2.2.20): cada optimización se
/// ejecuta de verdad contra un registro en memoria, un ejecutor que valida cada
/// comando con la lista blanca REAL (CommandPolicy) y un gestor de servicios
/// falso. Un comando que la política rechaza fallaría en el equipo del usuario
/// con CAO-SEC; aquí falla en la suite. Las que borran ficheros o matan
/// procesos reales no se aplican (se auditan solo Detect/Capture/Preview).
/// </summary>
public sealed class CatalogFunctionalAuditTests
{
    /// <summary>Efectos fuera de registro/ejecutor/servicios: no se aplican en la máquina del test.</summary>
    private static readonly HashSet<string> RealSideEffects = new(StringComparer.Ordinal)
    {
        "cleanup-memory-ram", "cleanup-app-caches", "cleanup-gpu-shader-cache",
        "cleanup-nvidia-downloader-cache", "cleanup-windows-update-cache", "cleanup-print-spooler-jobs",
        "clear-icon-thumbnail-cache", "rebuild-font-cache", "repair-windows-update",
        "disable-onedrive-autostart", "recover-windows-explorer", "restart-windows-explorer",
    };

    public static TheoryData<string> AllIds() =>
        new(OptimizationCatalog.All.Select(o => o.Definition.Id));

    private static IOptimization Resolve(string id) => OptimizationCatalog.All.First(o => o.Definition.Id == id);

    private static bool Applicable(IOptimization optimization) =>
        optimization is not TempFileCleanupOptimization && !RealSideEffects.Contains(optimization.Definition.Id);

    [Theory]
    [MemberData(nameof(AllIds))]
    public async Task DetectCaptureAndPreviewNeverThrow(string id)
    {
        var optimization = Resolve(id);
        var registry = new MemoryRegistry();
        var detect = Record.Exception(() => optimization.Detect(registry));
        Assert.Null(detect);
        Assert.Null(Record.Exception(() => optimization.Capture(registry)));
        var preview = await optimization.PreviewAsync(registry);
        Assert.Equal(id, preview.OptimizationId);
    }

    [Theory]
    [MemberData(nameof(AllIds))]
    public async Task EveryCommandAnOptimizationRunsIsAllowedByThePolicy(string id)
    {
        var optimization = Resolve(id);
        if (!Applicable(optimization)) return;

        var executor = new PolicyCheckingExecutor();
        var context = new OptimizationContext
        {
            Registry = new MemoryRegistry(),
            Executor = executor,
            Services = new RecordingServices(),
        };

        var apply = await Record.ExceptionAsync(() => optimization.ApplyAsync(context));
        Assert.Null(apply);
        Assert.Empty(executor.Rejected);

        // El revert también tiene que hablar el idioma de la política.
        var snapshot = optimization.Capture(context.Registry);
        var revert = await Record.ExceptionAsync(() => optimization.RevertAsync(context, snapshot));
        Assert.Null(revert);
        Assert.Empty(executor.Rejected);
    }

    [Theory]
    [MemberData(nameof(AllIds))]
    public async Task RegistryOptimizationsApplyVerifyAndRevertExactly(string id)
    {
        if (Resolve(id) is not RegistryOptimizationBase optimization) return;
        var registry = new MemoryRegistry();
        var context = new OptimizationContext { Registry = registry };

        var before = optimization.Detect(registry);
        var snapshot = optimization.Capture(registry);
        Assert.True((await optimization.ApplyAsync(context)).Success, $"{id}: apply falló");
        Assert.Equal(OptimizationState.AppliedByCao, optimization.Detect(registry));
        Assert.True((await ((IOptimization)optimization).VerifyAsync(context)).Verified, $"{id}: verify no pasó");
        Assert.True((await optimization.RevertAsync(context, snapshot)).Success, $"{id}: revert falló");
        Assert.Equal(before, optimization.Detect(registry));
        Assert.Empty(registry.Store);
    }

    [Fact]
    public void RepairActionsAreNeverInTheDefaultBatch()
    {
        foreach (var repair in CatalogProjections.RepairActions)
            Assert.DoesNotContain(CatalogProjections.BatchDefault, o => o.Definition.Id == repair.Definition.Id);
    }

    [Fact]
    public void HeavyFixesHaveTheLongTimeout()
    {
        foreach (var id in new[] { "run-system-file-checker", "repair-windows-image" })
            Assert.Contains(id, TimeoutProfile.HeavyOptimizationIds);
    }

    private sealed class PolicyCheckingExecutor : IPrivilegedCommandExecutor
    {
        public List<string> Rejected { get; } = [];

        public Task<PrivilegedCommandResult> ExecuteAsync(SystemCommandKey key, IReadOnlyList<string> arguments, CancellationToken ct = default)
        {
            if (CommandPolicy.Resolve(key, arguments) is null)
            {
                Rejected.Add($"{key} {string.Join(' ', arguments)}");
                return Task.FromResult(new PrivilegedCommandResult(-1, string.Empty, "rejected", false));
            }
            var stdout = key switch
            {
                SystemCommandKey.PowerCfgQueryAcValueIndex => "Current AC Power Setting Index: 0x00000001",
                SystemCommandKey.PowerCfgQueryActiveScheme => "Power Scheme GUID: 381b4222-f694-41f0-9685-ff5bb260df2e  (Balanced)",
                _ => string.Empty,
            };
            return Task.FromResult(new PrivilegedCommandResult(0, stdout, string.Empty, false));
        }
    }

    private sealed class RecordingServices : IServiceManager
    {
        public string? GetStartType(string serviceName) => "Manual";
        public void SetStartType(string serviceName, string startType) { }
        public Task StopAsync(string serviceName, CancellationToken ct = default) => Task.CompletedTask;
        public Task StartAsync(string serviceName, CancellationToken ct = default) => Task.CompletedTask;
        public bool Exists(string serviceName) => true;
    }
}
