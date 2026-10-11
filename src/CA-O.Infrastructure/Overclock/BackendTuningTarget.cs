using CAO.Core.Overclock;
using CAO.Core.Overclock.Recovery;
using CAO.Core.Overclock.Stability;

namespace CAO.Infrastructure.Overclock;

/// <summary>
/// Adapts a backend to the tuning engine's <see cref="ITuningTarget"/>.
///
/// The adapter exists instead of a parallel write path on purpose: a tuning run
/// and a manual change must go through the same backend method, or the two would
/// drift and the recovery would revert a value nothing else ever set.
/// </summary>
public sealed class BackendTuningTarget : ITuningTarget
{
    /// <summary>Reason when the current value could not be read before writing.</summary>
    public const string ReadFailedReasonKey = "overclock-tuning-read-failed";

    private readonly IOverclockBackend _backend;

    public BackendTuningTarget(IOverclockBackend backend) =>
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));

    /// <inheritdoc />
    public string BackendId => _backend.Id;

    /// <inheritdoc />
    public ValueTask<UnitSnapshot> CaptureAsync(CancellationToken cancellationToken) =>
        _backend.SnapshotAsync(cancellationToken);

    /// <inheritdoc />
    public async ValueTask<KnobWriteResult> ApplyAsync(
        KnobId id,
        KnobValue value,
        CancellationToken cancellationToken)
    {
        // Read first: the tuning engine decides what to try next from this
        // answer, and writing blind into a setting that cannot be read would
        // turn a readable refusal into an unreadable one.
        var read = await _backend.ReadAsync(id, cancellationToken).ConfigureAwait(false);
        if (!read.Success)
        {
            return KnobWriteResult.Fail(read.FailureReasonKey ?? ReadFailedReasonKey);
        }

        return await _backend.WriteAsync(id, value, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public ValueTask<RestoreOutcome> RestoreAsync(UnitSnapshot snapshot, CancellationToken cancellationToken) =>
        _backend.RestoreAsync(snapshot, cancellationToken);

    /// <inheritdoc />
    public ValueTask<KnobWriteResult> RestoreValueAsync(
        KnobId id,
        KnobValue value,
        CancellationToken cancellationToken) =>
        _backend.WriteAsync(id, value, cancellationToken);
}

/// <summary>
/// Resolves a checkpoint's backend id to a tuning target. Backends are wrapped
/// once and cached, so a run that is resumed after a reboot gets the same target
/// object the tuning engine already reasoned about.
/// </summary>
public sealed class BackendTuningTargetResolver : ITuningTargetResolver
{
    private readonly Dictionary<string, ITuningTarget> _targets;

    public BackendTuningTargetResolver(IReadOnlyList<IOverclockBackend> backends)
    {
        ArgumentNullException.ThrowIfNull(backends);

        _targets = new Dictionary<string, ITuningTarget>(StringComparer.Ordinal);
        foreach (var backend in backends)
        {
            // First one wins: the registry already resolves component conflicts,
            // and a silent overwrite here would make recovery pick a backend the
            // tuning run never used.
            _targets.TryAdd(backend.Id, new BackendTuningTarget(backend));
        }
    }

    public static BackendTuningTargetResolver FromBackends(params IOverclockBackend[] backends) =>
        new(backends);

    /// <inheritdoc />
    public bool TryResolve(string backendId, out ITuningTarget? target)
    {
        if (backendId is not null && _targets.TryGetValue(backendId, out var resolved))
        {
            target = resolved;
            return true;
        }

        target = null;
        return false;
    }
}