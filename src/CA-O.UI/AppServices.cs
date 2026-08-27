using CAO.Shared;
using CAO.UI.ViewModels;
using Microsoft.UI.Xaml;

namespace CAO.UI;

/// <summary>
/// Compatibility facade over <see cref="AppHost"/> DI container (FASE 4).
/// Nueva código debe resolver vía <c>AppHost.Resolve&lt;T&gt;()</c> / constructor injection;
/// este tipo se mantiene para no romper páginas existentes durante la migración por fases.
/// </summary>
internal static class AppServices
{
    public static UiState State => TryResolve<UiState>() ?? new UiState();

    public static Infrastructure.SystemInterop.SystemContextProvider ContextProvider =>
        TryResolve<Infrastructure.SystemInterop.SystemContextProvider>() ?? new();

    public static Infrastructure.SystemInterop.WmiSystemInfoProvider SystemInfo =>
        TryResolve<Infrastructure.SystemInterop.WmiSystemInfoProvider>() ?? new();

    public static Infrastructure.Logging.JsonHistoryLogger History =>
        TryResolve<Infrastructure.Logging.JsonHistoryLogger>() ?? new();

    public static Infrastructure.Persistence.FileSnapshotStore Snapshots =>
        TryResolve<Infrastructure.Persistence.FileSnapshotStore>() ?? new();

    public static Infrastructure.Persistence.FileTransactionJournal Journal =>
        TryResolve<Infrastructure.Persistence.FileTransactionJournal>() ?? new();

    /// <summary>Crash-recovery scanner driven by the transaction journal (spec 12).</summary>
    public static Core.Rollback.CrashRecoveryService Recovery =>
        TryResolve<Core.Rollback.CrashRecoveryService>() ?? new(Journal, Snapshots, id => DetectSafe(id));

    private static OptimizationState DetectSafe(string optimizationId)
    {
        try
        {
            var match = Catalog.FirstOrDefault(o =>
                o.Definition.Id.Equals(optimizationId, StringComparison.OrdinalIgnoreCase));
            return match is null ? OptimizationState.Unknown : match.Detect(Registry);
        }
        catch
        {
            return OptimizationState.Unknown;
        }
    }

    public static PrivilegedPipeClient Pipe =>
        TryResolve<PrivilegedPipeClient>() ?? new();

    public static Infrastructure.Windows.SystemRegistry.RegistryAccessor Registry =>
        TryResolve<Infrastructure.Windows.SystemRegistry.RegistryAccessor>() ?? new();

    /// <summary>Catalog instances used read-only by the UI for detection.</summary>
    public static IReadOnlyList<Core.Abstractions.IOptimization> Catalog { get; } =
        Core.Catalog.OptimizationCatalog.All;

    private static T? TryResolve<T>() where T : class
    {
        try { return AppHost.Provider.GetService(typeof(T)) as T; }
        catch { return null; }
    }
}
