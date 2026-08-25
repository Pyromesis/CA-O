# Adds evidence/risk classification metadata to every optimization Definition
# block (spec 92 contract: Evidence/Risk/Compatibility/SecurityImpact exist).
$ErrorActionPreference = "Stop"

$classifications = @{
    "DisableVbs.cs"                = @{ Impact = "WorkloadDependent"; Evidence = "Vendor";    Risk = "Critical"; Comp = "PotentialConflict"; Sec = "ReducedProtection" }
    "MaximumPowerPlan.cs"          = @{ Impact = "Small";             Evidence = "Official";  Risk = "Low";      Comp = "Compatible";        Sec = "None" }
    "DisableVisualEffects.cs"      = @{ Impact = "Tiny";              Evidence = "Empirical"; Risk = "Safe";     Comp = "Compatible";        Sec = "None" }
    "DisableSearchIndexing.cs"     = @{ Impact = "WorkloadDependent"; Evidence = "Empirical"; Risk = "Moderate"; Comp = "Conditional";       Sec = "None" }
    "DisableBackgroundApps.cs"     = @{ Impact = "Small";             Evidence = "Official";  Risk = "Low";      Comp = "NoKnownConflict";   Sec = "PrivacyOnly" }
    "ZeroMenuDelay.cs"             = @{ Impact = "Tiny";              Evidence = "Heuristic"; Risk = "Safe";     Comp = "Compatible";        Sec = "None" }
    "DisableTransparency.cs"       = @{ Impact = "Tiny";              Evidence = "Empirical"; Risk = "Safe";     Comp = "Compatible";        Sec = "None" }
    "DisableTelemetry.cs"          = @{ Impact = "None";              Evidence = "Official";  Risk = "Low";      Comp = "Compatible";        Sec = "PrivacyOnly" }
    "DisableCortana.cs"            = @{ Impact = "None";              Evidence = "Official";  Risk = "Low";      Comp = "NoKnownConflict";   Sec = "PrivacyOnly" }
    "DisableWidgets.cs"            = @{ Impact = "Tiny";              Evidence = "Official";  Risk = "Low";      Comp = "NoKnownConflict";   Sec = "PrivacyOnly" }
    "DisableCopilot.cs"            = @{ Impact = "None";              Evidence = "Vendor";    Risk = "Low";      Comp = "NoKnownConflict";   Sec = "PrivacyOnly" }
    "DisableSuggestions.cs"        = @{ Impact = "None";              Evidence = "Official";  Risk = "Low";      Comp = "Compatible";        Sec = "PrivacyOnly" }
    "DisableOneDriveAutostart.cs"  = @{ Impact = "Tiny";              Evidence = "Official";  Risk = "Low";      Comp = "Conditional";       Sec = "PrivacyOnly" }
    "DisableGameBarDvr.cs"         = @{ Impact = "WorkloadDependent"; Evidence = "Vendor";    Risk = "Low";      Comp = "NoKnownConflict";   Sec = "None" }
    "EnableGpuScheduling.cs"       = @{ Impact = "WorkloadDependent"; Evidence = "Vendor";    Risk = "Moderate"; Comp = "Conditional";       Sec = "None" }
    "NormalizeTcpAutoTuning.cs"    = @{ Impact = "WorkloadDependent"; Evidence = "Official";  Risk = "Low";      Comp = "Conditional";       Sec = "None" }
    "DisableHibernate.cs"          = @{ Impact = "None";              Evidence = "Official";  Risk = "Moderate"; Comp = "NoKnownConflict";   Sec = "None" }
    "OptimizeSystemDrive.cs"       = @{ Impact = "None";              Evidence = "Official";  Risk = "Low";      Comp = "Compatible";        Sec = "None" }
}

$files = Get-ChildItem -Recurse "src\CA-O.Core\Optimizations" -Filter *.cs |
    Where-Object { $_.Name -in $classifications.Keys }

foreach ($file in $files) {
    $c = $classifications[$file.Name]
    $text = [System.IO.File]::ReadAllText($file.FullName)
    if ($text -match "ExpectedImpact\s*=") {
        Write-Output "SKIP (ya clasificado): $($file.Name)"
        continue
    }

    $pattern = '(?m)^(\s*)Category = OptimizationCategory\.\w+,\s*$'
    $replacement = "`$0`n`$1ExpectedImpact = PerformanceImpact.$($c.Impact),`n`$1Evidence = EvidenceLevel.$($c.Evidence),`n`$1Risk = RiskLevel.$($c.Risk),`n`$1Compatibility = CompatibilityStatus.$($c.Comp),`n`$1SecurityImpact = SecurityImpact.$($c.Sec),"
    $newText = [regex]::Replace($text, $pattern, $replacement, [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
    if ($newText -eq $text) { throw "No se encontró la línea Category en $($file.Name)" }
    [System.IO.File]::WriteAllText($file.FullName, $newText, (New-Object System.Text.UTF8Encoding($false)))
    Write-Output "OK: $($file.Name)"
}
