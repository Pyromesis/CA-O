using CAO.Core.Optimizations.Storage;
using CAO.Shared;

namespace CAO.Core.Optimizations.System;

/// <summary>Deletes crash dumps older than 30 days from the system Minidump folder.</summary>
public sealed class StaleCrashDumpCleanup : TempFileCleanupOptimization
{
    protected override IReadOnlyList<(string Directory, string Pattern, int OlderThanDays)> Targets { get; } =
    [
        (@"%SystemRoot%\Minidump", "*.dmp", 30),
    ];

    public override OptimizationDefinition Definition => new()
    {
        Id = "stale-crash-dump-cleanup",
        NameEs = "Limpiar minidumps antiguos",
        NameEn = "Cleanup stale crash dumps",
        DescriptionEs = "Borra minivolcados de más de 30 días en la carpeta Minidump del sistema.",
        DescriptionEn = "Deletes dumps older than 30 days from the system Minidump folder.",
        TooltipEs = "Solo *.dmp con más de 30 días. Mantenimiento no reversible.",
        Category = OptimizationCategory.Storage,
        ExpectedImpact = PerformanceImpact.Tiny,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
        Flags = OptimizationFlags.NotReversible,
    };
}
