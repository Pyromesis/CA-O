using CAO.Core.Abstractions;
using CAO.Infrastructure.Logging;
using CAO.Infrastructure.Persistence;
using CAO.Infrastructure.SystemInterop;
using CAO.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace CAO.UI;

/// <summary>
/// Composition root con Microsoft.Extensions.DependencyInjection (FASE 4).
/// Reemplaza progresivamente el Service Locator estático AppServices: los ViewModels
/// reciben sus dependencias por constructor injection y AppServices delega al provider
/// para compatibilidad temporal (migración por fases, sin romper páginas existentes).
/// </summary>
internal static class AppHost
{
    private static ServiceProvider? _provider;

    public static ServiceProvider Provider =>
        _provider ?? throw new InvalidOperationException("AppHost no inicializado. Llama a Initialize() en App.OnLaunched.");

    public static void Initialize()
    {
        if (_provider is not null)
        {
            return;
        }

        var services = new ServiceCollection();

        // State singleton (compartido entre páginas)
        services.AddSingleton<UiState>();

        // Infrastructure
        services.AddSingleton<ISystemInfoProvider, WmiSystemInfoProvider>();
        services.AddSingleton<SystemContextProvider>();
        services.AddSingleton<SystemContextCache>();
        services.AddSingleton<JsonHistoryLogger>();
        services.AddSingleton<FileSnapshotStore>();
        services.AddSingleton<FileTransactionJournal>();
        services.AddSingleton<Infrastructure.Windows.SystemRegistry.RegistryAccessor>();
        services.AddSingleton<Infrastructure.Logging.StructuredLogger>();

        // Privileged IPC
        services.AddSingleton<PrivilegedPipeClient>();

        // Recovery
        services.AddSingleton<Core.Rollback.CrashRecoveryService>(sp =>
            new Core.Rollback.CrashRecoveryService(
                sp.GetRequiredService<FileTransactionJournal>(),
                sp.GetRequiredService<FileSnapshotStore>(),
                id =>
                {
                    var catalog = Core.Catalog.OptimizationCatalog.All;
                    var registry = sp.GetRequiredService<Infrastructure.Windows.SystemRegistry.RegistryAccessor>();
                    var match = catalog.FirstOrDefault(o => o.Definition.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
                    return match is null ? CAO.Shared.OptimizationState.Unknown : match.Detect(registry);
                }));

        // ViewModels (transient – cada página resuelve el suyo) (§5 MVVM real)
        services.AddTransient<AnalyzeViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<OptimizeViewModel>();
        services.AddTransient<GamingViewModel>();
        services.AddTransient<BenchmarkViewModel>();
        services.AddTransient<HistoryViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<RestoreViewModel>();
        services.AddTransient<DiagnosticsViewModel>();

        _provider = services.BuildServiceProvider();
    }

    public static T Resolve<T>() where T : notnull => Provider.GetRequiredService<T>();
}
