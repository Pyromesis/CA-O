using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using CAO.Core.Overclock;
using CAO.Core.Overclock.Reporting;
using CAO.Infrastructure.Gaming;
using CAO.Infrastructure.Overclock;
using CAO.Infrastructure.Overclock.Reporting;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CAO.UI.ViewModels;

/// <summary>
/// The six states every control in this section must be able to express
/// (design 6.10). A control that cannot say why it is inert is a lie, so
/// there is no "hidden" state: everything is either actionable or explained.
/// </summary>
public enum OverclockControlState
{
    /// <summary>Backend present and the value can be written.</summary>
    Available,

    /// <summary>No backend for this component exists on this machine (no NVIDIA GPU, etc).</summary>
    Unsupported,

    /// <summary>Backend exists in the catalog but its prerequisite is missing (XTU not installed).</summary>
    DependencyMissing,

    /// <summary>The backend refused the access level (NVAPI_INVALID_USER_PRIVILEGE).</summary>
    NoPermission,

    /// <summary>A write is in flight right now.</summary>
    Applying,

    /// <summary>The installed driver rejected the setter, so the section degrades to read-only.</summary>
    DriverBlocked,
}

/// <summary>One catalog knob rendered as a row. Immutable: it describes a probe, not a form.</summary>
public sealed record OverclockKnobRow(
    string Id,
    string Label,
    string Description,
    string CurrentValue,
    string Range,
    string Evidence,
    OverclockControlState State,
    string StateLabel,
    string Reason,
    bool IsActionable);

/// <summary>One hardware reading with its evidence tier. Never collapsed into a score.</summary>
public sealed record OverclockReadingRow(string Label, string Value, string Evidence, string Reason);

/// <summary>A detected game and what CA-O can offer it without touching its files.</summary>
public sealed record OverclockGameRow(string Name, string Executable, string Source, string State);

/// <summary>
/// One axis of the before/after comparison. There is no knob on it: the
/// profile is measured as a package, because overclocking is not additive and
/// splitting the delta per knob would be arithmetic dressed as measurement.
/// </summary>
public sealed record OverclockDeltaRow(
    string Axis,
    string Baseline,
    string Candidate,
    string Delta,
    string Verdict,
    string Reason);

/// <summary>
/// Read-only Overclock section. Phase F1: it reports what the hardware actually
/// exposes through vendor APIs and states plainly which controls are not yet
/// writable. It never claims a value it did not read.
/// </summary>
public sealed partial class OverclockViewModel : ObservableObject
{
    private readonly IOverclockBackendRegistry _registry;
    private readonly IReadOnlyList<IOverclockBackend> _backends;
    private readonly GameDetectionProvider _games;

    private IOverclockDeltaSource? _deltaSource;
    private IOverclockDeltaStore? _store;
    private DeltaSample? _baseline;
    private DeltaSample? _candidate;
    private OverclockDeltaReport? _report;
    private OverclockDeltaReport? _storedReport;

    public OverclockViewModel()
        : this(OverclockReadOnlyRegistry.CreateDefault(), DefaultBackends(), new GameDetectionProvider())
    {
    }

    public OverclockViewModel(
        IOverclockBackendRegistry registry,
        IReadOnlyList<IOverclockBackend> backends,
        GameDetectionProvider games,
        IOverclockDeltaSource? deltaSource = null,
        IOverclockDeltaStore? store = null)
    {
        _registry = registry;
        _backends = backends;
        _games = games;
        _deltaSource = deltaSource;
        _store = store;
    }

    /// <summary>
    /// Built on first use rather than in the constructor: the page must open
    /// without paying for a measurement nobody asked for.
    /// </summary>
    private IOverclockDeltaSource DeltaSource =>
        _deltaSource ??= new BenchmarkDeltaSource();

    private IOverclockDeltaStore Store =>
        _store ??= new FileOverclockDeltaStore();

    // Single source of truth: the registry decides the composition, the
    // ViewModel never re-lists the backends by hand (that divergence is
    // exactly how the CPU backend went missing once already).
    private static IReadOnlyList<IOverclockBackend> DefaultBackends() =>
        OverclockReadOnlyRegistry.DefaultBackends();

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _headline = string.Empty;

    [ObservableProperty]
    private string _statusLine = string.Empty;

    [ObservableProperty]
    private string _cpuSummary = string.Empty;

    [ObservableProperty]
    private string _gpuSummary = string.Empty;

    [ObservableProperty]
    private string _memorySummary = string.Empty;

    [ObservableProperty]
    private string _stabilitySummary = string.Empty;

    [ObservableProperty]
    private string _resultSummary = string.Empty;

