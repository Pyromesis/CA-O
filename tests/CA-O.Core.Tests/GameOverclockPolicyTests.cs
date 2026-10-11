using CAO.Core.Overclock;
using CAO.Core.Overclock.Gaming;
using CAO.Shared;
using Xunit;

namespace CAO.Core.Tests;

/// <summary>
/// The gate that decides whether a title may carry a tuning profile.
/// </summary>
public sealed class GameOverclockPolicyTests
{
    private const string ValorantId = "valorant";
    private const string FortniteId = "fortnite";

    [Fact]
    public void AKernelProtectedTitle_IsBlocked_EvenWhenTheScanComesBackClean()
    {
        var decision = GameOverclockPolicy.Decide(
            KernelTitle(),
            antiCheatScanSucceeded: true,
            detectedAntiCheats: [],
            profile: Profile(ValorantId));

        Assert.Equal(GameProfileState.BlockedByAntiCheat, decision.State);
        Assert.Equal(GameOverclockPolicy.ReasonKernelAntiCheat, decision.ReasonKey);
        Assert.False(decision.IsActionable);
    }

    [Fact]
    public void AUserModeTitle_IsBlocked_WhenAnAntiCheatIsActuallyFound()
    {
        var decision = GameOverclockPolicy.Decide(
            UserModeTitle(),
            antiCheatScanSucceeded: true,
            detectedAntiCheats: ["Vanguard"],
            profile: Profile(FortniteId));

        Assert.Equal(GameProfileState.BlockedByAntiCheat, decision.State);
        Assert.Equal(GameOverclockPolicy.ReasonAntiCheatDetected, decision.ReasonKey);
        Assert.False(decision.IsActionable);
    }

    [Fact]
    public void AFailedScan_ClosesTheRow_EvenThoughTheListIsEmpty()
    {
        // This is the whole reason the scan reports whether it managed to look. An
        // empty list here means "not allowed to check", not "nothing installed".
        var decision = GameOverclockPolicy.Decide(
            UserModeTitle(),
            antiCheatScanSucceeded: false,
            detectedAntiCheats: [],
            profile: Profile(FortniteId));

        Assert.Equal(GameProfileState.ScanUnavailable, decision.State);
        Assert.Equal(GameOverclockPolicy.ReasonScanUnavailable, decision.ReasonKey);
        Assert.False(decision.IsActionable);
    }

    [Fact]
    public void AnEmptyScanResult_WithAFailedScan_NeverReadsAsACleanMachine()
    {
        var closed = GameOverclockPolicy.Decide(UserModeTitle(), false, [], Profile(FortniteId));
        var open = GameOverclockPolicy.Decide(UserModeTitle(), true, [], Profile(FortniteId));

        Assert.NotEqual(closed.State, open.State);
        Assert.Equal(GameProfileState.Ready, open.State);
    }

    [Fact]
    public void AKnownTitleWithoutAValidatedProfile_SaysNoProfile_NotBlocked()
    {
        var decision = GameOverclockPolicy.Decide(
            UserModeTitle(),
            antiCheatScanSucceeded: true,
            detectedAntiCheats: [],
            profile: null);

        Assert.Equal(GameProfileState.NoValidatedProfile, decision.State);
        Assert.Equal(GameOverclockPolicy.ReasonNoProfile, decision.ReasonKey);
        Assert.False(decision.IsActionable);
    }

    [Fact]
    public void AnUnknownExecutable_IsReportedAsUnknown_BeforeAnythingElse()
    {
        // Even with a profile in hand the row cannot claim to know the title: the
        // profile was matched on an empty game id.
        var decision = GameOverclockPolicy.Decide(
            GameCandidate.Unknown("mystery.exe"),
            antiCheatScanSucceeded: false,
            detectedAntiCheats: [],
            profile: Profile(string.Empty));

        Assert.Equal(GameProfileState.UnknownGame, decision.State);
        Assert.Equal(GameOverclockPolicy.ReasonUnknownGame, decision.ReasonKey);
        Assert.False(decision.IsActionable);
    }

    [Fact]
    public void ACleanScanOnAUserModeTitle_WithAValidatedProfile_IsReady()
    {
        var decision = GameOverclockPolicy.Decide(
            UserModeTitle(),
            antiCheatScanSucceeded: true,
            detectedAntiCheats: [],
            profile: Profile(FortniteId));

        Assert.Equal(GameProfileState.Ready, decision.State);
        Assert.Equal(GameOverclockPolicy.ReasonReady, decision.ReasonKey);
        Assert.True(decision.IsActionable);
    }

