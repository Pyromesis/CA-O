using CAO.Shared;

namespace CAO.Core.Overclock.OverclockCatalog;

/// <summary>
/// The declarative catalog: every tunable CA-O can expose is data. Backends are
/// thin readers/writers; this class owns what exists, at what risk, and with
/// what evidence.
///
/// Nothing here appears with <see cref="EvidenceLevel.Unknown"/>: the same rule
/// the 93-item optimization catalog follows (README "Evidencia honesta").
/// </summary>
public static class OverclockCatalog
{
    public const string NvapiBackendId = "nvidia-nvapi";
    public const string IntelPolicyBackendId = "intel-policy";
    public const string SmbiosMemoryBackendId = "smbios-memory";
    public const string AmdAdlxBackendId = "amd-adlx";

    /// <summary>Knobs for NVIDIA GPUs, served by nvapi64.dll.</summary>
    public static IReadOnlyList<OverclockKnob> NvidiaGpuKnobs { get; } =
    [
        Build(new KnobId("nvidia.gpu.powerLimit"), OverclockComponent.Gpu,
            KnobRange.Scalar(0, 100), 1, KnobUnit.Percent, RiskLevel.Moderate, EvidenceLevel.Vendor,
            "knob_nvidia_power_limit", "knob_nvidia_power_limit_desc", requiresReboot: false,
            presets:
            [
                (OverclockProfile.Conservative, 100d),
                (OverclockProfile.Balanced, 110d),
                (OverclockProfile.Performance, 120d),
            ]),

        Build(new KnobId("nvidia.gpu.thermalLimit"), OverclockComponent.Gpu,
            KnobRange.Scalar(60, 95), 1, KnobUnit.Celsius, RiskLevel.High, EvidenceLevel.Vendor,
            "knob_nvidia_thermal_limit", "knob_nvidia_thermal_limit_desc", requiresReboot: false,
            presets:
            [
                (OverclockProfile.Conservative, 83d),
                (OverclockProfile.Balanced, 86d),
                (OverclockProfile.Performance, 90d),
            ],
            // CA-O never raises this past the driver's own default for the profile
            // Conservative; the ceiling is what the engine negotiates downward.
            dependsOn: [new KnobDependency("nvidia.gpu.powerLimit", DependencyKind.Requires)]),

        // Every frequency in this catalog is Hertz, including the offsets. The
        // vendor APIs report MHz, so the backend converts at the boundary: a
        // -500_000_000..500_000_000 Hz range IS the classic -500..500 MHz core
        // offset, and asking for a -500..500 *Hertz* shift would be a rounding
        // error wide that made the tuning engine walk 34 identical points.
        Build(new KnobId("nvidia.gpu.coreClockOffset"), OverclockComponent.Gpu,
            KnobRange.Scalar(-500_000_000d, 500_000_000d), 15_000_000d, KnobUnit.Hertz, RiskLevel.High, EvidenceLevel.Vendor,
            "knob_nvidia_core_offset", "knob_nvidia_core_offset_desc", requiresReboot: false,
            presets:
            [
                (OverclockProfile.Conservative, 0d),
                (OverclockProfile.Balanced, 60_000_000d),
                (OverclockProfile.Performance, 120_000_000d),
            ],
            // A clock offset without power headroom is dead performance plus extra risk.
            dependsOn: [new KnobDependency("nvidia.gpu.powerLimit", DependencyKind.PairsWith)]),

        Build(new KnobId("nvidia.gpu.memoryClockOffset"), OverclockComponent.Gpu,
            KnobRange.Scalar(-500_000_000d, 1_500_000_000d), 25_000_000d, KnobUnit.Hertz, RiskLevel.High, EvidenceLevel.Vendor,
            "knob_nvidia_mem_offset", "knob_nvidia_mem_offset_desc", requiresReboot: false,
            presets:
            [
                (OverclockProfile.Conservative, 0d),
                (OverclockProfile.Balanced, 250_000_000d),
                (OverclockProfile.Performance, 500_000_000d),
            ],
            dependsOn: [new KnobDependency("nvidia.gpu.powerLimit", DependencyKind.PairsWith)]),

        Build(new KnobId("nvidia.gpu.fanTarget"), OverclockComponent.Gpu,
            KnobRange.Scalar(0, 100), 5, KnobUnit.Percent, RiskLevel.Low, EvidenceLevel.Vendor,
            "knob_nvidia_fan_target", "knob_nvidia_fan_target_desc", requiresReboot: false,
            presets:
            [
                (OverclockProfile.Conservative, 0d),
                (OverclockProfile.Balanced, 0d),
                (OverclockProfile.Performance, 15d),
            ]),

        // Read-only reporting knob: CA-O measures the thermal ceiling and shows it,
        // it never invents it.
        Build(new KnobId("nvidia.gpu.clockReadout"), OverclockComponent.Gpu,
            KnobRange.Scalar(0, 4_000_000_000d), 1_000_000d, KnobUnit.Hertz, RiskLevel.Safe, EvidenceLevel.Vendor,
            "knob_nvidia_clock_readout", "knob_nvidia_clock_readout_desc", requiresReboot: false,
            presets: []),
    ];
    /// <summary>
    /// AMD knobs, served by amdadlx64.dll. Power limit only, in % of stock like
    /// the NVIDIA one; the backend converts from ADLX's offset-around-0 range. AMD clocks stay out on
    /// purpose: from Navi4 up SetGPUMaxFrequency takes an offset, before that
    /// an absolute frequency, and guessing which one a card expects is how a
    /// number the hardware never meant gets written.
    /// </summary>
    public static IReadOnlyList<OverclockKnob> AmdGpuKnobs { get; } =
    [
        Build(new KnobId("amd.gpu.powerLimit"), OverclockComponent.Gpu,
            KnobRange.Scalar(0, 100), 1, KnobUnit.Percent, RiskLevel.Moderate, EvidenceLevel.Vendor,
            "knob_amd_power_limit", "knob_amd_power_limit_desc", requiresReboot: false,
            presets:
            [
                (OverclockProfile.Conservative, 100d),
                (OverclockProfile.Balanced, 110d),
                (OverclockProfile.Performance, 120d),
            ],
            backendId: AmdAdlxBackendId),
    ];

