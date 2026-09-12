using CAO.Core.Optimization;
using CAO.Shared;
using Xunit;

namespace CAO.Core.Tests;

/// <summary>
/// Exclusión mutua de planes: activar un plan y después otro se pisan.
/// El motor bloquea con mensaje (revertir primero) y la UI lo muestra candado.
/// </summary>
public sealed class PowerConflictTests
{
    [Fact]
    public void SamePlanActive_IsAlreadyApplied()
    {
        var r = OptimizationConflicts.EvaluatePowerScheme(
            "maximum-power-plan", PowerSchemes.HighPerformanceGuid);
        Assert.Equal(OptimizationConflicts.ConflictOutcome.AlreadyApplied, r.Outcome);
    }

    [Fact]
    public void OtherPlanActive_IsBlocked_NamingActive()
    {
        var r = OptimizationConflicts.EvaluatePowerScheme(
            "configure-gaming-power-mode-ac", PowerSchemes.HighPerformanceGuid);
        Assert.Equal(OptimizationConflicts.ConflictOutcome.Blocked, r.Outcome);
        Assert.NotNull(r.ActiveMemberId);
        Assert.Contains(r.ActiveMemberId, r.MessageEs, StringComparison.Ordinal);
        Assert.Contains("configure-gaming-power-mode-ac", r.MessageEs, StringComparison.Ordinal);
    }

    [Fact]
    public void ForeignPlanActive_AllowsSwitch()
    {
        // Plan ajeno a CA-O (Economizador): cambiar es legítimo.
        var r = OptimizationConflicts.EvaluatePowerScheme(
            "maximum-power-plan", "a1841308-3541-4fab-bc81-f71556f20b4a");
        Assert.Equal(OptimizationConflicts.ConflictOutcome.None, r.Outcome);
    }

    [Fact]
    public void UnreadableActive_AllowsAttempt()
    {
        var r = OptimizationConflicts.EvaluatePowerScheme("maximum-power-plan", null);
        Assert.Equal(OptimizationConflicts.ConflictOutcome.None, r.Outcome);
    }

    [Fact]
    public void UnknownId_IsNone()
    {
        var r = OptimizationConflicts.EvaluatePowerScheme("flush-dns-cache", PowerSchemes.HighPerformanceGuid);
        Assert.Equal(OptimizationConflicts.ConflictOutcome.None, r.Outcome);
        Assert.False(OptimizationConflicts.IsPowerScheme("flush-dns-cache"));
    }

    [Fact]
    public void FindAppliedSibling_ReturnsOtherActiveMember()
    {
        var siblings = new List<(string Id, OptimizationState State)>
        {
            ("maximum-power-plan", OptimizationState.AppliedByCao),
            ("configure-gaming-power-mode-ac", OptimizationState.NotApplied),
            ("restore-balanced-power-dc", OptimizationState.NotApplied),
        };
        Assert.Equal("maximum-power-plan",
            OptimizationConflicts.FindAppliedSibling("configure-gaming-power-mode-ac", siblings));
        Assert.Null(OptimizationConflicts.FindAppliedSibling("maximum-power-plan", siblings));
        Assert.Null(OptimizationConflicts.FindAppliedSibling("flush-dns-cache", siblings));
    }
}
