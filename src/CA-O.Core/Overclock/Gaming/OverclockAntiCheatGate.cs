namespace CAO.Core.Overclock.Gaming;

/// <summary>How a backend reaches the hardware. This, not the mere presence of an anti-cheat, decides the gate.</summary>
public enum TuningMechanism
{
    /// <summary>
    /// The GPU vendor's own signed user-mode library (NVML, ADLX), the same path
    /// the vendor's control panel and MSI Afterburner use. No driver of CA-O's,
    /// no handle to any game process.
    /// </summary>
    VendorDriverApi,

    /// <summary>A Windows power plan value written through powercfg.</summary>
    WindowsPowerPolicy,

    /// <summary>Firmware tables read through WMI. Nothing is written.</summary>
    FirmwareReadOnly,

    /// <summary>
    /// A kernel driver of CA-O's own (MSR access and similar). Vanguard and EAC
    /// blocklist exactly this class of driver, so it is the one thing the gate closes.
    /// Also the answer for any backend the gate does not recognise.
    /// </summary>
    KernelDriver,
}

/// <summary>The gate's answer for one backend.</summary>
public sealed record AntiCheatGateDecision(bool Allowed, string ReasonKey);

/// <summary>
/// Decides which overclock writes are compatible with the anti-cheats installed.
///
/// The previous rule closed every write the moment Vanguard or EasyAntiCheat was
/// installed, which on a Valorant/Fortnite machine meant no overclock at all, while
/// none of CA-O's write paths touches what those anti-cheats watch: no kernel driver,
/// no access to the game process, no injection, no game files, no launch options
/// (OverclockGameSafetyTests checks that against the module's source). The rule now
/// blocks only the mechanism anti-cheats actually reject: a kernel driver.
/// </summary>
public static class OverclockAntiCheatGate
{
    public const string ReasonCompatible = "overclock-anticheat-compatible";
    public const string ReasonNoAntiCheat = "overclock-anticheat-none";
    public const string ReasonKernelDriverBlocked = "overclock-anticheat-kernel-driver";
    public const string ReasonKernelDriverScanUnavailable = "overclock-anticheat-kernel-scan-unavailable";

    public static TuningMechanism MechanismFor(string backendId) => backendId switch
    {
        OverclockCatalog.OverclockCatalog.NvapiBackendId => TuningMechanism.VendorDriverApi,
        OverclockCatalog.OverclockCatalog.AmdAdlxBackendId => TuningMechanism.VendorDriverApi,
        OverclockCatalog.OverclockCatalog.IntelPolicyBackendId => TuningMechanism.WindowsPowerPolicy,
        OverclockCatalog.OverclockCatalog.SmbiosMemoryBackendId => TuningMechanism.FirmwareReadOnly,

        // A backend nobody classified is treated as the riskiest kind until someone does.
        _ => TuningMechanism.KernelDriver,
    };

    /// <param name="mechanism">How the backend writes.</param>
    /// <param name="antiCheatScanSucceeded">False when the services registry could not be read.</param>
    /// <param name="detectedAntiCheats">Anti-cheats found installed.</param>
    public static AntiCheatGateDecision Decide(
        TuningMechanism mechanism,
        bool antiCheatScanSucceeded,
        IReadOnlyList<string> detectedAntiCheats)
    {
        ArgumentNullException.ThrowIfNull(detectedAntiCheats);

        if (mechanism == TuningMechanism.KernelDriver)
        {
            if (!antiCheatScanSucceeded)
            {
                // Only here does an unreadable registry matter: absence cannot be proven.
                return new(false, ReasonKernelDriverScanUnavailable);
            }

            if (detectedAntiCheats.Count > 0)
            {
                return new(false, ReasonKernelDriverBlocked);
            }
        }

        return new(true, detectedAntiCheats.Count > 0 ? ReasonCompatible : ReasonNoAntiCheat);
    }
}
