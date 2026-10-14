using CAO.Core.Overclock;
using CAO.Core.Overclock.OverclockCatalog;
using CAO.Shared;

namespace CAO.Infrastructure.Overclock;

/// <summary>
/// AMD backend: power limit only, through ADLX (<c>amdadlx64.dll</c>).
///
/// Why power limit only: it is the one tuning control whose unit is
/// unambiguous once the driver's stock value is known (see
/// <see cref="DriverStockPercent"/>). AMD clocks stay out on purpose: from Navi4 up
/// <c>SetGPUMaxFrequency</c> takes an offset from base frequency, before that
/// an absolute frequency, and this module refuses to guess which one a card
/// expects. Knobs without a public setter report <see cref="NvidiaGpuBackend.NoPublicSetterReasonKey"/>,
/// the same generic key the NVIDIA backend uses: one Localizer entry, one meaning.
///
/// Anti-cheat posture: P/Invoke into the vendor's own signed DLL, shipped by
/// the driver, invoked when present and never redistributed. No kernel
/// component, no overlay, no injection, no game directory (design 6.14).
///
/// Hardware honesty: this write path was verified against the official GPUOpen
/// headers (SDK v2.0.0.125) but has never executed on AMD silicon, because the
/// build machine carries an NVIDIA card. On a machine without AMD hardware the
/// probe below reports <c>None</c> with the real reason; it never claims otherwise.
/// </summary>
public sealed class AmdAdlxBackend : IOverclockBackend, IOverclockDetailSource, IOverclockWriteGate
{
    /// <summary>The driver wants a factory reset before it accepts manual tuning.</summary>
    public const string ResetNeededReasonKey = "overclock-adlx-reset-needed";

    /// <summary>The GPU is busy and refused the write for now.</summary>
    public const string GpuInUseReasonKey = "overclock-adlx-gpu-in-use";

    /// <summary>The driver's own power bounds are unreadable, so CA-O will not guess a safe value.</summary>
    public const string RangeUnavailableReasonKey = "overclock-adlx-range-unavailable";

    /// <summary>One or more snapshot values could not be put back.</summary>
    public const string RestoreFailedReasonKey = "overclock-gpu-restore-failed";

    private readonly IAmdTuningApi _tuning;
    private readonly object _gate = new();
    private int? _lastWriteStatus;

    /// <summary>Production instance: the real ADLX-backed tuning surface.</summary>
    public AmdAdlxBackend()
        : this(AdlxTuningApi.Instance)
    {
    }

    /// <summary>Test seam: injects the tuning surface so the backend can be exercised without AMD silicon.</summary>
    internal AmdAdlxBackend(IAmdTuningApi tuning)
    {
        _tuning = tuning;
    }

    public string Id => OverclockCatalog.AmdAdlxBackendId;

    /// <summary>
    /// Reports availability, never throws. A machine with no AMD driver is a
    /// supported configuration: the GPU section degrades with the real reason,
    /// it does not error out.
    /// </summary>
    public OverclockCapabilities Probe(CancellationToken ct)
    {
        if (!_tuning.IsAvailable)
        {
            return Unavailable(_tuning.UnavailableReasonKey ?? "overclock-adlx-missing", null);
        }

        if (!_tuning.TryDescribe(out AmdTuningDevice? device) || device is null)
        {
            return Unavailable("overclock-adlx-device-unavailable", null);
        }

        // ADLX's verified read chain exposes no driver-version getter, so the
        // evidence is Empirical: a real answer from the driver, without a
        // version string CA-O cannot produce.
        var availability = new BackendAvailability(
            Id,
            Available: true,
            UnavailableReasonKey: string.Empty,
            RemedyHintKey: null,
            RequiresRestart: false,
            DriverVersion: null,
            LastProbeEvidence: EvidenceLevel.Empirical);

        return new OverclockCapabilities(
            CpuBackendKind.PolicyOnly,
            GpuBackendKind.AmdAdlx,
            MemoryBackendKind.SmbiosReportOnly,
            [availability],
            OverclockCatalog.AmdGpuKnobs);
    }

    /// <summary>
    /// Reads the power limit through the write path's own getter, so the number
    /// on screen is the number that would be verified after a change. Anything
    /// else has no public setter and reports that instead of a fabricated zero.
    /// </summary>
    public ValueTask<KnobRead> ReadAsync(KnobId id, CancellationToken ct)
    {
        if (id != AmdKnobs.PowerLimit)
        {
            return ValueTask.FromResult(KnobRead.Fail(NvidiaGpuBackend.NoPublicSetterReasonKey));
        }

        int? percent = _tuning.ReadPowerLimitPercent();
        if (percent is null)
        {
            return ValueTask.FromResult(KnobRead.Fail("overclock-gpu-power-unavailable"));
        }

        if (!_tuning.TryDescribe(out AmdTuningDevice? device) || device is null)
        {
            return ValueTask.FromResult(KnobRead.Fail(RangeUnavailableReasonKey));
        }

        return ValueTask.FromResult(KnobRead.Ok(KnobValue.FromScalar(ToCatalogPercent(percent.Value, device))));
    }

