using CAO.Core.Overclock;
using CAO.Core.Overclock.OverclockCatalog;
using CAO.Shared;

namespace CAO.Infrastructure.Overclock;

/// <summary>
/// Merges the per-vendor answers into one <see cref="OverclockCapabilities"/>.
///
/// Rules this registry enforces, and which is why it exists at all:
///  - A backend that throws or cannot load is excluded silently. Its
///    unavailability still shows up as a <see cref="BackendAvailability"/> with
///    a reason, so nothing is hidden; it just never breaks the probe.
///  - The strongest backend per component wins, because the UI must not offer
///    CPU tuning when only a power policy exists.
///  - The knob list is the union, deduplicated, and only knobs whose backend is
///    actually present are exposed.
/// </summary>
public sealed class OverclockReadOnlyRegistry : IOverclockBackendRegistry
{
    private readonly IOverclockBackend[] _backends;

    public OverclockReadOnlyRegistry(params IOverclockBackend[] backends) =>
        _backends = backends;

    /// <summary>The composition CA-O ships in the read-only phase.</summary>
    /// <remarks>
    /// The CPU backend writes <c>powercfg</c> through the elevated UI process
    /// (decision D3), so it gets a direct <see cref="SystemCommandGateway"/> and
    /// not the named-pipe client: the setters are bound to the interactive
    /// session and must not be routed through the SYSTEM service.
    /// </remarks>
    public static OverclockReadOnlyRegistry CreateDefault() =>
        new([.. CreateDefaultBackends()]);

    /// <summary>
    /// The real backend list matching <see cref="CreateDefault"/>. Single source
    /// of truth: <see cref="CreateDefault"/> delegates here so the composition
    /// can never drift between the registry and the UI.
    /// </summary>
    public static IReadOnlyList<IOverclockBackend> DefaultBackends() =>
        CreateDefaultBackends();

    internal static IReadOnlyList<IOverclockBackend> CreateDefaultBackends() =>
    [
        new IntelPolicyBackend(
            new CAO.Infrastructure.Windows.Execution.SystemCommandGateway(),
            new SystemXtuProbe(new CAO.Infrastructure.Windows.SystemRegistry.RegistryAccessor())),
        new NvidiaNvapiBackend(),
        new SmbiosMemoryBackend(),
    ];

    /// <inheritdoc />
    public OverclockCapabilities ProbeAll(CancellationToken ct)
    {
        var details = new List<BackendAvailability>();
        var knobs = new List<OverclockKnob>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        var cpu = CpuBackendKind.None;
        var gpu = GpuBackendKind.None;
        var memory = MemoryBackendKind.None;

        foreach (IOverclockBackend backend in _backends)
        {
            ct.ThrowIfCancellationRequested();

            OverclockCapabilities probe;
            try
            {
                probe = backend.Probe(ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                // Design 6.12: a backend that cannot answer is reported, never
                // propagated. CA-O must open on a machine that has an exotic
                // vendor stack.
                details.Add(new BackendAvailability(
                    backend.Id,
                    Available: false,
                    UnavailableReasonKey: "overclock-backend-probe-failed",
                    RemedyHintKey: null,
                    RequiresRestart: false,
                    DriverVersion: null,
                    LastProbeEvidence: EvidenceLevel.Official));
                continue;
            }

            details.AddRange(probe.Details);

            cpu = Stronger(cpu, probe.Cpu);
            gpu = Stronger(gpu, probe.Gpu);
            memory = Stronger(memory, probe.Memory);

            bool backendPresent = probe.Details.Any(d => d.BackendId == backend.Id && d.Available);

            foreach (OverclockKnob knob in probe.Knobs)
            {
                if (knob.BackendId != backend.Id || !backendPresent)
                {
                    continue;
                }

                if (seen.Add(knob.Id.Value))
                {
                    knobs.Add(knob);
                }
            }
        }

        // Knobs whose backend did not answer are still listed: the UI shows them
        // as unavailable WITH the backend's reason, which is far more useful
        // than a section that simply has no controls in it.
        foreach (OverclockKnob knob in AllCatalogKnobs())
        {
            if (seen.Add(knob.Id.Value))
            {
                knobs.Add(knob);
            }
        }

        return new OverclockCapabilities(cpu, gpu, memory, details, knobs);
    }

    /// <summary>Every knob CA-O knows about, across every component.</summary>
    public static IReadOnlyList<OverclockKnob> AllCatalogKnobs() =>
    [
        .. OverclockCatalog.NvidiaGpuKnobs,
        .. OverclockCatalog.CpuPolicyKnobs,
        .. OverclockCatalog.MemoryKnobs,
    ];

    /// <summary>
    /// Reads one knob by asking every backend that claims it. The first backend
    /// whose id matches and which answers wins; disagreement between backends is
    /// impossible because a knob id belongs to exactly one backend.
    /// </summary>
    public ValueTask<KnobRead> ReadAsync(KnobId id, CancellationToken ct)
    {
        foreach (IOverclockBackend backend in _backends)
        {
            OverclockCapabilities probe = SafeProbe(backend, ct);
            bool owns = probe.Knobs.Any(k => k.Id == id && k.BackendId == backend.Id);
            if (!owns)
            {
                continue;
            }

            try
            {
                return backend.ReadAsync(id, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return ValueTask.FromResult(KnobRead.Fail("overclock-backend-read-failed"));
            }
        }

        return ValueTask.FromResult(KnobRead.Fail("overclock-knob-unknown"));
    }

    /// <summary>Asks every detail source in turn. Unreadable sensors keep their reason.</summary>
    public async ValueTask<IReadOnlyList<OverclockReading>> ReadDetailsAsync(
        IReadOnlyDictionary<string, string>? backendFilter,
        CancellationToken ct)
    {
        var readings = new List<OverclockReading>();

        foreach (IOverclockBackend backend in _backends)
        {
            if (backendFilter is { Count: > 0 }
                && (!backendFilter.TryGetValue(backend.Id, out _) || backendFilter[backend.Id] != "1"))
            {
                continue;
            }

            if (backend is not IOverclockDetailSource source)
            {
                continue;
            }

            ct.ThrowIfCancellationRequested();

            try
            {
                readings.AddRange(await source.ReadDetailsAsync(ct).ConfigureAwait(false));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                readings.Add(new OverclockReading(
                    "overclock_backend_error",
                    backend.Id,
                    EvidenceLevel.Empirical,
                    "overclock-backend-read-failed"));
            }
        }

        return readings;
    }

    private OverclockCapabilities SafeProbe(IOverclockBackend backend, CancellationToken ct)
    {
        try
        {
            return backend.Probe(ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return OverclockCapabilities.Empty;
        }
    }

    /// <summary>
    /// Backend strength order per component. CPU: none &lt; policy &lt; guided &lt;
    /// signed driver. GPU and memory follow their own enum, which is already
    /// declared weakest-first.
    /// </summary>
    private static CpuBackendKind Stronger(CpuBackendKind current, CpuBackendKind candidate) =>
        (int)candidate > (int)current ? candidate : current;

    private static GpuBackendKind Stronger(GpuBackendKind current, GpuBackendKind candidate) =>
        (int)candidate > (int)current ? candidate : current;

    private static MemoryBackendKind Stronger(MemoryBackendKind current, MemoryBackendKind candidate) =>
        (int)candidate > (int)current ? candidate : current;
}