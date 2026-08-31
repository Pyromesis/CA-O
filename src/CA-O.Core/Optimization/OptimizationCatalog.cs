using CAO.Core.Abstractions;
using CAO.Core.Optimizations;
using CAO.Core.Optimizations.Gaming;
using CAO.Core.Optimizations.Network;
using CAO.Core.Optimizations.Performance;
using CAO.Core.Optimizations.Power;
using CAO.Core.Optimizations.PrivacySecurity;
using CAO.Core.Optimizations.Startup;
using CAO.Core.Optimizations.Storage;
using CAO.Core.Optimizations.System;
using CAO.Shared;

namespace CAO.Core.Catalog;

/// <summary>The single source of truth of every toggle CA-O 2.0 offers.</summary>
public static class OptimizationCatalog
{
    // Performance (existing)
    public static readonly DisableVbs DisableVbs = new();
    public static readonly MaximumPowerPlan MaximumPowerPlan = new();
    public static readonly DisableVisualEffects DisableVisualEffects = new();
    public static readonly DisableSearchIndexing DisableSearchIndexing = new();
    public static readonly DisableBackgroundApps DisableBackgroundApps = new();
    public static readonly ZeroMenuDelay ZeroMenuDelay = new();
    public static readonly DisableTransparency DisableTransparency = new();

    // Privacy & security (existing)
    public static readonly DisableTelemetry DisableTelemetry = new();
    public static readonly DisableCortana DisableCortana = new();
    public static readonly DisableWidgets DisableWidgets = new();
    public static readonly DisableCopilot DisableCopilot = new();
    public static readonly DisableSuggestions DisableSuggestions = new();
    public static readonly DisableOneDriveAutostart DisableOneDriveAutostart = new();

    // Gaming (existing + new)
    public static readonly DisableGameBarDvr DisableGameBarDvr = new();
    public static readonly EnableGpuScheduling EnableGpuScheduling = new();
    public static readonly EnableGameMode EnableGameMode = new();
    public static readonly EnableWindowedGameOptimizations EnableWindowedGameOptimizations = new();
    public static readonly EnableVrr EnableVrr = new();
    public static readonly SetGamesHighPerformanceGpu SetGamesHighPerformanceGpu = new();
    public static readonly DisableBackgroundGameCaptures DisableBackgroundGameCaptures = new();
    public static readonly DisableGameBarAutoLaunch DisableGameBarAutoLaunch = new();
    public static readonly ConfigureGamingPowerModeAc ConfigureGamingPowerModeAc = new();
    public static readonly RestoreDefaultGpuPreference RestoreDefaultGpuPreference = new();
    public static readonly EnableAutoHdr EnableAutoHdr = new();
    public static readonly GamingDisplayRefreshRateAudit GamingDisplayRefreshRateAudit = new();

    // Power (new)
    public static readonly SetBestPerformanceAc SetBestPerformanceAc = new();
    public static readonly RestoreBalancedPowerDc RestoreBalancedPowerDc = new();
    public static readonly DisableUsbSelectiveSuspendAc DisableUsbSelectiveSuspendAc = new();
    public static readonly DisablePcieLinkStatePowerSavingAc DisablePcieLinkStatePowerSavingAc = new();
    public static readonly SetWirelessAdapterMaxPerformanceAc SetWirelessAdapterMaxPerformanceAc = new();
    public static readonly RestorePowerPlanAfterGaming RestorePowerPlanAfterGaming = new();
    public static readonly RemoveUnusedCustomPowerPlans RemoveUnusedCustomPowerPlans = new();

    // Storage (existing + new)
    public static readonly DisableHibernate DisableHibernate = new();
    public static readonly OptimizeSystemDrive OptimizeSystemDrive = new();
    public static readonly EnsureTrimEnabled EnsureTrimEnabled = new();
    public static readonly RetrimSystemSsd RetrimSystemSsd = new();
    public static readonly OptimizeHddMediaAware OptimizeHddMediaAware = new();
    public static readonly EnableStorageSense EnableStorageSense = new();
    public static readonly StorageSenseTempCleanup StorageSenseTempCleanup = new();
    public static readonly StorageSenseRecycleBinPolicy StorageSenseRecycleBinPolicy = new();
    public static readonly CleanupWindowsTemp CleanupWindowsTemp = new();
    public static readonly CleanupDeliveryOptimizationCache CleanupDeliveryOptimizationCache = new();
    public static readonly WindowsComponentStoreCleanup WindowsComponentStoreCleanup = new();
    public static readonly WindowsComponentStoreResetBase WindowsComponentStoreResetBase = new();
    public static readonly DiskCleanupSystemFiles DiskCleanupSystemFiles = new();
    public static readonly FreeLowStorageSpace FreeLowStorageSpace = new();
    public static readonly RestoreSystemManagedPagefile RestoreSystemManagedPagefile = new();

    // Network (existing + new)
    public static readonly NormalizeTcpAutoTuning NormalizeTcpAutoTuning = new();
    public static readonly EnableRss EnableRss = new();
    public static readonly RestoreTcpChecksumOffload RestoreTcpChecksumOffload = new();
    public static readonly RestoreUdpChecksumOffload RestoreUdpChecksumOffload = new();
    public static readonly RestoreLargeSendOffload RestoreLargeSendOffload = new();
    public static readonly ConfigureInterruptModerationForLowLatency ConfigureInterruptModerationForLowLatency = new();
    public static readonly DisableNicPowerSavingAc DisableNicPowerSavingAc = new();
    public static readonly RestoreWindowsTcpCongestionDefault RestoreWindowsTcpCongestionDefault = new();
    public static readonly FlushDnsCache FlushDnsCache = new();
    public static readonly ResetNetworkStackRepair ResetNetworkStackRepair = new();
    public static readonly DeliveryOptimizationBandwidthProfile DeliveryOptimizationBandwidthProfile = new();

