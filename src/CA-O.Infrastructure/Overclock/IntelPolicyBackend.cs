using CAO.Core.Overclock;
using CAO.Core.Overclock.Policy;
using CAO.Core.Overclock.OverclockCatalog;
using CAO.Core.Interfaces;
using CAO.Shared;
using CAO.Shared.Security;

namespace CAO.Infrastructure.Overclock;

/// <summary>Reads the facts about Intel XTU that the pure policy turns into a verdict.</summary>
public interface IXtuProbe
{
    XtuProbeFacts Probe(CancellationToken ct);
}

/// <summary>
/// The only writable CPU backend CA-O can ship without a signed driver.
///
/// What it actually does is write two values into the Windows power scheme:
/// the firmware boost behaviour and the maximum processor state ceiling.
/// That is a policy change, not an overclock - no core multiplier moves, no
/// voltage changes, nothing survives a reboot as a "tuned" state. The section
/// says exactly that in the UI instead of borrowing Hypertune's language.
///
/// A real CPU overclock needs MSR access, which needs either a signed kernel
/// driver (EAC and BattlEye blocklist its signers) or Intel XTU. XTU is
/// detected here and, when present, CA-O guides the user into XTU rather than
/// pretending to drive the embedded controller itself.
/// </summary>
public sealed class IntelPolicyBackend : IOverclockBackend, IOverclockDetailSource, IOverclockWriteGate
{
    public const string ReadOnlyReasonKey = "overclock-cpu-write-not-enabled";
    public const string ReadFailedReasonKey = "overclock-cpu-read-failed";
    public const string WriteFailedReasonKey = "overclock-cpu-write-failed";
    public const string VerifyFailedReasonKey = "overclock-cpu-verify-failed";
    public const string ActivationFailedReasonKey = "overclock-cpu-activation-failed";
    public const string UnknownKnobReasonKey = "overclock-cpu-unknown-knob";

    private readonly IPrivilegedCommandExecutor _executor;
    private readonly IXtuProbe _xtu;

    public IntelPolicyBackend(IPrivilegedCommandExecutor executor, IXtuProbe xtu)
    {
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(xtu);
        _executor = executor;
        _xtu = xtu;
    }

    public string Id => OverclockCatalog.IntelPolicyBackendId;

    public OverclockCapabilities Probe(CancellationToken ct)
    {
        XtuAvailability verdict = XtuAvailabilityPolicy.Decide(ProbeXtu(ct));

        IReadOnlyList<OverclockKnob> knobs = OverclockCatalog.CpuPolicyKnobs;
        IReadOnlyList<BackendAvailability> details =
        [
            new BackendAvailability(
                BackendId: OverclockCatalog.IntelPolicyBackendId,
                Available: true,
                UnavailableReasonKey: verdict.ReasonKey,
                RemedyHintKey: verdict.RemedyHintKey,
                RequiresRestart: false,
                DriverVersion: verdict.DetectedVersion,
                LastProbeEvidence: EvidenceLevel.Official),
        ];

        return new OverclockCapabilities(
            Cpu: verdict.Kind,
            Gpu: GpuBackendKind.None,
            Memory: MemoryBackendKind.None,
            Details: details,
            Knobs: knobs);
    }

    public ValueTask<KnobRead> ReadAsync(KnobId id, CancellationToken ct) =>
        new(ReadCoreAsync(id, ct));

    private async Task<KnobRead> ReadCoreAsync(KnobId id, CancellationToken ct)
    {
        if (!IntelPowerPolicy.TryResolve(id, out PowerSettingTarget target))
        {
            return KnobRead.Fail(UnknownKnobReasonKey);
        }

        PrivilegedCommandResult result = await RunQueryAsync(target, ct).ConfigureAwait(false);
        if (!result.Success)
        {
            return KnobRead.Fail(ReadFailedReasonKey);
        }

        int? value = IntelPowerPolicy.ParseAcSettingIndex(result.StdOut);
        return value.HasValue
            ? KnobRead.Ok(KnobValue.FromScalar(value.Value))
            : KnobRead.Fail(ReadFailedReasonKey);
    }

    public ValueTask<KnobWriteResult> WriteAsync(KnobId id, KnobValue value, CancellationToken ct) =>
        new(WriteCoreAsync(id, value, ct));

    private async Task<KnobWriteResult> WriteCoreAsync(KnobId id, KnobValue value, CancellationToken ct)
    {
        if (!IntelPowerPolicy.TryResolve(id, out PowerSettingTarget target))
        {
            return KnobWriteResult.Fail(UnknownKnobReasonKey);
        }

        IReadOnlyList<string>? setArgs = IntelPowerPolicy.BuildSetArguments(target, (int)value.Scalar);
        if (setArgs is null)
        {
            return KnobWriteResult.Fail(WriteFailedReasonKey);
        }

        PrivilegedCommandResult set = await RunAsync(SystemCommandKey.PowerCfgSetAcValueIndex, setArgs, ct).ConfigureAwait(false);
        if (!set.Success)
        {
            return KnobWriteResult.Fail(WriteFailedReasonKey);
        }

        if (IntelPowerPolicy.RequiresSchemeActivation &&
            !await ActivateCurrentSchemeAsync(ct).ConfigureAwait(false))
        {
            // The value was staged but is not live. Reporting success here
            // would be the classic CA-O bug: a change that never happened
            // dressed up as one that did.
            return KnobWriteResult.Fail(ActivationFailedReasonKey);
        }

        PrivilegedCommandResult verify = await RunQueryAsync(target, ct).ConfigureAwait(false);
        int? readBack = verify.Success ? IntelPowerPolicy.ParseAcSettingIndex(verify.StdOut) : null;
        if (!readBack.HasValue || readBack.Value != (int)value.Scalar)
        {
            return KnobWriteResult.Fail(VerifyFailedReasonKey);
        }

        return KnobWriteResult.Ok(KnobValue.FromScalar(readBack.Value));
    }

