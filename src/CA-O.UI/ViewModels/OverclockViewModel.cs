using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using CAO.Core.Abstractions;
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
using CAO.Infrastructure.SystemInterop;
using CAO.Shared;
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
public sealed record OverclockCeilingRow(string Knob, string Range, string Reason, string Label = "");
/// <summary>One writable knob with its tune button. The command travels with the row because inside a DataTemplate an x:Bind cannot reach the page ViewModel.</summary>
public sealed record OverclockTuningRow(string KnobId, string Label, string Detail, ICommand TuneCommand, string Description = "");
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
/// One entry of the profile picker. A record (value equality) so the
/// ComboBox keeps the selection even if the list instance is rebuilt.
/// Top-level like every other XAML-bound row: x:DataType cannot see nested types.
/// </summary>
public sealed record OverclockProfileChoice(OverclockProfile Profile, string Label);

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
    private IRestorePointService? _restorePointService;
    internal static readonly TimeSpan DefaultRowTimeout = TimeSpan.FromSeconds(30);

    internal const string RowTimedOutReasonKey = "overclock-row-timed-out";

    private readonly TimeSpan _rowTimeout;

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
        IOverclockJournal? journal = null,
        TimeSpan? rowTimeout = null,
        IRestorePointService? restorePointService = null)
    {
        _registry = registry;
        _backends = backends;
        _rowTimeout = rowTimeout ?? DefaultRowTimeout;
        _deltaSource = deltaSource;
        _store = store;
        _recovery = recovery;
        _ceilingSource = ceilingSource;
        _gameSource = gameSource;
        _tuningProbe = probe;
        _profileStore = profileStore;
        _checkpointStore = checkpointStore;
        _journal = journal;
        _restorePointService = restorePointService;
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
        _ceilingSource ??= new CompositeCeilingSource(
            new NvidiaTuningCeilingSource(NvmlTuningApi.Instance),
            new AmdTuningCeilingSource(AdlxTuningApi.Instance));

    /// <summary>
    /// The probe that can actually see the component being tuned. GPU offsets
    /// used to be graded by the CPU clock probe, which passes on an idle GPU,
    /// so every offset "validated" without the card ever running at it. An
    /// injected probe (tests) wins for every component.
    /// </summary>
    private IStabilityProbe ProbeFor(OverclockKnob knob) =>
        _tuningProbe ?? (knob.Component == OverclockComponent.Gpu
            ? _gpuProbe ??= new NvmlGpuStabilityProbe()
            : _cpuProbe ??= new WmiClockStallProbe());

    private static IReadOnlyList<ProbeMetric> MetricsFor(OverclockKnob knob) =>
        knob.Component == OverclockComponent.Gpu
            ? [ProbeMetric.GpuDeviceLost, ProbeMetric.GpuThermalLimit]
            : [ProbeMetric.EffectiveClockStall];

    private IStabilityProbe? _gpuProbe;
    private IStabilityProbe? _cpuProbe;

    /// <summary>
    /// Only clock offsets go through the stability bisection. Power limits and
    /// Windows power policy are headroom, not clocks: raising them cannot make
    /// the silicon unstable by itself, so walking them point by point was
    /// minutes of probing that proved nothing.
    /// </summary>
    internal static bool NeedsStabilityWalk(OverclockKnob knob) =>
        knob.Kind == KnobKind.Scalar && knob.Unit == KnobUnit.Hertz;

    private IValidatedProfileStore ProfileStore =>
        _profileStore ??= new FileValidatedProfileStore();

    private ITuningCheckpointStore CheckpointStore =>
        _checkpointStore ??= new FileTuningCheckpointStore();

    private IOverclockJournal JournalFor(Guid runId) =>
        _journal ?? new TransactionOverclockJournal(new FileTransactionJournal(), runId);

    /// <summary>
    /// The real WMI service, built on first use like every other hardware door:
    /// constructing the page must never pay for a restore point nobody asked
    /// for. Injectable so tests drive the gate without touching WMI.
    /// </summary>
    private IRestorePointService RestorePoints =>
        _restorePointService ??= new WmiRestorePointService();

    /// <summary>
    /// Puerta única de escritura: sin punto de restauración no se escribe nada.
    /// Orden del operador: crear SÍ O SÍ el punto antes de cualquier escritura
    /// de overclock; si no se puede crear, las tres vías (perfil, knob, auto)
    /// vuelven con mensaje y cero escrituras. Un timeout o una cancelación es
    /// un fallo, no un permiso: también bloquea.
    /// </summary>
    internal async Task<bool> EnsureRestorePointAsync(CancellationToken ct)
    {
        StatusLine = Localizer.Get("overclock-restore-point-creating");
        CrashLog.Step("overclock.restore-point-start");
        try
        {
            var (success, reason) = await RestorePoints
                .CreateAsync("CA-O overclock: punto previo a escritura", ct)
                .ConfigureAwait(true);
            if (success)
            {
                CrashLog.Step("overclock.restore-point-done");
                return true;
            }

            StatusLine = Localizer.Format("overclock-restore-point-blocked", reason);
            CrashLog.Step("overclock.restore-point-blocked");
            return false;
        }
        catch (OperationCanceledException)
        {
            StatusLine = Localizer.Format(
                "overclock-restore-point-blocked", Localizer.Get("overclock_cancelled"));
            CrashLog.Step("overclock.restore-point-cancelled");
            return false;
        }
        catch (Exception ex)
        {
            // El servicio real ya traga sus internals WMI; si algo llega aquí
            // es plomería, y su mensaje no sale al usuario.
            CrashLog.Write(ex);
            StatusLine = Localizer.Format(
                "overclock-restore-point-blocked", Localizer.Get("overclock_probe_failed"));
            CrashLog.Step("overclock.restore-point-failed");
            return false;
        }
    }

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

    /// <summary>Opens the 05 ESTABILIDAD block by itself when a run is waiting for a decision.</summary>
    [ObservableProperty]
    private bool _hasPendingRecoveries;
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

    /// <summary>
    /// The profile card lists writable controls only. It is fed from the same
    /// rows as the per-component sections but filtered to actionable ones, so a
    /// read-only telemetry row can never leak into the card that offers Aplicar.
    /// </summary>
    public ObservableCollection<OverclockKnobRow> ProfileKnobs { get; } = [];
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
            ProfileKnobs.Clear();
            _lastKnobs.Clear();
            _lastDriverVersions.Clear();

            // The knob loop runs on a worker thread: ResolveState asks the write gates
            // synchronously, and a backend that answers slowly must never freeze the
            // window. Every knob gets its own ceiling so one that never answers degrades
            // to a row instead of hanging the section. Adds stay here because the
            // collections are bound to the UI thread.
            var builtRows = await Task.Run(
                () => BuildAllRowsAsync(capabilities, ct), ct).ConfigureAwait(true);

            foreach (var (knob, row) in builtRows)
            {
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
                        knob.Id.Value, row.Label, row.Range, StartTuningCommand, row.Description));
                    ProfileKnobs.Add(row);
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
            CrashLog.Step("refresh.cancelled");
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
    /// Builds every knob row off the UI thread with one ceiling per knob.
    /// Only row data is produced here; the Adds happen back on the UI thread.
    /// </summary>
    private async Task<List<(OverclockKnob Knob, OverclockKnobRow Row)>> BuildAllRowsAsync(
        OverclockCapabilities capabilities,
        CancellationToken ct)
    {
        var rows = new List<(OverclockKnob, OverclockKnobRow)>();
        foreach (var knob in capabilities.Knobs)
        {
            ct.ThrowIfCancellationRequested();

            OverclockKnobRow row;
            try
            {
                // The linked token lets cooperative backends cancel early;
                // WaitAsync is the hard ceiling for the ones that cannot.
                using var knobCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                knobCts.CancelAfter(_rowTimeout);
                row = await BuildRowAsync(knob, capabilities, knobCts.Token)
                    .WaitAsync(_rowTimeout, ct)
                    .ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                row = TimedOutRow(knob);
            }

            rows.Add((knob, row));
        }

        return rows;
    }

    private OverclockKnobRow TimedOutRow(OverclockKnob knob) =>
        new(
            knob.Id.Value,
            Localizer.Get(knob.LabelKey),
            Localizer.Get(knob.DescriptionKey),
            Localizer.Get(RowTimedOutReasonKey),
            DescribeRange(knob),
            Localizer.GetEvidenceLabel(knob.Evidence),
            OverclockControlState.DriverBlocked,
            StateLabel(OverclockControlState.DriverBlocked),
            Localizer.Get(RowTimedOutReasonKey),
            IsActionable: false);

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

            // A setting the vendor never exposed is read-only by nature, not a
            // driver that blocked CA-O: saying "blocked by the driver" sends the
            // user to update a driver that will never add the setter.
            return gate.BlockReasonKey(knob.Id) == NvidiaGpuBackend.NoPublicSetterReasonKey
                ? OverclockControlState.Unsupported
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
                    : Localizer.Get(WriteBlockReason(backend, knob) ?? "overclock_read_failed");
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

    private bool? _memoryProfileActive;

    private async Task FillReadingsAsync(CancellationToken ct)
    {
        _memoryProfileActive = null;
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

            // CPU readings used to land in the GPU list: the telemetry showed the
            // turbo mode under "graphics".
            var sink = backend.Id switch
            {
                OverclockCatalogBackendIds.Memory => MemoryReadings,
                OverclockCatalogBackendIds.Cpu => CpuReadings,
                _ => GpuReadings,
            };

            foreach (var reading in readings)
            {
                if (reading.LabelKey == "overclock_memory_profile" && reading.UnavailableReasonKey is null)
                {
                    _memoryProfileActive = reading.Value == "overclock_memory_profile_active";
                }

                sink.Add(new OverclockReadingRow(
                    Label(reading.LabelKey),
                    // Some readings carry a verdict key as their value (the XMP
                    // inference); it used to reach the screen untranslated.
                    Localizer.Get(reading.Value),
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
            ? Localizer.Format("overclock-games-anti-cheats-compatible", string.Join(", ", snapshot.DetectedAntiCheats))
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

        // RAM cannot be overclocked from Windows: the only lever is XMP/EXPO in
        // the BIOS. Say which side of it this machine is on instead of a generic note.
        MemorySummary = capabilities.Memory != MemoryBackendKind.SmbiosReportOnly
            ? Localizer.Get(mem?.UnavailableReasonKey ?? "overclock-memory-none")
            : _memoryProfileActive switch
            {
                true => Localizer.Get("overclock-memory-xmp-active"),
                false => Localizer.Get("overclock-memory-xmp-hint"),
                _ => Localizer.Get("overclock-memory-smbios"),
            };

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
        if (knob.Kind == KnobKind.Enum && knob.Range.Allowed is { Count: > 0 })
        {
            // Turbo mode reads as words, not as "2".
            var named = Localizer.Get(string.Concat("overclock-boost-", value.Index.ToString(CultureInfo.InvariantCulture)));
            return knob.Id == CAO.Core.Overclock.Policy.IntelPowerPolicy.BoostModeKnob && !named.StartsWith("overclock-", StringComparison.Ordinal)
                ? named
                : value.Index.ToString(CultureInfo.InvariantCulture);
        }

        var unit = knob.Unit switch
        {
            KnobUnit.Hertz => Localizer.Get("overclock-unit-hertz"),
            KnobUnit.Percent => "%",
            KnobUnit.Celsius => "°C",
            _ => string.Empty,
        };

        var number = FormatNumber(knob.Unit, value.Scalar);
        return unit.Length == 0 ? number : string.Concat(number, " ", unit);
    }

    /// <summary>
    /// Every frequency in the catalog is Hertz; people read MHz. Offsets are
    /// negative too, so the conversion keys on the magnitude, not on the sign
    /// (a -500 MHz bound used to print as "-500000000").
    /// </summary>
    private static string FormatNumber(KnobUnit unit, double value) =>
        unit == KnobUnit.Hertz
            ? (value / 1_000_000d).ToString("0.##", CultureInfo.InvariantCulture)
            : value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string DescribeRange(OverclockKnob knob)
    {
        if (knob.Kind == KnobKind.Enum)
        {
            return string.Empty;
        }

        var min = FormatNumber(knob.Unit, knob.Range.Min);
        var max = FormatNumber(knob.Unit, knob.Range.Max);
        var unit = knob.Unit switch
        {
            KnobUnit.Hertz => " " + Localizer.Get("overclock-unit-hertz"),
            KnobUnit.Percent => " %",
            KnobUnit.Celsius => " °C",
            _ => string.Empty,
        };
        return string.Concat(min, " - ", max, unit);
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

        HasPendingRecoveries = PendingRecoveries.Count > 0;
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

    private OverclockCeilingRow ToCeilingRow(TuningCeiling ceiling)
    {
        var hertz = string.Equals(ceiling.Unit, NvidiaTuningCeilingSource.HertzUnitKey, StringComparison.Ordinal);
        var range = ceiling.Min is { } min && ceiling.Max is { } max
            ? Localizer.Format(
                "overclock-ceiling-range",
                FormatNumber(hertz ? KnobUnit.Hertz : KnobUnit.Percent, min),
                FormatNumber(hertz ? KnobUnit.Hertz : KnobUnit.Percent, max),
                Localizer.Get(ceiling.Unit))
            : string.Empty;

        // The id stays in Knob (it is what the recovery and the tests key on);
        // the screen shows the same words as the control row.
        var label = _lastKnobs.TryGetValue(ceiling.Knob.Value, out var knob)
            ? Localizer.Get(knob.LabelKey)
            : ceiling.Knob.Value;

        return new(
            ceiling.Knob.Value,
            range,
            ceiling.ReasonKey is null ? string.Empty : Localizer.Get(ceiling.ReasonKey),
            label);
    }
    /// <summary>
    /// Runs the stability bisection on one knob, then keeps the answer. A run that
    /// reaches its ceiling without a fault becomes a validated profile, which is
    /// the only thing that unlocks game rows: nothing else is allowed to.
    /// The announced ceiling is the catalog Performance preset for the knob.
    /// </summary>
    /// <summary>
    /// Maps tuning progress to the global mascot mood. Pure mapping so the branches are unit-testable; the UiState write itself stays at the call sites (testhost has no DI container).
    /// </summary>
    public static string MoodForTuning(bool running, bool? validated) =>
        running ? "Working" : validated == true ? "Celebrate" : "Idle";

    /// <summary>
    /// Writes the global mascot mood, never throwing: testhost has no DI container and a cosmetic indicator must not break a tuning run.
    /// </summary>
    private static void SetMood(string mood)
    {
        try
        {
            AppHost.Resolve<UiState>().MascotMood = mood;
        }
        catch (InvalidOperationException)
        {
        }
        catch (NullReferenceException)
        {
        }
    }

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
            var (scanOk, antiCheats) = await ScanAntiCheatsAsync(ct).ConfigureAwait(true);
            if (FilterByAntiCheat([(knob, backend)], scanOk, antiCheats).Blocked is { Count: > 0 } refused)
            {
                StatusLine = Localizer.Format("overclock-auto-blocked-anticheat", string.Join(", ", refused));
                return;
            }

            // Sin punto no hay bisección: el motor escribe checkpoints, journal
            // y valores del driver desde el primer punto.
            if (!await EnsureRestorePointAsync(ct).ConfigureAwait(true))
            {
                SetMood(MoodForTuning(false, null));
                CrashLog.Step("tuning.blocked-no-restore-point");
                return;
            }

            StatusLine = Localizer.Get("overclock-tuning-running");
            SetMood(MoodForTuning(true, null));
            var progress = new Progress<TuningProgress>(p =>
                StabilitySummary = Localizer.Format(
                    "overclock-tuning-point",
                    p.PointIndex,
                    p.EstimatedPoints,
                    FormatValue(knob, p.Value)));

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
            SetMood(MoodForTuning(false, null));
            CrashLog.Write(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Window per tuning point: long enough for a driver reset to show up under load.</summary>
    internal static readonly TimeSpan StepDuration = TimeSpan.FromSeconds(15);

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
            ProbeFor(knob),
            ProposedCeiling: ceiling,
            Step: knob.Step,
            StepDuration: StepDuration,
            Thresholds: StabilityThresholds.Default,
            Metrics: MetricsFor(knob))
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
            SetMood(MoodForTuning(false, null));
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
            // Unverified already put the last good value back: nothing is left in
            // the air, so it must not show up as a pending run after a restart.
            Phase = validated || result.Status == TuningStatus.Unverified
                ? CheckpointPhase.Closed
                : CheckpointPhase.Interrupted,
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
            SetMood(MoodForTuning(false, true));
            StabilitySummary = Localizer.Format("overclock-tuning-points", result.Points.Count);
            await FillGamesAsync(ct).ConfigureAwait(true);
            await LoadCeilingsAsync(ct).ConfigureAwait(true);
        }
        else
        {
            StatusLine = result.Status switch
            {
                TuningStatus.Rejected => Localizer.Get("overclock-tuning-rejected"),
                TuningStatus.Unverified => Localizer.Format(
                    "overclock-tuning-unverified",
                    Localizer.Get(result.ReasonKey ?? "overclock-tuning-no-verdict")),
                _ => Localizer.Get("overclock-tuning-interrupted"),
            };
            SetMood(MoodForTuning(false, null));
        }
    }

    /// <summary>
    /// Names the open runs that block a new write, in the same words the 05
    /// ESTABILIDAD section shows them (row.Knob), plus how to clear them.
    /// The button labels come from their own keys so no language hardcodes
    /// Revertir/Mantener/Descartar here. Any future caller gated on
    /// PendingRecoveries must reuse this instead of Get(key).
    /// </summary>
    private string BuildBlockedRecoveryMessage() =>
        Localizer.Format(
            "overclock-auto-blocked-recovery",
            PendingRecoveries.Count,
            string.Join(", ", PendingRecoveries.Select(r => r.Knob)),
            Localizer.Get("overclock-recovery-revert"),
            Localizer.Get("overclock-recovery-keep"),
            Localizer.Get("overclock-recovery-discard"));

    /// <summary>
    /// Automatic overclock: walks every actionable knob through the same bisection
    /// <see cref="StartTuningAsync"/> runs (same engine, same checkpoints, no new
    /// machinery), then PROPOSES the result instead of applying a preset.
    ///
    /// Pressing the button authorizes the guided probe writes, exactly like pressing
    /// each per-knob Ajustar would. What it never does is silently apply a profile:
    /// the outcome is a summary plus the existing Aplicar button left to the user.
    /// Progress follows H7 (OptimizePage): the bar stays indeterminate while IsBusy
    /// and the ceiling is announced per knob, so a long silent bisection reads as
    /// expected instead of hung. Blocked by pending D4 recoveries and by the
    /// anti-cheat gate before any write. A per-knob failure skips that knob and
    /// continues with the rest; cancel stops cleanly between knobs.
    /// </summary>
    [RelayCommand]
    private async Task StartAutoTuningAsync(CancellationToken ct)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            // D4: an open run outranks any new write. The scan also refreshes the
            // recovery section so the block explains itself on screen.
            await LoadPendingRecoveriesAsync(ct).ConfigureAwait(true);
            if (PendingRecoveries.Count > 0)
            {
                ApplySummary = BuildBlockedRecoveryMessage();
                StatusLine = ApplySummary;
                CrashLog.Step("auto.blocked-recovery");
                return;
            }

            // Anti-cheat gate, per backend. Having Vanguard or EasyAntiCheat installed
            // used to stop the whole run, which on a Valorant/Fortnite machine meant no
            // overclock at all. What actually conflicts with an anti-cheat is a kernel
            // driver of CA-O's own; the vendor GPU API and the Windows power plan do not
            // touch anything they watch. So only kernel-driver backends are skipped.
            var (scanOk, antiCheats) = await ScanAntiCheatsAsync(ct).ConfigureAwait(true);
            var (targets, blocked) = FilterByAntiCheat(CollectAutoTargets(), scanOk, antiCheats);
            if (targets.Count == 0)
            {
                ApplySummary = blocked.Count > 0
                    ? Localizer.Format("overclock-auto-blocked-anticheat", string.Join(", ", blocked))
                    : Localizer.Get("overclock-profile-no-actionable");
                StatusLine = ApplySummary;
                CrashLog.Step(blocked.Count > 0 ? "auto.blocked-anticheat" : "auto.no-actionable");
                return;
            }

            // Después de las puertas locales (recovery, anti-cheat, accionables):
            // crear un punto para una pasada que ya está bloqueada sería pagar
            // minutos de WMI para no escribir nada. Sigue garantizando cero
            // escrituras si falla, porque ningún write corre antes de aquí.
            if (!await EnsureRestorePointAsync(ct).ConfigureAwait(true))
            {
                ApplySummary = StatusLine;
                CrashLog.Step("auto.blocked-no-restore-point");
                return;
            }

            StatusLine = Localizer.Get("overclock-auto-running");
            SetMood(MoodForTuning(true, null));
            CrashLog.Step("auto.start");

            // Snapshot every backend the run can touch BEFORE the first write, so
            // "Restaurar valores" undoes the whole automatic run in one click.
            await CaptureAutoSnapshotsAsync(targets, ct).ConfigureAwait(true);

            // Phase 1: headroom. Power limits and the Windows turbo policy go
            // straight to the Performance preset (the backend clamps to what the
            // driver allows). They must land before the clock walk: an offset
            // tested without power headroom is graded on a throttled card.
            var applied = 0;
            var validated = 0;
            var skipped = 0;
            var runId = Guid.NewGuid();
            var journal = JournalFor(runId);
            var headroom = targets.Where(t => !NeedsStabilityWalk(t.Knob)).ToList();
            if (headroom.Count > 0)
            {
                await journal.WriteAsync(TransactionPhase.OverclockStarted, $"run={runId} auto=headroom", ct).ConfigureAwait(true);
            }

            foreach (var (knob, backend) in headroom)
            {
                ct.ThrowIfCancellationRequested();
                if (knob.PresetValue(OverclockProfile.Performance) is not { } preset)
                {
                    skipped++;
                    continue;
                }

                StatusLine = Localizer.Format("overclock-auto-headroom-knob", Localizer.Get(knob.LabelKey));
                var result = await Task.Run(
                    () => ApplyOneAsync(new ApplyWork(backend, knob, preset), journal, ct), ct).ConfigureAwait(true);
                if (result.Success || result.Unchanged)
                {
                    applied++;
                }
                else
                {
                    skipped++;
                }
            }

            // Phase 2: clocks. Each offset is walked step by step under the probe
            // that watches the GPU itself; a step without load is not a pass.
            foreach (var (knob, backend) in targets.Where(t => NeedsStabilityWalk(t.Knob)))
            {
                ct.ThrowIfCancellationRequested();

                var ceiling = AutoCeilingText(knob);
                StabilitySummary = Localizer.Format(
                    "overclock-auto-tuning-knob", Localizer.Get(knob.LabelKey), ceiling);
                StatusLine = StabilitySummary;

                var progress = new Progress<TuningProgress>(p =>
                    StabilitySummary = Localizer.Format(
                        "overclock-tuning-point",
                        p.PointIndex,
                        p.EstimatedPoints,
                        FormatValue(knob, p.Value)));

                TuningOutcome outcome;
                try
                {
                    // Driver P/Invoke, WMI and file I/O: none of it on the UI thread.
                    outcome = await Task.Run(
                        () => ExecuteTuningAsync(knob, backend, progress, ct), ct).ConfigureAwait(true);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // One knob failing must not cancel the knobs that never ran.
                    CrashLog.Write(ex);
                    skipped++;
                    continue;
                }

                await FinishTuningAsync(knob, backend, outcome, ct).ConfigureAwait(true);
                if (outcome.Result is { Status: TuningStatus.CeilingReached or TuningStatus.StabilityCeilingFound })
                {
                    validated++;
                }
                else
                {
                    skipped++;
                }
            }

            ApplySummary = Localizer.Format("overclock-auto-proposal", applied, validated, skipped + blocked.Count)
                + AntiCheatNote(antiCheats, blocked);
            StatusLine = ApplySummary;
            SetMood(MoodForTuning(false, applied + validated > 0));
            CrashLog.Step("auto.done");
        }
        catch (OperationCanceledException)
        {
            StatusLine = Localizer.Get("overclock_cancelled");
            SetMood(MoodForTuning(false, null));
            CrashLog.Step("auto.cancelled");
        }
        catch (Exception ex)
        {
            StatusLine = Localizer.Get("overclock-tuning-failed");
            SetMood(MoodForTuning(false, null));
            CrashLog.Write(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Reads the anti-cheat registry. A failed read is not a reason to stop: it only
    /// matters for kernel-driver backends, which the gate closes on its own.
    /// </summary>
    private async Task<(bool ScanOk, IReadOnlyList<string> AntiCheats)> ScanAntiCheatsAsync(CancellationToken ct)
    {
        try
        {
            var games = await Task.Run(() => GameSource.ReadAsync(ct).AsTask(), ct).ConfigureAwait(true);
            return (games.AntiCheatScanSucceeded, games.DetectedAntiCheats);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex);
            return (false, []);
        }
    }

    /// <summary>Splits the targets into the ones the anti-cheat gate allows and the labels of the ones it blocks.</summary>
    internal static (List<(OverclockKnob Knob, IOverclockBackend Backend)> Allowed, List<string> Blocked) FilterByAntiCheat(
        IEnumerable<(OverclockKnob Knob, IOverclockBackend Backend)> targets,
        bool scanOk,
        IReadOnlyList<string> antiCheats)
    {
        var allowed = new List<(OverclockKnob, IOverclockBackend)>();
        var blocked = new List<string>();
        foreach (var target in targets)
        {
            var decision = OverclockAntiCheatGate.Decide(
                OverclockAntiCheatGate.MechanismFor(target.Backend.Id), scanOk, antiCheats);
            if (decision.Allowed)
            {
                allowed.Add(target);
            }
            else
            {
                blocked.Add(Localizer.Get(target.Knob.LabelKey));
            }
        }

        return (allowed, blocked);
    }

    /// <summary>Says out loud why the run was safe with the anti-cheats on this machine.</summary>
    private static string AntiCheatNote(IReadOnlyList<string> antiCheats, IReadOnlyList<string> blocked)
    {
        if (antiCheats.Count == 0)
        {
            return string.Empty;
        }

        var note = " " + Localizer.Format("overclock-anticheat-compatible-note", string.Join(", ", antiCheats));
        return blocked.Count == 0
            ? note
            : note + " " + Localizer.Format("overclock-anticheat-skipped-note", string.Join(", ", blocked));
    }

    /// <summary>One snapshot per backend the automatic run is about to touch.</summary>
    private async Task CaptureAutoSnapshotsAsync(
        IReadOnlyList<(OverclockKnob Knob, IOverclockBackend Backend)> targets,
        CancellationToken ct)
    {
        _appliedSnapshots.Clear();
        foreach (var backend in targets.Select(t => t.Backend).DistinctBy(b => b.Id))
        {
            try
            {
                _appliedSnapshots[backend.Id] = await Task.Run(
                    () => backend.SnapshotAsync(ct).AsTask(), ct).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // No snapshot means no one-click restore for this backend; the
                // per-knob checkpoints still exist. Never a reason to stop.
                CrashLog.Write(ex);
            }
        }
    }

    private List<(OverclockKnob Knob, IOverclockBackend Backend)> CollectAutoTargets()
    {
        var targets = new List<(OverclockKnob, IOverclockBackend)>();
        CollectAutoFrom(CpuKnobs, targets);
        CollectAutoFrom(GpuKnobs, targets);
        CollectAutoFrom(MemoryKnobs, targets);
        return targets;
    }

    private void CollectAutoFrom(
        IEnumerable<OverclockKnobRow> rows,
        List<(OverclockKnob Knob, IOverclockBackend Backend)> targets)
    {
        foreach (var row in rows)
        {
            if (!row.IsActionable)
            {
                continue;
            }

            if (!_lastKnobs.TryGetValue(row.Id, out var knob) || knob is null)
            {
                continue;
            }

            if (FindBackend(knob.BackendId) is not { } backend)
            {
                continue;
            }

            targets.Add((knob, backend));
        }
    }

    private static string AutoCeilingText(OverclockKnob knob) =>
        knob.PresetValue(OverclockProfile.Performance) is { } preset
            ? FormatValue(knob, preset)
            : Localizer.Get("overclock-ceiling-unavailable");

    /// <summary>Outcome of a single knob write inside a profile apply.</summary>
    public sealed record AppliedKnob(KnobId Knob, string BackendId, bool Success, string? FailureReasonKey, bool Unchanged = false);

    /// <summary>One backend snapshot kept in memory for the in-page restore.</summary>
    public sealed record BackendSnapshot(string BackendId, UnitSnapshot Snapshot);

    /// <summary>Outcome of a whole profile apply run.</summary>
    public sealed record ApplyOutcome(
        Guid RunId,
        OverclockProfile Profile,
        IReadOnlyList<AppliedKnob> Applied,
        IReadOnlyList<BackendSnapshot> Snapshots);

    /// <summary>Outcome of a whole in-page restore run.</summary>
    public sealed record RestoreRunOutcome(Guid RunId, int Restored, int Failed);

    private sealed record ApplyWork(IOverclockBackend Backend, OverclockKnob Knob, KnobValue Value);

    public ObservableCollection<OverclockProfileChoice> ProfileChoices => _profileChoices ??= BuildProfileChoices();

    private ObservableCollection<OverclockProfileChoice>? _profileChoices;

    [ObservableProperty]
    private OverclockProfileChoice? _selectedChoice = ChoiceFor(OverclockProfile.Balanced);

    [ObservableProperty]
    private string _applySummary = string.Empty;

    public string ApplyNote => Localizer.Get("overclock-profile-note");

    public string ApplyButtonLabel => Localizer.Get("overclock-profile-apply");

    public string AutoTuneButtonLabel => Localizer.Get("overclock-auto-apply");

    public string TelemetryHeader => Localizer.Get("overclock-telemetry-header");

    public string RestoreButtonLabel => Localizer.Get("overclock-profile-restore");

    private readonly Dictionary<string, UnitSnapshot> _appliedSnapshots = new(StringComparer.Ordinal);

    private static ObservableCollection<OverclockProfileChoice> BuildProfileChoices() =>
        new(new[]
        {
            ChoiceFor(OverclockProfile.Conservative),
            ChoiceFor(OverclockProfile.Balanced),
            ChoiceFor(OverclockProfile.Performance),
        });

    private static OverclockProfileChoice ChoiceFor(OverclockProfile profile) =>
        new(profile, Localizer.Get(profile switch
        {
            OverclockProfile.Conservative => "overclock-profile-conservative",
            OverclockProfile.Performance => "overclock-profile-performance",
            _ => "overclock-profile-balanced",
        }));

    [RelayCommand]
    private async Task ApplyProfileAsync(CancellationToken ct)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        var profile = SelectedChoice?.Profile ?? OverclockProfile.Balanced;
        var runId = Guid.NewGuid();
        StatusLine = Localizer.Get("overclock-profile-applying");
        CrashLog.Step("refresh.apply-start");
        try
        {
            // Sin punto no hay apply: ExecuteApplyAsync abre el journal, guarda
            // checkpoints y escribe en el driver desde el primer knob.
            if (!await EnsureRestorePointAsync(ct).ConfigureAwait(true))
            {
                ApplySummary = StatusLine;
                CrashLog.Step("refresh.apply-blocked-no-restore-point");
                return;
            }

            var (scanOk, antiCheats) = await ScanAntiCheatsAsync(ct).ConfigureAwait(true);
            var collected = CollectApplyWork(profile);
            var (allowed, blocked) = FilterByAntiCheat(
                collected.Select(w => (w.Knob, w.Backend)), scanOk, antiCheats);
            var work = collected.Where(w => allowed.Any(a => a.Knob.Id == w.Knob.Id)).ToList();
            if (work.Count == 0)
            {
                ApplySummary = blocked.Count > 0
                    ? Localizer.Format("overclock-auto-blocked-anticheat", string.Join(", ", blocked))
                    : Localizer.Get("overclock-profile-no-actionable");
                StatusLine = ApplySummary;
                CrashLog.Step("refresh.apply-done");
                return;
            }

            var outcome = await Task.Run(() => ExecuteApplyAsync(profile, runId, work, ct), ct).ConfigureAwait(true);
            await FinishApplyAsync(outcome, ct).ConfigureAwait(true);
            CrashLog.Step("refresh.apply-done");
        }
        catch (OperationCanceledException)
        {
            StatusLine = Localizer.Get("overclock_cancelled");
            CrashLog.Step("refresh.apply-cancelled");
        }
        catch (Exception ex)
        {
            StatusLine = Localizer.Get("overclock_probe_failed");
            CrashLog.Step("refresh.apply-failed");
            CrashLog.Write(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private List<ApplyWork> CollectApplyWork(OverclockProfile profile)
    {
        var work = new List<ApplyWork>();
        CollectFrom(CpuKnobs, work, profile);
        CollectFrom(GpuKnobs, work, profile);
        CollectFrom(MemoryKnobs, work, profile);
        return work;
    }

    private void CollectFrom(IEnumerable<OverclockKnobRow> rows, List<ApplyWork> work, OverclockProfile profile)
    {
        foreach (var row in rows)
        {
            if (!row.IsActionable)
            {
                continue;
            }

            if (!_lastKnobs.TryGetValue(row.Id, out OverclockKnob? knob) || knob is null)
            {
                continue;
            }

            var backend = FindBackend(knob.BackendId);
            if (backend is null)
            {
                continue;
            }

            if (knob.PresetValue(profile) is not KnobValue preset)
            {
                continue;
            }

            work.Add(new ApplyWork(backend, knob, preset));
        }
    }

    private async Task<ApplyOutcome> ExecuteApplyAsync(
        OverclockProfile profile, Guid runId, IReadOnlyList<ApplyWork> work, CancellationToken ct)
    {
        var journal = JournalFor(runId);
        await journal.WriteAsync(TransactionPhase.OverclockStarted, $"run={runId} profile={profile}", ct).ConfigureAwait(false);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var snapshots = new List<BackendSnapshot>();
        foreach (var item in work)
        {
            if (!seen.Add(item.Backend.Id))
            {
                continue;
            }

            var snapshot = await item.Backend.SnapshotAsync(ct).ConfigureAwait(false);
            snapshots.Add(new BackendSnapshot(item.Backend.Id, snapshot));
        }

        var applied = new List<AppliedKnob>();
        foreach (var item in work)
        {
            applied.Add(await ApplyOneAsync(item, journal, ct).ConfigureAwait(false));
        }

        return new ApplyOutcome(runId, profile, applied, snapshots);
    }

    /// <summary>
    /// True when the driver already reports the preset value: same discrete index and scalar within a relative epsilon. Writing it again would succeed without changing anything and lie in the report.
    /// </summary>
    public static bool SameValue(CAO.Core.Overclock.KnobValue preset, CAO.Core.Overclock.KnobValue current) =>
        preset.Index == current.Index
        && System.Math.Abs(preset.Scalar - current.Scalar) <= 1e-6 * System.Math.Max(1.0, System.Math.Max(System.Math.Abs(preset.Scalar), System.Math.Abs(current.Scalar)));

    private async Task<AppliedKnob> ApplyOneAsync(
        ApplyWork item, IOverclockJournal journal, CancellationToken ct)
    {
        var knob = item.Knob;
        var backend = item.Backend;
        var checkpointId = Guid.NewGuid();
        var lastGood = 0d;

        try
        {
            var current = await backend.ReadAsync(knob.Id, ct).AsTask().WaitAsync(_rowTimeout, ct).ConfigureAwait(false);
            lastGood = current.Success ? current.Value.Scalar : 0d;
            if (current.Success && SameValue(item.Value, current.Value))
            {
                return new AppliedKnob(knob.Id, backend.Id, false, null, Unchanged: true);
            }

            await CheckpointStore.SaveAsync(new TuningCheckpoint
            {
                RunId = checkpointId,
                BackendId = backend.Id,
                Knob = knob.Id.Value,
                Component = knob.Component.ToString(),
                AppliedValue = lastGood,
                LastKnownGoodValue = lastGood,
                Phase = CheckpointPhase.Started,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            }, ct).ConfigureAwait(false);
            await journal.WriteAsync(TransactionPhase.OverclockCheckpointTaken, $"knob={knob.Id.Value}", ct).ConfigureAwait(false);

            var write = await backend.WriteAsync(knob.Id, item.Value, ct).AsTask().WaitAsync(_rowTimeout, ct).ConfigureAwait(false);
            if (write.Success)
            {
                await journal.WriteAsync(TransactionPhase.OverclockStepApplied, $"knob={knob.Id.Value} value={FormatApplyValue(knob, item.Value)}", ct).ConfigureAwait(false);
                await CheckpointStore.SaveAsync(new TuningCheckpoint
                {
                    RunId = checkpointId,
                    BackendId = backend.Id,
                    Knob = knob.Id.Value,
                    Component = knob.Component.ToString(),
                    AppliedValue = write.Applied?.Scalar ?? item.Value.Scalar,
                    LastKnownGoodValue = lastGood,
                    Phase = CheckpointPhase.Closed,
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
            }, ct).ConfigureAwait(false);
                return new AppliedKnob(knob.Id, backend.Id, true, null);
            }

            await journal.WriteAsync(TransactionPhase.OverclockStepRejected, $"knob={knob.Id.Value} reason={write.FailureReasonKey}", ct).ConfigureAwait(false);
            await CheckpointStore.SaveAsync(new TuningCheckpoint
            {
                RunId = checkpointId,
                BackendId = backend.Id,
                Knob = knob.Id.Value,
                Component = knob.Component.ToString(),
                AppliedValue = lastGood,
                LastKnownGoodValue = lastGood,
                Phase = CheckpointPhase.Interrupted,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            }, ct).ConfigureAwait(false);
            return new AppliedKnob(knob.Id, backend.Id, false, write.FailureReasonKey);
        }
        catch (TimeoutException)
        {
            await journal.WriteAsync(TransactionPhase.OverclockStepRejected, $"knob={knob.Id.Value} reason={RowTimedOutReasonKey}", ct).ConfigureAwait(false);
            await CheckpointStore.SaveAsync(new TuningCheckpoint
            {
                RunId = checkpointId,
                BackendId = backend.Id,
                Knob = knob.Id.Value,
                Component = knob.Component.ToString(),
                AppliedValue = lastGood,
                LastKnownGoodValue = lastGood,
                Phase = CheckpointPhase.Interrupted,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            }, ct).ConfigureAwait(false);
            return new AppliedKnob(knob.Id, backend.Id, false, RowTimedOutReasonKey);
        }
    }

    private static string FormatApplyValue(OverclockKnob knob, KnobValue value) =>
        knob.Range.Allowed is { Count: > 0 }
            ? value.Index.ToString(CultureInfo.InvariantCulture)
            : value.Scalar.ToString("0.##", CultureInfo.InvariantCulture);

    private async Task FinishApplyAsync(ApplyOutcome outcome, CancellationToken ct)
    {
        _appliedSnapshots.Clear();
        foreach (var snapshot in outcome.Snapshots)
        {
            _appliedSnapshots[snapshot.BackendId] = snapshot.Snapshot;
        }

        var ok = 0;
        var unchanged = 0;
        foreach (var knob in outcome.Applied)
        {
            if (knob.Unchanged)
            {
                unchanged++;
            }
            else if (knob.Success)
            {
                ok++;
                }
            }

        ApplySummary = Localizer.Format(
            "overclock-profile-summary",
            ok,
            unchanged,
            outcome.Applied.Count - ok - unchanged);
        StatusLine = ApplySummary;

        IsBusy = false;
        await RefreshAsync(ct).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RestoreAppliedAsync(CancellationToken ct)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusLine = Localizer.Get("overclock-profile-restoring");
        CrashLog.Step("refresh.restore-start");
        try
        {
            if (_appliedSnapshots.Count == 0)
            {
                ApplySummary = Localizer.Get("overclock-profile-nothing");
                StatusLine = ApplySummary;
                CrashLog.Step("refresh.restore-done");
                return;
            }

            var snapshots = new List<BackendSnapshot>();
            foreach (var entry in _appliedSnapshots)
            {
                snapshots.Add(new BackendSnapshot(entry.Key, entry.Value));
            }

            var runId = Guid.NewGuid();
            var outcome = await Task.Run(() => ExecuteRestoreAsync(runId, snapshots, ct), ct).ConfigureAwait(true);
            await FinishRestoreAsync(outcome, ct).ConfigureAwait(true);
            CrashLog.Step("refresh.restore-done");
        }
        catch (OperationCanceledException)
        {
            StatusLine = Localizer.Get("overclock_cancelled");
            CrashLog.Step("refresh.restore-cancelled");
        }
        catch (Exception ex)
        {
            StatusLine = Localizer.Get("overclock_probe_failed");
            CrashLog.Step("refresh.restore-failed");
            CrashLog.Write(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<RestoreRunOutcome> ExecuteRestoreAsync(
        Guid runId, IReadOnlyList<BackendSnapshot> snapshots, CancellationToken ct)
    {
        var journal = JournalFor(runId);
        await journal.WriteAsync(TransactionPhase.OverclockStarted, $"run={runId} restore=true", ct).ConfigureAwait(false);

        var restored = 0;
        var failed = 0;
        foreach (var item in snapshots)
        {
            var backend = FindBackend(item.BackendId);
            if (backend is null)
            {
                failed++;
                continue;
            }

            try
            {
                var outcome = await backend.RestoreAsync(item.Snapshot, ct).AsTask().WaitAsync(_rowTimeout, ct).ConfigureAwait(false);
                if (outcome.Success)
                {
                    restored++;
                    await journal.WriteAsync(TransactionPhase.OverclockStepApplied, $"restore backend={item.BackendId} restored={outcome.RestoredCount}", ct).ConfigureAwait(false);
                }
                else
                {
                    failed++;
                    await journal.WriteAsync(TransactionPhase.OverclockStepRejected, $"restore backend={item.BackendId} reason={outcome.FailureReasonKey}", ct).ConfigureAwait(false);
                }
            }
            catch (TimeoutException)
            {
                failed++;
                await journal.WriteAsync(TransactionPhase.OverclockStepRejected, $"restore backend={item.BackendId} reason={RowTimedOutReasonKey}", ct).ConfigureAwait(false);
            }
        }

        return new RestoreRunOutcome(runId, restored, failed);
    }

    private async Task FinishRestoreAsync(RestoreRunOutcome outcome, CancellationToken ct)
    {
        ApplySummary = Localizer.Format("overclock-profile-restored", outcome.Restored, outcome.Failed);
        StatusLine = ApplySummary;

        IsBusy = false;
        await RefreshAsync(ct).ConfigureAwait(true);
    }
    private static class OverclockCatalogBackendIds
    {
        public const string Cpu = "intel-policy";
        public const string Gpu = "nvidia-nvapi";
        public const string Memory = "smbios-memory";
    }
}