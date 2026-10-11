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
    /// The catalog documents a kernel-level anti-cheat for this title, or an anti-cheat
    /// was found installed. CA-O does not touch a machine that a game protects this way.
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
    /// The anti-cheat services registry could not be read, so the absence of an
    /// anti-cheat cannot be claimed and the row stays closed.
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
    public static GameProfileDecision Decide(
        GameCandidate candidate,
        bool antiCheatScanSucceeded,
        IReadOnlyList<string> detectedAntiCheats,
        GameOverclockProfile? profile)
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

        if (candidate.AntiCheatPolicy == GameAntiCheatPolicy.KernelLevel)
        {
            // The catalog documents this title as kernel protected. That is a vendor
            // statement about the game, not a reading of this machine, so it holds even
            // when the scan came back clean.
            return Closed(GameProfileState.BlockedByAntiCheat, ReasonKernelAntiCheat, profile);
        }

        if (!antiCheatScanSucceeded)
        {
            return Closed(GameProfileState.ScanUnavailable, ReasonScanUnavailable, profile);
        }

        if (detectedAntiCheats.Count > 0)
        {
            return Closed(GameProfileState.BlockedByAntiCheat, ReasonAntiCheatDetected, profile);
        }

        return new GameProfileDecision(GameProfileState.Ready, ReasonReady, IsActionable: true, profile.GuidanceEs);
    }

    private static GameProfileDecision Closed(
        GameProfileState state,
        string reasonKey,
        GameOverclockProfile? profile = null) =>
        new(state, reasonKey, IsActionable: false, profile?.GuidanceEs ?? []);
}
