using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using CAO.Core.Overclock;
using CAO.Infrastructure.Gaming;
using CAO.Infrastructure.Overclock;
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
/// Read-only Overclock section. Phase F1: it reports what the hardware actually
/// exposes through vendor APIs and states plainly which controls are not yet
/// writable. It never claims a value it did not read.
/// </summary>
public sealed partial class OverclockViewModel : ObservableObject
{
    private readonly IOverclockBackendRegistry _registry;
    private readonly IReadOnlyList<IOverclockBackend> _backends;
    private readonly GameDetectionProvider _games;

    public OverclockViewModel()
        : this(OverclockReadOnlyRegistry.CreateDefault(), DefaultBackends(), new GameDetectionProvider())
    {
    }

    public OverclockViewModel(
        IOverclockBackendRegistry registry,
        IReadOnlyList<IOverclockBackend> backends,
        GameDetectionProvider games)
    {
        _registry = registry;
        _backends = backends;
        _games = games;
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

    public ObservableCollection<OverclockKnobRow> CpuKnobs { get; } = [];

    public ObservableCollection<OverclockKnobRow> GpuKnobs { get; } = [];

    public ObservableCollection<OverclockKnobRow> MemoryKnobs { get; } = [];

    public ObservableCollection<OverclockReadingRow> CpuReadings { get; } = [];

    public ObservableCollection<OverclockReadingRow> GpuReadings { get; } = [];

    public ObservableCollection<OverclockReadingRow> MemoryReadings { get; } = [];

    public ObservableCollection<OverclockGameRow> Games { get; } = [];

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
            ReasonFor(state, availability, knob),
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
        OverclockKnob knob)
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
                if (backendWriteReason(knob) is { } reason)
                {
                    return Localizer.Get(reason);
                }

                return availability is null
                    ? Localizer.Get("overclock_read_failed")
                    : Localizer.Get(availability.UnavailableReasonKey);
        }

        static string? backendWriteReason(OverclockKnob k) => k.Component switch
        {
            OverclockComponent.Gpu => NvidiaNvapiBackend.ReadOnlyReasonKey,
            OverclockComponent.Memory => SmbiosMemoryBackend.ReadOnlyReasonKey,
            _ => "overclock-cpu-write-not-enabled",
        };
    }

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
        ResultSummary = Localizer.Get("overclock-result-pending");
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

    private static class OverclockCatalogBackendIds
    {
        public const string Cpu = "intel-policy";
        public const string Gpu = "nvidia-nvapi";
        public const string Memory = "smbios-memory";
    }
}