    public ValueTask<UnitSnapshot> SnapshotAsync(CancellationToken ct) => new(SnapshotCoreAsync(ct));

    private async Task<UnitSnapshot> SnapshotCoreAsync(CancellationToken ct)
    {
        List<KeyValuePair<KnobId, KnobValue>> values = [];
        foreach (OverclockKnob knob in OverclockCatalog.CpuPolicyKnobs)
        {
            KnobRead read = await ReadCoreAsync(knob.Id, ct).ConfigureAwait(false);
            if (read.Success)
            {
                values.Add(new KeyValuePair<KnobId, KnobValue>(knob.Id, read.Value));
            }
        }

        return new UnitSnapshot(OverclockComponent.Cpu, values, DateTimeOffset.UtcNow, DriverVersion: null);
    }

    public ValueTask<RestoreOutcome> RestoreAsync(UnitSnapshot snapshot, CancellationToken ct) =>
        new(RestoreCoreAsync(snapshot, ct));

    private async Task<RestoreOutcome> RestoreCoreAsync(UnitSnapshot snapshot, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Values.Count == 0)
        {
            return new RestoreOutcome(Success: true, snapshot.Values.Count, Verified: true, FailureReasonKey: null);
        }

        int restored = 0;
        bool anyFailed = false;
        foreach (KeyValuePair<KnobId, KnobValue> entry in snapshot.Values)
        {
            KnobWriteResult result = await WriteCoreAsync(entry.Key, entry.Value, ct).ConfigureAwait(false);
            if (result.Success)
            {
                restored++;
            }
            else
            {
                // Never break on the first failure: a partial restore that
                // leaves the other knob wrong is worse than one that tries.
                anyFailed = true;
            }
        }

        return anyFailed
            ? new RestoreOutcome(Success: false, restored, Verified: false, FailureReasonKey: WriteFailedReasonKey)
            : new RestoreOutcome(Success: true, restored, Verified: true, FailureReasonKey: null);
    }

    public ValueTask<IReadOnlyList<OverclockReading>> ReadDetailsAsync(CancellationToken ct) =>
        new(ReadDetailsCoreAsync(ct));

    private async Task<IReadOnlyList<OverclockReading>> ReadDetailsCoreAsync(CancellationToken ct)
    {
        XtuAvailability verdict = XtuAvailabilityPolicy.Decide(ProbeXtu(ct));
        List<OverclockReading> readings =
        [
            new("overclock_cpu_backend", verdict.Kind.ToString(), EvidenceLevel.Official),
            new("overclock_cpu_xtu_reason", verdict.ReasonKey, EvidenceLevel.Official, verdict.DetectedVersion is null ? null : "overclock_cpu_xtu_version"),
        ];

        foreach (OverclockKnob knob in OverclockCatalog.CpuPolicyKnobs)
        {
            KnobRead read = await ReadCoreAsync(knob.Id, ct).ConfigureAwait(false);
            readings.Add(read.Success
                ? new OverclockReading(knob.LabelKey, Format(read.Value), EvidenceLevel.Official)
                : new OverclockReading(knob.LabelKey, "-", EvidenceLevel.Official, read.FailureReasonKey));
        }

        readings.Add(new OverclockReading(
            "overclock_cpu_scope",
            "policy",
            EvidenceLevel.Official,
            "overclock_cpu_scope_note"));

        return readings;
    }

    /// <summary>
    /// A knob is writable only when CA-O has already proven it can read it.
    /// Offering a control whose current value is unknown is how "applied"
    /// becomes a lie, so the gate and the read share the same condition.
    /// </summary>
    public bool CanWrite(KnobId knob)
    {
        if (!IntelPowerPolicy.TryResolve(knob, out _))
        {
            return false;
        }

        KnobRead read = ReadCoreAsync(knob, CancellationToken.None).GetAwaiter().GetResult();
        return read.Success;
    }

    public string? BlockReasonKey(KnobId knob) =>
        IntelPowerPolicy.TryResolve(knob, out _) ? null : UnknownKnobReasonKey;

    private XtuProbeFacts ProbeXtu(CancellationToken ct) => _xtu.Probe(ct);

    private Task<PrivilegedCommandResult> RunQueryAsync(in PowerSettingTarget target, CancellationToken ct)
    {
        IReadOnlyList<string>? args = IntelPowerPolicy.BuildQueryArguments(target);
        return args is null
            ? Task.FromResult(new PrivilegedCommandResult(-1, string.Empty, UnknownKnobReasonKey, TimedOut: false))
            : RunAsync(SystemCommandKey.PowerCfgQueryAcValueIndex, args, ct).AsTask();
    }

    private async Task<bool> ActivateCurrentSchemeAsync(CancellationToken ct)
    {
        PrivilegedCommandResult result = await RunAsync(
            SystemCommandKey.PowerCfgSetActiveCurrent,
            ["/setactive", IntelPowerPolicy.TargetScheme],
            ct).ConfigureAwait(false);
        return result.Success;
    }

    private async ValueTask<PrivilegedCommandResult> RunAsync(
        SystemCommandKey key,
        IReadOnlyList<string> arguments,
        CancellationToken ct)
    {
        try
        {
            return await _executor.ExecuteAsync(key, arguments, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // the section must degrade, never crash the page
        catch (Exception)
        {
            return new PrivilegedCommandResult(-1, string.Empty, "overclock-cpu-exec-failed", TimedOut: false);
        }
#pragma warning restore CA1031
    }

    private static string Format(KnobValue value) =>
        value.Scalar.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
}
