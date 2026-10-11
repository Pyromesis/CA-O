using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using CAO.Core.Overclock;
using CAO.Core.Overclock.Reporting;
using CAO.Core.Overclock.Stability;
using CAO.Core.Overclock.Recovery;
using CAO.Core.Overclock.Gaming;
using CAO.Infrastructure.Overclock.Gaming;
using CAO.UI.Helpers;
using CAO.Infrastructure.Overclock;
using CAO.Infrastructure.Overclock.Reporting;
using CAO.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;

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
public sealed record OverclockGameRow(string Name, string Executable, string State, string Reason, bool IsActionable);

/// <summary>
/// A tuning run this machine restarted while it was open. The user picks what
/// happens to it (design D4): there is no option that is "the right one", and
/// the row says why it is being offered rather than claiming the machine hung.
/// </summary>
public sealed record OverclockRecoveryRow(
    string RunId,
    string Knob,
    string Action,
    string Reason,
    string Detail,
    ICommand RevertCommand,
    ICommand KeepCommand,
    ICommand DiscardCommand);

/// <summary>
/// The bound the hardware actually imposes on a setting. Naming the setting
/// here is a fact about the setting, not a share of the measured result.
/// </summary>
public sealed record OverclockCeilingRow(string Knob, string Range, string Reason);
/// <summary>One writable knob with its tune button. The command travels with the row because inside a DataTemplate an x:Bind cannot reach the page ViewModel.</summary>
public sealed record OverclockTuningRow(string KnobId, string Label, string Detail, ICommand TuneCommand);
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
    private IGameOverclockSource? _gameSource;

    [ObservableProperty]
    private string _gameSummary = string.Empty;

    private IOverclockDeltaSource? _deltaSource;
    private IOverclockDeltaStore? _store;
    private DeltaSample? _baseline;
    private DeltaSample? _candidate;
    private OverclockDeltaReport? _report;
    private OverclockDeltaReport? _storedReport;
    private OverclockRecoveryService? _recovery;
    private IOverclockCeilingSource? _ceilingSource;
    private IStabilityProbe? _tuningProbe;
    private IValidatedProfileStore? _profileStore;
    private ITuningCheckpointStore? _checkpointStore;
    private IOverclockJournal? _journal;
    private readonly Dictionary<string, OverclockKnob> _lastKnobs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _lastDriverVersions = new(StringComparer.Ordinal);
    public OverclockViewModel()
        : this(OverclockReadOnlyRegistry.CreateDefault(), DefaultBackends())
    {
    }

    public OverclockViewModel(
        IOverclockBackendRegistry registry,
        IReadOnlyList<IOverclockBackend> backends,
        IOverclockDeltaSource? deltaSource = null,
        IOverclockDeltaStore? store = null,
        OverclockRecoveryService? recovery = null,
        IOverclockCeilingSource? ceilingSource = null,
        IGameOverclockSource? gameSource = null,
        IStabilityProbe? probe = null,
        IValidatedProfileStore? profileStore = null,
        ITuningCheckpointStore? checkpointStore = null,
        IOverclockJournal? journal = null)
    {
        _registry = registry;
        _backends = backends;
        _deltaSource = deltaSource;
        _store = store;
        _recovery = recovery;
        _ceilingSource = ceilingSource;
        _gameSource = gameSource;
        _tuningProbe = probe;
        _profileStore = profileStore;
        _checkpointStore = checkpointStore;
        _journal = journal;
    }

    /// <summary>
    /// Built on first use rather than in the constructor: the page must open
    /// without paying for a measurement nobody asked for.
    /// </summary>
    private IOverclockDeltaSource DeltaSource =>
        _deltaSource ??= new BenchmarkDeltaSource();

    /// <summary>
    /// Game profiles are read on demand for the same reason: opening the page must
    /// not walk the process list before anyone asked for a row.
    /// </summary>
    private IGameOverclockSource GameSource =>
        _gameSource ??= new SystemGameOverclockSource();

    private IOverclockDeltaStore Store =>
        _store ??= new FileOverclockDeltaStore();

    /// <summary>
    /// The recovery engine, wired on first use with the real checkpoint store,
    /// the real boot probe and the same backend list the section already probes.
    /// Injectable so the page can be driven through every decision without a
    /// machine that actually restarted mid-tuning.
    /// </summary>
    private OverclockRecoveryService Recovery =>
        _recovery ??= new OverclockRecoveryService(
            new FileTuningCheckpointStore(),
            new SystemBootHealthProbe(),
            new BackendTuningTargetResolver(DefaultBackends()));

    private IOverclockCeilingSource CeilingSource =>
        _ceilingSource ??= new NvidiaTuningCeilingSource(NvmlTuningApi.Instance);

    private IStabilityProbe TuningProbe =>
        _tuningProbe ??= new WmiClockStallProbe();

    private IValidatedProfileStore ProfileStore =>
        _profileStore ??= new FileValidatedProfileStore();

    private ITuningCheckpointStore CheckpointStore =>
        _checkpointStore ??= new FileTuningCheckpointStore();

    private IOverclockJournal JournalFor(Guid runId) =>
        _journal ?? new TransactionOverclockJournal(new FileTransactionJournal(), runId);

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

    [ObservableProperty]
    private string _recoverySummary = string.Empty;
    public ObservableCollection<OverclockKnobRow> CpuKnobs { get; } = [];

    public ObservableCollection<OverclockKnobRow> GpuKnobs { get; } = [];

    public ObservableCollection<OverclockKnobRow> MemoryKnobs { get; } = [];

    public ObservableCollection<OverclockReadingRow> CpuReadings { get; } = [];

    public ObservableCollection<OverclockReadingRow> GpuReadings { get; } = [];

    public ObservableCollection<OverclockReadingRow> MemoryReadings { get; } = [];

    public ObservableCollection<OverclockGameRow> Games { get; } = [];

    public ObservableCollection<OverclockDeltaRow> DeltaRows { get; } = [];

    public ObservableCollection<OverclockRecoveryRow> PendingRecoveries { get; } = [];

    public ObservableCollection<OverclockCeilingRow> Ceilings { get; } = [];

    public ObservableCollection<OverclockTuningRow> TuningTargets { get; } = [];
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
            CrashLog.Step("refresh.probe-start");

            // Vendor P/Invoke and WMI both block; none of them belong on the UI thread.
            var capabilities = await Task.Run(() => _registry.ProbeAll(ct), ct).ConfigureAwait(true);
            CrashLog.Step("refresh.probe-done");

            CpuKnobs.Clear();
            GpuKnobs.Clear();
            MemoryKnobs.Clear();
            CpuReadings.Clear();
            GpuReadings.Clear();
            MemoryReadings.Clear();
            Games.Clear();
            PendingRecoveries.Clear();
            Ceilings.Clear();
            TuningTargets.Clear();
            _lastKnobs.Clear();
            _lastDriverVersions.Clear();

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

                _lastKnobs[knob.Id.Value] = knob;
                if (row.IsActionable)
                {
                    TuningTargets.Add(new OverclockTuningRow(
                        knob.Id.Value, row.Label, row.Range, StartTuningCommand));
                }
            }

            CrashLog.Step("refresh.rows-done");
            await FillReadingsAsync(ct).ConfigureAwait(true);
            CrashLog.Step("refresh.readings-done");
            await FillGamesAsync(ct).ConfigureAwait(true);
            CrashLog.Step("refresh.games-done");

            Summarize(capabilities);
            await LoadSavedDeltaAsync(ct).ConfigureAwait(true);
            CrashLog.Step("refresh.delta-done");
            await LoadCeilingsAsync(ct).ConfigureAwait(true);
            CrashLog.Step("refresh.ceilings-done");
            await LoadPendingRecoveriesAsync(ct).ConfigureAwait(true);
            CrashLog.Step("refresh.recoveries-done");

            StatusLine = Localizer.Format("overclock_probe_done", capabilities.Knobs.Count);
            CrashLog.Step("refresh.done");
        }
        catch (OperationCanceledException)
        {
            StatusLine = Localizer.Get("overclock_cancelled");
        }
        catch (Exception ex)
        {
            StatusLine = Localizer.Get("overclock_probe_failed");
            CrashLog.Step("refresh.failed");
            CrashLog.Write(ex);
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
            CrashLog.Write(ex);
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
            CrashLog.Write(ex);
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
                CrashLog.Write(ex);
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
                CrashLog.Write(ex);
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
        GameOverclockSnapshot snapshot;
        try
        {
            snapshot = await GameSource.ReadAsync(ct).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // One unreadable section must not take the rest of the page down, and an
            // empty list here would read as "no games installed", which the scan
            // never proved.
            CrashLog.Write(ex);
            GameSummary = Localizer.Get("overclock-games-scan-failed");
            return;
        }

        foreach (var entry in snapshot.Entries)
        {
            Games.Add(new OverclockGameRow(
                entry.Candidate.DisplayName,
                entry.Candidate.ExecutableName,
                GameStateLabel(entry.Decision.State),
                Localizer.Get(entry.Decision.ReasonKey),
                entry.Decision.IsActionable));
        }

        // An anti-cheat on the machine outranks the count of running games. It is a fact
        // about the system, not about the process list, and hiding it behind "no games
        // running" would report a clean machine while the thing that bans people sits
        // installed in the service registry.
        GameSummary = snapshot.DetectedAntiCheats.Count > 0
            ? Localizer.Format("overclock-games-anti-cheats", string.Join(", ", snapshot.DetectedAntiCheats))
            : snapshot.Entries.Count == 0
                ? Localizer.Get("overclock-games-none")
                : Localizer.Format("overclock-games-count", snapshot.Entries.Count);
    }

    private static string GameStateLabel(GameProfileState state) => state switch
    {
        GameProfileState.Ready => Localizer.Get("overclock-state-available"),
        GameProfileState.BlockedByAntiCheat => Localizer.Get("overclock-games-state-blocked"),
        GameProfileState.NoValidatedProfile => Localizer.Get("overclock-games-state-no-profile"),
        GameProfileState.UnknownGame => Localizer.Get("overclock-games-state-unknown"),
        _ => Localizer.Get("overclock-games-state-unscanable"),
    };

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

        foreach (var detail in capabilities.Details)
        {
            if (detail.DriverVersion is { Length: > 0 } version)
            {
                _lastDriverVersions[detail.BackendId] = version;
            }
        }
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

    /// <summary>
    /// Scans for runs that survived a restart and offers each one to the user.
    ///
    /// The scan runs on a worker thread because it opens every checkpoint file
    /// and asks the boot probe, and a failure there must never take the hardware
    /// view down with it: a machine whose boot state cannot be read still has to
    /// show its clocks.
    /// </summary>
    private async Task LoadPendingRecoveriesAsync(CancellationToken ct)
    {
        PendingRecoveries.Clear();
        RecoverySummary = string.Empty;

        IReadOnlyList<RecoveryOutcome> outcomes;
        try
        {
            outcomes = await Task.Run(() => Recovery.RecoverAllAsync(ct), ct).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            RecoverySummary = Localizer.Get("overclock-recovery-scan-failed");
            CrashLog.Write(ex);
            return;
        }

        foreach (var outcome in outcomes)
        {
            if (outcome.NeedsUserDecision)
            {
                PendingRecoveries.Add(ToRecoveryRow(outcome));
            }
        }

        RecoverySummary = PendingRecoveries.Count == 0
            ? Localizer.Get("overclock-recovery-none")
            : Localizer.Format("overclock-recovery-pending", PendingRecoveries.Count);
    }

    /// <summary>Applies the user's choice to one open run, then re-scans.</summary>
    private async Task ResolveRecoveryAsync(
        OverclockRecoveryRow row,
        RecoveryChoice choice,
        CancellationToken ct)
    {
        // A dropped row must not be reported as a resolution.
        if (IsBusy || !Guid.TryParse(row.RunId, out var runId))
        {
            return;
        }

        IsBusy = true;
        try
        {
            StatusLine = Localizer.Get("overclock-recovery-resolving");
            var result = await Recovery.ResolveAsync(runId, choice, ct).ConfigureAwait(true);

            // Succeeded and RunClosed are different things: a revert the driver
            // refused leaves the run open on purpose, and saying otherwise would
            // hide a machine that is still running the value that crashed it.
            StatusLine = Localizer.Format(
                result.Succeeded ? "overclock-recovery-done" : "overclock-recovery-failed",
                Localizer.Get(result.ReasonKey ?? OverclockRecoveryService.ReasonBackendUnavailable));
        }
        catch (OperationCanceledException)
        {
            StatusLine = Localizer.Get("overclock_cancelled");
        }
        catch (Exception ex)
        {
            StatusLine = Localizer.Get("overclock-recovery-failed");
            CrashLog.Write(ex);
        }
        finally
        {
            IsBusy = false;
        }

        await LoadPendingRecoveriesAsync(CancellationToken.None).ConfigureAwait(true);
    }

    [RelayCommand]
    private Task RevertRecoveryAsync(OverclockRecoveryRow row, CancellationToken ct) =>
        ResolveRecoveryAsync(row, RecoveryChoice.Revert, ct);

    [RelayCommand]
    private Task KeepRecoveryAsync(OverclockRecoveryRow row, CancellationToken ct) =>
        ResolveRecoveryAsync(row, RecoveryChoice.Keep, ct);

    [RelayCommand]
    private Task DiscardRecoveryAsync(OverclockRecoveryRow row, CancellationToken ct) =>
        ResolveRecoveryAsync(row, RecoveryChoice.Discard, ct);

    /// <summary>
    /// The commands travel with the row on purpose. Inside a DataTemplate an
    /// x:Bind cannot reach the page that owns the ViewModel, and reaching for a
    /// Binding with ElementName just to avoid it would trade the compiler's
    /// check for a silent null.
    /// </summary>
    private OverclockRecoveryRow ToRecoveryRow(RecoveryOutcome outcome) =>
        new(
            outcome.RunId.ToString(),
            outcome.Knob.Value,
            Localizer.Get(ActionKey(outcome.Action)),
            outcome.FailureReasonKey is { } failure ? Localizer.Get(failure) : Localizer.Get(outcome.ReasonKey),
            Localizer.Format("overclock-recovery-detail", outcome.Knob.Value),
            RevertRecoveryCommand,
            KeepRecoveryCommand,
            DiscardRecoveryCommand);

    private static string ActionKey(RecoveryAction action) => action switch
    {
        RecoveryAction.AutoRevert => "overclock-recovery-action-auto",
        RecoveryAction.NotifyUser => "overclock-recovery-action-notify",
        _ => "overclock-recovery-action-none",
    };

    /// <summary>
    /// Reads the real bounds the driver imposes. Missing is not the same as
    /// unbounded, so a knob whose range could not be read still gets a row with
    /// its reason instead of silently disappearing.
    /// </summary>
    private async Task LoadCeilingsAsync(CancellationToken ct)
    {
        Ceilings.Clear();

        try
        {
            var ceilings = await Task.Run(() => CeilingSource.ReadAsync(ct).AsTask(), ct).ConfigureAwait(true);
            foreach (var ceiling in ceilings)
            {
                Ceilings.Add(ToCeilingRow(ceiling));
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex);
        }
    }

    private static OverclockCeilingRow ToCeilingRow(TuningCeiling ceiling)
    {
        var range = ceiling.Min is { } min && ceiling.Max is { } max
            ? Localizer.Format(
                "overclock-ceiling-range",
                CeilingNumber(min),
                CeilingNumber(max),
                Localizer.Get(ceiling.Unit))
            : string.Empty;

        return new(
            ceiling.Knob.Value,
            range,
            ceiling.ReasonKey is null ? string.Empty : Localizer.Get(ceiling.ReasonKey));
    }

    private static string CeilingNumber(double value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);
    /// <summary>
    /// Runs the stability bisection on one knob, then keeps the answer. A run that
    /// reaches its ceiling without a fault becomes a validated profile, which is
    /// the only thing that unlocks game rows: nothing else is allowed to.
    /// The announced ceiling is the catalog Performance preset for the knob.
    /// </summary>
    [RelayCommand]
    private async Task StartTuningAsync(string knobId, CancellationToken ct)
    {
        if (IsBusy)
        {
            return;
        }

        if (!_lastKnobs.TryGetValue(knobId, out var knob) || FindBackend(knob.BackendId) is not { } backend)
        {
            StatusLine = Localizer.Get("overclock-tuning-unknown");
            return;
        }

        IsBusy = true;
        try
        {
            StatusLine = Localizer.Get("overclock-tuning-running");
            var progress = new Progress<TuningProgress>(p =>
                StabilitySummary = Localizer.Format(
                    "overclock-tuning-point",
                    p.PointIndex,
                    p.EstimatedPoints,
                    p.Value.Scalar.ToString("0.##", CultureInfo.InvariantCulture)));

            // Driver P/Invoke, WMI and file I/O: none of it belongs on the UI thread.
            var outcome = await Task.Run(
                () => ExecuteTuningAsync(knob, backend, progress, ct), ct).ConfigureAwait(true);

            await FinishTuningAsync(knob, backend, outcome, ct).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            StatusLine = Localizer.Get("overclock_cancelled");
        }
        catch (Exception ex)
        {
            StatusLine = Localizer.Get("overclock-tuning-failed");
            CrashLog.Write(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private sealed record TuningOutcome(
        TuningRunResult? Result,
        string? FailureReasonKey,
        double StartValue,
        double Ceiling,
        Guid RunId);

    private async Task<TuningOutcome> ExecuteTuningAsync(
        OverclockKnob knob,
        IOverclockBackend backend,
        IProgress<TuningProgress> progress,
        CancellationToken ct)
    {
        var runId = Guid.NewGuid();
        var target = new BackendTuningTarget(backend);

        var read = await backend.ReadAsync(knob.Id, ct).ConfigureAwait(false);
        if (!read.Success)
        {
            // No start value, no run. Inventing one would tune from a lie.
            return new TuningOutcome(null, read.FailureReasonKey ?? "overclock-tuning-no-start", 0, 0, runId);
        }

        if (knob.PresetValue(OverclockProfile.Performance) is not { } preset)
        {
            // No catalog ceiling, no announced ceiling, no run.
            return new TuningOutcome(null, "overclock-tuning-unknown", read.Value.Scalar, read.Value.Scalar, runId);
        }

        var ceiling = preset.Scalar;
        if (ceiling <= read.Value.Scalar)
        {
            return new TuningOutcome(null, "overclock-tuning-at-ceiling", read.Value.Scalar, ceiling, runId);
        }

        await CheckpointStore.SaveAsync(new TuningCheckpoint
        {
            RunId = runId,
            BackendId = backend.Id,
            Knob = knob.Id.Value,
            Component = knob.Component.ToString(),
            AppliedValue = read.Value.Scalar,
            LastKnownGoodValue = read.Value.Scalar,
            Phase = CheckpointPhase.Started,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        }, ct).ConfigureAwait(false);

        var engine = new TuningEngine(JournalFor(runId));
        var request = new TuningRequest(
            runId,
            knob,
            target,
            TuningProbe,
            ProposedCeiling: ceiling,
            Step: knob.Step,
            StepDuration: TimeSpan.FromSeconds(10),
            Thresholds: StabilityThresholds.Default,
            Metrics: [ProbeMetric.EffectiveClockStall])
        {
            StartingValue = read.Value.Scalar,
            Ceiling = ceiling,
        };

        var result = await engine.RunAsync(request, progress, ct).ConfigureAwait(false);
        return new TuningOutcome(result, null, read.Value.Scalar, ceiling, runId);
    }

    private async Task FinishTuningAsync(
        OverclockKnob knob,
        IOverclockBackend backend,
        TuningOutcome outcome,
        CancellationToken ct)
    {
        if (outcome.Result is null)
        {
            StatusLine = Localizer.Get(outcome.FailureReasonKey ?? "overclock-tuning-failed");
            return;
        }

        var result = outcome.Result;
        var validated = result.Status is TuningStatus.CeilingReached or TuningStatus.StabilityCeilingFound;
        await CheckpointStore.SaveAsync(new TuningCheckpoint
        {
            RunId = outcome.RunId,
            BackendId = backend.Id,
            Knob = knob.Id.Value,
            Component = knob.Component.ToString(),
            AppliedValue = result.AppliedValue.Scalar,
            LastKnownGoodValue = result.AppliedValue.Scalar,
            StableCeiling = result.StableCeiling,
            Phase = validated ? CheckpointPhase.Closed : CheckpointPhase.Interrupted,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        }, ct).ConfigureAwait(true);

        if (validated)
        {
            await ProfileStore.SaveAsync(new ValidatedOverclockProfile(
                knob.Id,
                knob.Component,
                result.AppliedValue.Scalar,
                result.StableCeiling,
                DateTimeOffset.UtcNow,
                _lastDriverVersions.TryGetValue(backend.Id, out var driver) ? driver : null,
                result.Points.Count), ct).ConfigureAwait(true);

            StatusLine = Localizer.Get("overclock-tuning-validated");
            StabilitySummary = Localizer.Format("overclock-tuning-points", result.Points.Count);
            await FillGamesAsync(ct).ConfigureAwait(true);
            await LoadCeilingsAsync(ct).ConfigureAwait(true);
        }
        else
        {
            StatusLine = Localizer.Get(result.Status == TuningStatus.Rejected
                ? "overclock-tuning-rejected"
                : "overclock-tuning-interrupted");
        }
    }

    private static class OverclockCatalogBackendIds
    {
        public const string Cpu = "intel-policy";
        public const string Gpu = "nvidia-nvapi";
        public const string Memory = "smbios-memory";
    }
}