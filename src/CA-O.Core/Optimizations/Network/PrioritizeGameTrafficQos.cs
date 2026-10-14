using CAO.Core.Abstractions;
using CAO.Core.Optimizations;
using CAO.Shared;

namespace CAO.Core.Optimizations.Network;

/// <summary>
/// Policy-based QoS (el mismo mecanismo que despliega una GPO de Windows):
/// una política por ejecutable de juego competitivo que marca su tráfico con
/// DSCP 46 (Expedited Forwarding, la clase de la voz). En Wi-Fi, WMM mapea
/// DSCP 46 a la cola de voz (AC_VO) y el juego sale antes que descargas o
/// streams del mismo equipo; un router con QoS/SQM también lo prioriza. Sin
/// límite de ancho de banda (Throttle Rate -1). "Do not use NLA" hace que las
/// políticas se apliquen fuera de dominio (redes domésticas).
/// Revert borra los valores: una política sin "Version" se ignora. Requiere
/// reinicio para que la pila QoS relea las políticas.
/// </summary>
public sealed class PrioritizeGameTrafficQos : RegistryOptimizationBase
{
    internal const string PoliciesKey = @"SOFTWARE\Policies\Microsoft\Windows\QoS";
    internal const string NlaKey = @"SYSTEM\CurrentControlSet\Services\Tcpip\QoS";
    internal const string PolicyPrefix = "CA-O Game ";
    internal const string DscpExpeditedForwarding = "46";

    /// <summary>Ejecutables de juego online (el proceso que abre los sockets, no el launcher).</summary>
    internal static readonly IReadOnlyList<string> GameExecutables =
    [
        "VALORANT-Win64-Shipping.exe",
        "cs2.exe",
        "FortniteClient-Win64-Shipping.exe",
        "r5apex.exe",
        "r5apex_dx12.exe",
        "Overwatch.exe",
        "League of Legends.exe",
        "RainbowSix.exe",
        "cod.exe",
        "TslGame.exe",
        "dota2.exe",
        "RocketLeague.exe",
        "Marvel-Win64-Shipping.exe",
        "RobloxPlayerBeta.exe",
        "GTA5.exe",
    ];

    protected override IReadOnlyList<ValueTarget> Targets { get; } = BuildTargets();

    private static List<ValueTarget> BuildTargets()
    {
        var targets = new List<ValueTarget>
        {
            new(RegistryHive2.LocalMachine, NlaKey, "Do not use NLA", "1", RegistryValueKind2.String),
        };
        foreach (var exe in GameExecutables)
        {
            var key = PoliciesKey + @"\" + PolicyPrefix + Path.GetFileNameWithoutExtension(exe);
            void Add(string name, string value) =>
                targets.Add(new(RegistryHive2.LocalMachine, key, name, value, RegistryValueKind2.String));
            Add("Version", "1.0");
            Add("Application Name", exe);
            Add("Protocol", "*");
            Add("Local Port", "*");
            Add("Local IP", "*");
            Add("Local IP Prefix Length", "*");
            Add("Remote Port", "*");
            Add("Remote IP", "*");
            Add("Remote IP Prefix Length", "*");
            Add("DSCP Value", DscpExpeditedForwarding);
            Add("Throttle Rate", "-1");
        }
        return targets;
    }

    public override OptimizationDefinition Definition => new()
    {
        Id = "prioritize-game-traffic-qos",
        NameEs = "Prioridad de red para juegos (QoS DSCP 46)",
        NameEn = "Network priority for games (QoS DSCP 46)",
        DescriptionEs = "Marca el tráfico de los juegos competitivos como prioritario (clase de voz) frente a descargas y streams.",
        DescriptionEn = "Marks competitive games' traffic as priority (voice class) over downloads and streams.",
        TooltipEs = "Políticas QoS de Windows por ejecutable (Valorant, CS2, Fortnite, Apex, LoL, R6, CoD, PUBG...). En Wi-Fi usa la cola de voz; en router con QoS también cuenta. No limita ancho de banda ni toca el juego. Requiere reinicio. Reversible.",
        Category = OptimizationCategory.Network,
        ExpectedImpact = PerformanceImpact.WorkloadDependent,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.Medium,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Conditional,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Medium,
        Flags = OptimizationFlags.RequiresReboot,
    };

    public override Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        WriteTargets(context);
        return Task.FromResult(OperationResult.Ok(
            $"Prioridad DSCP 46 para {GameExecutables.Count} juegos. Reinicia para aplicar."));
    }
}
