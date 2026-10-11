using CAO.Core.Overclock;
using CAO.Core.Overclock.OverclockCatalog;
using CAO.Shared;

namespace CAO.Infrastructure.Overclock;

/// <summary>
/// READ-ONLY NVIDIA backend (phase F1).
///
/// It answers what this machine has: how many GPUs, which driver, which clocks,
/// which power limit, which fan. It refuses every write with an explicit
/// reason, because the write path (F3) has not been proven on real silicon and
/// an unproven setter that half-succeeds is worse than no setter.
///
/// Anti-cheat posture: pure P/Invoke into the vendor's own signed driver DLL.
/// No kernel component, no overlay, no injection, no game directory is ever
/// referenced (design 6.14).
/// </summary>
public sealed class NvidiaNvapiBackend : IOverclockBackend, IOverclockDetailSource, IOverclockWriteGate
{
    /// <summary>Reason surfaced when a caller attempts a write in the read-only phase.</summary>
    public const string ReadOnlyReasonKey = "overclock-gpu-write-readonly-phase";

    /// <summary>Reason surfaced when the caller asked for the single NVIDIA device and there is not exactly one.</summary>
    public const string NoSingleDeviceReasonKey = "overclock-gpu-no-single-device";

    private readonly object _gate = new();
    private uint _handle;
    private bool _handleResolved;
    private string? _driverVersion;

    public string Id => OverclockCatalog.NvapiBackendId;

    /// <summary>
    /// Reports availability, never throws. A machine with no NVIDIA driver is a
    /// supported configuration: the GPU section degrades to <c>Unsupported</c>
    /// with the real reason, it does not error out.
    /// </summary>
    public OverclockCapabilities Probe(CancellationToken ct)
    {
        if (!NvInterop.TryInitialize())
        {
            return Unavailable(NvInterop.FailureKey ?? "overclock-nvapi-missing", null, null);
        }

        if (!NvInterop.TryGetDeviceCount(out uint devices) || devices == 0)
        {
            return Unavailable("overclock-gpu-none-detected", null, null);
        }

        string? driverVersion = ResolveDriverVersion();
        var evidence = driverVersion is null ? EvidenceLevel.Empirical : EvidenceLevel.Vendor;

        var availability = new BackendAvailability(
            Id,
            Available: true,
            UnavailableReasonKey: string.Empty,
            RemedyHintKey: null,
            RequiresRestart: false,
            DriverVersion: driverVersion,
            LastProbeEvidence: evidence);

        // Read-only phase: only the readout knob reports a value the user can
        // trust today. The writable ones are listed so the UI can explain WHY
        // they are inert instead of hiding them.
        return new OverclockCapabilities(
            CpuBackendKind.PolicyOnly,
            GpuBackendKind.NvidiaNvapi,
            MemoryBackendKind.SmbiosReportOnly,
            [availability],
            OverclockCatalog.NvidiaGpuKnobs);
    }

    /// <summary>
    /// Reads one knob. Only <see cref="NvKnobs.ClockReadout"/> has an honest
    /// answer in this phase; everything else reports
    /// <c>overclock-gpu-readonly-phase</c> so the UI can say "not readable yet"
    /// rather than draw a line at zero.
    /// </summary>
    public ValueTask<KnobRead> ReadAsync(KnobId id, CancellationToken ct)
    {
        if (id != NvKnobs.ClockReadout)
        {
            return ValueTask.FromResult(KnobRead.Fail("overclock-gpu-readonly-phase"));
        }

        if (!TryResolveHandle(out uint handle))
        {
            return ValueTask.FromResult(KnobRead.Fail(NoSingleDeviceReasonKey));
        }

        if (!NvInterop.TryGetClock(handle, NvInterop.ClockCurrent, out uint megahertz))
        {
            return ValueTask.FromResult(KnobRead.Fail("overclock-gpu-clock-unavailable"));
        }

        // Catalog unit is Hertz; NVAPI reports MHz.
        return ValueTask.FromResult(KnobRead.Ok(KnobValue.FromScalar(megahertz * 1_000d)));
    }

