using CAO.Core.Gaming;
using CAO.Core.Overclock;
using CAO.Core.Overclock.Gaming;
using CAO.Core.Overclock.Reporting;
using CAO.Shared;

namespace CAO.Infrastructure.Overclock.Gaming;

/// <summary>
/// Game profiles built from tuning runs on this machine. A validated GPU answer
/// unlocks one profile per known title, referencing the Custom preset: the value
/// was measured here, not taken from a vendor slide. No validated answer means
/// no profile, which is why every row says "no validated profile" on a fresh
/// machine instead of offering a number nobody ran.
/// </summary>
public sealed class StoreBackedGameProfileRepository : IGameProfileRepository
{
    public const string CustomPresetLabelKey = "preset_custom";

    private readonly IValidatedProfileStore _profiles;

    public StoreBackedGameProfileRepository(IValidatedProfileStore profiles) =>
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));

    public IReadOnlyList<GameOverclockProfile> List() =>
        ListAsync(CancellationToken.None).GetAwaiter().GetResult();

    public async Task<IReadOnlyList<GameOverclockProfile>> ListAsync(CancellationToken ct)
    {
        IReadOnlyList<ValidatedOverclockProfile> validated;
        try
        {
            validated = await _profiles.ListAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // An unreadable profile store is not "no validated profile": it is
            // unknown, and the gate reports that through ScanUnavailable rows
            // only when the scan itself failed. Here the honest answer is to
            // offer nothing rather than a profile whose backing file is gone.
            return [];
        }

        if (!validated.Any(v => v.Component == OverclockComponent.Gpu))
        {
            return [];
        }

        return GameProfileCatalog.All
            .Select(g => new GameOverclockProfile(
                g.GameId,
                g.DisplayName,
                OverclockProfile.Custom,
                CustomPresetLabelKey,
                PreferHighPerformanceGpu: true,
                GpuPreferenceEvidence: EvidenceLevel.Empirical,
                g.GuidanceEs))
            .ToList();
    }
}
