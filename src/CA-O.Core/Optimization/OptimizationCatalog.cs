using CAO.Core.Abstractions;
using CAO.Core.Optimizations;
using CAO.Core.Optimizations.Gaming;
using CAO.Core.Optimizations.Network;
using CAO.Core.Optimizations.Performance;
using CAO.Core.Optimizations.PrivacySecurity;
using CAO.Core.Optimizations.Storage;
using CAO.Shared;

namespace CAO.Core.Catalog;

/// <summary>The single source of truth of every toggle CA-O 2.0 offers.</summary>
public static class OptimizationCatalog
{
    public static readonly DisableVbs DisableVbs = new();
    public static readonly MaximumPowerPlan MaximumPowerPlan = new();
    public static readonly DisableVisualEffects DisableVisualEffects = new();
    public static readonly DisableSearchIndexing DisableSearchIndexing = new();
    public static readonly DisableBackgroundApps DisableBackgroundApps = new();
    public static readonly ZeroMenuDelay ZeroMenuDelay = new();
    public static readonly DisableTransparency DisableTransparency = new();

    public static readonly DisableTelemetry DisableTelemetry = new();
    public static readonly DisableCortana DisableCortana = new();
    public static readonly DisableWidgets DisableWidgets = new();
    public static readonly DisableCopilot DisableCopilot = new();
    public static readonly DisableSuggestions DisableSuggestions = new();
    public static readonly DisableOneDriveAutostart DisableOneDriveAutostart = new();

    public static readonly DisableGameBarDvr DisableGameBarDvr = new();
    public static readonly EnableGpuScheduling EnableGpuScheduling = new();

    public static readonly NormalizeTcpAutoTuning NormalizeTcpAutoTuning = new();
    public static readonly DisableHibernate DisableHibernate = new();
    public static readonly OptimizeSystemDrive OptimizeSystemDrive = new();

    /// <summary>All toggleable optimizations (startup-apps management is separate).</summary>
    public static IReadOnlyList<IOptimization> All { get; } = new IOptimization[]
    {
        // Performance
        DisableVbs, MaximumPowerPlan, DisableVisualEffects, DisableSearchIndexing,
        DisableBackgroundApps, ZeroMenuDelay, DisableTransparency,
        // Privacy & security
        DisableTelemetry, DisableCortana, DisableWidgets, DisableCopilot,
        DisableSuggestions, DisableOneDriveAutostart,
        // Gaming
        DisableGameBarDvr, EnableGpuScheduling,
        // Network
        NormalizeTcpAutoTuning,
        // Storage
        DisableHibernate, OptimizeSystemDrive,
    };
}
