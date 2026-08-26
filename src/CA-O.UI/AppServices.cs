using CAO.Shared;
using CAO.UI.ViewModels;
using Microsoft.UI.Xaml;

namespace CAO.UI;

/// <summary>Composition root for UI-side services (no service locator magic).</summary>
internal static class AppServices
{
    public static UiState State { get; } = new();

    public static Infrastructure.SystemInterop.SystemContextProvider ContextProvider { get; } = new();

    public static Infrastructure.SystemInterop.WmiSystemInfoProvider SystemInfo { get; } = new();

    public static Infrastructure.Logging.JsonHistoryLogger History { get; } = new();

    public static Infrastructure.Persistence.FileSnapshotStore Snapshots { get; } = new();

    public static Infrastructure.Persistence.FileTransactionJournal Journal { get; } = new();

    /// <summary>Crash-recovery scanner driven by the transaction journal (spec 12).</summary>
    public static Core.Rollback.CrashRecoveryService Recovery { get; } = new(
        Journal,
        Snapshots,
        id => DetectSafe(id));

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

    public static PrivilegedPipeClient Pipe { get; } = new();

    public static Infrastructure.Windows.SystemRegistry.RegistryAccessor Registry { get; } = new();

    /// <summary>Catalog instances used read-only by the UI for detection.</summary>
    public static IReadOnlyList<Core.Abstractions.IOptimization> Catalog { get; } =
        Core.Catalog.OptimizationCatalog.All;
}