    /// <summary>
    /// The value ADLX reports for "stock". Uses the driver's own default when
    /// IADLXManualPowerTuning1 answers; otherwise infers it from the range: a
    /// range that dips below zero or tops out at a small number is an offset
    /// around 0 (Adrenalin's "-10 % .. +15 %"), anything else is absolute % of TDP.
    /// </summary>
    internal static int DriverStockPercent(AmdTuningDevice device)
    {
        if (device.DefaultPowerLimitPercent is int stock)
        {
            return stock;
        }

        bool isOffset = device.MinPowerLimitPercent is < 0 || device.MaxPowerLimitPercent is <= 50;
        return isOffset ? 0 : 100;
    }

    /// <summary>
    /// The catalog speaks "% of stock" (100 = factory). ADLX may speak an offset
    /// around 0 instead; writing the catalog's 100 into an offset range would be
    /// clamped to the maximum, so the Conservative profile maxed the card out.
    /// </summary>
    internal static double ToCatalogPercent(int driverValue, AmdTuningDevice device) =>
        100 + (driverValue - DriverStockPercent(device));

    internal static int ToDriverPercent(double catalogValue, AmdTuningDevice device) =>
        (int)Math.Round(catalogValue) - 100 + DriverStockPercent(device);

    /// <summary>
    /// Applies the power limit, then reads it back. A write that does not stick
    /// is a failure, never a success: reporting "applied" for a value the
    /// hardware ignored is exactly the kind of lie this feature exists to remove.
    /// </summary>
    public ValueTask<KnobWriteResult> WriteAsync(KnobId id, KnobValue value, CancellationToken ct)
    {
        if (!_tuning.IsAvailable)
        {
            return ValueTask.FromResult(KnobWriteResult.Fail(_tuning.UnavailableReasonKey ?? "overclock-adlx-missing"));
        }

        if (id != AmdKnobs.PowerLimit)
        {
            return ValueTask.FromResult(KnobWriteResult.Fail(NvidiaGpuBackend.NoPublicSetterReasonKey));
        }

        return ValueTask.FromResult(WritePowerLimit(value));
    }

    /// <summary>
    /// Captures the power limit in the DRIVER's own unit (raw ADLX value), the
    /// same way the NVIDIA path stores milliwatts: the restore hands it straight
    /// back to the driver with no conversion that could drift in between.
    /// </summary>
    public ValueTask<UnitSnapshot> SnapshotAsync(CancellationToken ct)
    {
        var values = new List<KeyValuePair<KnobId, KnobValue>>();

        int? percent = _tuning.ReadPowerLimitPercent();
        if (percent is not null)
        {
            values.Add(new KeyValuePair<KnobId, KnobValue>(
                AmdKnobs.PowerLimit,
                KnobValue.FromScalar(percent.Value)));
        }

        return ValueTask.FromResult(
            new UnitSnapshot(OverclockComponent.Gpu, values, DateTimeOffset.UtcNow, null));
    }

    /// <summary>
    /// Puts every captured value back. A value that cannot be restored does not
    /// abort the batch (design 6.12).
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
    /// number with <see cref="EvidenceLevel.Empirical"/> or an explicit "not
    /// available" with its reason. There is no third option.
    /// </summary>
    public ValueTask<IReadOnlyList<OverclockReading>> ReadDetailsAsync(CancellationToken ct)
    {
        var readings = new List<OverclockReading>();

        if (!_tuning.IsAvailable)
        {
            string reason = _tuning.UnavailableReasonKey ?? "overclock-adlx-missing";
            readings.Add(new OverclockReading("overclock_gpu_name", string.Empty, EvidenceLevel.Empirical, reason));
            return new ValueTask<IReadOnlyList<OverclockReading>>(readings);
        }

        readings.Add(Power());
        readings.Add(WriteLimits());
        readings.Add(Driver());
        readings.Add(LastWriteStatus());

        return new ValueTask<IReadOnlyList<OverclockReading>>(readings);
    }

    /// <summary>True only for the power limit, on a machine where ADLX answers.</summary>
    public bool CanWrite(KnobId knob)
    {
        if (!_tuning.IsAvailable || knob != AmdKnobs.PowerLimit)
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
            return _tuning.UnavailableReasonKey ?? "overclock-adlx-missing";
        }

        if (knob != AmdKnobs.PowerLimit)
        {
            return NvidiaGpuBackend.NoPublicSetterReasonKey;
        }

