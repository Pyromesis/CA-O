namespace CAO.Core.Overclock;

/// <summary>
/// A vendor-agnostic tuning surface. NVAPI, ADLX and a future signed MSR
/// backend have no common value type worth inventing, so each backend exposes
/// its own knobs and the shared engine owns policy, validation, journaling,
/// stability and deltas.
/// </summary>
public interface IOverclockBackend
{
    /// <summary>Stable identifier, e.g. "nvidia-nvapi", "intel-xtu", "msr-signed".</summary>
    string Id { get; }

    /// <summary>
    /// Reports what THIS machine supports right now, with this driver version.
    /// Must never throw for a missing library: absence is a state, not an error.
    /// </summary>
    OverclockCapabilities Probe(CancellationToken ct);

    ValueTask<KnobRead> ReadAsync(KnobId id, CancellationToken ct);

    ValueTask<KnobWriteResult> WriteAsync(KnobId id, KnobValue value, CancellationToken ct);

    /// <summary>Captures the full pre-change state of this backend's component.</summary>
    ValueTask<UnitSnapshot> SnapshotAsync(CancellationToken ct);

    /// <summary>Restores a previously captured state. Idempotent by contract.</summary>
    ValueTask<RestoreOutcome> RestoreAsync(UnitSnapshot snapshot, CancellationToken ct);
}

/// <summary>
/// Handles the operations a declarative knob cannot express (VBIOS reset,
/// memory profile guidance, guided XTU hand-off).
/// </summary>
public interface IOverclockExtension
{
    string Id { get; }

    bool Handles(OverclockOperation op);

    ValueTask<ExtensionResult> ExecuteAsync(OverclockOperation op, CancellationToken ct);
}

/// <summary>
/// Fans a probe across every registered backend and merges the results. Adding
/// a backend — including a future signed driver — is one registration here and
/// nothing else changes (design 6.14).
/// </summary>
public interface IOverclockBackendRegistry
{
    OverclockCapabilities ProbeAll(CancellationToken ct);
}