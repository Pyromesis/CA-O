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

    /// <summary>Knobs for NVIDIA GPUs, served by nvapi64.dll.</summary>
    public static IReadOnlyList<OverclockKnob> NvidiaGpuKnobs { get; } =
    [
        Build(new KnobId("nvidia.gpu.powerLimit"), OverclockComponent.Gpu,
            KnobRange.Scalar(0, 100), 1, KnobUnit.Percent, RiskLevel.Moderate, EvidenceLevel.Vendor,
            "knob_nvidia_power_limit", "knob_nvidia_power_limit_desc", requiresReboot: false,
            presets:
            [
                (OverclockProfile.Conservative, 100),
                (OverclockProfile.Balanced, 110),
                (OverclockProfile.Performance, 120),
            ]),

        Build(new KnobId("nvidia.gpu.thermalLimit"), OverclockComponent.Gpu,
            KnobRange.Scalar(60, 95), 1, KnobUnit.Celsius, RiskLevel.High, EvidenceLevel.Vendor,
            "knob_nvidia_thermal_limit", "knob_nvidia_thermal_limit_desc", requiresReboot: false,
            presets:
            [
                (OverclockProfile.Conservative, 83),
                (OverclockProfile.Balanced, 86),
                (OverclockProfile.Performance, 90),
            ],
            // CA-O never raises this past the driver's own default for the profile
            // Conservative; the ceiling is what the engine negotiates downward.
            dependsOn: [new KnobDependency("nvidia.gpu.powerLimit", DependencyKind.Requires)]),

        Build(new KnobId("nvidia.gpu.coreClockOffset"), OverclockComponent.Gpu,
            KnobRange.Scalar(-500, 500), 15, KnobUnit.Hertz, RiskLevel.High, EvidenceLevel.Vendor,
            "knob_nvidia_core_offset", "knob_nvidia_core_offset_desc", requiresReboot: false,
            presets:
            [
                (OverclockProfile.Conservative, 0),
                (OverclockProfile.Balanced, 60),
                (OverclockProfile.Performance, 120),
            ],
            // A clock offset without power headroom is dead performance plus extra risk.
            dependsOn: [new KnobDependency("nvidia.gpu.powerLimit", DependencyKind.PairsWith)]),

        Build(new KnobId("nvidia.gpu.memoryClockOffset"), OverclockComponent.Gpu,
            KnobRange.Scalar(-500, 1500), 25, KnobUnit.Hertz, RiskLevel.High, EvidenceLevel.Vendor,
            "knob_nvidia_mem_offset", "knob_nvidia_mem_offset_desc", requiresReboot: false,
            presets:
            [
                (OverclockProfile.Conservative, 0),
                (OverclockProfile.Balanced, 250),
                (OverclockProfile.Performance, 500),
            ],
            dependsOn: [new KnobDependency("nvidia.gpu.powerLimit", DependencyKind.PairsWith)]),

        Build(new KnobId("nvidia.gpu.fanTarget"), OverclockComponent.Gpu,
            KnobRange.Scalar(0, 100), 5, KnobUnit.Percent, RiskLevel.Low, EvidenceLevel.Vendor,
            "knob_nvidia_fan_target", "knob_nvidia_fan_target_desc", requiresReboot: false,
            presets:
            [
                (OverclockProfile.Conservative, 0),
                (OverclockProfile.Balanced, 0),
                (OverclockProfile.Performance, 15),
            ]),

        // Read-only reporting knob: CA-O measures the thermal ceiling and shows it,
        // it never invents it.
        Build(new KnobId("nvidia.gpu.clockReadout"), OverclockComponent.Gpu,
            KnobRange.Scalar(0, 4000), 1, KnobUnit.Hertz, RiskLevel.Safe, EvidenceLevel.Vendor,
            "knob_nvidia_clock_readout", "knob_nvidia_clock_readout_desc", requiresReboot: false,
            presets: []),
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
                (OverclockProfile.Conservative, KnobValue.FromIndex(0)),
                (OverclockProfile.Balanced, KnobValue.FromIndex(2)),
                (OverclockProfile.Performance, KnobValue.FromIndex(2)),
            ]),

        Build(new KnobId("cpu.policy.maxFrequencyPercent"), OverclockComponent.Cpu,
            KnobRange.Scalar(20, 100), 1, KnobUnit.Percent, RiskLevel.Low, EvidenceLevel.Official,
            "knob_cpu_max_freq", "knob_cpu_max_freq_desc", requiresReboot: false,
            presets:
            [
                (OverclockProfile.Conservative, 100),
                (OverclockProfile.Balanced, 100),
                (OverclockProfile.Performance, 100),
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
            KnobRange.Scalar(0, 20000), 1, KnobUnit.Hertz, RiskLevel.Safe, EvidenceLevel.Official,
            "knob_memory_nominal", "knob_memory_nominal_desc", requiresReboot: false, presets: []),

        Build(new KnobId("memory.smbios.configuredSpeed"), OverclockComponent.Memory,
            KnobRange.Scalar(0, 20000), 1, KnobUnit.Hertz, RiskLevel.Safe, EvidenceLevel.Official,
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
        IReadOnlyList<KnobDependency>? dependsOn = null)
    {
        string backendId = component switch
        {
            OverclockComponent.Gpu => NvapiBackendId,
            OverclockComponent.Cpu => IntelPolicyBackendId,
            _ => SmbiosMemoryBackendId,
        };

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
            backendId,
            presets.Select(p => new KnobPreset(p.Profile, p.Value, $"preset_{p.Profile.ToString().ToLowerInvariant()}")).ToArray(),
            dependsOn ?? []);
    }
}