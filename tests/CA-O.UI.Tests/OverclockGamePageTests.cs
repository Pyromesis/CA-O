using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CAO.Core.Overclock;
using CAO.Core.Overclock.Gaming;
using CAO.Infrastructure.Overclock;
using CAO.Infrastructure.Overclock.Gaming;
using CAO.Shared;
using CAO.UI.ViewModels;
using Xunit;

namespace CAO.UI.Tests;

/// <summary>
/// The game section is the one place where the answer is "do not touch it". These
/// tests are about the row saying the right thing, not about a game being present.
/// </summary>
public sealed class OverclockGamePageTests
{
    [Fact]
    public async Task NoGamesInstalled_SaysSoAndLeavesNoRows()
    {
        var vm = ViewModel(Source());

        await Refresh(vm);

        Assert.Empty(vm.Games);
        Assert.Equal(Localizer.Get("overclock-games-none"), vm.GameSummary);
    }

    [Fact]
    public async Task AReadyRow_CarriesTheNameTheExecutableAndAnActionableFlag()
    {
        var vm = ViewModel(Source(Entry("fortnite", "Fortnite", GameProfileState.Ready, true)));

        await Refresh(vm);

        var row = Assert.Single(vm.Games);
        Assert.Equal("Fortnite", row.Name);
        Assert.Equal("fortniteclient-win64-shipping.exe", row.Executable);
        Assert.True(row.IsActionable);
    }

    [Fact]
    public async Task AReadyRow_NamesTheReasonInPlainWordsRatherThanTheKey()
    {
        var vm = ViewModel(Source(Entry("fortnite", "Fortnite", GameProfileState.Ready, true)));

        await Refresh(vm);

        var reason = Assert.Single(vm.Games).Reason;
        Assert.NotEmpty(reason);
        Assert.NotEqual("overclock-game-ready", reason);
    }

    [Fact]
    public async Task ABlockedRow_IsNeverActionable()
    {
        var vm = ViewModel(Source(Entry("valorant", "VALORANT", GameProfileState.BlockedByAntiCheat, false)));

        await Refresh(vm);

        var row = Assert.Single(vm.Games);
        Assert.False(row.IsActionable);
        Assert.NotEqual("overclock-game-anticheat-kernel", row.Reason);
    }

    [Fact]
    public async Task TheRowMirrorsWhateverTheSourceDecided()
    {
        var vm = ViewModel(Source(
            Entry("valorant", "VALORANT", GameProfileState.BlockedByAntiCheat, false),
            Entry("fortnite", "Fortnite", GameProfileState.Ready, true),
            Entry("cs2", "Counter-Strike 2", GameProfileState.ScanUnavailable, false)));

        await Refresh(vm);

        Assert.Equal(3, vm.Games.Count);
        Assert.Equal(
            [false, true, false],
            vm.Games.Select(r => r.IsActionable));
        Assert.All(vm.Games, r => Assert.NotEmpty(r.State));
    }

