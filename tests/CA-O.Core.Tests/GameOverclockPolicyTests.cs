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
    public void AKernelProtectedTitle_IsBlocked_ForAKernelDriverProfile_EvenWhenTheScanComesBackClean()
    {
        var decision = GameOverclockPolicy.Decide(
            KernelTitle(),
            antiCheatScanSucceeded: true,
            detectedAntiCheats: [],
            profile: Profile(ValorantId),
            mechanism: TuningMechanism.KernelDriver);

        Assert.Equal(GameProfileState.BlockedByAntiCheat, decision.State);
        Assert.Equal(GameOverclockPolicy.ReasonKernelAntiCheat, decision.ReasonKey);
        Assert.False(decision.IsActionable);
    }

    [Fact]
    public void AKernelDriverProfile_IsBlocked_WhenAnAntiCheatIsActuallyFound()
    {
        var decision = GameOverclockPolicy.Decide(
            UserModeTitle(),
            antiCheatScanSucceeded: true,
            detectedAntiCheats: ["Vanguard"],
            profile: Profile(FortniteId),
            mechanism: TuningMechanism.KernelDriver);

        Assert.Equal(GameProfileState.BlockedByAntiCheat, decision.State);
        Assert.Equal(GameOverclockPolicy.ReasonAntiCheatDetected, decision.ReasonKey);
        Assert.False(decision.IsActionable);
    }

    [Fact]
    public void AFailedScan_ClosesAKernelDriverRow_EvenThoughTheListIsEmpty()
    {
        // This is the whole reason the scan reports whether it managed to look. An
        // empty list here means "not allowed to check", not "nothing installed".
        var decision = GameOverclockPolicy.Decide(
            UserModeTitle(),
            antiCheatScanSucceeded: false,
            detectedAntiCheats: [],
            profile: Profile(FortniteId),
            mechanism: TuningMechanism.KernelDriver);

        Assert.Equal(GameProfileState.ScanUnavailable, decision.State);
        Assert.Equal(GameOverclockPolicy.ReasonScanUnavailable, decision.ReasonKey);
        Assert.False(decision.IsActionable);
    }

    [Fact]
    public void AnEmptyScanResult_WithAFailedScan_NeverReadsAsACleanMachine()
    {
        var closed = GameOverclockPolicy.Decide(UserModeTitle(), false, [], Profile(FortniteId), TuningMechanism.KernelDriver);
        var open = GameOverclockPolicy.Decide(UserModeTitle(), true, [], Profile(FortniteId), TuningMechanism.KernelDriver);

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
            GameOverclockPolicy.Decide(KernelTitle(), true, [], Profile(ValorantId), TuningMechanism.KernelDriver),
            GameOverclockPolicy.Decide(KernelTitle(), true, [], null),
            GameOverclockPolicy.Decide(UserModeTitle(), false, [], Profile(FortniteId), TuningMechanism.KernelDriver),
            GameOverclockPolicy.Decide(UserModeTitle(), true, ["BattlEye"], Profile(FortniteId), TuningMechanism.KernelDriver),
            GameOverclockPolicy.Decide(KernelTitle(), true, ["Vanguard"], Profile(ValorantId)),
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
        const TuningMechanism api = TuningMechanism.VendorDriverApi;
        const TuningMechanism kernel = TuningMechanism.KernelDriver;
        var cases = new (GameCandidate Candidate, bool ScanOk, string[] Found, GameOverclockProfile? Profile, TuningMechanism Mechanism, GameProfileState Expected)[]
        {
            (UserModeTitle(), true, [], profile, api, GameProfileState.Ready),
            (UserModeTitle(), true, [], null, api, GameProfileState.NoValidatedProfile),
            (UserModeTitle(), true, ["Vanguard"], profile, kernel, GameProfileState.BlockedByAntiCheat),
            (KernelTitle(), true, [], profile, kernel, GameProfileState.BlockedByAntiCheat),
            (UserModeTitle(), false, [], profile, kernel, GameProfileState.ScanUnavailable),
            (GameCandidate.Unknown("mystery.exe"), true, [], profile, api, GameProfileState.UnknownGame),
        };

        foreach (var (candidate, scanOk, found, candidateProfile, mechanism, expected) in cases)
        {
            var decision = GameOverclockPolicy.Decide(candidate, scanOk, found, candidateProfile, mechanism);

            Assert.Equal(expected, decision.State);
            Assert.Equal(expected == GameProfileState.Ready, decision.IsActionable);
        }
    }

    [Fact]
    public void ValorantAndFortnite_WithVanguardAndEac_AreReady_ForVendorApiProfiles()
    {
        // Lo que CA-O hace de verdad: API del driver de NVIDIA/AMD y plan de energia.
        // Ni driver propio, ni acceso al proceso del juego: el anti-cheat no lo ve.
        foreach (var title in new[] { KernelTitle(), UserModeTitle() })
        {
            var decision = GameOverclockPolicy.Decide(
                title, antiCheatScanSucceeded: true, ["Vanguard", "EasyAntiCheat"], Profile(title.GameId));

            Assert.Equal(GameProfileState.Ready, decision.State);
            Assert.Equal(GameOverclockPolicy.ReasonReadyAntiCheatSafe, decision.ReasonKey);
            Assert.True(decision.IsActionable);
        }
    }

    [Fact]
    public void AFailedScan_DoesNotCloseAVendorApiProfile()
    {
        var decision = GameOverclockPolicy.Decide(UserModeTitle(), antiCheatScanSucceeded: false, [], Profile(FortniteId));

        Assert.Equal(GameProfileState.Ready, decision.State);
    }

    [Theory]
    [InlineData("nvidia-nvapi", TuningMechanism.VendorDriverApi)]
    [InlineData("amd-adlx", TuningMechanism.VendorDriverApi)]
    [InlineData("intel-policy", TuningMechanism.WindowsPowerPolicy)]
    [InlineData("smbios-memory", TuningMechanism.FirmwareReadOnly)]
    [InlineData("msr-signed", TuningMechanism.KernelDriver)]
    [InlineData("something-new", TuningMechanism.KernelDriver)]
    public void EveryShippedBackend_IsClassified_AndUnknownOnesAreTreatedAsKernel(string backendId, TuningMechanism expected) =>
        Assert.Equal(expected, OverclockAntiCheatGate.MechanismFor(backendId));

    [Theory]
    [InlineData(TuningMechanism.VendorDriverApi, true)]
    [InlineData(TuningMechanism.WindowsPowerPolicy, true)]
    [InlineData(TuningMechanism.FirmwareReadOnly, true)]
    [InlineData(TuningMechanism.KernelDriver, false)]
    public void WithVanguardInstalled_OnlyAKernelDriverIsBlocked(TuningMechanism mechanism, bool allowed) =>
        Assert.Equal(allowed, OverclockAntiCheatGate.Decide(mechanism, true, ["Vanguard"]).Allowed);

    private static GameCandidate KernelTitle() =>
        new(ValorantId, "VALORANT", "valorant-win64-shipping.exe", GameAntiCheatPolicy.KernelLevel);

    private static GameCandidate UserModeTitle() =>
        new(FortniteId, "Fortnite", "FortniteClient-Win64-Shipping.exe", GameAntiCheatPolicy.UserMode);

    private static GameOverclockProfile Profile(string gameId) =>
        new(gameId, "Test", OverclockProfile.Balanced, "preset_balanced", true,
            EvidenceLevel.Benchmark, ["Reduce the mouse polling floor only if you know it helps."]);
}