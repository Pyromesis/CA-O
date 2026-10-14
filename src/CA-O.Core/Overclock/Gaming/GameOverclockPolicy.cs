using CAO.Shared;

namespace CAO.Core.Overclock.Gaming;

/// <summary>
/// How a game row can end up. Only <see cref="Ready"/> can be applied, and every other
/// state carries the reason why it cannot.
/// </summary>
public enum GameProfileState
{
    /// <summary>
    /// The title is known, a validated overclock profile references it and the
    /// anti-cheat gate is open. Nothing was inferred to get here.
    /// </summary>
    Ready,

    /// <summary>
    /// The profile would need a kernel driver of CA-O's own on a machine an anti-cheat
    /// protects. Vendor-API and power-policy profiles never land here.
    /// </summary>
    BlockedByAntiCheat,

    /// <summary>
    /// The title is known but no validated overclock profile references it yet. That is
    /// different from being blocked: nothing forbids it, there is simply nothing to apply.
    /// </summary>
    NoValidatedProfile,

    /// <summary>The executable matches nothing in the catalog.</summary>
    UnknownGame,

    /// <summary>
    /// The profile needs a kernel driver and the anti-cheat registry could not be read,
    /// so the absence of an anti-cheat cannot be claimed and the row stays closed.
    /// </summary>
    ScanUnavailable,
}

/// <summary>
/// A detected executable as the domain sees it. Core cannot reference the detection
/// provider's own record, so the adapter projects into this one.
/// </summary>
/// <param name="GameId">
/// Empty when the executable is not in the catalog. That is the only way this type
/// says "unknown", so the state cannot disagree with the data.
/// </param>
public sealed record GameCandidate(
    string GameId,
    string DisplayName,
    string ExecutableName,
    GameAntiCheatPolicy AntiCheatPolicy)
{
    /// <summary>An executable the catalog does not know.</summary>
    public static GameCandidate Unknown(string executableName) =>
        new(string.Empty, executableName, executableName, GameAntiCheatPolicy.None);

    public bool IsKnown => GameId.Length > 0;
}

/// <summary>
/// The link between a game and a tuning profile that has already been validated on this
/// machine. It references a profile; it does not define one.
/// </summary>
/// <remarks>
/// There is deliberately no launch options field and no process priority field. Both were
/// considered and both were removed: injecting launch options into someone else's
/// process is the exact behaviour anti-cheat exists to detect, and a profile that needs it
/// is a profile CA-O will not ship.
/// </remarks>
public sealed record GameOverclockProfile(
    string GameId,
    string DisplayName,
    OverclockProfile Profile,
    string ProfileLabelKey,
    bool PreferHighPerformanceGpu,
    EvidenceLevel GpuPreferenceEvidence,
    IReadOnlyList<string> GuidanceEs);

/// <summary>The outcome of the gate for one game.</summary>
public sealed record GameProfileDecision(
    GameProfileState State,
    string ReasonKey,
    bool IsActionable,
    IReadOnlyList<string> GuidanceEs);

/// <summary>
/// Decides whether a game may carry a tuning profile.
/// </summary>
public static class GameOverclockPolicy
{
    public const string ReasonReady = "overclock-game-ready";
    public const string ReasonReadyAntiCheatSafe = "overclock-game-ready-anticheat-safe";
    public const string ReasonUnknownGame = "overclock-game-unknown";
    public const string ReasonNoProfile = "overclock-game-no-profile";
    public const string ReasonKernelAntiCheat = "overclock-game-anticheat-kernel";
    public const string ReasonAntiCheatDetected = "overclock-game-anticheat-detected";
    public const string ReasonScanUnavailable = "overclock-game-anticheat-scan-unavailable";

    /// <summary>
    /// Decides one row.
    /// </summary>
    /// <param name="candidate">The detected executable, already projected into the domain.</param>
    /// <param name="antiCheatScanSucceeded">
    /// Whether the services registry could actually be read. False means an empty list is
    /// ignorance, not a clean machine, and the row stays closed.
    /// </param>
    /// <param name="detectedAntiCheats">Names of the anti-cheats found, empty when none were.</param>
    /// <param name="profile">The validated profile for this title, or null when there is none.</param>
    /// <param name="mechanism">
    /// How the profile reaches the hardware. Every backend CA-O ships is a vendor API
    /// or a power policy, which anti-cheats do not watch; only a kernel driver closes
    /// the row (see <see cref="OverclockAntiCheatGate"/>).
    /// </param>
    public static GameProfileDecision Decide(
        GameCandidate candidate,
        bool antiCheatScanSucceeded,
        IReadOnlyList<string> detectedAntiCheats,
        GameOverclockProfile? profile,
        TuningMechanism mechanism = TuningMechanism.VendorDriverApi)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(detectedAntiCheats);

        if (!candidate.IsKnown)
        {
            return Closed(GameProfileState.UnknownGame, ReasonUnknownGame);
        }

        if (profile is null)
        {
            return Closed(GameProfileState.NoValidatedProfile, ReasonNoProfile);
        }

        if (mechanism == TuningMechanism.KernelDriver && candidate.AntiCheatPolicy == GameAntiCheatPolicy.KernelLevel)
        {
            // The catalog documents this title as kernel protected. That is a vendor
            // statement about the game, not a reading of this machine, so it holds even
            // when the scan came back clean - but only against a kernel driver.
            return Closed(GameProfileState.BlockedByAntiCheat, ReasonKernelAntiCheat, profile);
        }

        var gate = OverclockAntiCheatGate.Decide(mechanism, antiCheatScanSucceeded, detectedAntiCheats);
        if (!gate.Allowed)
        {
            return gate.ReasonKey == OverclockAntiCheatGate.ReasonKernelDriverScanUnavailable
                ? Closed(GameProfileState.ScanUnavailable, ReasonScanUnavailable, profile)
                : Closed(GameProfileState.BlockedByAntiCheat, ReasonAntiCheatDetected, profile);
        }

        if (detectedAntiCheats.Count > 0 || candidate.AntiCheatPolicy == GameAntiCheatPolicy.KernelLevel)
        {
            // Open, and the row says why it is safe instead of leaving the user to guess.
            return new GameProfileDecision(GameProfileState.Ready, ReasonReadyAntiCheatSafe, IsActionable: true, profile.GuidanceEs);
        }

        return new GameProfileDecision(GameProfileState.Ready, ReasonReady, IsActionable: true, profile.GuidanceEs);
    }

    private static GameProfileDecision Closed(
        GameProfileState state,
        string reasonKey,
        GameOverclockProfile? profile = null) =>
        new(state, reasonKey, IsActionable: false, profile?.GuidanceEs ?? []);
}
