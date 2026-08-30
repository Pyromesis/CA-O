using CAO.Core.Abstractions;
using CAO.Core.Diagnostics;
using CAO.Infrastructure.Persistence;
using CAO.Shared;

namespace CAO.Infrastructure.Services;

/// <summary>
/// Orquestador único de análisis (§5-6): único responsable de obtener contexto, ejecutar módulos con
/// estado propio, registrar duración/errores, persistir resultado, calcular health y recommendations.
/// Elimina duplicación entre DashboardViewModel/AnalyzeViewModel/AnalyzePage.
/// </summary>
public sealed class SystemAnalysisService : IAnalysisCoordinator
{
    private readonly ISystemContextProvider _contextProvider;
    private readonly AnalysisStateStore _store;
    private readonly IRegistryAccessor _registry;

    public SystemAnalysisService(ISystemContextProvider contextProvider, AnalysisStateStore store, IRegistryAccessor registry)
    {
        _contextProvider = contextProvider;
        _store = store;
        _registry = registry;
    }

    public async Task<AnalysisReport> RunAsync(CancellationToken ct = default)
    {
        var correlationId = Correlation.New();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var modules = new List<ModuleResult>();
        var warnings = new List<string>();
        SystemContext? context = null;
        SystemDiagnosticReport? health = null;
        IReadOnlyList<Recommendation> recommendations = Array.Empty<Recommendation>();

        var m1 = await RunModuleAsync("System", async () => { context = await _contextProvider.GetAsync(ct); return $"{context.CpuName} / {context.RamGb}GB"; }, ct);
        modules.Add(m1);
        if (!m1.Success) warnings.Add($"System: {m1.Warning}");

        if (!ct.IsCancellationRequested && m1.Success)
        {
            var tasks = new List<Task<ModuleResult>>
            {
                RunModuleAsync("Network", async () => { var p = new Networking.NetworkDiagnosticsProvider(); var r = await p.MeasureAsync(ct); return $"{r.Interfaces.Count} interfaces"; }, ct),
                RunModuleAsync("Storage", () => { var p = new Storage.StorageDiagnosticsProvider(); var r = p.Measure(); return Task.FromResult($"{r.Volumes.Count} volúmenes"); }, ct),
                RunModuleAsync("Security", () => { var p = new Security.SecurityDiagnosticsProvider(); var r = p.Measure(); return Task.FromResult($"{r.Features.Count} features"); }, ct),
                RunModuleAsync("Drivers", async () => { var p = new SystemInterop.DriverDiagnosticsProvider(); var r = await p.MeasureAsync(ct); return $"{r.Drivers.Count} drivers"; }, ct),
            };
            var results = await Task.WhenAll(tasks);
            modules.AddRange(results);
            foreach (var r in results.Where(x => !x.Success)) warnings.Add($"{r.Module}: {r.Warning}");
        }

        string analysisState;
        if (m1.Success)
        {
            try
            {
                health = HealthEngine.Evaluate(context);
                var catalog = Core.Catalog.OptimizationCatalog.All;
                recommendations = Core.Engine.RecommendationEngine.BuildAll(catalog, _registry, context!);
                analysisState = warnings.Count == 0 ? "Completed" : "CompletedWithWarnings";
            }
            catch (Exception ex) { warnings.Add($"Health/Recommendations: {ex.Message}"); analysisState = "CompletedWithWarnings"; }
        }
        else analysisState = "Failed";
        if (ct.IsCancellationRequested) analysisState = "Cancelled";

        sw.Stop();
        var result = new AnalysisReport(analysisState, modules, context, health, recommendations, sw.Elapsed, warnings, correlationId);

        try
        {
            var fp = context != null ? AnalysisStateStore.ComputeGamesFingerprint(context.GamesDetected) : null;
            _store.SaveAnalysis(new AnalysisStateStore.PersistedAnalysis(
                SchemaVersion: AnalysisStateStore.SchemaVersion,
                AppVersion: AppVersion.Semantic,
                WindowsBuild: context?.WindowsBuild ?? 0,
                TimestampUtc: DateTime.UtcNow,
                Context: context,
                Recommendations: recommendations,
                Health: health,
                AnalysisState: analysisState,
                Warnings: warnings,
                Duration: sw.Elapsed,
                ErrorCode: analysisState == "Failed" ? ErrorCodes.UiAnalyzeFailed : null,
                CorrelationId: correlationId,
                InstalledGamesFingerprint: fp
            ));
        }
        catch { }

        return result;
    }

    private static async Task<ModuleResult> RunModuleAsync(string module, Func<Task<string>> work, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try { ct.ThrowIfCancellationRequested(); var val = await work(); sw.Stop(); return new ModuleResult(module, true, sw.Elapsed, val, null, null); }
        catch (OperationCanceledException) { sw.Stop(); return new ModuleResult(module, false, sw.Elapsed, null, "Cancelado", "CAO-CANCELLED"); }
        catch (Exception ex) { sw.Stop(); return new ModuleResult(module, false, sw.Elapsed, null, ex.Message, ErrorCodes.UiAnalyzeFailed); }
    }
}