    [Fact]
    public void GuidanceComesFromTheProfile_WhenThereIsOne()
    {
        var decision = GameOverclockPolicy.Decide(
            UserModeTitle(),
            antiCheatScanSucceeded: false,
            detectedAntiCheats: [],
            profile: Profile(FortniteId));

        Assert.Equal(["Reduce the mouse polling floor only if you know it helps."], decision.GuidanceEs);
    }

    [Fact]
    public void WithoutAProfile_TheGuidanceIsEmptyRatherThanInvented()
    {
        var decision = GameOverclockPolicy.Decide(UserModeTitle(), true, [], null);

        Assert.Empty(decision.GuidanceEs);
    }

    [Fact]
    public void NothingIsActionableUnlessTheDecisionSaysReady()
    {
        var rows = new[]
        {
            GameOverclockPolicy.Decide(KernelTitle(), true, [], Profile(ValorantId)),
            GameOverclockPolicy.Decide(KernelTitle(), true, [], null),
            GameOverclockPolicy.Decide(UserModeTitle(), false, [], Profile(FortniteId)),
            GameOverclockPolicy.Decide(UserModeTitle(), true, ["BattlEye"], Profile(FortniteId)),
            GameOverclockPolicy.Decide(GameCandidate.Unknown("x.exe"), true, [], null),
            GameOverclockPolicy.Decide(UserModeTitle(), true, [], Profile(FortniteId)),
        };

        foreach (var row in rows)
        {
            Assert.Equal(row.State == GameProfileState.Ready, row.IsActionable);
            Assert.False(string.IsNullOrWhiteSpace(row.ReasonKey));
        }
    }

    [Fact]
    public void TheGateRefusesToRunWithoutTheCandidateOrTheFindings()
    {
        Assert.Throws<ArgumentNullException>(() =>
            GameOverclockPolicy.Decide(null!, true, [], null));
        Assert.Throws<ArgumentNullException>(() =>
            GameOverclockPolicy.Decide(UserModeTitle(), true, null!, null));
    }

    [Fact]
    public void AnUnknownCandidateSaysSoAndAKnownOneDoesNot()
    {
        Assert.False(GameCandidate.Unknown("x.exe").IsKnown);
        Assert.True(UserModeTitle().IsKnown);
    }

    [Fact]
    public void OnlyReadyCarriesAnActionableFlag_AndEveryOtherStateIsReachable()
    {
        var profile = Profile(FortniteId);
        var cases = new (GameCandidate Candidate, bool ScanOk, string[] Found, GameOverclockProfile? Profile, GameProfileState Expected)[]
        {
            (UserModeTitle(), true, [], profile, GameProfileState.Ready),
            (UserModeTitle(), true, [], null, GameProfileState.NoValidatedProfile),
            (UserModeTitle(), true, ["Vanguard"], profile, GameProfileState.BlockedByAntiCheat),
            (KernelTitle(), true, [], profile, GameProfileState.BlockedByAntiCheat),
            (UserModeTitle(), false, [], profile, GameProfileState.ScanUnavailable),
            (GameCandidate.Unknown("mystery.exe"), true, [], profile, GameProfileState.UnknownGame),
        };

        foreach (var (candidate, scanOk, found, candidateProfile, expected) in cases)
        {
            var decision = GameOverclockPolicy.Decide(candidate, scanOk, found, candidateProfile);

            Assert.Equal(expected, decision.State);
            Assert.Equal(expected == GameProfileState.Ready, decision.IsActionable);
        }
    }

    private static GameCandidate KernelTitle() =>
        new(ValorantId, "VALORANT", "valorant-win64-shipping.exe", GameAntiCheatPolicy.KernelLevel);

    private static GameCandidate UserModeTitle() =>
        new(FortniteId, "Fortnite", "FortniteClient-Win64-Shipping.exe", GameAntiCheatPolicy.UserMode);

    private static GameOverclockProfile Profile(string gameId) =>
        new(gameId, "Test", OverclockProfile.Balanced, "preset_balanced", true,
            EvidenceLevel.Benchmark, ["Reduce the mouse polling floor only if you know it helps."]);
}