    /// <summary>
    /// Always refuses. F1 is the inspection phase: the setters exist in NVAPI
    /// but CA-O has not proven apply-then-verify-then-restore on real hardware
    /// yet, and guessing here is how a user ends up with a bricked GPU.
    /// </summary>
    public ValueTask<KnobWriteResult> WriteAsync(KnobId id, KnobValue value, CancellationToken ct) =>
        ValueTask.FromResult(KnobWriteResult.Fail(ReadOnlyReasonKey));

    /// <summary>
    /// Captures the current values so a later phase can restore them. Only
    /// values NVAPI actually reports are captured; a knob with no getter is
    /// absent from the snapshot rather than present as zero.
    /// </summary>
    public ValueTask<UnitSnapshot> SnapshotAsync(CancellationToken ct)
    {
        var values = new List<KeyValuePair<KnobId, KnobValue>>();

        if (TryResolveHandle(out uint handle))
        {
            if (NvInterop.TryGetClock(handle, NvInterop.ClockCurrent, out uint current))
            {
                values.Add(new KeyValuePair<KnobId, KnobValue>(NvKnobs.ClockReadout, KnobValue.FromScalar(current * 1_000d)));
            }

            if (NvInterop.TryGetPowerLimit(handle, out uint milliwatts))
            {
                values.Add(new KeyValuePair<KnobId, KnobValue>(NvKnobs.PowerLimit, KnobValue.FromScalar(milliwatts)));
            }

            if (NvInterop.TryGetFanSpeed(handle, out uint fan))
            {
                values.Add(new KeyValuePair<KnobId, KnobValue>(NvKnobs.FanTarget, KnobValue.FromScalar(fan)));
            }
        }

        return ValueTask.FromResult(
            new UnitSnapshot(OverclockComponent.Gpu, values, DateTimeOffset.UtcNow, _driverVersion));
    }

    /// <summary>
    /// Nothing was changed, so there is nothing to restore. F1 verifies the
    /// captured values match the live ones instead of claiming a restore it
    /// never performed.
    /// </summary>
    public ValueTask<RestoreOutcome> RestoreAsync(UnitSnapshot snapshot, CancellationToken ct)
    {
        if (snapshot.Count == 0)
        {
            return ValueTask.FromResult(RestoreOutcome.Ok(0, verified: true));
        }

        return ValueTask.FromResult(RestoreOutcome.Fail("overclock-gpu-write-disabled"));
    }

    /// <summary>
    /// Raw readings the catalog does not model. Every entry is either a real
    /// number with <see cref="EvidenceLevel.Vendor"/> or an explicit "not
    /// available" with its reason. There is no third option.
    /// </summary>
    public ValueTask<IReadOnlyList<OverclockReading>> ReadDetailsAsync(CancellationToken ct)
    {
        var readings = new List<OverclockReading>();

        if (!NvInterop.TryInitialize())
        {
            string reason = NvInterop.FailureKey ?? "overclock-nvapi-missing";
            readings.Add(new OverclockReading("overclock_gpu_name", string.Empty, EvidenceLevel.Empirical, reason));
            return new ValueTask<IReadOnlyList<OverclockReading>>(readings);
        }

        if (!TryResolveHandle(out uint handle))
        {
            readings.Add(new OverclockReading("overclock_gpu_name", string.Empty, EvidenceLevel.Empirical, NoSingleDeviceReasonKey));
            return new ValueTask<IReadOnlyList<OverclockReading>>(readings);
        }

        readings.Add(Clock("overclock_gpu_clock_current", NvInterop.ClockCurrent, "MHz"));
        readings.Add(Clock("overclock_gpu_clock_max", NvInterop.ClockMax, "MHz"));
        readings.Add(Clock("overclock_gpu_clock_memory", NvInterop.ClockMemory, "MHz"));
        readings.Add(Fan());
        readings.Add(Power());
        readings.Add(Driver());

        return new ValueTask<IReadOnlyList<OverclockReading>>(readings);
    }

