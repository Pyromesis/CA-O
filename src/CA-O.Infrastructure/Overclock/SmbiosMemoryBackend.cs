using System.Globalization;
using System.Management;
using System.Runtime.InteropServices;
using CAO.Core.Overclock;
using CAO.Core.Overclock.OverclockCatalog;
using CAO.Shared;

namespace CAO.Infrastructure.Overclock;

/// <summary>
/// READ-ONLY memory backend (design 6.7).
///
/// It reports what SMBIOS states: the nominal speed of the module and the speed
/// the firmware says is currently configured. That is all user mode can see.
///
/// XMP/EXPO lives in the SPD EEPROM and is only surfaced by the firmware during
/// POST. CA-O therefore never claims to read or write it. Comparing the two
/// numbers tells the user whether the profile looks active, and that inference
/// is graded <see cref="EvidenceLevel.Empirical"/> because it is an inference,
/// not a measurement.
/// </summary>
public sealed class SmbiosMemoryBackend : IOverclockBackend, IOverclockDetailSource, IOverclockWriteGate
{
    /// <summary>Reason when WMI has no Win32_PhysicalMemory instances.</summary>
    public const string NoMemoryReasonKey = "overclock-memory-none-detected";

    /// <summary>Reason when SMBIOS exposes no speed figures at all.</summary>
    public const string SpeedUnknownReasonKey = "overclock-memory-speed-unknown";

    /// <summary>
    /// Memory has no writable backend in user mode, so this reason is constant.
    /// The UI shows it rather than presenting a dead control.
    /// </summary>
    public const string ReadOnlyReasonKey = "overclock-memory-readonly-spd";

    private readonly IReadOnlyList<MemoryModule> _modules;

    public SmbiosMemoryBackend()
        : this(QueryMemoryModules())
    {
    }

    internal SmbiosMemoryBackend(IReadOnlyList<MemoryModule> modules) => _modules = modules;

    /// <summary>One SMBIOS memory module as the firmware describes it.</summary>
    public sealed record MemoryModule(
        string DeviceLocator,
        string Manufacturer,
        string PartNumber,
        uint NominalSpeedMt,
        uint? ConfiguredSpeedMt)
    {
        /// <summary>
        /// Whether the configured speed exceeds the nominal one, i.e. whether an
        /// XMP/EXPO-style profile appears to be in use. Inference, hence
        /// <see cref="EvidenceLevel.Empirical"/>.
        /// </summary>
        public bool ProfileLooksActive =>
            ConfiguredSpeedMt is { } configured && configured > NominalSpeedMt && NominalSpeedMt > 0;
    }

    public string Id => OverclockCatalog.SmbiosMemoryBackendId;

    /// <summary>
    /// Reports availability. Memory reporting works on any Windows machine,
    /// which is why <see cref="OverclockCapabilities.Cpu"/> and
    /// <see cref="OverclockCapabilities.Gpu"/> are left at
    /// <c>PolicyOnly</c>/<c>None</c> here: this backend knows nothing about
    /// them. <see cref="OverclockReadOnlyRegistry"/> is what merges the answers.
    /// </summary>
    public OverclockCapabilities Probe(CancellationToken ct)
    {
        bool anySpeed = _modules.Any(m => m.NominalSpeedMt > 0 || m.ConfiguredSpeedMt is > 0);

        var availability = new BackendAvailability(
            Id,
            Available: _modules.Count > 0,
            UnavailableReasonKey: _modules.Count > 0 ? string.Empty : NoMemoryReasonKey,
            RemedyHintKey: null,
            RequiresRestart: false,
            DriverVersion: null,
            LastProbeEvidence: EvidenceLevel.Official);

        return new OverclockCapabilities(
            CpuBackendKind.None,
            GpuBackendKind.None,
            anySpeed ? MemoryBackendKind.SmbiosReportOnly : MemoryBackendKind.None,
            [availability],
            OverclockCatalog.MemoryKnobs);
    }

    /// <summary>Reads nominal or configured speed across all modules (max wins).</summary>
    public ValueTask<KnobRead> ReadAsync(KnobId id, CancellationToken ct)
    {
        if (id == MemoryKnobs.NominalSpeed)
        {
            uint nominal = _modules.Count == 0 ? 0 : (uint)_modules.Max(m => m.NominalSpeedMt);
            return nominal == 0
                ? ValueTask.FromResult(KnobRead.Fail(SpeedUnknownReasonKey))
                : ValueTask.FromResult(KnobRead.Ok(KnobValue.FromScalar(ToHertz(nominal))));
        }

        if (id == MemoryKnobs.ConfiguredSpeed)
        {
            uint peak = _modules.Count == 0 ? 0u : _modules.Max(m => m.ConfiguredSpeedMt ?? 0u);

            return peak == 0
                ? ValueTask.FromResult(KnobRead.Fail(SpeedUnknownReasonKey))
                : ValueTask.FromResult(KnobRead.Ok(KnobValue.FromScalar(ToHertz(peak))));
        }

        return ValueTask.FromResult(KnobRead.Fail("overclock-memory-unknown-knob"));
    }

    /// <summary>Always refuses: the SPD EEPROM is the firmware's domain, not ours.</summary>
    public ValueTask<KnobWriteResult> WriteAsync(KnobId id, KnobValue value, CancellationToken ct) =>
        ValueTask.FromResult(KnobWriteResult.Fail(ReadOnlyReasonKey));

