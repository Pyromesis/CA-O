using CAO.Core.Overclock;
using CAO.Core.Overclock.Gaming;
using CAO.Core.Overclock.Reporting;
using CAO.Core.Overclock.Stability;
using CAO.Infrastructure.Overclock;
using CAO.Infrastructure.Overclock.Gaming;
using CAO.Infrastructure.Persistence;
using CAO.Shared;
using Xunit;

namespace CAO.Infrastructure.Tests.Overclock;

/// <summary>F7 support: journal adapter, AMD ceilings, validated-profile game repository.</summary>
// Same collection as TransactionJournalRetentionTests (same reason: the shared ParsePasses counter).
[Collection("FileTransactionJournal")]
public sealed class TuningSupportTests : IDisposable
{
    private readonly string _directory;

    public TuningSupportTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "cao-tuning-support", Guid.NewGuid().ToString("D"));
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public async Task Journal_WritesNonTerminalPhases_UnderTheRunId()
    {
        var runId = Guid.NewGuid();
        var journal = new TransactionOverclockJournal(new FileTransactionJournal(_directory), runId);

        await journal.WriteAsync(TransactionPhase.OverclockStarted, $"run={runId:D}", CancellationToken.None);
        await journal.WriteAsync(TransactionPhase.OverclockStepApplied, "amd.gpu.powerLimit", CancellationToken.None);

        var loaded = new FileTransactionJournal(_directory).LoadAll();
        var entry = Assert.Single(loaded);
        Assert.Equal(runId, entry.TransactionId);
        Assert.Equal("overclock-tune", TransactionOverclockJournal.OptimizationId);
        Assert.Equal(2, entry.Events.Count);
        Assert.All(entry.Events, e => Assert.False(e.Terminal));
    }

    [Fact]
    public async Task AmdCeilings_ReportTheDriverRange_InPercent()
    {
        var source = new AmdTuningCeilingSource(new FakeAmdCeilingApi(
            true, new AmdTuningDevice(null, 90, 120, 100)));

        var ceilings = await source.ReadAsync(CancellationToken.None).AsTask();

        var ceiling = Assert.Single(ceilings);
        Assert.Equal(AmdKnobs.PowerLimit, ceiling.Knob);
        Assert.Equal(90d, ceiling.Min);
        Assert.Equal(120d, ceiling.Max);
        Assert.Equal("overclock-unit-percent", ceiling.Unit);
        Assert.Null(ceiling.ReasonKey);
    }

    [Fact]
    public async Task AmdCeilings_KeepTheRowWithAReason_WhenTheDriverDoesNotAnswer()
    {
        var source = new AmdTuningCeilingSource(new FakeAmdCeilingApi(true, null));

        var ceilings = await source.ReadAsync(CancellationToken.None).AsTask();

        var ceiling = Assert.Single(ceilings);
        Assert.Null(ceiling.Min);
        Assert.Null(ceiling.Max);
        Assert.False(string.IsNullOrWhiteSpace(ceiling.ReasonKey));
    }

    [Fact]
    public async Task AmdCeilings_AreEmpty_WhenThereIsNoWritePath()
    {
        var source = new AmdTuningCeilingSource(new FakeAmdCeilingApi(false, null));

        var ceilings = await source.ReadAsync(CancellationToken.None).AsTask();

        Assert.Empty(ceilings);
    }

    [Fact]
    public async Task GameRepository_OffersNothing_UntilARunValidatesAGpuAnswer()
    {
        var profiles = new FileValidatedProfileStore(Path.Combine(_directory, "profiles"));
        var repository = new StoreBackedGameProfileRepository(profiles);

        Assert.Empty(await repository.ListAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GameRepository_OffersEveryKnownTitle_OnceAGpuAnswerExists()
    {
        var profiles = new FileValidatedProfileStore(Path.Combine(_directory, "profiles"));
        await profiles.SaveAsync(
            new ValidatedOverclockProfile(
                new KnobId("amd.gpu.powerLimit"), OverclockComponent.Gpu, 110d, 115d,
                DateTimeOffset.UtcNow, null, 11),
            CancellationToken.None);
        var repository = new StoreBackedGameProfileRepository(profiles);

        var listed = await repository.ListAsync(CancellationToken.None);

        Assert.Equal(CAO.Core.Gaming.GameProfileCatalog.All.Count, listed.Count);
        Assert.All(listed, p =>
        {
            Assert.Equal(OverclockProfile.Custom, p.Profile);
            Assert.True(p.PreferHighPerformanceGpu);
        });
    }

    [Fact]
    public async Task GameRepository_IgnoresNonGpuAnswers()
    {
        var profiles = new FileValidatedProfileStore(Path.Combine(_directory, "profiles"));
        await profiles.SaveAsync(
            new ValidatedOverclockProfile(
                new KnobId("cpu.policy.boostMode"), OverclockComponent.Cpu, 2d, null,
                DateTimeOffset.UtcNow, null, 3),
            CancellationToken.None);
        var repository = new StoreBackedGameProfileRepository(profiles);

        Assert.Empty(await repository.ListAsync(CancellationToken.None));
    }

    private sealed class FakeAmdCeilingApi(bool available, AmdTuningDevice? described) : IAmdTuningApi
    {
        public bool IsAvailable => available;

        public string? UnavailableReasonKey => available ? null : "overclock-adlx-missing";

        public bool TryDescribe(out AmdTuningDevice? device)
        {
            device = described;
            return device is not null;
        }

        public int? ReadPowerLimitPercent() => described?.DefaultPowerLimitPercent;

        public int SetPowerLimitPercent(int percent) => 0;
    }
}
