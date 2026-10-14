using CAO.Core.Overclock.Reporting;

namespace CAO.Infrastructure.Overclock;

/// <summary>
/// Asks every vendor's ceiling source and concatenates the answers. Each source
/// already returns nothing when its vendor is absent, so an NVIDIA machine gets
/// NVIDIA rows and an AMD machine AMD rows; before this, the page only ever
/// asked NVIDIA and an AMD card showed no ceiling at all. One source failing
/// does not hide the others.
/// </summary>
public sealed class CompositeCeilingSource : IOverclockCeilingSource
{
    private readonly IOverclockCeilingSource[] _sources;

    public CompositeCeilingSource(params IOverclockCeilingSource[] sources) =>
        _sources = sources ?? throw new ArgumentNullException(nameof(sources));

    public async ValueTask<IReadOnlyList<TuningCeiling>> ReadAsync(CancellationToken ct)
    {
        var all = new List<TuningCeiling>();
        foreach (var source in _sources)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                all.AddRange(await source.ReadAsync(ct).ConfigureAwait(false));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
#pragma warning disable CA1031 // one vendor's failure must not blank the other's rows
            catch (Exception)
            {
            }
#pragma warning restore CA1031
        }

        return all;
    }
}
