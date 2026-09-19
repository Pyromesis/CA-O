using System;
using System.Collections.Generic;
using System.Linq;
using CAO.Core.Abstractions;

namespace CAO.Core.Catalog;

/// <summary>Proyecciones honestas sobre OptimizationCatalog.All (spec §5.3).
/// Proyección, no borrado: All sigue intacto y todo ID sigue resolviendo.</summary>
public static class CatalogProjections
{
    public static readonly IReadOnlySet<string> RepairIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "restart-windows-audio-services", "restart-dns-client", "restart-bluetooth-service",
        "restart-print-spooler", "restart-windows-search", "repair-windows-update",
        "fix-microphone-access", "restart-desktop-compositor", "recover-windows-explorer",
        "disable-bluetooth-absolute-volume", "restart-windows-explorer", "resync-system-clock",
        "flush-dns-cache", "reset-network-stack-repair",
    };

    public static readonly IReadOnlySet<string> DiagnosticIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "gaming-display-refresh-rate-audit", "free-low-storage-space",
        "pending-reboot-maintenance", "optimize-startup-recovery-state",
    };

    public static readonly IReadOnlySet<string> RestoreIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "restore-tcp-checksum-offload", "restore-udp-checksum-offload",
        "restore-large-send-offload", "restore-windows-tcp-congestion-default",
        "restore-sysmain-default", "restore-system-managed-pagefile",
    };

    public static IReadOnlyList<IOptimization> RepairActions =>
        OptimizationCatalog.All.Where(o => RepairIds.Contains(o.Definition.Id)).ToList();

    public static IReadOnlyList<IOptimization> Diagnostics =>
        OptimizationCatalog.All.Where(o => DiagnosticIds.Contains(o.Definition.Id)).ToList();

    public static IReadOnlyList<IOptimization> Restores =>
        OptimizationCatalog.All.Where(o => RestoreIds.Contains(o.Definition.Id)).ToList();

    /// <summary>Batch default: perf real. Excluye repairs/diagnostics/restores.</summary>
    public static IReadOnlyList<IOptimization> BatchDefault =>
        OptimizationCatalog.All.Where(o =>
            !RepairIds.Contains(o.Definition.Id) &&
            !DiagnosticIds.Contains(o.Definition.Id) &&
            !RestoreIds.Contains(o.Definition.Id)).ToList();
}
