$optimizations = @(
    'DisableBackgroundApps','DisableCopilot','DisableCortana','DisableGameBarDvr',
    'DisableSuggestions','DisableTelemetry','DisableTransparency','DisableVisualEffects',
    'DisableWidgets','EnableGameMode','EnableGpuScheduling','ZeroMenuDelay',
    'DisableOneDriveAutostart','DisableSearchIndexing','MaximumPowerPlan',
    'DisableHibernate','DisableVbs','NormalizeTcpAutoTuning','OptimizeSystemDrive'
)

Write-Host "=== Auditing 19 Production Optimizations ===" -ForegroundColor Green

$results = @()
foreach ($opt in $optimizations) {
    $files = Get-ChildItem -Path src -Recurse -Filter "$opt.cs" -ErrorAction Ignore
    if ($files.Count -gt 0) {
        $file = $files[0].FullName
        $content = Get-Content $file -Raw
        $hasApply = $content -match 'ApplyAsync'
        $hasRevert = $content -match 'RevertAsync'
        $hasVerify = $content -match 'VerifyAsync'
        $status = @()
        if ($hasApply) { $status += "APPLY:YES" } else { $status += "APPLY:NO" }
        if ($hasRevert) { $status += "REVERT:YES" } else { $status += "REVERT:NO" }
        if ($hasVerify) { $status += "VERIFY:YES" }
        $results += [PSCustomObject]@{Optimization=$opt; Status=($status -join " "); File=$file}
        Write-Host "$opt : $(($status -join ' '))" -ForegroundColor Cyan
    } else {
        Write-Host "$opt : NOT_FOUND" -ForegroundColor Red
        $results += [PSCustomObject]@{Optimization=$opt; Status="NOT_FOUND"; File=""}
    }
}

Write-Host "`n=== Summary ===" -ForegroundColor Green
$complete = $results | Where-Object { $_.Status -match 'APPLY:YES' }
$withRevert = $results | Where-Object { $_.Status -match 'REVERT:YES' }
Write-Host "Total: $($results.Count)" -ForegroundColor Green
Write-Host "With APPLY: $($complete.Count)" -ForegroundColor Cyan
Write-Host "With REVERT: $($withRevert.Count)" -ForegroundColor Cyan

$withoutRevert = $results | Where-Object { $_.Status -match 'APPLY:YES' -and $_.Status -notmatch 'REVERT:YES' }
if ($withoutRevert.Count -gt 0) {
    Write-Host "`nOptimizations with APPLY but NO custom REVERT (using base impl):" -ForegroundColor Yellow
    $withoutRevert | ForEach-Object { Write-Host "  - $($_.Optimization)" }
}
