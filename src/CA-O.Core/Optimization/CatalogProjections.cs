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
        "restart-print-spooler", "cleanup-print-spooler-jobs", "restart-windows-search", "repair-windows-update",
        "fix-microphone-access", "restart-desktop-compositor", "recover-windows-explorer",
        "disable-bluetooth-absolute-volume", "restart-windows-explorer", "resync-system-clock",
        "flush-dns-cache", "reset-network-stack-repair",
    };

    public static readonly IReadOnlySet<string> DiagnosticIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "analyze-component-store", "free-low-storage-space",
        "pending-reboot-maintenance", "optimize-startup-recovery-state",
    };

    // Los restores se excluyen del lote: aplicarlos dentro del batch
    // anularía el propio lote (p. ej. restaurar el plan equilibrado o la
    // GPU por defecto justo después de optimizar). Auditoría 2026-09-29
    // (C4): restore-balanced-power-dc, restore-windows-search-default y
    // restore-default-gpu-preference estaban en BatchDefault y se excluyen.
    public static readonly IReadOnlySet<string> RestoreIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "restore-tcp-checksum-offload", "restore-udp-checksum-offload",
        "restore-large-send-offload", "restore-windows-tcp-congestion-default",
        "restore-sysmain-default", "restore-system-managed-pagefile",
        "restore-balanced-power-dc", "restore-windows-search-default",
        "restore-default-gpu-preference",
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
