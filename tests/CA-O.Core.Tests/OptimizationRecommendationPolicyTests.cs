using CAO.Core.Engine;
using CAO.Shared;
using Xunit;

namespace CAO.Core.Tests;

public sealed class OptimizationRecommendationPolicyTests
{
    [Fact]
    public void RecommendedProfileRejectsSecurityTradeoff()
    {
        var definition = CreateDefinition() with
        {
            Flags = OptimizationFlags.SecurityTradeoff,
            SecurityImpact = SecurityImpact.ReducedProtection,
        };

        var decision = OptimizationRecommendationPolicy.Evaluate(definition, RecommendationProfile.Recommended);

        Assert.False(decision.Allowed);
    }

    [Fact]
    public void SafeGamingAllowsOnlyKnownSafeCompatibleChanges()
    {
        var definition = CreateDefinition();

        var decision = OptimizationRecommendationPolicy.Evaluate(definition, RecommendationProfile.SafeGaming);

        Assert.True(decision.Allowed);
    }

    [Fact]
    public void SafeGamingRejectsUnknownCompatibility()
    {
        var definition = CreateDefinition() with
        {
            Compatibility = CompatibilityStatus.Unknown,
        };

        var decision = OptimizationRecommendationPolicy.Evaluate(definition, RecommendationProfile.SafeGaming);

        Assert.False(decision.Allowed);
    }

    [Fact]
    public void PrivilegedValidatorRejectsUnsafeOptimizationId()
    {
        var request = new PrivilegedOperationRequest
        {
            RequestId = Guid.NewGuid(),
            Nonce = "nonce",
            Operation = PrivilegedOperation.ApplyOptimization,
            Parameters = new OperationParameters { OptimizationId = "../../registry" },
        };

        var valid = PrivilegedOperationValidator.TryValidate(request, out _);

        Assert.False(valid);
    }

    [Fact]
    public void PrivilegedValidatorAcceptsKnownTypedRequest()
    {
        var request = new PrivilegedOperationRequest
        {
            RequestId = Guid.NewGuid(),
            Nonce = "nonce",
            Operation = PrivilegedOperation.RevertOptimization,
            Parameters = new OperationParameters { OptimizationId = "disable-vbs" },
        };

        var valid = PrivilegedOperationValidator.TryValidate(request, out _);

        Assert.True(valid);
    }

    [Fact]
    public void PrivilegedValidatorRejectsOversizedOrControlNonce()
    {
        var request = new PrivilegedOperationRequest
        {
            RequestId = Guid.NewGuid(),
            Nonce = new string('x', 129),
            Operation = PrivilegedOperation.DetectOptimization,
            Parameters = new OperationParameters { OptimizationId = "disable-vbs" },
        };

        Assert.False(PrivilegedOperationValidator.TryValidate(request, out _));
    }

    [Fact]
    public void SystemHealthDoesNotInventScoresForUnmeasuredDimensions()
    {
        var system = new CAO.Core.Abstractions.SystemInfoReport(
            "Windows 11 build 26200", "Windows 11", 16, "Test CPU", true, false, false)
        {
            WindowsBuild = 26200,
        };

        var report = SystemHealthAnalyzer.Analyze(system);

        Assert.Null(report.Scores.Single(score => score.Dimension == HealthDimension.Network).Score);
        Assert.False(report.Scores.Single(score => score.Dimension == HealthDimension.Network).IsMeasured);
    }

    [Fact]
    public void BenchmarkAnalyzerReportsFrameTimePercentiles()
    {
        var statistics = BenchmarkAnalyzer.AnalyzeFrameTimes(new[] { 10d, 20d, 30d, 40d });

        Assert.Equal(4, statistics.SampleCount);
        Assert.Equal(40d, statistics.AverageFps, 3);
        Assert.Equal(25d, statistics.P50FrameTimeMs, 3);
        Assert.Equal(39.7d, statistics.P99FrameTimeMs, 3);
    }

    [Fact]
    public void BenchmarkAnalyzerRejectsInsufficientComparisonData()
    {
        var comparison = BenchmarkAnalyzer.Compare(
            new FrameTimeStatistics(0, 0, 0, 0, 0, 0, 0, 0),
            BenchmarkAnalyzer.AnalyzeFrameTimes(new[] { 16d }));

        Assert.Equal(BenchmarkVerdict.InsufficientData, comparison.Verdict);
    }

    private static OptimizationDefinition CreateDefinition() => new()
    {
        Id = "test-safe-change",
        NameEs = "Cambio seguro",
        NameEn = "Safe change",
        DescriptionEs = "Cambio de prueba",
        DescriptionEn = "Test change",
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Benchmark,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
    };
}