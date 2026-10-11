using CAO.Core.Overclock;
using CAO.Core.Overclock.OverclockCatalog;
using CAO.Shared;

namespace CAO.Infrastructure.Overclock;

/// <summary>
/// NVIDIA backend, entirely on NVML.
///
/// History this file must not repeat: reads used to go through NVAPI with
/// hand-written signatures (a 32-bit handle where the driver writes a 64-byte
/// struct), and the write path squeezed the NVML device pointer through a
/// 32-bit integer. Both corrupt memory on x64; the second one crashed the
/// installed 2.2.5 with 0xc0000005 mid-probe. NVML exposes every read CA-O
/// needs (count, handle, name, clocks, fan, power, driver) with documented
/// signatures, so the old layer is gone and the device pointer stays a
/// pointer from the driver to every call.
///
/// Anti-cheat posture is unchanged: one P/Invoke into the vendor's own signed
/// DLL, shipped by the driver, invoked when present and never redistributed.
/// No kernel component, no overlay, no injection, no game directory.
///
/// Units: the catalog speaks Hertz and percent; NVML speaks MHz and milliwatts.
/// Every crossing of that boundary happens in this file, once, explicitly.
/// </summary>
public sealed class NvidiaGpuBackend : IOverclockBackend, IOverclockDetailSource, IOverclockWriteGate
{
    /// <summary>Reason surfaced when the caller asked for the single NVIDIA device and there is not exactly one.</summary>
    public const string NoSingleDeviceReasonKey = "overclock-gpu-no-single-device";

    /// <summary>The knob has no public vendor setter at all. Honest, not a failure of CA-O.</summary>
    public const string NoPublicSetterReasonKey = "overclock-gpu-no-public-setter";

    /// <summary>The driver accepted the write but the value did not stick.</summary>
    public const string VerifyFailedReasonKey = "overclock-gpu-write-verify-failed";

    /// <summary>The driver refused because CA-O is not running elevated.</summary>
    public const string NoPermissionReasonKey = "overclock-gpu-write-no-permission";

    /// <summary>The driver refused for a reason CA-O does not classify.</summary>
    public const string RejectedReasonKey = "overclock-gpu-write-rejected";

    /// <summary>Power limit is a percentage, and CA-O could not find the reference it is a percentage OF.</summary>
    public const string PowerAnchorUnavailableReasonKey = "overclock-gpu-power-anchor-unavailable";

    /// <summary>Clock offset bounds are unreadable, so CA-O will not guess a safe value.</summary>
    public const string OffsetBoundsUnavailableReasonKey = "overclock-gpu-offset-bounds-unavailable";

    /// <summary>One or more snapshot values could not be put back.</summary>
    public const string RestoreFailedReasonKey = "overclock-gpu-restore-failed";

    /// <summary>MHz in one Hertz. The catalog is Hertz everywhere; NVML is MHz everywhere.</summary>
    private const double HertzPerMegahertz = 1_000_000d;

    private readonly INvidiaTuningApi _tuning;
    private readonly object _gate = new();
    private string? _driverVersion;
    private int? _lastWriteStatus;

    /// <summary>Production instance: the real NVML-backed surface for reads and writes.</summary>
    public NvidiaGpuBackend()
        : this(NvmlTuningApi.Instance)
    {
    }

    /// <summary>Test seam: injects the write surface so the backend can be exercised without a GPU.</summary>
    internal NvidiaGpuBackend(INvidiaTuningApi tuning)
    {
        _tuning = tuning;
    }

    // Wire-stable id: checkpoints on disk already reference "nvidia-nvapi".
    public string Id => OverclockCatalog.NvapiBackendId;

