using CAO.Shared;

namespace CAO.Core.Engine;

/// <summary>
/// Composite Optimization Score (spec 10). Combines expected/measured benefit,
/// evidence quality, risk, security impact, compatibility and reversibility.
/// Deliberately conservative: unknown evidence scores low, security reductions
/// subtract points, irreversibility subtracts points. No marketing numbers.
/// </summary>
public static class OptimizationScoreCalculator
{
    public static int? Compute(OptimizationDefinition definition)
    {
        var benefit = BenefitPoints(definition.ExpectedImpact);
        if (benefit is null)
        {
            return null; // DiagnosticOnly changes are scored by diagnostics, not here.
        }

        double score = benefit.Value;
        score += EvidencePoints(definition.Evidence);
        score -= RiskPenalty(definition.Risk);
        score -= SecurityPenalty(definition.SecurityImpact);
        score += CompatibilityPoints(definition.Compatibility);
        score += definition.Reversible ? 4 : -8;

        return Math.Clamp((int)Math.Round(score), 0, 100);
    }

    private static int? BenefitPoints(PerformanceImpact impact) => impact switch
    {
        PerformanceImpact.None => 5,
        PerformanceImpact.Tiny => 15,
        PerformanceImpact.Small => 30,
        PerformanceImpact.Moderate => 45,
        PerformanceImpact.Large => 55,
        PerformanceImpact.WorkloadDependent => 35,
        PerformanceImpact.DiagnosticOnly => null,
        _ => null,
    };

    private static int EvidencePoints(EvidenceLevel evidence) => evidence switch
    {
        EvidenceLevel.Official => 20,
        EvidenceLevel.Vendor => 16,
        EvidenceLevel.Benchmark => 18,
        EvidenceLevel.Empirical => 10,
        EvidenceLevel.Heuristic => 2,
        EvidenceLevel.Unknown => -6,
        _ => 0,
    };

    private static int RiskPenalty(RiskLevel risk) => risk switch
    {
        RiskLevel.Safe => 0,
        RiskLevel.Low => 2,
        RiskLevel.Moderate => 8,
        RiskLevel.High => 18,
        RiskLevel.Critical => 30,
        _ => 25,
    };

    private static int SecurityPenalty(SecurityImpact security) => security switch
    {
        SecurityImpact.None => 0,
        SecurityImpact.PrivacyOnly => 1,
        SecurityImpact.IncreasedProtection => 6,
        SecurityImpact.ReducedProtection => 22,
        SecurityImpact.Unknown => 8,
        _ => 8,
    };

    private static int CompatibilityPoints(CompatibilityStatus compatibility) => compatibility switch
    {
        CompatibilityStatus.Compatible => 12,
        CompatibilityStatus.NoKnownConflict => 8,
        CompatibilityStatus.Conditional => 3,
        CompatibilityStatus.PotentialConflict => -8,
        CompatibilityStatus.Incompatible => -30,
        CompatibilityStatus.Unknown => -5,
        _ => 0,
    };
}