    [ObservableProperty]
    private string _baselineSummary = string.Empty;

    public ObservableCollection<OverclockKnobRow> CpuKnobs { get; } = [];

    public ObservableCollection<OverclockKnobRow> GpuKnobs { get; } = [];

    public ObservableCollection<OverclockKnobRow> MemoryKnobs { get; } = [];

    public ObservableCollection<OverclockReadingRow> CpuReadings { get; } = [];

    public ObservableCollection<OverclockReadingRow> GpuReadings { get; } = [];

    public ObservableCollection<OverclockReadingRow> MemoryReadings { get; } = [];

    public ObservableCollection<OverclockGameRow> Games { get; } = [];

    public ObservableCollection<OverclockDeltaRow> DeltaRows { get; } = [];

    /// <summary>Reentrancy guard: a second probe while one is running is dropped, not queued.</summary>
    [RelayCommand]
    private async Task RefreshAsync(CancellationToken ct)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            Headline = Localizer.Get("overclock_title");
            StatusLine = Localizer.Get("overclock_probing");

            // Vendor P/Invoke and WMI both block; none of them belong on the UI thread.
            var capabilities = await Task.Run(() => _registry.ProbeAll(ct), ct).ConfigureAwait(true);

            CpuKnobs.Clear();
            GpuKnobs.Clear();
            MemoryKnobs.Clear();
            CpuReadings.Clear();
            GpuReadings.Clear();
            MemoryReadings.Clear();
            Games.Clear();

            foreach (var knob in capabilities.Knobs)
            {
                var row = await BuildRowAsync(knob, capabilities, ct).ConfigureAwait(true);
                switch (knob.Component)
                {
                    case OverclockComponent.Cpu:
                        CpuKnobs.Add(row);
                        break;
                    case OverclockComponent.Gpu:
                        GpuKnobs.Add(row);
                        break;
                    default:
                        MemoryKnobs.Add(row);
                        break;
                }
            }

            await FillReadingsAsync(ct).ConfigureAwait(true);
            await FillGamesAsync(ct).ConfigureAwait(true);

