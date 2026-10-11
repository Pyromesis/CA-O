using CAO.Core.Overclock;
using CAO.Core.Overclock.Gaming;
using CAO.Infrastructure.Gaming;
using CAO.Infrastructure.Overclock.Gaming;
using CAO.Shared;
using Xunit;

namespace CAO.Infrastructure.Tests.Overclock;

/// <summary>
/// The gate itself is tested in CA-O.Core.Tests; what is exercised here is the wiring
/// around it: that a detected executable becomes a candidate the gate understands, and
/// that a scanner which was not allowed to look is never read as a scanner that found
/// nothing. Both fakes stand in for providers that are sealed and shared with the rest
/// of the app, so this runs without enumerating processes or reading the registry.
/// </summary>
public sealed class GameOverclockSourceTests
{
    private const string KernelExecutable = "valorant.exe";
    private const string UserModeExecutable = "fortniteclient-win64-shipping.exe";
    private const string UnknownExecutable = "algo-inexistente.exe";

    [Fact]
    public async Task AKnownExecutable_IsProjectedIntoItsCatalogEntry()
    {
        var source = Source([Detected(KernelExecutable)]);

        var snapshot = await Read(source);

        var candidate = Assert.Single(snapshot.Entries).Candidate;
        Assert.True(candidate.IsKnown);
        Assert.Equal("valorant", candidate.GameId);
        Assert.Equal(GameAntiCheatPolicy.KernelLevel, candidate.AntiCheatPolicy);
        Assert.Equal(KernelExecutable, candidate.ExecutableName);
    }

    [Fact]
    public async Task AnExecutableNobodyKnows_IsReportedAsUnknown_AndTheNameStaysVisible()
    {
        var source = Source([Detected(UnknownExecutable, "Juego sin catalogar")]);

        var entry = Assert.Single((await Read(source)).Entries);

        Assert.False(entry.Candidate.IsKnown);
        Assert.Equal(string.Empty, entry.Candidate.GameId);
        Assert.Equal(GameProfileState.UnknownGame, entry.Decision.State);
        Assert.False(entry.Decision.IsActionable);
    }

    [Fact]
    public async Task AValidatedProfile_OnlyReachesTheRow_WhenThatGameIsActuallyInstalled()
    {
        var source = Source([Detected(UserModeExecutable)], profiles: [Profile("valorant")]);

        var entry = Assert.Single((await Read(source)).Entries);

        Assert.Equal(GameProfileState.NoValidatedProfile, entry.Decision.State);
        Assert.Null(entry.Profile);
    }

    [Fact]
    public async Task ACleanScanAndAValidatedProfile_LeaveTheRowReady()
    {
        var source = Source([Detected(UserModeExecutable)], profiles: [Profile("fortnite")]);

        var entry = Assert.Single((await Read(source)).Entries);

        Assert.Equal(GameProfileState.Ready, entry.Decision.State);
        Assert.True(entry.Decision.IsActionable);
        Assert.Equal(GameOverclockPolicy.ReasonReady, entry.Decision.ReasonKey);
    }

    [Fact]
    public async Task AKernelTitle_StaysBlocked_EvenWithACleanScanAndAValidatedProfile()
    {
        var source = Source([Detected(KernelExecutable)], profiles: [Profile("valorant")]);

        var entry = Assert.Single((await Read(source)).Entries);

        Assert.Equal(GameProfileState.BlockedByAntiCheat, entry.Decision.State);
        Assert.Equal(GameOverclockPolicy.ReasonKernelAntiCheat, entry.Decision.ReasonKey);
        Assert.False(entry.Decision.IsActionable);
    }

    [Fact]
    public async Task AnAntiCheatThatIsActuallyFound_ClosesTheRow()
    {
        var source = Source([Detected(UserModeExecutable)], antiCheats: [AntiCheatKind.BattlEye], profiles: [Profile("fortnite")]);

        var snapshot = await Read(source);

        Assert.Equal(GameProfileState.BlockedByAntiCheat, Assert.Single(snapshot.Entries).Decision.State);
        Assert.Equal(["BattlEye"], snapshot.DetectedAntiCheats);
    }

    [Fact]
    public async Task AFailedScan_ClosesEveryRow_EvenThoughTheListOfFindingsIsEmpty()
    {
        var source = Source([Detected(UserModeExecutable)], scanSucceeded: false, profiles: [Profile("fortnite")]);

        var snapshot = await Read(source);

        Assert.False(snapshot.AntiCheatScanSucceeded);
        Assert.Empty(snapshot.DetectedAntiCheats);
        Assert.Equal(GameProfileState.ScanUnavailable, Assert.Single(snapshot.Entries).Decision.State);
    }