    /// <summary>
    /// Captures what SMBIOS reports. Restoring is impossible and the snapshot
    /// says so honestly, rather than pretending a revert happened.
    /// </summary>
    public ValueTask<UnitSnapshot> SnapshotAsync(CancellationToken ct)
    {
        uint nominal = _modules.Count == 0 ? 0 : (uint)_modules.Max(m => m.NominalSpeedMt);
        uint configured = _modules.Count == 0 ? 0 : (uint)_modules.Max(m => m.ConfiguredSpeedMt ?? 0u);

        var values = new List<KeyValuePair<KnobId, KnobValue>>();
        if (nominal > 0)
        {
            values.Add(new KeyValuePair<KnobId, KnobValue>(MemoryKnobs.NominalSpeed, KnobValue.FromScalar(ToHertz(nominal))));
        }

        if (configured > 0)
        {
            values.Add(new KeyValuePair<KnobId, KnobValue>(MemoryKnobs.ConfiguredSpeed, KnobValue.FromScalar(ToHertz(configured))));
        }

        return ValueTask.FromResult(new UnitSnapshot(OverclockComponent.Memory, values, DateTimeOffset.UtcNow, null));
    }

    /// <summary>Nothing was changed, so nothing to restore.</summary>
    public ValueTask<RestoreOutcome> RestoreAsync(UnitSnapshot snapshot, CancellationToken ct) =>
        ValueTask.FromResult(snapshot.Count == 0
            ? RestoreOutcome.Ok(0, verified: true)
            : RestoreOutcome.Fail(ReadOnlyReasonKey));

    /// <summary>Per-module detail plus the honest XMP/EXPO verdict.</summary>
    public ValueTask<IReadOnlyList<OverclockReading>> ReadDetailsAsync(CancellationToken ct)
    {
        var readings = new List<OverclockReading>();

        if (_modules.Count == 0)
        {
            readings.Add(new OverclockReading("overclock_memory_modules", string.Empty, EvidenceLevel.Empirical, NoMemoryReasonKey));
            return new ValueTask<IReadOnlyList<OverclockReading>>(readings);
        }

        foreach (MemoryModule module in _modules)
        {
            string label = string.IsNullOrWhiteSpace(module.DeviceLocator) ? module.PartNumber : module.DeviceLocator;
            readings.Add(new OverclockReading(
                "overclock_memory_module",
                FormattableString.Invariant($"{label}: {module.NominalSpeedMt} MT/s nominal") +
                (module.ConfiguredSpeedMt is { } configured
                    ? FormattableString.Invariant($", {configured} MT/s configured")
                    : string.Empty),
                EvidenceLevel.Official));
        }

        bool anyActive = _modules.Any(m => m.ProfileLooksActive);
        readings.Add(new OverclockReading(
            "overclock_memory_profile",
            anyActive ? "overclock_memory_profile_active" : "overclock_memory_profile_inactive",
            EvidenceLevel.Empirical,
            UnavailableReasonKey: _modules.All(m => m.ConfiguredSpeedMt is null) ? SpeedUnknownReasonKey : null));

        return new ValueTask<IReadOnlyList<OverclockReading>>(readings);
    }

    /// <summary>No memory knob is writable.</summary>
    public bool CanWrite(KnobId knob) => false;

    /// <summary>Always the SPD reason.</summary>
    public string? BlockReasonKey(KnobId knob) => ReadOnlyReasonKey;

    /// <summary>
    /// SMBIOS reports megatransfers per second; the catalog models Hertz so the
    /// UI formats speeds the way every other CA-O readout does.
    /// </summary>
    internal static double ToHertz(uint megaTransfersPerSecond) => megaTransfersPerSecond * 1_000_000d;

    /// <summary>
    /// Reads every SMBIOS memory module. Never throws: a machine whose
    /// firmware lies about the class (or a container) yields an empty list, and
    /// the UI explains why instead of crashing.
    /// </summary>
    private static IReadOnlyList<MemoryModule> QueryMemoryModules()
    {
        var modules = new List<MemoryModule>();

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT DeviceLocator, Manufacturer, PartNumber, Speed, ConfiguredClockSpeed FROM Win32_PhysicalMemory");

            using ManagementObjectCollection results = searcher.Get();
            foreach (ManagementBaseObject row in results)
            {
                using (row)
                {
                    uint nominal = ReadUInt32(row["Speed"]);
                    uint? configured = ReadUInt32(row["ConfiguredClockSpeed"]);

                    modules.Add(new MemoryModule(
                        ReadString(row["DeviceLocator"]),
                        ReadString(row["Manufacturer"]),
                        ReadString(row["PartNumber"]),
                        nominal,
                        configured > 0 ? configured : null));
                }
            }
        }
        catch (ManagementException)
        {
            // WMI refused the namespace. An empty list is the honest answer.
        }
        catch (UnauthorizedAccessException)
        {
            // Same: no access means no claim.
        }
        catch (COMException)
        {
            // COM failure talking to WMI.
        }

        return modules;
    }

    private static string ReadString(object? value) =>
        value?.ToString()?.Trim() is { Length: > 0 } text ? text : string.Empty;

    private static uint ReadUInt32(object? value)
    {
        if (value is null)
        {
            return 0;
        }

        try
        {
            return Convert.ToUInt32(value, CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            return 0;
        }
        catch (InvalidCastException)
        {
            return 0;
        }
        catch (OverflowException)
        {
            return 0;
        }
    }
}

/// <summary>Catalog ids for the memory knob set.</summary>
public static class MemoryKnobs
{
    public static readonly KnobId NominalSpeed = "memory.smbios.nominalSpeed";

    public static readonly KnobId ConfiguredSpeed = "memory.smbios.configuredSpeed";
}