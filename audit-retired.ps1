$retired = @(
    'EnableWindowedGameOptimizations', 'EnableVrr', 'SetGamesHighPerformanceGpu',
    'DisableBackgroundGameCaptures', 'DisableGameBarAutoLaunch', 'ConfigureGamingPowerModeAc',
    'RestoreDefaultGpuPreference', 'EnableAutoHdr', 'GamingDisplayRefreshRateAudit',
    'SetBestPerformanceAc', 'RestoreBalancedPowerDc', 'DisableUsbSelectiveSuspendAc',
    'DisablePcieLinkStatePowerSavingAc', 'SetWirelessAdapterMaxPerformanceAc',
    'RestorePowerPlanAfterGaming', 'RemoveUnusedCustomPowerPlans',
    'DisableHibernate', 'OptimizeSystemDrive', 'EnsureTrimEnabled', 'RetrimSystemSsd',
    'OptimizeHddMediaAware', 'EnableStorageSense', 'StorageSenseTempCleanup',
    'StorageSenseRecycleBinPolicy', 'CleanupWindowsTemp', 'CleanupDeliveryOptimizationCache',
    'WindowsComponentStoreCleanup', 'WindowsComponentStoreResetBase', 'DiskCleanupSystemFiles',
    'FreeLowStorageSpace', 'RestoreSystemManagedPagefile', 'NormalizeTcpAutoTuning',
    'EnableRss', 'RestoreTcpChecksumOffload', 'RestoreUdpChecksumOffload',
    'RestoreLargeSendOffload', 'ConfigureInterruptModerationForLowLatency',
    'DisableNicPowerSavingAc', 'RestoreWindowsTcpCongestionDefault', 'FlushDnsCache',
    'ResetNetworkStackRepair', 'DeliveryOptimizationBandwidthProfile',
    'DisableUnnecessaryStartupApps', 'DisableHeavyStartupApps', 'DelaySafeThirdPartyServiceStart',
    'DisableSelectedThirdPartyBackgroundTask', 'RestoreSysmainDefault', 'RestoreWindowsSearchDefault',
    'CreateRestorePointBeforeOptimizationBatch', 'PendingRebootMaintenance',
    'StaleCrashDumpCleanup', 'OptimizeStartupRecoveryState'
)

Write-Host "Checking 49 retired optimizations..." -ForegroundColor Yellow
$found = 0
$notfound = @()

foreach ($opt in $retired) {
    $files = Get-ChildItem -Path src -Recurse -Filter "$opt.cs" -ErrorAction Ignore
    if ($files.Count -gt 0) {
        $found++
    } else {
        $notfound += $opt
    }
}

Write-Host "Found as classes: $found / 49" -ForegroundColor Green
Write-Host "NOT found (stubs only): $($notfound.Count) / 49" -ForegroundColor Red

if ($notfound.Count -gt 0) {
    Write-Host "`nMissing implementations:" -ForegroundColor Red
    $notfound | ForEach-Object { Write-Host "  - $_" }
}