    [Fact]
    public async Task TheScanOutcome_TravelsWithTheSnapshot_SoTheSectionCanSayWhyItClosedARow()
    {
        var succeeded = await Read(Source([Detected(UserModeExecutable)], antiCheats: [AntiCheatKind.Vanguard]));
        var refused = await Read(Source([Detected(UserModeExecutable)], scanSucceeded: false));

        Assert.True(succeeded.AntiCheatScanSucceeded);
        Assert.False(refused.AntiCheatScanSucceeded);
    }

    [Fact]
    public async Task AFailingDetector_ReturnsNoRows_WithoutPretendingTheMachineIsClean()
    {
        var source = Source(detector: new StubDetector { ThrowOnDetect = true }, profiles: [Profile("fortnite")]);

        var snapshot = await Read(source);

        Assert.Empty(snapshot.Entries);
        Assert.True(snapshot.AntiCheatScanSucceeded);
    }

    [Fact]
    public async Task Cancellation_IsNotSwallowedAsAnEmptyMachine()
    {
        var source = Source(detector: new StubDetector { CancelOnDetect = true });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await Read(source));
    }

    [Fact]
    public async Task TheDefaultRepository_ReportsNoValidatedProfile_InsteadOfGuessingOne()
    {
        var source = new SystemGameOverclockSource(
            new StubDetector(Detected(UserModeExecutable)),
            new StubScanner { Succeeds = true },
            new EmptyGameProfileRepository());

        var entry = Assert.Single((await source.ReadAsync(CancellationToken.None)).Entries);

        Assert.Null(entry.Profile);
        Assert.Equal(GameProfileState.NoValidatedProfile, entry.Decision.State);
        Assert.False(entry.Decision.IsActionable);
    }

    [Fact]
    public void TheSection_RejectsMissingCollaborators()
    {
        var detector = new StubDetector();
        var scanner = new StubScanner();
        var repository = new StubRepository([]);

        Assert.Throws<ArgumentNullException>(() => new SystemGameOverclockSource(null!, scanner, repository));
        Assert.Throws<ArgumentNullException>(() => new SystemGameOverclockSource(detector, null!, repository));
        Assert.Throws<ArgumentNullException>(() => new SystemGameOverclockSource(detector, scanner, null!));
    }

    private static SystemGameOverclockSource Source(
        DetectedGame[]? games = null,
        bool scanSucceeded = true,
        AntiCheatKind[]? antiCheats = null,
        GameOverclockProfile[]? profiles = null,
        IGameDetector? detector = null) =>
        new(
            detector ?? new StubDetector(games ?? [Detected(UserModeExecutable)]),
            new StubScanner { Succeeds = scanSucceeded, Found = antiCheats ?? [] },
            new StubRepository(profiles ?? []));

    private static Task<GameOverclockSnapshot> Read(SystemGameOverclockSource source) =>
        source.ReadAsync(CancellationToken.None).AsTask();

    private static DetectedGame Detected(string executableName, string name = "Juego") =>
        new(name, executableName, "stub");

    private static GameOverclockProfile Profile(string gameId) =>
        new(gameId, gameId, OverclockProfile.Balanced, "preset_balanced", true, EvidenceLevel.Benchmark, ["Ajuste recomendado"]);

    private sealed class StubDetector : IGameDetector
    {
        private readonly DetectedGame[] _games;

        public StubDetector(params DetectedGame[] games) => _games = games;

        public bool ThrowOnDetect { get; init; }

        public bool CancelOnDetect { get; init; }

        public Task<IReadOnlyList<DetectedGame>> DetectAsync(CancellationToken ct = default)
        {
            if (CancelOnDetect)
            {
                throw new OperationCanceledException(ct);
            }

            if (ThrowOnDetect)
            {
                throw new InvalidOperationException("process enumeration failed");
            }

            return Task.FromResult<IReadOnlyList<DetectedGame>>(_games);
        }
    }

    private sealed class StubScanner : IAntiCheatScanner
    {
        public bool Succeeds { get; init; } = true;

        public IReadOnlyList<AntiCheatKind> Found { get; init; } = [];

        public bool TryScan(out IReadOnlyList<AntiCheatInfo> found)
        {
            found = [.. Found.Select(kind => new AntiCheatInfo(kind, "stub", ["stub"]))];
            return Succeeds;
        }
    }

    private sealed class StubRepository(IReadOnlyList<GameOverclockProfile> profiles) : IGameProfileRepository
    {
        public IReadOnlyList<GameOverclockProfile> List() => profiles;
    }
}