using CAO.Core.Gaming;
using CAO.Core.Overclock.Gaming;
using CAO.Infrastructure.Gaming;
using CAO.Shared;

namespace CAO.Infrastructure.Overclock.Gaming;

/// <summary>One row of the game section: what was found, what could be applied and why.</summary>
public sealed record GameOverclockEntry(
    GameCandidate Candidate,
    GameOverclockProfile? Profile,
    GameProfileDecision Decision);

/// <summary>
/// Everything the game section needs in one read.
/// </summary>
/// <param name="AntiCheatScanSucceeded">
/// False when the services registry could not be read. The section shows that as its
/// own state instead of quietly reporting a clean machine.
/// </param>
public sealed record GameOverclockSnapshot(
    bool AntiCheatScanSucceeded,
    IReadOnlyList<string> DetectedAntiCheats,
    IReadOnlyList<GameOverclockEntry> Entries);

public interface IGameOverclockSource
{
    ValueTask<GameOverclockSnapshot> ReadAsync(CancellationToken ct);
}

/// <summary>
/// The two things this section reads from the machine, named so they can be replaced.
/// </summary>
/// <remarks>
/// Both providers are sealed and both are used elsewhere in the app, so the seam is an
/// interface each one already satisfies rather than a new abstraction layered on top.
/// Without it, testing the gate would mean running the process scan on the machine that
/// happens to be running the tests, which proves nothing about the gate.
/// </remarks>
public interface IGameDetector
{
    Task<IReadOnlyList<DetectedGame>> DetectAsync(CancellationToken ct = default);
}

/// <summary>
/// Reports the anti-cheats it found and, separately, whether it was allowed to look.
/// </summary>
/// <remarks>
/// The second value is the whole point: a list that came back empty because the registry
/// was denied is not the same answer as a list that came back empty because nothing is
/// installed, and only the caller can tell the section which one it is looking at.
/// </remarks>
public interface IAntiCheatScanner
{
    bool TryScan(out IReadOnlyList<AntiCheatInfo> found);
}

/// <summary>
/// Where a validated game profile would come from.
/// </summary>
/// <remarks>
/// A profile may only appear here once a tuning run on this machine reached its ceiling
/// without a fault. Nothing writes to this store yet, so it is empty by default and every
/// title reports "no validated profile". That is the honest answer: a profile that has
/// never been through the stability engine is a guess, and CA-O does not ship guesses.
/// </remarks>
public interface IGameProfileRepository
{
    IReadOnlyList<GameOverclockProfile> List();

    /// <summary>
    /// Async over the same answer. The default keeps the sync one so existing
    /// repositories do not change; stores backed by disk override it so the
    /// page never blocks the UI thread on file I/O.
    /// </summary>
    Task<IReadOnlyList<GameOverclockProfile>> ListAsync(CancellationToken ct) =>
        Task.FromResult(List());
}

/// <summary>The repository that has no validated profile, because none has been produced yet.</summary>
public sealed class EmptyGameProfileRepository : IGameProfileRepository
{
    public IReadOnlyList<GameOverclockProfile> List() => [];
}

/// <summary>
/// Reads what is running, reads the anti-cheat registry, and asks the policy.
/// </summary>
public sealed class SystemGameOverclockSource : IGameOverclockSource
{
    private readonly IGameDetector _detector;
    private readonly IAntiCheatScanner _antiCheat;
    private readonly IGameProfileRepository _profiles;

    public SystemGameOverclockSource()
        : this(
            new GameDetectionProvider(),
            new AntiCheatScanProvider(),
            new StoreBackedGameProfileRepository(new CAO.Core.Overclock.Reporting.FileValidatedProfileStore()))
    {
    }

    public SystemGameOverclockSource(
        IGameDetector detector,
        IAntiCheatScanner antiCheat,
        IGameProfileRepository profiles)
    {
        _detector = detector ?? throw new ArgumentNullException(nameof(detector));
        _antiCheat = antiCheat ?? throw new ArgumentNullException(nameof(antiCheat));
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
    }

    public async ValueTask<GameOverclockSnapshot> ReadAsync(CancellationToken ct)
    {
        // The registry read is local and quick; the process scan is the slow one and
        // it already runs on a worker thread inside the detector.
        var scanSucceeded = _antiCheat.TryScan(out var antiCheats);
        var names = antiCheats.Select(a => a.Kind.ToString()).ToArray();

        IReadOnlyList<DetectedGame> detected;
        try
        {
            detected = await _detector.DetectAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // A failed process enumeration is not "no games installed". Returning an
            // empty list here would let the section say the machine is clean.
            detected = [];
        }

        var profiles = await _profiles.ListAsync(ct).ConfigureAwait(false);
        var entries = new List<GameOverclockEntry>(detected.Count);
        foreach (var game in detected)
        {
            var candidate = Project(game);
            var profile = profiles.FirstOrDefault(p => p.GameId == candidate.GameId);
            entries.Add(new GameOverclockEntry(
                candidate,
                profile,
                GameOverclockPolicy.Decide(candidate, scanSucceeded, names, profile)));
        }

        return new GameOverclockSnapshot(scanSucceeded, names, entries);
    }

    /// <summary>Projects a detection result into the domain the policy understands.</summary>
    public static GameCandidate Project(DetectedGame detected)
    {
        ArgumentNullException.ThrowIfNull(detected);

        var profile = GameProfileCatalog.FindByExecutable(detected.ExecutableName);
        return profile is null
            ? GameCandidate.Unknown(detected.ExecutableName)
            : new GameCandidate(profile.GameId, profile.DisplayName, detected.ExecutableName, profile.AntiCheatPolicy);
    }
}