    /// <summary>
    /// CPU policy knobs. These are Windows power-plan settings, not clock
    /// overclocking: without a signed driver there is no MSR access, so CA-O
    /// never claims "+X MHz".
    /// </summary>
    public static IReadOnlyList<OverclockKnob> CpuPolicyKnobs { get; } =
    [
        Build(new KnobId("cpu.policy.boostMode"), OverclockComponent.Cpu,
            KnobRange.Discrete([0, 1, 2]), 1, KnobUnit.Enumerated,
            RiskLevel.Low, EvidenceLevel.Official,
            "knob_cpu_boost_mode", "knob_cpu_boost_mode_desc", requiresReboot: false,
            presets:
            [
                // 0 = turbo off, 1 = turbo normal, 2 = turbo agresivo. Ningun perfil
                // apaga el turbo: "Conservador" en una pestana de overclock es el
                // comportamiento de fabrica, no un procesador mas lento.
                (OverclockProfile.Conservative, KnobValue.FromIndex(1)),
                (OverclockProfile.Balanced, KnobValue.FromIndex(2)),
                (OverclockProfile.Performance, KnobValue.FromIndex(2)),
            ]),

        Build(new KnobId("cpu.policy.maxFrequencyPercent"), OverclockComponent.Cpu,
            KnobRange.Scalar(20, 100), 1, KnobUnit.Percent, RiskLevel.Low, EvidenceLevel.Official,
            "knob_cpu_max_freq", "knob_cpu_max_freq_desc", requiresReboot: false,
            presets:
            [
                (OverclockProfile.Conservative, 100d),
                (OverclockProfile.Balanced, 100d),
                (OverclockProfile.Performance, 100d),
            ]),
    ];

    /// <summary>
    /// Memory is report-only. Nominal and configured speed are real SMBIOS
    /// values; the XMP/EXPO profile itself is not readable in user mode and CA-O
    /// says so instead of claiming otherwise (design 6.7).
    /// </summary>
    public static IReadOnlyList<OverclockKnob> MemoryKnobs { get; } =
    [
        Build(new KnobId("memory.smbios.nominalSpeed"), OverclockComponent.Memory,
            KnobRange.Scalar(0, 20_000_000_000d), 1_000_000d, KnobUnit.Hertz, RiskLevel.Safe, EvidenceLevel.Official,
            "knob_memory_nominal", "knob_memory_nominal_desc", requiresReboot: false, presets: []),

        Build(new KnobId("memory.smbios.configuredSpeed"), OverclockComponent.Memory,
            KnobRange.Scalar(0, 20_000_000_000d), 1_000_000d, KnobUnit.Hertz, RiskLevel.Safe, EvidenceLevel.Official,
            "knob_memory_configured", "knob_memory_configured_desc", requiresReboot: false, presets: []),
    ];

    private static OverclockKnob Build(
        KnobId id,
        OverclockComponent component,
        KnobRange range,
        double step,
        KnobUnit unit,
        RiskLevel risk,
        EvidenceLevel evidence,
        string labelKey,
        string descriptionKey,
        bool requiresReboot,
        (OverclockProfile Profile, KnobValue Value)[] presets,
        IReadOnlyList<KnobDependency>? dependsOn = null,
        string? backendId = null)
    {
        string resolvedBackendId = backendId ?? (component switch
        {
            OverclockComponent.Gpu => NvapiBackendId,
            OverclockComponent.Cpu => IntelPolicyBackendId,
            _ => SmbiosMemoryBackendId,
        });

        return new OverclockKnob(
            id,
            component,
            range.Allowed is { Count: > 0 } ? KnobKind.Enum : KnobKind.Scalar,
            range,
            step,
            unit,
            risk,
            evidence,
            labelKey,
            descriptionKey,
            requiresReboot,
            KnobScope.Session,
            resolvedBackendId,
            presets.Select(p => new KnobPreset(p.Profile, p.Value, $"preset_{p.Profile.ToString().ToLowerInvariant()}")).ToArray(),
            dependsOn ?? []);
    }
}