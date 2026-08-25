using CAO.Core.Gaming;
using CAO.Core.Profiles;
using CAO.Shared;
using Xunit;

namespace CAO.Core.Tests;

/// <summary>
/// Profile engine tests (spec 14, 59-60, 95, 104): Safe Gaming never touches
/// security; anti-cheat presence hard-blocks security-reducing changes;
/// Expert is the only escape hatch and still requires confirmation.
/// </summary>
public sealed class ProfileEngineTests
{
    private static SystemContext Context(bool vanguard = false) =>
        SystemContextFactory.Default() with
        {
            AntiCheats = vanguard
                ? [new AntiCheatInfo(AntiCheatKind.Vanguard, "HKLM\\Services", ["vgk"])]
                : [],
        };

    [Fact]
    public void SafeGamingRejectsSecurityTradeoff()
    {
        var definition = Definition(flags: OptimizationFlags.SecurityTradeoff,
            security: SecurityImpact.ReducedProtection);

        var decision = ProfileEngine.Evaluate(ProfileId.Safe, definition, Context());

        Assert.False(decision.Allowed);
    }

    [Fact]
    public void SafeGamingRejectsModerateRisk()
    {
        var decision = ProfileEngine.Evaluate(ProfileId.Safe, Definition(risk: RiskLevel.Moderate), Context());

        Assert.False(decision.Allowed);
    }

    [Fact]
    public void SafeGamingAcceptsLowRiskWellDocumentedChange()
    {
        var decision = ProfileEngine.Evaluate(ProfileId.Safe, Definition(), Context());

        Assert.True(decision.Allowed, decision.ReasonEs);
    }

    [Theory]
    [InlineData("disable-vbs")]
    public void DefaultBlockListIsEnforcedOutsideExpertMode(string id)
    {
        var definition = Definition(id: id, flags: OptimizationFlags.SecurityTradeoff | OptimizationFlags.ExpertOnly);

        foreach (var profile in new[] { ProfileId.Safe, ProfileId.Balanced, ProfileId.Gaming, ProfileId.Competitive })
        {
            Assert.False(ProfileEngine.Evaluate(profile, definition, Context()).Allowed,
                $"El perfil {profile} no debe permitir {id}.");
        }

        // Expert may proceed, but only with explicit confirmation (by contract).
        Assert.True(ProfileEngine.Evaluate(ProfileId.Expert, definition, Context()).Allowed);
    }

    [Fact]
    public void VanguardPresenceHardBlocksSecurityReduction()
    {
        var definition = Definition(security: SecurityImpact.ReducedProtection,
            flags: OptimizationFlags.SecurityTradeoff | OptimizationFlags.ExpertOnly);

        // Even in Expert mode the anti-cheat guard reason must be surfaced.
        var guard = AntiCheatGuard.Evaluate(definition, Context(vanguard: true));

        Assert.Equal("blocked-anticheat", guard.Code);
    }

    [Fact]
    public void PrivacyProfileOnlyManagesPrivacyChanges()
    {
        var privacy = Definition(category: OptimizationCategory.PrivacySecurity);
        var performance = Definition(id: "perf-one", category: OptimizationCategory.Performance);

        Assert.True(ProfileEngine.Evaluate(ProfileId.Privacy, privacy, Context()).Allowed);
        Assert.False(ProfileEngine.Evaluate(ProfileId.Privacy, performance, Context()).Allowed);
    }

    [Fact]
    public void SecurityProfileNeverReducesProtections()
    {
        var hardening = Definition(security: SecurityImpact.IncreasedProtection,
            category: OptimizationCategory.PrivacySecurity);
        var reduction = Definition(id: "reduce-one",
            security: SecurityImpact.ReducedProtection,
            flags: OptimizationFlags.SecurityTradeoff,
            category: OptimizationCategory.PrivacySecurity);

        Assert.True(ProfileEngine.Evaluate(ProfileId.Security, hardening, Context()).Allowed);
        Assert.False(ProfileEngine.Evaluate(ProfileId.Security, reduction, Context()).Allowed);
    }

    [Fact]
    public void LaptopOnBatteryBlocksHeavyGamingImpact()
    {
        var context = Context() with { IsLaptop = true, OnBattery = true };
        var heavy = Definition(impact: PerformanceImpact.Large);

        var decision = ProfileEngine.Evaluate(ProfileId.Gaming, heavy, context);

        Assert.False(decision.Allowed);
    }

    [Fact]
    public void ThermalThrottlingBlocksPerformanceCategoryInGaming()
    {
        var context = Context() with { ThermalState = ThermalState.Throttling };
        var tweak = Definition(category: OptimizationCategory.Performance);

        Assert.False(ProfileEngine.Evaluate(ProfileId.Gaming, tweak, context).Allowed);
    }

    private static OptimizationDefinition Definition(
        string id = "profile-test-change",
        OptimizationCategory category = OptimizationCategory.Performance,
        RiskLevel risk = RiskLevel.Low,
        EvidenceLevel evidence = EvidenceLevel.Benchmark,
        CompatibilityStatus compatibility = CompatibilityStatus.Compatible,
        SecurityImpact security = SecurityImpact.None,
        PerformanceImpact impact = PerformanceImpact.Small,
        OptimizationFlags flags = OptimizationFlags.None) => new()
    {
        Id = id,
        NameEs = "Cambio de prueba",
        NameEn = "Test change",
        DescriptionEs = "Definición usada en pruebas de perfiles.",
        DescriptionEn = "Definition used by profile tests.",
        Category = category,
        ExpectedImpact = impact,
        Evidence = evidence,
        Risk = risk,
        Compatibility = compatibility,
        SecurityImpact = security,
        Flags = flags,
    };
}