    /// <summary>
    /// Reports availability, never throws. A machine with no NVIDIA driver is a
    /// supported configuration: the GPU section degrades to <c>Unsupported</c>
    /// with the real reason, it does not error out.
    /// </summary>
    public OverclockCapabilities Probe(CancellationToken ct)
    {
        if (!NvmlInterop.TryInitialize())
        {
            return Unavailable(NvmlInterop.FailureKey ?? "overclock-nvml-missing", null, null);
        }

        if (!NvmlInterop.TryGetDeviceCount(out uint devices) || devices == 0)
        {
            return Unavailable("overclock-gpu-none-detected", null, null);
        }

        // CA-O tunes the primary adapter only. Reporting Available on a
        // multi-GPU box would offer a profile that fits no single card.
        if (devices != 1)
        {
            return Unavailable(NoSingleDeviceReasonKey, null, null);
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

        return new OverclockCapabilities(
            CpuBackendKind.PolicyOnly,
            GpuBackendKind.NvidiaNvapi,
            MemoryBackendKind.SmbiosReportOnly,
            [availability],
            OverclockCatalog.NvidiaGpuKnobs);
    }

    /// <summary>
    /// Reads one knob. Writable knobs report their live value through the write
    /// path's own getter, so the number on screen is the number that would be
    /// verified after a change. Knobs with no public setter still report a
    /// truthful failure instead of drawing a line at zero.
    /// </summary>
    public ValueTask<KnobRead> ReadAsync(KnobId id, CancellationToken ct)
    {
        if (id == NvKnobs.ClockReadout)
        {
            if (!NvmlInterop.TryResolveHandle(out IntPtr handle))
            {
                return ValueTask.FromResult(KnobRead.Fail(NoSingleDeviceReasonKey));
            }

            if (!NvmlInterop.TryGetClockInfo(handle, NvidiaClockDomain.Graphics, out uint megahertz))
            {
                return ValueTask.FromResult(KnobRead.Fail("overclock-gpu-clock-unavailable"));
            }

            // Catalog unit is Hertz; NVML reports MHz.
            return ValueTask.FromResult(KnobRead.Ok(KnobValue.FromScalar(megahertz * HertzPerMegahertz)));
        }

        if (id == NvKnobs.PowerLimit)
        {
            return ValueTask.FromResult(ReadPowerLimitPercent());
        }

        if (id == NvKnobs.CoreClockOffset)
        {
            return ValueTask.FromResult(ReadOffset(NvidiaClockDomain.Graphics));
        }

        if (id == NvKnobs.MemoryClockOffset)
        {
            return ValueTask.FromResult(ReadOffset(NvidiaClockDomain.Memory));
        }

        return ValueTask.FromResult(KnobRead.Fail(NoPublicSetterReasonKey));
    }

    /// <summary>
    /// Applies one knob, then reads it back. A write that does not stick is a
    /// failure, never a success: reporting "applied" for a value the hardware
    /// ignored is exactly the kind of lie this feature exists to remove.
    /// </summary>
    public ValueTask<KnobWriteResult> WriteAsync(KnobId id, KnobValue value, CancellationToken ct)
    {
        if (!_tuning.IsAvailable)
        {
            return ValueTask.FromResult(KnobWriteResult.Fail(_tuning.UnavailableReasonKey ?? "overclock-nvml-missing"));
        }

        if (id == NvKnobs.PowerLimit)
        {
            return ValueTask.FromResult(WritePowerLimit(value));
        }

        if (id == NvKnobs.CoreClockOffset)
        {
            return ValueTask.FromResult(WriteOffset(id, value, NvidiaClockDomain.Graphics));
        }

        if (id == NvKnobs.MemoryClockOffset)
        {
            return ValueTask.FromResult(WriteOffset(id, value, NvidiaClockDomain.Memory));
        }

        return ValueTask.FromResult(KnobWriteResult.Fail(NoPublicSetterReasonKey));
    }

    /// <summary>
    /// Captures the values a later restore needs. The power limit is stored in
    /// MILLIWATTS, not in the catalog's percent: a percentage is only meaningful
    /// relative to an anchor that can change between the snapshot and the
    /// restore, and restoring to a slightly different wattage is worse than
    /// storing the unit the driver actually takes.
    /// </summary>
    public ValueTask<UnitSnapshot> SnapshotAsync(CancellationToken ct)
    {
        var values = new List<KeyValuePair<KnobId, KnobValue>>();

        if (NvmlInterop.TryResolveHandle(out IntPtr handle)
            && NvmlInterop.TryGetClockInfo(handle, NvidiaClockDomain.Graphics, out uint current))
        {
            values.Add(new KeyValuePair<KnobId, KnobValue>(
                NvKnobs.ClockReadout,
                KnobValue.FromScalar(current * HertzPerMegahertz)));
        }

        int? milliwatts = _tuning.ReadPowerLimitMilliwatts();
        if (milliwatts is not null)
        {
            values.Add(new KeyValuePair<KnobId, KnobValue>(
                NvKnobs.PowerLimit,
                KnobValue.FromScalar(milliwatts.Value)));
        }

        int? core = _tuning.ReadCoreOffsetMegahertz();
        if (core is not null)
        {
            values.Add(new KeyValuePair<KnobId, KnobValue>(
                NvKnobs.CoreClockOffset,
                KnobValue.FromScalar(core.Value * HertzPerMegahertz)));
        }

        int? memory = _tuning.ReadMemoryOffsetMegahertz();
        if (memory is not null)
        {
            values.Add(new KeyValuePair<KnobId, KnobValue>(
                NvKnobs.MemoryClockOffset,
                KnobValue.FromScalar(memory.Value * HertzPerMegahertz)));
        }

        if (NvmlInterop.TryResolveHandle(out IntPtr fanHandle) && NvmlInterop.TryGetFanSpeed(fanHandle, out uint percent))
        {
            values.Add(new KeyValuePair<KnobId, KnobValue>(NvKnobs.FanTarget, KnobValue.FromScalar(percent)));
        }

        return ValueTask.FromResult(
            new UnitSnapshot(OverclockComponent.Gpu, values, DateTimeOffset.UtcNow, _driverVersion));
    }

    /// <summary>
    /// Puts every captured value back. A snapshot value that cannot be restored
    /// does not abort the batch: one bad knob must not strand the others in a
    /// half-tuned state (design 6.12).
    /// </summary>
    public ValueTask<RestoreOutcome> RestoreAsync(UnitSnapshot snapshot, CancellationToken ct)
    {
        if (snapshot.Count == 0)
        {
            return ValueTask.FromResult(RestoreOutcome.Ok(0, verified: true));
        }

        int restored = 0;
        int failed = 0;

        foreach (KeyValuePair<KnobId, KnobValue> entry in snapshot.Values)
        {
            KnobWriteResult result = RestoreOne(entry.Key, entry.Value);
            if (result.Success)
            {
                restored++;
            }
            else
            {
                failed++;
            }
        }

        return ValueTask.FromResult(
            failed == 0
                ? new RestoreOutcome(true, restored, true, null)
                : new RestoreOutcome(false, restored, false, RestoreFailedReasonKey));
    }

    /// <summary>
    /// Raw readings the catalog does not model. Every entry is either a real
    /// number with <see cref="EvidenceLevel.Vendor"/> or an explicit "not
    /// available" with its reason. There is no third option.
    /// </summary>
    public ValueTask<IReadOnlyList<OverclockReading>> ReadDetailsAsync(CancellationToken ct)
    {
        var readings = new List<OverclockReading>();

        if (!NvmlInterop.TryInitialize())
        {
            string reason = NvmlInterop.FailureKey ?? "overclock-nvml-missing";
            readings.Add(new OverclockReading("overclock_gpu_name", string.Empty, EvidenceLevel.Empirical, reason));
            return new ValueTask<IReadOnlyList<OverclockReading>>(readings);
        }

        if (!NvmlInterop.TryResolveHandle(out IntPtr handle))
        {
            readings.Add(new OverclockReading("overclock_gpu_name", string.Empty, EvidenceLevel.Empirical, NoSingleDeviceReasonKey));
            return new ValueTask<IReadOnlyList<OverclockReading>>(readings);
        }

        readings.Add(Name(handle));
        readings.Add(Clock(handle, "overclock_gpu_clock_current", NvidiaClockDomain.Graphics, max: false));
        readings.Add(Clock(handle, "overclock_gpu_clock_max", NvidiaClockDomain.Graphics, max: true));
        readings.Add(Clock(handle, "overclock_gpu_clock_memory", NvidiaClockDomain.Memory, max: false));
        readings.Add(Fan(handle));
        readings.Add(Power());
        readings.Add(Driver());
        readings.Add(WriteLimits());
        readings.Add(LastWriteStatus());

        return new ValueTask<IReadOnlyList<OverclockReading>>(readings);
    }

    /// <summary>True only for the three knobs with a public vendor setter, on a driver that has NVML.</summary>
    public bool CanWrite(KnobId knob)
    {
        if (!_tuning.IsAvailable || !IsWritable(knob))
        {
            return false;
        }

        return _tuning.TryDescribe(out _);
    }

    /// <summary>The real blocker, so the UI explains itself instead of showing a dead control.</summary>
    public string? BlockReasonKey(KnobId knob)
    {
        if (!_tuning.IsAvailable)
        {
            return _tuning.UnavailableReasonKey ?? "overclock-nvml-missing";
        }

        if (!IsWritable(knob))
        {
            return NoPublicSetterReasonKey;
        }

        return _tuning.TryDescribe(out _) ? null : "overclock-nvml-device-unavailable";
    }

    private void RecordStatus(int status)
    {
        lock (_gate)
        {
            _lastWriteStatus = status;
        }
    }

    private static bool IsWritable(KnobId knob) =>
        knob == NvKnobs.PowerLimit
        || knob == NvKnobs.CoreClockOffset
        || knob == NvKnobs.MemoryClockOffset;

    private KnobRead ReadOffset(NvidiaClockDomain domain)
    {
        int? megahertz = domain == NvidiaClockDomain.Graphics
            ? _tuning.ReadCoreOffsetMegahertz()
            : _tuning.ReadMemoryOffsetMegahertz();

        return megahertz is null
            ? KnobRead.Fail("overclock-gpu-offset-unavailable")
            : KnobRead.Ok(KnobValue.FromScalar(megahertz.Value * HertzPerMegahertz));
    }

    private KnobRead ReadPowerLimitPercent()
    {
        int? milliwatts = _tuning.ReadPowerLimitMilliwatts();
        if (milliwatts is null)
        {
            return KnobRead.Fail("overclock-gpu-power-unavailable");
        }

        if (!TryGetPowerAnchor(out int anchor))
        {
            return KnobRead.Fail(PowerAnchorUnavailableReasonKey);
        }

        return KnobRead.Ok(KnobValue.FromScalar(ToPercent(milliwatts.Value, anchor)));
    }

    private KnobWriteResult WritePowerLimit(KnobValue requested)
    {
        if (!TryGetPowerAnchor(out int anchor))
        {
            return KnobWriteResult.Fail(PowerAnchorUnavailableReasonKey);
        }

        if (!_tuning.TryDescribe(out NvidiaTuningDevice? described) || described is null)
        {
            return KnobWriteResult.Fail("overclock-nvml-device-unavailable");
        }

        int milliwatts = FromPercent((int)Math.Round(requested.Scalar), anchor);

        // Clamp to what the hardware says it accepts rather than letting the
        // driver reject a perfectly reasonable percentage.
        if (described.MinPowerLimitMilliwatts is int min && milliwatts < min)
        {
            milliwatts = min;
        }

        if (described.MaxPowerLimitMilliwatts is int max && milliwatts > max)
        {
            milliwatts = max;
        }

        int status = _tuning.SetPowerLimitMilliwatts(milliwatts);
        return FinishWrite(status, milliwatts, _tuning.ReadPowerLimitMilliwatts(), (int)ToPercent(milliwatts, anchor));
    }

    private KnobWriteResult WriteOffset(KnobId knob, KnobValue requested, NvidiaClockDomain domain)
    {
        if (!_tuning.TryDescribe(out NvidiaTuningDevice? described) || described is null)
        {
            return KnobWriteResult.Fail("overclock-nvml-device-unavailable");
        }

        int? low = domain == NvidiaClockDomain.Graphics ? described.CoreOffsetMinMegahertz : described.MemoryOffsetMinMegahertz;
        int? high = domain == NvidiaClockDomain.Graphics ? described.CoreOffsetMaxMegahertz : described.MemoryOffsetMaxMegahertz;

        if (low is null || high is null)
        {
            // Without the driver's own bounds, CA-O would be inventing the safe
            // range. It does not.
            return KnobWriteResult.Fail(OffsetBoundsUnavailableReasonKey);
        }

        int requestedMegahertz = (int)Math.Round(requested.Scalar / HertzPerMegahertz);
        int target = Math.Clamp(requestedMegahertz, low.Value, high.Value);

        int status = domain == NvidiaClockDomain.Graphics
            ? _tuning.SetCoreOffsetMegahertz(target)
            : _tuning.SetMemoryOffsetMegahertz(target);

        int? readBack = domain == NvidiaClockDomain.Graphics
            ? _tuning.ReadCoreOffsetMegahertz()
            : _tuning.ReadMemoryOffsetMegahertz();

        // The reported value is the one the driver actually got, not the one
        // that was asked for: after a clamp those differ, and showing the
        // request would be a number the hardware never ran at.
        return FinishWrite(
            status,
            target,
            readBack,
            target,
            observed => KnobValue.FromScalar(observed * HertzPerMegahertz));
    }

    private KnobWriteResult FinishWrite(int status, int target, int? readBack, int reportedValue)
    {
        RecordStatus(status);

        switch (NvmlInterop.ClassifyWrite(status))
        {
            case NvidiaTuningWriteOutcome.DriverBlocked:
                return KnobWriteResult.Blocked(BlockReasonKey(NvKnobs.PowerLimit) ?? "overclock-gpu-driver-blocked");

            case NvidiaTuningWriteOutcome.NoPermission:
                return KnobWriteResult.Fail(NoPermissionReasonKey);

            case NvidiaTuningWriteOutcome.Rejected:
                return KnobWriteResult.Fail(RejectedReasonKey);

            default:
                break;
        }

        // The driver said yes. Only a read-back decides whether it stuck, and a
        // value that did not stick is a failure, not a blocked driver.
        if (readBack is not int observed || observed != target)
        {
            return KnobWriteResult.Fail(VerifyFailedReasonKey);
        }

        return KnobWriteResult.Ok(KnobValue.FromScalar(reportedValue));
    }

    private KnobWriteResult FinishWrite(int status, int target, int? readBack, int reportedValue, Func<int, KnobValue> project)
    {
        RecordStatus(status);

        switch (NvmlInterop.ClassifyWrite(status))
        {
            case NvidiaTuningWriteOutcome.DriverBlocked:
                return KnobWriteResult.Blocked(BlockReasonKey(NvKnobs.CoreClockOffset) ?? "overclock-gpu-driver-blocked");

            case NvidiaTuningWriteOutcome.NoPermission:
                return KnobWriteResult.Fail(NoPermissionReasonKey);

            case NvidiaTuningWriteOutcome.Rejected:
                return KnobWriteResult.Fail(RejectedReasonKey);

            default:
                break;
        }

        if (readBack is not int observed || observed != target)
        {
            return KnobWriteResult.Fail(VerifyFailedReasonKey);
        }

        return KnobWriteResult.Ok(project(reportedValue));
    }

    private KnobWriteResult RestoreOne(KnobId knob, KnobValue stored)
    {
        if (knob == NvKnobs.PowerLimit)
        {
            // Stored in milliwatts: see SnapshotAsync.
            int status = _tuning.SetPowerLimitMilliwatts((int)Math.Round(stored.Scalar));
            RecordStatus(status);
            return NvmlInterop.ClassifyWrite(status) == NvidiaTuningWriteOutcome.Applied
                ? KnobWriteResult.Ok(stored)
                : KnobWriteResult.Fail(RejectedReasonKey);
        }

        if (knob == NvKnobs.CoreClockOffset || knob == NvKnobs.MemoryClockOffset)
        {
            bool isCore = knob == NvKnobs.CoreClockOffset;
            int target = (int)Math.Round(stored.Scalar / HertzPerMegahertz);
            int status = isCore
                ? _tuning.SetCoreOffsetMegahertz(target)
                : _tuning.SetMemoryOffsetMegahertz(target);

            RecordStatus(status);

            int? readBack = isCore ? _tuning.ReadCoreOffsetMegahertz() : _tuning.ReadMemoryOffsetMegahertz();
            return NvmlInterop.ClassifyWrite(status) == NvidiaTuningWriteOutcome.Applied && readBack == target
                ? KnobWriteResult.Ok(stored)
                : KnobWriteResult.Fail(VerifyFailedReasonKey);
        }

        // Clock readout and fan duty are not settings; there is nothing to put back.
        return KnobWriteResult.Ok(stored);
    }

    /// <summary>
    /// The wattage that means "100%". The factory limit, so the percentage is
    /// stable across sessions; the hardware minimum only as a last resort. A
    /// percentage with no anchor is a meaningless number, so when neither is
    /// readable CA-O refuses instead of guessing.
    /// </summary>
    private bool TryGetPowerAnchor(out int milliwatts)
    {
        milliwatts = 0;
        if (!_tuning.TryDescribe(out NvidiaTuningDevice? described) || described is null)
        {
            return false;
        }

        if (described.DefaultPowerLimitMilliwatts is int factory && factory > 0)
        {
            milliwatts = factory;
            return true;
        }

        if (described.MinPowerLimitMilliwatts is int lowest && lowest > 0)
        {
            milliwatts = lowest;
            return true;
        }

        return false;
    }

    private static int FromPercent(int percent, int anchorMilliwatts) =>
        (int)Math.Round(anchorMilliwatts * (percent / 100d), MidpointRounding.AwayFromZero);

    private static double ToPercent(int milliwatts, int anchorMilliwatts) =>
        Math.Round(milliwatts * 100d / anchorMilliwatts, 2, MidpointRounding.AwayFromZero);

    private static OverclockReading Name(IntPtr handle) =>
        NvmlInterop.TryGetDeviceName(handle, out string name)
            ? new OverclockReading("overclock_gpu_name", name, EvidenceLevel.Vendor)
            : new OverclockReading("overclock_gpu_name", string.Empty, EvidenceLevel.Empirical, "overclock-nvml-device-unavailable");

    private static OverclockReading Clock(IntPtr handle, string labelKey, NvidiaClockDomain domain, bool max)
    {
        uint mhz = 0;
        bool ok = max
            ? NvmlInterop.TryGetMaxClockInfo(handle, domain, out mhz)
            : NvmlInterop.TryGetClockInfo(handle, domain, out mhz);

        return ok
            ? new OverclockReading(labelKey, FormattableString.Invariant($"{mhz} MHz"), EvidenceLevel.Vendor)
            : new OverclockReading(labelKey, string.Empty, EvidenceLevel.Empirical, "overclock-gpu-clock-unavailable");
    }

    private static OverclockReading Fan(IntPtr handle)
    {
        if (NvmlInterop.TryGetFanSpeed(handle, out uint percent))
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
        int? milliwatts = _tuning.ReadPowerLimitMilliwatts();
        return milliwatts is int mw
            ? new OverclockReading(
                "overclock_gpu_power_limit",
                FormattableString.Invariant($"{mw / 1000d} W"),
                EvidenceLevel.Vendor)
            : new OverclockReading("overclock_gpu_power_limit", string.Empty, EvidenceLevel.Empirical, "overclock-gpu-power-unavailable");
    }

    private OverclockReading Driver()
    {
        string? version = _driverVersion;
        return version is null
            ? new OverclockReading("overclock_gpu_driver", string.Empty, EvidenceLevel.Empirical, "overclock-gpu-driver-unavailable")
            : new OverclockReading("overclock_gpu_driver", version, EvidenceLevel.Vendor);
    }

    /// <summary>The ceilings the driver reports, so the UI can show what bounds the tuning.</summary>
    private OverclockReading WriteLimits()
    {
        if (!_tuning.TryDescribe(out NvidiaTuningDevice? described) || described is null)
        {
            return new OverclockReading("overclock_gpu_write_limits", string.Empty, EvidenceLevel.Empirical, _tuning.UnavailableReasonKey ?? "overclock-nvml-missing");
        }

        if (described.MaxPowerLimitMilliwatts is not int max || described.MinPowerLimitMilliwatts is not int min)
        {
            return new OverclockReading("overclock_gpu_write_limits", string.Empty, EvidenceLevel.Empirical, "overclock-nvml-device-unavailable");
        }

        return new OverclockReading(
            "overclock_gpu_write_limits",
            FormattableString.Invariant($"{min / 1000d} W - {max / 1000d} W"),
            EvidenceLevel.Vendor);
    }

    /// <summary>
    /// The raw status of the last write. CA-O does not classify codes it does
    /// not know, so the number itself is what the user gets (design 6.4).
    /// </summary>
    private OverclockReading LastWriteStatus()
    {
        lock (_gate)
        {
            return _lastWriteStatus is int status
                ? new OverclockReading(
                    "overclock_gpu_last_write_status",
                    status.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    EvidenceLevel.Vendor)
                : new OverclockReading("overclock_gpu_last_write_status", string.Empty, EvidenceLevel.Empirical, "overclock-gpu-no-write-yet");
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

            if (NvmlInterop.TryGetSystemDriverVersion(out string version))
            {
                _driverVersion = version;
                return version;
            }

            return null;
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
