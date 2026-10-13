using System.Globalization;
using System.Text.RegularExpressions;

namespace CAO.Core.Overclock.Policy;

/// <summary>
/// Intel CPU performance policy through the Windows power scheme. This is
/// deliberately NOT an overclock: it caps the maximum processor state and
/// selects the firmware boost behaviour. CA-O never presents it as a clock
/// increase, because it is not one (design 6.7, decision D "option D").
///
/// This type is pure: no I/O, no elevation, no process. It owns the two
/// things that must be right for the privileged layer to stay safe - which
/// GUID pairs are allowed, and which integer range each one accepts - so
/// both can be proven by tests that never touch the machine.
/// </summary>
public static partial class IntelPowerPolicy
{
    /// <summary>SUB_PROCESSOR, the only subgroup this feature may touch.</summary>
    public const string ProcessorSubgroupGuid = "54533251-82be-4824-96c1-47b60b740d00";

    /// <summary>PERFBOOSTMODE: 0 disabled, 1 enabled, 2 aggressive.</summary>
    public const string BoostModeSettingGuid = "be337241-0d82-4146-a960-4f3749d470c3";

    /// <summary>PROCTHROTTLEMAX: ceiling as a percentage of nominal, 0-100.</summary>
    public const string MaxProcessorStateSettingGuid = "bc5038f7-23e0-4960-96da-33abaf5935ec";

    /// <summary>The scheme CA-O writes to. Never a duplicated custom scheme.</summary>
    public const string TargetScheme = "SCHEME_CURRENT";

    public const int BoostModeMinValue = 0;
    public const int BoostModeMaxValue = 2;

    /// <summary>
    /// Percentages below this leave the machine in a state most users will
    /// read as "the app broke my computer", so the floor is a product
    /// decision rather than a technical one.
    /// </summary>
    public const int MaxFrequencyPercentMin = 20;

    public const int MaxFrequencyPercentMax = 100;

    /// <summary>
    /// <c>powercfg /setacvalueindex</c> only stages the value; the scheme must
    /// be re-activated for it to take effect. Callers that forget this would
    /// report "applied" for a change that never happened, which is exactly
    /// the failure mode CA-O forbids (durable rule: never say applied
    /// without inspecting).
    /// </summary>
    public const bool RequiresSchemeActivation = true;

    public static KnobId BoostModeKnob { get; } = new("cpu.policy.boostMode");

    public static KnobId MaxFrequencyKnob { get; } = new("cpu.policy.maxFrequencyPercent");

    /// <summary>Maps a catalogue knob to the GUID pair that implements it.</summary>
    public static bool TryResolve(KnobId knob, out PowerSettingTarget target)
    {
        string value = knob.Value;
        if (string.Equals(value, BoostModeKnob.Value, StringComparison.Ordinal))
        {
            target = new PowerSettingTarget(ProcessorSubgroupGuid, BoostModeSettingGuid);
            return true;
        }

        if (string.Equals(value, MaxFrequencyKnob.Value, StringComparison.Ordinal))
        {
            target = new PowerSettingTarget(ProcessorSubgroupGuid, MaxProcessorStateSettingGuid);
            return true;
        }

        target = default;
        return false;
    }

    /// <summary>
    /// True when the pair is one this feature is allowed to read or write.
    /// Comparison is case-insensitive because the GUIDs travel as command
    /// line arguments, and a caller is free to pass them upper or lower.
    /// </summary>
    public static bool IsAllowedTarget(string subgroup, string setting) =>
        IsSameGuid(subgroup, ProcessorSubgroupGuid) &&
        (IsSameGuid(setting, BoostModeSettingGuid) || IsSameGuid(setting, MaxProcessorStateSettingGuid));

