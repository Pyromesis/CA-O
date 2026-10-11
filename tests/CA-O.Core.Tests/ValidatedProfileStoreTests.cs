using CAO.Core.Overclock;
using CAO.Core.Overclock.Reporting;
using Xunit;

namespace CAO.Core.Tests;

/// <summary>Validated profiles: round-trip, upsert, quarantine. No hardware involved.</summary>
public sealed class ValidatedProfileStoreTests : IDisposable
{
    private readonly string _directory;

    public ValidatedProfileStoreTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "cao-validated-tests", Guid.NewGuid().ToString("D"));
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
    public async Task RoundTrip_KeepsEveryField()
    {
        var store = new FileValidatedProfileStore(_directory);
        var profile = new ValidatedOverclockProfile(
            new KnobId("amd.gpu.powerLimit"),
            OverclockComponent.Gpu,
            110d,
            115d,
            new DateTimeOffset(2026, 10, 11, 9, 0, 0, TimeSpan.Zero),
            "580.68",
            11);

        await store.SaveAsync(profile, CancellationToken.None);
        var listed = await store.ListAsync(CancellationToken.None);

        var back = Assert.Single(listed);
        Assert.Equal(profile.Knob, back.Knob);
        Assert.Equal(OverclockComponent.Gpu, back.Component);
        Assert.Equal(110d, back.StableValue);
        Assert.Equal(115d, back.StableCeiling);
        Assert.Equal(profile.ValidatedAtUtc, back.ValidatedAtUtc);
        Assert.Equal("580.68", back.DriverVersion);
        Assert.Equal(11, back.PointsPassed);
    }

    [Fact]
    public async Task SavingTwice_KeepsTheLatestRunInsteadOfHistory()
    {
        var store = new FileValidatedProfileStore(_directory);
        var knob = new KnobId("nvidia.gpu.powerLimit");
        await store.SaveAsync(
            new ValidatedOverclockProfile(knob, OverclockComponent.Gpu, 110d, null, DateTimeOffset.UtcNow, null, 5),
            CancellationToken.None);
        await store.SaveAsync(
            new ValidatedOverclockProfile(knob, OverclockComponent.Gpu, 115d, 118d, DateTimeOffset.UtcNow, null, 9),
            CancellationToken.None);

        var listed = await store.ListAsync(CancellationToken.None);

        var back = Assert.Single(listed);
        Assert.Equal(115d, back.StableValue);
        Assert.Equal(118d, back.StableCeiling);
    }

    [Fact]
    public async Task CorruptFile_IsQuarantinedAndNeverDeleted()
    {
        var store = new FileValidatedProfileStore(_directory);
        await File.WriteAllTextAsync(
            Path.Combine(_directory, "amd.gpu.powerLimit.json"), "{not json", CancellationToken.None);

        var listed = await store.ListAsync(CancellationToken.None);

        Assert.Empty(listed);
        Assert.Empty(Directory.GetFiles(_directory, "*.json"));
        Assert.Single(Directory.GetFiles(_directory, "*.json.corrupt-*-unparseable"));
    }

    [Fact]
    public async Task MissingDirectory_ListsNothingInsteadOfThrowing()
    {
        var store = new FileValidatedProfileStore(Path.Combine(_directory, "no-such-dir"));

        var listed = await store.ListAsync(CancellationToken.None);

        Assert.Empty(listed);
    }
}
