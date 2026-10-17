using CAO.Core.Abstractions;
using CAO.Core.Optimizations;
using CAO.Shared;

namespace CAO.Core.Optimizations.Network;

/// <summary>
/// QoS DSCP 46 para el chat de voz (Discord, TeamSpeak, Mumble): es la clase
/// para la que existe Expedited Forwarding. Con voz y juego marcados, una
/// descarga en el mismo equipo ya no corta la voz ni sube el ping. Mismo
/// mecanismo y mismas reglas que prioritize-game-traffic-qos.
/// </summary>
public sealed class PrioritizeVoiceChatQos : QosPolicyOptimization
{
    internal const string PolicyPrefix = "CA-O Voice ";

    internal static readonly IReadOnlyList<string> VoiceExecutables =
    [
        "Discord.exe",
        "DiscordPTB.exe",
        "DiscordCanary.exe",
        "ts3client_win64.exe",
        "TeamSpeak.exe",
        "mumble.exe",
    ];

    protected override string PolicyNamePrefix => PolicyPrefix;

    protected override IReadOnlyList<string> Executables => VoiceExecutables;

    public override OptimizationDefinition Definition => new()
    {
        Id = "prioritize-voice-chat-qos",
        NameEs = "Prioridad de red para chat de voz (QoS)",
        NameEn = "Network priority for voice chat (QoS)",
        DescriptionEs = "Marca el tráfico de Discord, TeamSpeak y Mumble como voz para que no se corte con descargas.",
        DescriptionEn = "Marks Discord, TeamSpeak and Mumble traffic as voice so downloads do not cut it.",
        TooltipEs = "Políticas QoS de Windows por ejecutable con DSCP 46, sin límite de ancho de banda. En Wi-Fi usa la cola de voz. Combina con la prioridad de juegos. Requiere reinicio. Reversible.",
        Category = OptimizationCategory.Network,
        ExpectedImpact = PerformanceImpact.WorkloadDependent,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.Medium,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Conditional,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
        Flags = OptimizationFlags.RequiresReboot,
    };

    public override Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        WriteTargets(context);
        return Task.FromResult(OperationResult.Ok(
            $"Prioridad DSCP 46 para {VoiceExecutables.Count} apps de voz. Reinicia para aplicar."));
    }
}