            Summarize(capabilities);
            await LoadSavedDeltaAsync(ct).ConfigureAwait(true);
            StatusLine = Localizer.Format("overclock_probe_done", capabilities.Knobs.Count);
        }
        catch (OperationCanceledException)
        {
            StatusLine = Localizer.Get("overclock_cancelled");
        }
        catch (Exception ex)
        {
            StatusLine = Localizer.Get("overclock_probe_failed");
            App.WriteCrashLog(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Captures the "before" pass. Without one there is nothing to compare
    /// against, and a delta against an imagined baseline is precisely the
    /// number this section refuses to invent.
    /// </summary>
    [RelayCommand]
    private async Task CaptureBaselineAsync(CancellationToken ct)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            StatusLine = Localizer.Get("overclock-delta-running");

            // The trial set is the heaviest thing this page can ask for: it
            // runs CPU, memory and disk passes several times. It never touches
            // the UI thread, and its ceiling is announced before it starts.
            _baseline = await DeltaSource.CaptureAsync(ct).ConfigureAwait(true);

            // A new baseline invalidates every comparison made against the
            // old one. Leaving the previous verdict on screen would attribute
            // a candidate to a baseline the user can no longer reproduce.
            _candidate = null;
            _report = null;
            DeltaRows.Clear();
            BaselineSummary = Localizer.Format(
                "overclock-delta-baseline-captured", FormatTime(_baseline.TakenAtUtc));
            ResultSummary = Localizer.Get("overclock-result-pending");
            StatusLine = Localizer.Get("overclock-delta-baseline-done");
        }
        catch (OperationCanceledException)
        {
            StatusLine = Localizer.Get("overclock_cancelled");
        }
        catch (Exception ex)
        {
            StatusLine = Localizer.Get("overclock-delta-failed");
            App.WriteCrashLog(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Captures the "after" pass and grades it against the baseline. The
    /// verdict is per axis and for the profile as a whole; it never splits the
    /// delta across knobs, because the knobs are not independent.
    /// </summary>
    [RelayCommand]
    private async Task CaptureCandidateAsync(CancellationToken ct)
    {
        if (IsBusy)
        {
            return;
        }

        if (_baseline is null)
        {
            StatusLine = Localizer.Get("overclock-delta-baseline-missing");
            return;
        }

        IsBusy = true;
        try
        {
            StatusLine = Localizer.Get("overclock-delta-running");
            _candidate = await DeltaSource.CaptureAsync(ct).ConfigureAwait(true);

            _report = OverclockDeltaBuilder.Build(_baseline, _candidate);
            FillDeltaRows(_report);
            ResultSummary = VerdictLabel(_report.Verdict);
            StatusLine = Localizer.Get("overclock-delta-candidate-done");

            await Store.SaveAsync(_report, ct).ConfigureAwait(true);
            _storedReport = _report;
        }
        catch (OperationCanceledException)
        {
            StatusLine = Localizer.Get("overclock_cancelled");
        }
        catch (Exception ex)
        {
            // The report is already on screen by now: failing to cache it must
            // not take the measurement down with it.
            StatusLine = Localizer.Get("overclock-delta-save-failed");
            App.WriteCrashLog(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Shows the last saved comparison when this session has not measured one
    /// yet, so reopening the page does not make a real result vanish. A stored
    /// report with no data is skipped: an empty table is not an improvement.
    /// </summary>
    private async Task LoadSavedDeltaAsync(CancellationToken ct)
    {
        if (_storedReport is null)
        {
            try
            {
                _storedReport = await Store.LoadAsync(ct).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                // A missing or unreadable cache is a normal state, not a
                // reason to stop showing the hardware.
                App.WriteCrashLog(ex);
                return;
            }
        }

        if (_storedReport is not { HasData: true })
        {
            return;
        }

        _report ??= _storedReport;
        FillDeltaRows(_report);
        ResultSummary = VerdictLabel(_report.Verdict);
        BaselineSummary = Localizer.Format(
            "overclock-delta-baseline-captured", FormatTime(_report.MeasuredAtUtc));
    }

    private async Task<OverclockKnobRow> BuildRowAsync(
        OverclockKnob knob,
        OverclockCapabilities capabilities,
        CancellationToken ct)
    {
        var backend = FindBackend(knob.BackendId);
        var availability = capabilities.Details.FirstOrDefault(d =>
            string.Equals(d.BackendId, knob.BackendId, StringComparison.Ordinal));

        // A knob whose backend never answered is not "supported" — it is unknown.
        var state = ResolveState(knob, backend, availability);

        var current = string.Empty;
        if (backend is not null && state is not (OverclockControlState.Unsupported
            or OverclockControlState.DependencyMissing))
        {
            var read = await backend.ReadAsync(knob.Id, ct).ConfigureAwait(true);
            current = read.Success
                ? FormatValue(knob, read.Value)
                : Localizer.Get(read.FailureReasonKey ?? "overclock_read_failed");
        }
        else if (backend is null)
        {
            current = Localizer.Get("overclock_no_backend");
        }

        return new OverclockKnobRow(
            knob.Id.Value,
            Localizer.Get(knob.LabelKey),
            Localizer.Get(knob.DescriptionKey),
            current,
            DescribeRange(knob),
            Localizer.GetEvidenceLabel(knob.Evidence),
            state,
            StateLabel(state),
            ReasonFor(state, availability, knob, backend),
            state == OverclockControlState.Available);
    }

    private static OverclockControlState ResolveState(
        OverclockKnob knob,
        IOverclockBackend? backend,
        BackendAvailability? availability)
    {
        if (backend is null)
        {
            return OverclockControlState.DependencyMissing;
        }

        // The backend exists but its prerequisite (XTU, driver version) did not materialise.
        if (availability is { Available: false })
        {
            return OverclockControlState.DependencyMissing;
        }

        if (backend is IOverclockWriteGate gate)
        {
            if (gate.CanWrite(knob.Id))
            {
                return OverclockControlState.Available;
            }

            var blocked = gate.BlockReasonKey(knob.Id);
            return blocked is null
                ? OverclockControlState.DriverBlocked
                : OverclockControlState.DriverBlocked;
        }

        // Report-only by construction (memory lives in the SPD EEPROM).
        return OverclockControlState.Unsupported;
    }

    private string ReasonFor(
        OverclockControlState state,
        BackendAvailability? availability,
        OverclockKnob knob,
        IOverclockBackend? backend)
    {
        switch (state)
        {
            case OverclockControlState.Available:
                return string.Empty;
            case OverclockControlState.Unsupported:
                return knob.Component == OverclockComponent.Memory
                    ? Localizer.Get("overclock-memory-readonly-spd")
                    : Localizer.Get("overclock_read_failed");
            case OverclockControlState.DependencyMissing:
                return availability is null
                    ? Localizer.Get("overclock_no_backend")
                    : Localizer.Get(availability.UnavailableReasonKey);
            default:
                if (WriteBlockReason(backend, knob) is { } reason)
                {
                    return Localizer.Get(reason);
                }

                return availability is null
                    ? Localizer.Get("overclock_read_failed")
                    : Localizer.Get(availability.UnavailableReasonKey);
        }

    }

    /// <summary>
    /// Why a knob is not writable, asked of the backend that owns it.
    ///
    /// This used to be a switch on the component with the reason keys typed in
    /// by hand, which is exactly how the GPU section ended up referencing a
    /// constant that no longer existed. Only the backend can tell a missing DLL
    /// from a missing device from a driver that does not implement the setter.
    /// </summary>
    private static string? WriteBlockReason(IOverclockBackend? backend, OverclockKnob knob) =>
        backend is IOverclockWriteGate gate ? gate.BlockReasonKey(knob.Id) : null;

    private async Task FillReadingsAsync(CancellationToken ct)
    {
        foreach (var backend in _backends)
        {
            if (backend is not IOverclockDetailSource source)
            {
                continue;
            }

            IReadOnlyList<OverclockReading> readings;
            try
            {
                readings = await source.ReadDetailsAsync(ct).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // One backend failing must not blank the other two sections.
                App.WriteCrashLog(ex);
                readings = [new OverclockReading(backend.Id, "-", Shared.EvidenceLevel.Unknown, "overclock-readings-failed")];
            }

            var sink = backend.Id == OverclockCatalogBackendIds.Memory
                ? MemoryReadings
                : GpuReadings;

            foreach (var reading in readings)
            {
                sink.Add(new OverclockReadingRow(
                    Label(reading.LabelKey),
                    reading.Value,
                    Localizer.GetEvidenceLabel(reading.Evidence),
                    reading.UnavailableReasonKey is null ? string.Empty : Localizer.Get(reading.UnavailableReasonKey)));
            }
        }
    }

    private async Task FillGamesAsync(CancellationToken ct)
    {
        var detected = await _games.DetectAsync(ct).ConfigureAwait(true);
        foreach (var game in detected)
        {
            Games.Add(new OverclockGameRow(
                game.Name,
                game.ExecutableName,
                game.Source,
                Localizer.Get("overclock-game-no-profile")));
        }
    }

    private void Summarize(OverclockCapabilities capabilities)
    {
        var cpu = capabilities.Details.FirstOrDefault(d =>
            string.Equals(d.BackendId, OverclockCatalogBackendIds.Cpu, StringComparison.Ordinal));
        var gpu = capabilities.Details.FirstOrDefault(d =>
            string.Equals(d.BackendId, OverclockCatalogBackendIds.Gpu, StringComparison.Ordinal));
        var mem = capabilities.Details.FirstOrDefault(d =>
            string.Equals(d.BackendId, OverclockCatalogBackendIds.Memory, StringComparison.Ordinal));

        CpuSummary = capabilities.Cpu switch
        {
            CpuBackendKind.PolicyOnly => Localizer.Get("overclock-cpu-policy-only"),
            CpuBackendKind.XtuGuided => Localizer.Get("overclock-cpu-xtu"),
            CpuBackendKind.MsrSigned => Localizer.Get("overclock-cpu-msr"),
            _ => Localizer.Get(cpu?.UnavailableReasonKey ?? "overclock-cpu-none"),
        };

        GpuSummary = capabilities.Gpu switch
        {
            GpuBackendKind.NvidiaNvapi => gpu?.DriverVersion is { Length: > 0 } v
                ? Localizer.Format("overclock-gpu-driver", v)
                : Localizer.Get("overclock-gpu-nvapi"),
            GpuBackendKind.AmdAdlx => Localizer.Get("overclock-gpu-adlx"),
            _ => Localizer.Get(gpu?.UnavailableReasonKey ?? "overclock-gpu-none"),
        };

        MemorySummary = capabilities.Memory == MemoryBackendKind.SmbiosReportOnly
            ? Localizer.Get("overclock-memory-smbios")
            : Localizer.Get(mem?.UnavailableReasonKey ?? "overclock-memory-none");

        StabilitySummary = Localizer.Get("overclock-stability-pending");

        // A re-probe must not wipe a delta the user already paid a minute for;
        // only a page that has never measured anything says "pending".
        ResultSummary = _report is null
            ? Localizer.Get("overclock-result-pending")
            : VerdictLabel(_report.Verdict);
    }

    private IOverclockBackend? FindBackend(string backendId) =>
        _backends.FirstOrDefault(b => string.Equals(b.Id, backendId, StringComparison.Ordinal));

    private string StateLabel(OverclockControlState state) => Localizer.Get(state switch
    {
        OverclockControlState.Available => "overclock-state-available",
        OverclockControlState.Unsupported => "overclock-state-unsupported",
        OverclockControlState.DependencyMissing => "overclock-state-dependency",
        OverclockControlState.NoPermission => "overclock-state-nopermission",
        OverclockControlState.Applying => "overclock-state-applying",
        _ => "overclock-state-driverblocked",
    });

    private static string FormatValue(OverclockKnob knob, KnobValue value)
    {
        if (knob.Kind == KnobKind.Enum && knob.Range.Allowed is { Count: > 0 } allowed)
        {
            return value.Index >= 0 && value.Index < allowed.Count
                ? value.Index.ToString(CultureInfo.InvariantCulture)
                : value.ToString();
        }

        var unit = knob.Unit switch
        {
            KnobUnit.Hertz => Localizer.Get("overclock-unit-hertz"),
            KnobUnit.Percent => "%",
            KnobUnit.Celsius => "°C",
            KnobUnit.Enumerated => string.Empty,
            _ => string.Empty,
        };

        var number = knob.Unit == KnobUnit.Hertz && value.Scalar >= 1_000_000d
            ? (value.Scalar / 1_000_000d).ToString("0.##", CultureInfo.InvariantCulture)
            : value.Scalar.ToString("0.##", CultureInfo.InvariantCulture);

        return unit.Length == 0 ? number : string.Concat(number, " ", unit);
    }

    private static string DescribeRange(OverclockKnob knob)
    {
        if (knob.Kind == KnobKind.Enum)
        {
            return string.Empty;
        }

        var min = knob.Range.Min.ToString("0.##", CultureInfo.InvariantCulture);
        var max = knob.Range.Max.ToString("0.##", CultureInfo.InvariantCulture);
        return string.Concat(min, " - ", max);
    }

    /// <summary>
    /// Vendor keys are dotted; a key we have no translation for must still read as
    /// a label instead of leaking "overclock_gpu_clock_current" to the user.
    /// </summary>
    private static string Label(string key)
    {
        var localized = Localizer.Get(key);
        if (!string.Equals(localized, key, StringComparison.Ordinal))
        {
            return localized;
        }

        var text = key.Replace('.', '_').Replace('_', ' ');
        var builder = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (ch == ' ')
            {
                continue;
            }

            if (builder.Length == 0 || char.IsUpper(ch))
            {
                if (builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(char.ToUpperInvariant(ch));
            }
            else
            {
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }

    private void FillDeltaRows(OverclockDeltaReport report)
    {
        DeltaRows.Clear();
        foreach (var axis in report.Axes)
        {
            DeltaRows.Add(new OverclockDeltaRow(
                AxisLabel(axis.Axis),
                FormatDeltaNumber(axis.Baseline, axis.Unit),
                FormatDeltaNumber(axis.Candidate, axis.Unit),
                axis.DeltaPercent is { } delta
                    ? Localizer.Format(
                        "overclock-delta-percent",
                        delta.ToString("+0.##;-0.##;0", CultureInfo.InvariantCulture))
                    : Localizer.Get("overclock-delta-unavailable"),
                VerdictLabel(axis.Verdict),
                string.IsNullOrEmpty(axis.ReasonKey) ? string.Empty : Localizer.Get(axis.ReasonKey)));
        }
    }

    private static string AxisLabel(DeltaAxis axis) => Localizer.Get(axis switch
    {
        DeltaAxis.Cpu => "overclock-axis-cpu",
        DeltaAxis.Memory => "overclock-axis-memory",
        DeltaAxis.StorageRead => "overclock-axis-storage-read",
        _ => "overclock-axis-storage-write",
    });

    private static string VerdictLabel(DeltaVerdict verdict) => Localizer.Get(verdict switch
    {
        DeltaVerdict.Improvement => "overclock-delta-verdict-improvement",
        DeltaVerdict.NoMeasurableChange => "overclock-delta-verdict-no-change",
        DeltaVerdict.Regression => "overclock-delta-verdict-regression",
        _ => "overclock-delta-verdict-no-data",
    });

    private static string FormatDeltaNumber(double? value, string unitKey)
    {
        if (value is not { } number)
        {
            return Localizer.Get("overclock-delta-unavailable");
        }

        var unit = Localizer.Get(unitKey);
        var text = number.ToString("0.##", CultureInfo.InvariantCulture);

        // An untranslated unit key would otherwise be glued onto the number
        // and read as part of it.
        return unit.Length == 0 || string.Equals(unit, unitKey, StringComparison.Ordinal)
            ? text
            : string.Concat(text, " ", unit);
    }

    private static string FormatTime(DateTimeOffset at) =>
        at.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    private static class OverclockCatalogBackendIds
    {
        public const string Cpu = "intel-policy";
        public const string Gpu = "nvidia-nvapi";
        public const string Memory = "smbios-memory";
    }
}