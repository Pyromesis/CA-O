using CAO.Shared;

namespace CAO.Core.Gaming;

/// <summary>
/// Known game profiles (FASE 22/25). Only vendor-documented facts: which
/// latency tech the game exposes and its anti-cheat posture. CA-O derives
/// NO registry tweaks from this table; guidance is advisory.
/// </summary>
public static class GameProfileCatalog
{
    public static readonly IReadOnlyList<GameProfile> All =
    [
        new GameProfile
        {
            GameId = "valorant",
            DisplayName = "VALORANT",
            Executables = ["valorant.exe", "valorant-win64-shipping.exe"],
            Launcher = "Riot Client",
            ReflexAvailable = false,
            AntiLagAvailable = false,
            AntiCheatPolicy = GameAntiCheatPolicy.KernelLevel,
            GuidanceEs =
            [
                "Vanguard exige Secure Boot/TPM según configuración: NO desactive VBS/HVCI.",
                "Use el escalado y el modo de pantalla del cliente; CA-O no toca archivos del juego.",
            ],
        },
        new GameProfile
        {
            GameId = "cs2",
            DisplayName = "Counter-Strike 2",
            Executables = ["cs2.exe"],
            Launcher = "Steam",
            ReflexAvailable = false,
            AntiLagAvailable = false,
            AntiCheatPolicy = GameAntiCheatPolicy.KernelLevel,
            GuidanceEs =
            [
                "VSync y limitador nativo del juego tienen prioridad sobre hacks globales.",
                "Con VRR activo, limite FPS por debajo del refresco para estabilidad de frame-time.",
            ],
        },
        new GameProfile
        {
            GameId = "fortnite",
            DisplayName = "Fortnite",
            Executables = ["fortniteclient-win64-shipping.exe"],
            Launcher = "Epic Games",
            ReflexAvailable = true,
            AntiLagAvailable = false,
            AntiCheatPolicy = GameAntiCheatPolicy.UserMode,
            GuidanceEs =
            [
                "Reflex disponible en ajustes del juego: actívelo allí (prioridad sobre Ultra Low Latency global).",
                "Easy Anti-Cheat presente: no modifique servicios de seguridad.",
            ],
        },
        new GameProfile
        {
            GameId = "apex-legends",
            DisplayName = "Apex Legends",
            Executables = ["r5apex.exe"],
            Launcher = "EA App / Steam",
            ReflexAvailable = true,
            AntiLagAvailable = false,
            AntiCheatPolicy = GameAntiCheatPolicy.UserMode,
            GuidanceEs =
            [
                "Reflex configurable in-game; manténgalo preferente frente a tweaks globales.",
                "Easy Anti-Cheat presente.",
            ],
        },
        new GameProfile
        {
            GameId = "overwatch-2",
            DisplayName = "Overwatch 2",
            Executables = ["overwatch.exe"],
            Launcher = "Battle.net",
            ReflexAvailable = true,
            AntiLagAvailable = false,
            AntiCheatPolicy = GameAntiCheatPolicy.UserMode,
            GuidanceEs =
            [
                "Reflex en opciones de juego; combine con cap de FPS estable.",
                "Sin conflicto conocido con optimizaciones Safe.",
            ],
        },
        new GameProfile
        {
            GameId = "league-of-legends",
            DisplayName = "League of Legends",
            Executables = ["league of legends.exe"],
            Launcher = "Riot Client",
            ReflexAvailable = false,
            AntiLagAvailable = false,
            AntiCheatPolicy = GameAntiCheatPolicy.UserMode,
            GuidanceEs = ["Anti-cheat Vanguard presente en modo kernel: no toque VBS/HVCI."],
        },
        new GameProfile
        {
            GameId = "rainbow-six",
            DisplayName = "Rainbow Six Siege",
            Executables = ["rainbowsix.exe"],
            Launcher = "Ubisoft Connect",
            ReflexAvailable = false,
            AntiLagAvailable = false,
            AntiCheatPolicy = GameAntiCheatPolicy.KernelLevel,
            GuidanceEs = ["BattlEye kernel: VBS/HVCI bloqueados."],
        },
        new GameProfile
        {
            GameId = "call-of-duty",
            DisplayName = "Call of Duty",
            Executables = ["cod.exe"],
            Launcher = "Battle.net",
            ReflexAvailable = true,
            AntiLagAvailable = false,
            AntiCheatPolicy = GameAntiCheatPolicy.KernelLevel,
            GuidanceEs = ["Ricochet kernel: no desactive seguridad."],
        },
        new GameProfile
        {
            GameId = "destiny-2",
            DisplayName = "Destiny 2",
            Executables = ["destiny2.exe"],
            Launcher = "Steam",
            ReflexAvailable = false,
            AntiLagAvailable = false,
            AntiCheatPolicy = GameAntiCheatPolicy.UserMode,
            GuidanceEs = ["BattlEye: VBS/HVCI bloqueados en modo protegido."],
        },
    ];

    /// <summary>Matches a detected executable name against the catalog.</summary>
    public static GameProfile? FindByExecutable(string executableName) =>
        All.FirstOrDefault(profile => profile.Executables.Contains(
            executableName.Replace(".exe", string.Empty, StringComparison.OrdinalIgnoreCase) + ".exe",
            StringComparer.OrdinalIgnoreCase))
        ?? All.FirstOrDefault(profile => profile.Executables.Any(
            exe => exe.Equals(executableName, StringComparison.OrdinalIgnoreCase)));

    /// <summary>Profiles whose anti-cheat posture demands conservative blocking.</summary>
    public static bool IsKernelProtected(this GameProfile profile) =>
        profile.AntiCheatPolicy == GameAntiCheatPolicy.KernelLevel;
}