    // Startup (new)
    public static readonly DisableUnnecessaryStartupApps DisableUnnecessaryStartupApps = new();
    public static readonly DisableHeavyStartupApps DisableHeavyStartupApps = new();
    public static readonly DelaySafeThirdPartyServiceStart DelaySafeThirdPartyServiceStart = new();
    public static readonly DisableSelectedThirdPartyBackgroundTask DisableSelectedThirdPartyBackgroundTask = new();
    public static readonly RestoreSysmainDefault RestoreSysmainDefault = new();
    public static readonly RestoreWindowsSearchDefault RestoreWindowsSearchDefault = new();

    // System/Maintenance (new)
    public static readonly CreateRestorePointBeforeOptimizationBatch CreateRestorePointBeforeOptimizationBatch = new();
    public static readonly PendingRebootMaintenance PendingRebootMaintenance = new();
    public static readonly StaleCrashDumpCleanup StaleCrashDumpCleanup = new();
    public static readonly OptimizeStartupRecoveryState OptimizeStartupRecoveryState = new();

    /// <summary>All toggleable optimizations - SOLO implementaciones reales (19). Los 49 STUBs HKCU\Software\CA-O fueron retirados del catálogo de producción (ver docs/OPTIMIZATION-CATALOG.md). Preferible 19 reales que 68 falsas.</summary>
    public static IReadOnlyList<IOptimization> All { get; } = new IOptimization[]
    {
        // 15 IMPLEMENTED (registro real / powercfg / bcdedit / services / defrag)
        DisableBackgroundApps, DisableCopilot, DisableCortana, DisableGameBarDvr, DisableSuggestions,
        DisableTelemetry, DisableTransparency, DisableVisualEffects, DisableWidgets,
        EnableGameMode, EnableGpuScheduling, ZeroMenuDelay,
        DisableOneDriveAutostart, DisableSearchIndexing, MaximumPowerPlan,
        // 4 PARTIALLY_IMPLEMENTED (aplica real pero necesita Verify robusto - se mantiene como EXPERIMENTAL/Optional)
        DisableHibernate, DisableVbs, NormalizeTcpAutoTuning, OptimizeSystemDrive,
    };

    /// <summary>Catálogo completo histórico (68) - solo para tests de trazabilidad docs ↔ código. No usar en producción.</summary>
    public static IReadOnlyList<IOptimization> AllLegacy { get; } = new IOptimization[]
    {
        DisableVbs, MaximumPowerPlan, DisableVisualEffects, DisableSearchIndexing,
        DisableBackgroundApps, ZeroMenuDelay, DisableTransparency,
        DisableTelemetry, DisableCortana, DisableWidgets, DisableCopilot,
        DisableSuggestions, DisableOneDriveAutostart,
        DisableGameBarDvr, EnableGpuScheduling,
        EnableGameMode, EnableWindowedGameOptimizations, EnableVrr,
        SetGamesHighPerformanceGpu, DisableBackgroundGameCaptures,
        DisableGameBarAutoLaunch, ConfigureGamingPowerModeAc,
        RestoreDefaultGpuPreference, EnableAutoHdr,
        GamingDisplayRefreshRateAudit,
        SetBestPerformanceAc, RestoreBalancedPowerDc,
        DisableUsbSelectiveSuspendAc, DisablePcieLinkStatePowerSavingAc,
        SetWirelessAdapterMaxPerformanceAc, RestorePowerPlanAfterGaming,
        RemoveUnusedCustomPowerPlans,
        DisableHibernate, OptimizeSystemDrive,
        EnsureTrimEnabled, RetrimSystemSsd, OptimizeHddMediaAware,
        EnableStorageSense, StorageSenseTempCleanup,
        StorageSenseRecycleBinPolicy, CleanupWindowsTemp,
        CleanupDeliveryOptimizationCache, WindowsComponentStoreCleanup,
        WindowsComponentStoreResetBase, DiskCleanupSystemFiles,
        FreeLowStorageSpace, RestoreSystemManagedPagefile,
        NormalizeTcpAutoTuning,
        EnableRss, RestoreTcpChecksumOffload, RestoreUdpChecksumOffload,
        RestoreLargeSendOffload, ConfigureInterruptModerationForLowLatency,
        DisableNicPowerSavingAc, RestoreWindowsTcpCongestionDefault,
        FlushDnsCache, ResetNetworkStackRepair,
        DeliveryOptimizationBandwidthProfile,
        DisableUnnecessaryStartupApps, DisableHeavyStartupApps,
        DelaySafeThirdPartyServiceStart, DisableSelectedThirdPartyBackgroundTask,
        RestoreSysmainDefault, RestoreWindowsSearchDefault,
        CreateRestorePointBeforeOptimizationBatch, PendingRebootMaintenance,
        StaleCrashDumpCleanup, OptimizeStartupRecoveryState,
    };
}