    /// <summary>
    /// Validates and clamps a requested value for the given pair. Returns
    /// false for any pair outside <see cref="IsAllowedTarget"/> so a future
    /// knob cannot silently ride on this channel.
    /// </summary>
    public static bool TryNormalize(in PowerSettingTarget target, int requested, out int normalized)
    {
        normalized = requested;
        if (!IsAllowedTarget(target.Subgroup, target.Setting))
        {
            return false;
        }

        if (IsSameGuid(target.Setting, BoostModeSettingGuid))
        {
            if (requested < BoostModeMinValue || requested > BoostModeMaxValue)
            {
                return false;
            }

            return true;
        }

        if (requested < MaxFrequencyPercentMin || requested > MaxFrequencyPercentMax)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Builds the exact argument list for <c>powercfg /setacvalueindex</c>.
    /// Returns null when the pair or the value would not pass the policy, so
    /// callers cannot accidentally construct a command that the privileged
    /// gateway would reject later.
    /// </summary>
    public static IReadOnlyList<string>? BuildSetArguments(in PowerSettingTarget target, int value)
    {
        if (!TryNormalize(target, value, out int normalized))
        {
            return null;
        }

        return
        [
            "/setacvalueindex",
            TargetScheme,
            target.Subgroup,
            target.Setting,
            normalized.ToString(CultureInfo.InvariantCulture),
        ];
    }

    /// <summary>
    /// Builds the argument list for reading the current value. Returns null
    /// for any pair outside the policy.
    /// </summary>
    public static IReadOnlyList<string>? BuildQueryArguments(in PowerSettingTarget target) =>
        IsAllowedTarget(target.Subgroup, target.Setting)
            ? ["/q", TargetScheme, target.Subgroup, target.Setting]
            : null;

    /// <summary>
    /// Extracts the AC setting index from <c>powercfg /q</c> output.
    ///
    /// The output is localized, so matching on "Power Setting Index" would
    /// break on any non-English machine. Instead the parser takes the
    /// penultimate hexadecimal literal (AC) and the last one (DC), which is
    /// locale-independent: GUIDs in this output are brace-delimited, never
    /// bare hex. A real PROCTHROTTLEMAX block emits Minimum/Maximum/
    /// Increment before the current indices, so taking the first literal
    /// would read Minimum (0x00000000) instead of the current AC value.
    /// Returns null when no literal is present, which callers must treat as
    /// "could not inspect", never as a default value.
    /// </summary>
    public static int? ParseAcSettingIndex(string? stdout)
    {
        if (string.IsNullOrWhiteSpace(stdout))
        {
            return null;
        }

        MatchCollection matches = HexIndexPattern().Matches(stdout);
        if (matches.Count == 0)
        {
            return null;
        }

        string value = matches.Count == 1 ? matches[0].Value : matches[matches.Count - 2].Value;
        return int.Parse(value.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    /// <summary>Same as <see cref="ParseAcSettingIndex"/> for the DC index (last literal).</summary>
    public static int? ParseDcSettingIndex(string? stdout)
    {
        if (string.IsNullOrWhiteSpace(stdout))
        {
            return null;
        }

        MatchCollection matches = HexIndexPattern().Matches(stdout);
        return matches.Count >= 2
            ? int.Parse(matches[matches.Count - 1].Value.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)
            : null;
    }

    private static bool IsSameGuid(string candidate, string known) =>
        !string.IsNullOrWhiteSpace(candidate) &&
        string.Equals(candidate.Trim(), known, StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"0x[0-9a-fA-F]{1,8}", RegexOptions.CultureInvariant)]
    private static partial Regex HexIndexPattern();
}

/// <summary>A subgroup GUID plus a setting GUID, both as the strings powercfg wants.</summary>
public readonly record struct PowerSettingTarget(string Subgroup, string Setting);

/// <summary>
/// Firmware boost behaviour exposed by PERFBOOSTMODE. Aggressive mode is not
/// available on every Intel part; the backend reports that honestly instead
/// of applying a value the firmware will ignore.
/// </summary>
public enum CpuBoostMode
{
    /// <summary>PERFBOOSTMODE = 0. Turbo is off; sustained clocks are lower.</summary>
    Disabled = 0,

    /// <summary>PERFBOOSTMODE = 1. Turbo follows the firmware's normal rules.</summary>
    Enabled = 1,

    /// <summary>PERFBOOSTMODE = 2. Turbo is allowed to exceed its own limits for longer.</summary>
    Aggressive = 2,
}