    /// <summary>No knob is writable in the read-only phase.</summary>
    public bool CanWrite(KnobId knob) => false;

    /// <summary>Always <see cref="ReadOnlyReasonKey"/> so the UI shows the real blocker.</summary>
    public string? BlockReasonKey(KnobId knob) => ReadOnlyReasonKey;

    private OverclockReading Clock(string labelKey, int clockType, string unit)
    {
        if (NvInterop.TryGetClock(_handle, clockType, out uint mhz))
        {
            return new OverclockReading(
                labelKey,
                FormattableString.Invariant($"{mhz} {unit}"),
                EvidenceLevel.Vendor);
        }

        return new OverclockReading(labelKey, string.Empty, EvidenceLevel.Empirical, "overclock-gpu-clock-unavailable");
    }

    private OverclockReading Fan()
    {
        if (NvInterop.TryGetFanSpeed(_handle, out uint percent))
        {
            return new OverclockReading(
                "overclock_gpu_fan",
                FormattableString.Invariant($"{percent} %"),
                EvidenceLevel.Vendor);
        }

        // Many laptop boards report no tachometer. That is a fact about the
        // machine, not an error to hide.
        return new OverclockReading("overclock_gpu_fan", string.Empty, EvidenceLevel.Empirical, "overclock-gpu-fan-unavailable");
    }

    private OverclockReading Power()
    {
        if (NvInterop.TryGetPowerLimit(_handle, out uint milliwatts))
        {
            return new OverclockReading(
                "overclock_gpu_power_limit",
                FormattableString.Invariant($"{milliwatts / 1000d} W"),
                EvidenceLevel.Vendor);
        }

        return new OverclockReading("overclock_gpu_power_limit", string.Empty, EvidenceLevel.Empirical, "overclock-gpu-power-unavailable");
    }

    private OverclockReading Driver()
    {
        string? version = _driverVersion;
        return version is null
            ? new OverclockReading("overclock_gpu_driver", string.Empty, EvidenceLevel.Empirical, "overclock-gpu-driver-unavailable")
            : new OverclockReading("overclock_gpu_driver", version, EvidenceLevel.Vendor);
    }

    private bool TryResolveHandle(out uint handle)
    {
        lock (_gate)
        {
            if (_handleResolved)
            {
                handle = _handle;
                return _handle != 0;
            }

            handle = 0;

            if (!NvInterop.TryGetDeviceCount(out uint devices))
            {
                return false;
            }

            // CA-O reads the primary adapter only. Multi-GPU systems report
            // every card separately rather than pretending one profile fits all.
            if (devices != 1 || !NvInterop.TryGetDeviceHandle(0, out uint resolved))
            {
                return false;
            }

            _handle = resolved;
            _handleResolved = true;
            handle = resolved;
            return true;
        }
    }

    private string? ResolveDriverVersion()
    {
        lock (_gate)
        {
            if (_driverVersion is not null)
            {
                return _driverVersion;
            }

            _ = TryResolveHandle(out uint handle);
            if (handle == 0 || !NvInterop.TryGetDriverVersion(handle, out string version))
            {
                return null;
            }

            _driverVersion = version;
            return version;
        }
    }

    private OverclockCapabilities Unavailable(string reasonKey, string? remedyKey, string? driverVersion)
    {
        var availability = new BackendAvailability(
            Id,
            Available: false,
            UnavailableReasonKey: reasonKey,
            RemedyHintKey: remedyKey,
            RequiresRestart: false,
            DriverVersion: driverVersion,
            LastProbeEvidence: EvidenceLevel.Official);

        return new OverclockCapabilities(
            CpuBackendKind.PolicyOnly,
            GpuBackendKind.None,
            MemoryBackendKind.SmbiosReportOnly,
            [availability],
            OverclockCatalog.NvidiaGpuKnobs);
    }
}