        return _tuning.TryDescribe(out _) ? null : "overclock-adlx-device-unavailable";
    }

    private void RecordStatus(int status)
    {
        lock (_gate)
        {
            _lastWriteStatus = status;
        }
    }

    private KnobWriteResult WritePowerLimit(KnobValue requested)
    {
        if (!_tuning.TryDescribe(out AmdTuningDevice? described) || described is null)
        {
            return KnobWriteResult.Fail("overclock-adlx-device-unavailable");
        }

        if (described.MinPowerLimitPercent is null || described.MaxPowerLimitPercent is null)
        {
            // Without the driver's own bounds, CA-O would be inventing the safe
            // range. It does not.
            return KnobWriteResult.Fail(RangeUnavailableReasonKey);
        }

        int target = Math.Clamp(
            ToDriverPercent(requested.Scalar, described),
            described.MinPowerLimitPercent.Value,
            described.MaxPowerLimitPercent.Value);

        int status = _tuning.SetPowerLimitPercent(target);
        RecordStatus(status);

        switch (AmdAdlxInterop.ClassifyWrite(status))
        {
            case AmdTuningWriteOutcome.DriverBlocked:
                return KnobWriteResult.Blocked(BlockReasonKey(AmdKnobs.PowerLimit) ?? "overclock-gpu-driver-blocked");

            case AmdTuningWriteOutcome.ResetNeeded:
                return KnobWriteResult.Fail(ResetNeededReasonKey);

            case AmdTuningWriteOutcome.GpuInUse:
                return KnobWriteResult.Fail(GpuInUseReasonKey);

            case AmdTuningWriteOutcome.Rejected:
                return KnobWriteResult.Fail(NvidiaGpuBackend.RejectedReasonKey);

            default:
                break;
        }

        // The driver said yes. Only a read-back decides whether it stuck, and a
        // value that did not stick is a failure, not a blocked driver.
        // The reported value is the one the driver actually got, not the one
        // that was asked for: after a clamp those differ.
        int? readBack = _tuning.ReadPowerLimitPercent();
        return readBack == target
            ? KnobWriteResult.Ok(KnobValue.FromScalar(ToCatalogPercent(target, described)))
            : KnobWriteResult.Fail(NvidiaGpuBackend.VerifyFailedReasonKey);
    }

    private KnobWriteResult RestoreOne(KnobId knob, KnobValue stored)
    {
        if (knob == AmdKnobs.PowerLimit)
        {
            int status = _tuning.SetPowerLimitPercent((int)Math.Round(stored.Scalar));
            RecordStatus(status);
            return AmdAdlxInterop.ClassifyWrite(status) == AmdTuningWriteOutcome.Applied
                ? KnobWriteResult.Ok(stored)
                : KnobWriteResult.Fail(NvidiaGpuBackend.RejectedReasonKey);
        }

        // Anything else was never a setting; there is nothing to put back.
        return KnobWriteResult.Ok(stored);
    }

    private OverclockReading Power()
    {
        int? percent = _tuning.ReadPowerLimitPercent();
        if (percent is null)
        {
            return new OverclockReading("overclock_gpu_power_limit", string.Empty, EvidenceLevel.Empirical, "overclock-gpu-power-unavailable");
        }

        double shown = _tuning.TryDescribe(out AmdTuningDevice? device) && device is not null
            ? ToCatalogPercent(percent.Value, device)
            : percent.Value;
        return new OverclockReading(
            "overclock_gpu_power_limit",
            FormattableString.Invariant($"{shown} %"),
            EvidenceLevel.Empirical);
    }

    private OverclockReading WriteLimits()
    {
        if (!_tuning.TryDescribe(out AmdTuningDevice? described) || described is null)
        {
            return new OverclockReading("overclock_gpu_write_limits", string.Empty, EvidenceLevel.Empirical, _tuning.UnavailableReasonKey ?? "overclock-adlx-missing");
        }

        if (described.MinPowerLimitPercent is not int min || described.MaxPowerLimitPercent is not int max)
        {
            return new OverclockReading("overclock_gpu_write_limits", string.Empty, EvidenceLevel.Empirical, RangeUnavailableReasonKey);
        }

        return new OverclockReading(
            "overclock_gpu_write_limits",
            FormattableString.Invariant($"{ToCatalogPercent(min, described)} % - {ToCatalogPercent(max, described)} %"),
            EvidenceLevel.Empirical);
    }

    private OverclockReading Driver() =>
        // The verified ADLX chain exposes no driver-version getter. Saying so
        // beats showing a version CA-O never read.
        new OverclockReading("overclock_gpu_driver", string.Empty, EvidenceLevel.Empirical, "overclock-gpu-driver-unavailable");

    private OverclockReading LastWriteStatus()
    {
        lock (_gate)
        {
            return _lastWriteStatus is int status
                ? new OverclockReading(
                    "overclock_gpu_last_write_status",
                    status.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    EvidenceLevel.Empirical)
                : new OverclockReading("overclock_gpu_last_write_status", string.Empty, EvidenceLevel.Empirical, "overclock-gpu-no-write-yet");
        }
    }

    private OverclockCapabilities Unavailable(string reasonKey, string? driverVersion)
    {
        var availability = new BackendAvailability(
            Id,
            Available: false,
            UnavailableReasonKey: reasonKey,
            RemedyHintKey: null,
            RequiresRestart: false,
            DriverVersion: driverVersion,
            LastProbeEvidence: EvidenceLevel.Official);

        return new OverclockCapabilities(
            CpuBackendKind.PolicyOnly,
            GpuBackendKind.None,
            MemoryBackendKind.SmbiosReportOnly,
            [availability],
            OverclockCatalog.AmdGpuKnobs);
    }
}
