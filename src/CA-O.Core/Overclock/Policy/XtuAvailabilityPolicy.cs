namespace CAO.Core.Overclock.Policy;

/// <summary>
/// What the machine actually told us about Intel XTU. Produced by an
/// infrastructure probe; consumed by <see cref="XtuAvailabilityPolicy"/>,
/// which is the only thing allowed to turn these facts into a verdict.
/// </summary>
/// <param name="CpuVendor">"GenuineIntel", "AuthenticAMD", or whatever WMI returned.</param>
/// <param name="IsLaptop">Laptops frequently ship with the ratio locked by the OEM.</param>
public sealed record XtuProbeFacts(
    string CpuVendor,
    bool IsLaptop,
    bool XtuInstalled,
    bool XtuServicePresent,
    string? XtuVersion,
    bool XtuOverclockingSupported)
{
    public static XtuProbeFacts Unknown { get; } = new(
        CpuVendor: string.Empty,
        IsLaptop: false,
        XtuInstalled: false,
        XtuServicePresent: false,
        XtuVersion: null,
        XtuOverclockingSupported: false);
}

/// <summary>
/// The verdict: which CPU backend this machine can honestly claim, and why.
/// <see cref="CpuBackendKind.MsrSigned"/> is never produced here. That kind
/// is reserved for a future signed driver and until one exists, reporting it
/// would be a lie the user pays for with a crash.
/// </summary>
public sealed record XtuAvailability(
    CpuBackendKind Kind,
    bool OverclockingAvailable,
    string ReasonKey,
    string? RemedyHintKey,
    string? DetectedVersion)
{
    public static XtuAvailability PolicyOnly(string reasonKey, string? remedy = null) =>
        new(CpuBackendKind.PolicyOnly, false, reasonKey, remedy, null);

    public static XtuAvailability Guided(string version) =>
        new(CpuBackendKind.XtuGuided, true, "overclock-cpu-xtu-available", "overclock-cpu-xtu-open-xtu", version);
}

/// <summary>
/// Pure decision table for CPU backend selection. Every branch maps to a
/// localized reason key that exists in the UI, so the user always learns the
/// real cause instead of a generic "unsupported".
/// </summary>
public static class XtuAvailabilityPolicy
{
    public const string ReasonAmdCpu = "overclock-cpu-xtu-intel-only";
    public const string ReasonUnknownCpu = "overclock-cpu-vendor-unknown";
    public const string ReasonXtuNotInstalled = "overclock-cpu-xtu-not-installed";
    public const string ReasonXtuServiceMissing = "overclock-cpu-xtu-service-missing";
    public const string ReasonXtuOcUnsupported = "overclock-cpu-xtu-oc-unsupported";
    public const string ReasonXtuLaptopLocked = "overclock-cpu-xtu-locked-laptop";
    public const string ReasonXtuAvailable = "overclock-cpu-xtu-available";
    public const string HintInstallXtu = "overclock-cpu-xtu-install-hint";
    public const string HintOpenXtu = "overclock-cpu-xtu-open-xtu";

    /// <summary>
    /// Decides which CPU backend applies. Power policy (boost mode and the
    /// processor state ceiling) is available on every machine, so the kind is
    /// never <see cref="CpuBackendKind.None"/> unless the CPU itself could
    /// not be identified.
    /// </summary>
    public static XtuAvailability Decide(XtuProbeFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        if (!IsIntel(facts.CpuVendor))
        {
            return IsKnownVendor(facts.CpuVendor)
                ? XtuAvailability.PolicyOnly(ReasonAmdCpu)
                : XtuAvailability.PolicyOnly(ReasonUnknownCpu);
        }

        if (!facts.XtuInstalled)
        {
            return XtuAvailability.PolicyOnly(ReasonXtuNotInstalled, HintInstallXtu);
        }

        // Installed but not running: the service is what actually talks to
        // the embedded controller, so without it XTU is a UI shell.
        if (!facts.XtuServicePresent)
        {
            return XtuAvailability.PolicyOnly(ReasonXtuServiceMissing, HintInstallXtu);
        }

        if (!facts.XtuOverclockingSupported)
        {
            return XtuAvailability.PolicyOnly(ReasonXtuOcUnsupported, HintInstallXtu);
        }

        // A laptop ratio is usually locked by the OEM firmware. XTU reports
        // the toggle as present and then refuses to move it; telling the user
        // to "try anyway" is a guaranteed dead end.
        if (facts.IsLaptop)
        {
            return XtuAvailability.PolicyOnly(ReasonXtuLaptopLocked, HintOpenXtu);
        }

        return XtuAvailability.Guided(facts.XtuVersion ?? "unknown");
    }

    private static bool IsIntel(string vendor) =>
        vendor.Contains("Intel", StringComparison.OrdinalIgnoreCase);

    private static bool IsKnownVendor(string vendor) =>
        vendor.Contains("AMD", StringComparison.OrdinalIgnoreCase) ||
        vendor.Contains("GenuineIntel", StringComparison.OrdinalIgnoreCase) ||
        vendor.Contains("Intel", StringComparison.OrdinalIgnoreCase);
}