    [Fact]
    public async Task AnAntiCheatOnTheMachine_IsNamedInTheSummary()
    {
        var vm = ViewModel(Source([], scanSucceeded: true, antiCheats: ["BattlEye"]));

        await Refresh(vm);

        Assert.Contains("BattlEye", vm.GameSummary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnAntiCheatOnTheMachine_IsNamedEvenWhenNothingIsRunning()
    {
        // The anti-cheat lives in the service registry, not in the process list. Hiding
        // it behind "no games running" would read as a clean machine.
        var vm = ViewModel(Source([], scanSucceeded: true, antiCheats: ["Vanguard"]));

        await Refresh(vm);

        Assert.Contains("Vanguard", vm.GameSummary, StringComparison.Ordinal);
        Assert.Empty(vm.Games);
    }

    [Fact]
    public async Task ACleanMachine_SaysHowManyGamesItFound()
    {
        var vm = ViewModel(Source(
            Entry("fortnite", "Fortnite", GameProfileState.Ready, true),
            Entry("destiny-2", "Destiny 2", GameProfileState.NoValidatedProfile, false)));

        await Refresh(vm);

        Assert.Contains("2", vm.GameSummary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AFailureScanningGames_SaysSoAndLeavesNoRows()
    {
        var vm = ViewModel(new ThrowingGameSource());

        await Refresh(vm);

        Assert.Equal(Localizer.Get("overclock-games-scan-failed"), vm.GameSummary);
        Assert.Empty(vm.Games);
    }

    [Fact]
    public async Task AnEmptyRowList_StaysReportedAsNoGames()
    {
        // The scan that failed here is the service registry, not the process list. The
        // row list came back because the games themselves were looked for and none were
        // running, so "no games installed" is still the truth and is what it must say.
        var vm = ViewModel(Source([], scanSucceeded: false, antiCheats: []));

        await Refresh(vm);

        Assert.Equal(Localizer.Get("overclock-games-none"), vm.GameSummary);
        Assert.Empty(vm.Games);
    }

    [Fact]
    public async Task Cancellation_IsNotReportedAsAFailedScan()
    {
        var vm = ViewModel(new CancellingGameSource());

        await Refresh(vm);

        Assert.NotEqual(Localizer.Get("overclock-games-scan-failed"), vm.GameSummary);
        Assert.Equal(Localizer.Get("overclock_cancelled"), vm.StatusLine);
    }

    [Fact]
    public void ThePage_WiresTheGameSection()
    {
        var xaml = ReadRepoFile("src", "CA-O.UI", "OverclockPage.xaml");

        Assert.Contains("ViewModel.GameSummary", xaml, StringComparison.Ordinal);
        Assert.Contains("ViewModel.Games", xaml, StringComparison.Ordinal);
        Assert.Contains("OverclockGameRow", xaml, StringComparison.Ordinal);
        Assert.Contains("{x:Bind Reason}", xaml, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("overclock-games-none")]
    [InlineData("overclock-games-count")]
    [InlineData("overclock-games-anti-cheats")]
    [InlineData("overclock-games-scan-failed")]
    [InlineData("overclock-games-state-blocked")]
    [InlineData("overclock-games-state-no-profile")]
    [InlineData("overclock-games-state-unknown")]
    [InlineData("overclock-games-state-unscanable")]
    [InlineData("overclock-game-ready")]
    [InlineData("overclock-game-unknown")]
    [InlineData("overclock-game-no-profile")]
    [InlineData("overclock-game-anticheat-kernel")]
    [InlineData("overclock-game-anticheat-detected")]
    [InlineData("overclock-game-anticheat-scan-unavailable")]
    public void EveryGameKey_IsTranslatedInBothLanguages(string key)
    {
        var occurrences = ReadRepoFile("src", "CA-O.UI", "Resources", "Localizer.cs")
            .Split('\n')
            .Count(line => line.TrimStart().StartsWith($"[\"{key}\"]", StringComparison.Ordinal));

        Assert.True(occurrences >= 2, $"'{key}' appears {occurrences} time(s); a key with one entry shows its raw form in half the app.");
    }

    private static async Task Refresh(OverclockViewModel vm) =>
        await vm.RefreshCommand.ExecuteAsync(null);

    private static OverclockViewModel ViewModel(IGameOverclockSource source) =>
        OverclockTestHarness.Create(new OverclockReadOnlyRegistry(), [], gameSource: source);

    private static StubGameSource Source(
        params GameOverclockEntry[] entries) =>
        new(true, [], entries);

    private static StubGameSource Source(
        IReadOnlyList<GameOverclockEntry> entries,
        bool scanSucceeded,
        IReadOnlyList<string> antiCheats) =>
        new(scanSucceeded, antiCheats, entries);

    private static GameOverclockEntry Entry(
        string gameId,
        string displayName,
        GameProfileState state,
        bool actionable)
    {
        var candidate = new GameCandidate(
            gameId,
            displayName,
            ExecutableFor(gameId),
            GameAntiCheatPolicy.UserMode);
        var decision = new GameProfileDecision(state, ReasonFor(state), actionable, []);
        return new GameOverclockEntry(candidate, null, decision);
    }

    private static string ExecutableFor(string gameId) => gameId switch
    {
        "valorant" => "valorant.exe",
        "cs2" => "cs2.exe",
        "fortnite" => "fortniteclient-win64-shipping.exe",
        "destiny-2" => "destiny2.exe",
        _ => gameId + ".exe",
    };

    private static string ReasonFor(GameProfileState state) => state switch
    {
        GameProfileState.Ready => GameOverclockPolicy.ReasonReady,
        GameProfileState.BlockedByAntiCheat => GameOverclockPolicy.ReasonKernelAntiCheat,
        GameProfileState.NoValidatedProfile => GameOverclockPolicy.ReasonNoProfile,
        GameProfileState.UnknownGame => GameOverclockPolicy.ReasonUnknownGame,
        _ => GameOverclockPolicy.ReasonScanUnavailable,
    };

    private static string ReadRepoFile(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CA-O.sln")))
        {
            dir = dir.Parent;
        }

        if (dir is null)
        {
            throw new InvalidOperationException("CA-O.sln not found above the test output directory.");
        }

        return File.ReadAllText(Path.Combine(new[] { dir.FullName }.Concat(parts).ToArray()));
    }

    private sealed class StubGameSource(
        bool scanSucceeded,
        IReadOnlyList<string> antiCheats,
        IReadOnlyList<GameOverclockEntry> entries) : IGameOverclockSource
    {
        public ValueTask<GameOverclockSnapshot> ReadAsync(CancellationToken ct) =>
            new(new GameOverclockSnapshot(scanSucceeded, antiCheats, entries));
    }

    private sealed class ThrowingGameSource : IGameOverclockSource
    {
        public ValueTask<GameOverclockSnapshot> ReadAsync(CancellationToken ct) =>
            throw new InvalidOperationException("the process list refused to be enumerated");
    }

    private sealed class CancellingGameSource : IGameOverclockSource
    {
        public ValueTask<GameOverclockSnapshot> ReadAsync(CancellationToken ct) =>
            throw new OperationCanceledException(ct);
    }
}
