using Microsoft.Win32;
using CAO.Infrastructure.Overclock.Gaming;
using CAO.Shared;

namespace CAO.Infrastructure.Gaming;

/// <summary>
/// Detects installed anti-cheats by enumerating registered services and
/// kernel drivers (spec 60). Detection is read-only and never guarantees
/// future compatibility; the guard treats presence conservatively.
/// </summary>
public sealed class AntiCheatScanProvider : IAntiCheatScanner
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

    /// <summary>
    /// Returns what was found and nothing about whether the machine let us look.
    /// An empty list here means "no hit", which a caller that needs to reason about
    /// absence must not read as "no anti-cheat installed".
    /// </summary>
    public IReadOnlyList<AntiCheatInfo> Scan()
    {
        TryScan(out var found);
        return found;
    }

    /// <summary>
    /// The same scan, but it reports whether the registry could actually be read.
    /// </summary>
    /// <remarks>
    /// The overclock game gate needs the difference between "there is nothing
    /// installed" and "I was not allowed to check". On a hardened machine
    /// <see cref="Scan"/> returns an empty list in both cases, and a gate that
    /// treats the second as the first opens itself on exactly the machines where
    /// it matters most.
    /// </remarks>
    public bool TryScan(out IReadOnlyList<AntiCheatInfo> found)
    {
        var results = new List<AntiCheatInfo>();

        try
        {
            using var services = Registry.LocalMachine.OpenSubKey(ServicesKey);
            if (services is null)
            {
                found = results;
                return false;
            }

            foreach (var (kind, components) in Known)
            {
                var hits = new List<string>();
                foreach (var component in components)
                {
                    using var sub = services.OpenSubKey(component);
                    if (sub is not null)
                    {
                        hits.Add(component);
                    }
                }

                if (hits.Count > 0)
                {
                    results.Add(new AntiCheatInfo(kind, ServicesKey, hits));
                }
            }
        }
        catch
        {
            // Hardened systems may deny enumeration. Absence of data must not
            // look like certainty, so this reports "could not look", not "none".
            found = [];
            return false;
        }

        found = results;
        return true;
    }
}
