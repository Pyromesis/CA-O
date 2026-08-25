using CAO.Core.Abstractions;
using CAO.Core.Engine;
using CAO.Shared;
using Xunit;

namespace CAO.Core.Tests;

/// <summary>
/// Recommendation pipeline tests (spec 13, 95): the engine classifies every
/// optimization into exactly one bucket against a measured context and never
/// produces an "apply everything" plan.
/// </summary>
public sealed class RecommendationEngineTests
{
    private readonly MemoryRegistry _registry = new();

    private static OptimizationDefinition Definition(
        string id = "rec-test",
        EvidenceLevel evidence = EvidenceLevel.Benchmark,
        RiskLevel risk = RiskLevel.Low,
        CompatibilityStatus compatibility = CompatibilityStatus.Compatible,
        SecurityImpact security = SecurityImpact.None,
        PerformanceImpact impact = PerformanceImpact.Small,
        OptimizationFlags flags = OptimizationFlags.None) => new()
    {
        Id = id,
        NameEs = "Prueba",
        NameEn = "Test",
        DescriptionEs = "Definición de prueba para recomendaciones.",
        DescriptionEn = "Test definition for recommendations.",
        ExpectedImpact = impact,
        Evidence = evidence,
        Risk = risk,
        Compatibility = compatibility,
        SecurityImpact = security,
        Flags = flags,
    };

    private sealed class SimpleOptimization(OptimizationDefinition definition) : IOptimization
    {
        public OptimizationDefinition Definition { get; } = definition;

        public OptimizationState Detect(IRegistryAccessor registry) =>
            registry.GetValue(RegistryHive2.CurrentUser, @"SOFTWARE\CA-O\Rec", "V") is 1
                ? OptimizationState.AppliedByCao
                : OptimizationState.NotApplied;

        public OptimizationSnapshot Capture(IRegistryAccessor registry) => new();

        public Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
        {
            context.Registry.SetValue(RegistryHive2.CurrentUser, @"SOFTWARE\CA-O\Rec", "V", 1, RegistryValueKind2.DWord);
            return Task.FromResult(OperationResult.Ok("ok"));
        }

        public Task<OperationResult> RevertAsync(
            OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default) =>
            Task.FromResult(OperationResult.Ok("revertido"));
    }

    [Fact]
    public void WellDocumentedLowRiskChangeIsRecommended()
    {
        var recommendation = Build(Definition());

        Assert.Equal(RecommendationBucket.Recommended, recommendation.Bucket);
        Assert.NotNull(recommendation.Score);
    }

    [Fact]
    public void UnknownEvidenceIsExperimentalNotRecommended()
    {
        var recommendation = Build(Definition(evidence: EvidenceLevel.Unknown));

        Assert.Equal(RecommendationBucket.Experimental, recommendation.Bucket);
    }

    [Fact]
    public void SecurityTradeoffIsSecuritySensitiveNeverRecommended()
    {
        var recommendation = Build(Definition(security: SecurityImpact.ReducedProtection,
            flags: OptimizationFlags.SecurityTradeoff | OptimizationFlags.ExpertOnly));

        Assert.Equal(RecommendationBucket.SecuritySensitive, recommendation.Bucket);
    }

    [Fact]
    public void DefaultBlockListLandsInExperimentalWithReason()
    {
        var recommendation = Build(Definition(id: "svchost-split-threshold-hack",
            evidence: EvidenceLevel.Empirical));

        Assert.Equal(RecommendationBucket.Experimental, recommendation.Bucket);
        Assert.Equal("blocked-by-default", recommendation.Reason.Code);
    }

    [Fact]
    public void VanguardMakesSecurityReductionSensitiveAndFlagged()
    {
        var context = SystemContextFactory.Default() with
        {
            AntiCheats = [new AntiCheatInfo(AntiCheatKind.Vanguard, "HKLM\\Services", ["vgc"])],
        };
        var recommendation = Build(Definition(id: "disable-vbs",
            security: SecurityImpact.ReducedProtection,
            flags: OptimizationFlags.SecurityTradeoff | OptimizationFlags.ExpertOnly), context);

        Assert.Equal(RecommendationBucket.SecuritySensitive, recommendation.Bucket);
        Assert.True(recommendation.AntiCheatConflictRisk);
    }

    [Fact]
    public void SsdRequirementWithoutSsdIsNotApplicable()
    {
        var context = SystemContextFactory.Default() with { HasSsd = false };
        var recommendation = Build(Definition(flags: OptimizationFlags.RecommendedOnSsd), context);

        Assert.Equal(RecommendationBucket.NotApplicable, recommendation.Bucket);
    }

    [Fact]
    public void ThermalThrottlingMakesPerformanceChangesNotApplicable()
    {
        var context = SystemContextFactory.Default() with { ThermalState = ThermalState.Throttling };
        var recommendation = Build(Definition(), context);

        Assert.Equal(RecommendationBucket.NotApplicable, recommendation.Bucket);
    }

    [Fact]
    public void AlreadyAppliedChangeIsOptionalAndKeptUnderWatch()
    {
        _registry.SetValue(RegistryHive2.CurrentUser, @"SOFTWARE\CA-O\Rec", "V", 1, RegistryValueKind2.DWord);

        var recommendation = Build(Definition());

        Assert.Equal(RecommendationBucket.Optional, recommendation.Bucket);
        Assert.Equal("already-applied", recommendation.Reason.Code);
    }

    [Fact]
    public void MaintenanceActionIsAlwaysOptional()
    {
        var recommendation = Build(Definition(flags: OptimizationFlags.NotReversible));

        Assert.Equal(RecommendationBucket.Optional, recommendation.Bucket);
        Assert.Equal("maintenance", recommendation.Reason.Code);
    }

    [Fact]
    public void DiagnosticOnlyChangesAreNotScored()
    {
        var score = OptimizationScoreCalculator.Compute(Definition(impact: PerformanceImpact.DiagnosticOnly));

        Assert.Null(score);
    }

    private Recommendation Build(OptimizationDefinition definition, SystemContext? context = null) =>
        RecommendationEngine.Build(new SimpleOptimization(definition), _registry, context ?? SystemContextFactory.Default());
}
