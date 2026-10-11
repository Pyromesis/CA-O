using CAO.Core.Overclock.Recovery;
using CAO.Infrastructure.Benchmarking;

namespace CAO.Infrastructure.Overclock;

/// <summary>
/// The real boot health probe, on purpose weaker than its name suggests.
///
/// The honest answer is what matters here. CA-O does not ship
/// System.Diagnostics.EventLog, so it cannot read bugcheck 1001 or event 6008,
/// and it refuses to add a NuGet just to be able to say "you crashed". What it
/// CAN prove is temporal: when the service compares this report against a
/// checkpoint's timestamp it knows whether the machine restarted while a tuning
/// run was open. That is a fact. "It crashed" would be a guess.
///
/// So a clean boot here means "this probe cannot see anything wrong", not "this
/// machine booted cleanly". The wording in the UI says the machine restarted
/// with the run still open, never that it hung.
/// </summary>
public sealed class SystemBootHealthProbe : IBootHealthProbe
{
    /// <summary>Localization key for "the boot instant could not be read".</summary>
    public const string UnavailableReasonKey = "overclock-boot-info-unavailable";

    /// <inheritdoc />
    public async Task<BootHealthReport> GetAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // WMI on the UI thread is a rule violation in this repo, and the caller
        // is the page's refresh path.
        var boot = await Task.Run(BootInfoProvider.GetBootInfo, cancellationToken).ConfigureAwait(false);

        return boot is null
            ? BootHealthReport.Unclean(UnavailableReasonKey)
            : new BootHealthReport(true, new DateTimeOffset(boot.BootTimeUtc, TimeSpan.Zero), null);
    }
}