using Microsoft.Win32;
using CAO.Shared;

namespace CAO.Infrastructure.Gaming;

/// <summary>
/// Detects installed anti-cheats by enumerating registered services and
/// kernel drivers (spec 60). Detection is read-only and never guarantees
/// future compatibility; the guard treats presence conservatively.
/// </summary>
public sealed class AntiCheatScanProvider
{
    private static readonly (AntiCheatKind Kind, string[] Components)[] Known =
    [
        (AntiCheatKind.Vanguard, ["vgc", "vgk", "Vanguard"]),
        (AntiCheatKind.EasyAntiCheat, ["EasyAntiCheat", "EasyAntiCheat_EOS"]),
        (AntiCheatKind.BattlEye, ["BEService", "BEDaisy", "BATTLEYE"]),
        (AntiCheatKind.Faceit, ["FACEIT", "FACEITService", "FaceitClient"]),
        (AntiCheatKind.Ricochet, ["Ricochet", "RicochetService"]),
    ];

    private const string ServicesKey = @"SYSTEM\CurrentControlSet\Services";

    public IReadOnlyList<AntiCheatInfo> Scan()
    {
        var found = new List<AntiCheatInfo>();

        try
        {
            using var services = Registry.LocalMachine.OpenSubKey(ServicesKey);
            if (services is null)
            {
                return found;
            }

            foreach (var (kind, components) in Known)
            {
                var hits = new List<string>();
                foreach (var component in components)
                {
                    if (services.OpenSubKey(component) is not null)
                    {
                        hits.Add(component);
                    }
                }

                if (hits.Count > 0)
                {
                    found.Add(new AntiCheatInfo(kind, ServicesKey, hits));
                }
            }
        }
        catch
        {
            // Hardened systems may deny enumeration; absence of data must not
            // look like certainty, callers treat empty list as "not detected".
        }

        return found;
    }
}
