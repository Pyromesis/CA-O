using CAO.Shared;

namespace CAO.Core.Optimizations.Troubleshoot;

/// <summary>Wi-Fi que se cae, no ve redes o se queda "conectando": reinicia WLAN AutoConfig.</summary>
public sealed class RestartWlanService : RestartWindowsServiceOptimization
{
    protected override string ServiceName => "WlanSvc";
    protected override string ServiceLabel => "Wi-Fi (WLAN AutoConfig)";

    public override OptimizationDefinition Definition => new()
    {
        Id = "restart-wlan-service",
        NameEs = "Reiniciar Wi-Fi (WLAN AutoConfig)",
        NameEn = "Restart Wi-Fi (WLAN AutoConfig)",
        DescriptionEs = "Reinicia el servicio Wi-Fi de Windows. Para Wi-Fi que se cae, no ve redes o no termina de conectar.",
        DescriptionEn = "Restarts the Windows Wi-Fi service. For Wi-Fi that drops, sees no networks or never finishes connecting.",
        TooltipEs = "Detiene e inicia WlanSvc. El Wi-Fi se corta unos segundos y se reconecta solo. Las redes guardadas se conservan.",
        Category = OptimizationCategory.Network,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
        Reversible = false,
        Flags = OptimizationFlags.NotReversible,
    };
}

/// <summary>Game Pass que no inicia sesión, juegos de la Store que no abren: reinicia los servicios Xbox.</summary>
public sealed class RestartXboxServices : RestartWindowsServiceOptimization
{
    protected override string ServiceName => "GamingServices";
    protected override string ServiceLabel => "Servicios Xbox";

    protected override IReadOnlyList<string> ServiceNames { get; } =
        ["GamingServices", "GamingServicesNet", "XblAuthManager", "XblGameSave", "XboxNetApiSvc"];

    public override OptimizationDefinition Definition => new()
    {
        Id = "restart-xbox-services",
        NameEs = "Reiniciar servicios de Xbox y Game Pass",
        NameEn = "Restart Xbox and Game Pass services",
        DescriptionEs = "Para Game Pass que no inicia sesión, juegos de la Microsoft Store que no abren o partidas guardadas que no sincronizan.",
        DescriptionEn = "For Game Pass sign-in failures, Microsoft Store games that do not launch or saves that do not sync.",
        TooltipEs = "Reinicia Gaming Services, Xbox Live Auth Manager, Game Save y Xbox Networking. Cierra los juegos de Xbox antes. Los que no estén instalados se omiten.",
        Category = OptimizationCategory.Gaming,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
        Reversible = false,
        Flags = OptimizationFlags.NotReversible,
    };
}

/// <summary>Teclado táctil, emojis (Win+.) o escritura con IME que se quedan pillados.</summary>
public sealed class RestartTextInputService : RestartWindowsServiceOptimization
{
    protected override string ServiceName => "TextInputManagementService";
    protected override string ServiceLabel => "Entrada de texto";

    public override OptimizationDefinition Definition => new()
    {
        Id = "restart-text-input-service",
        NameEs = "Reiniciar entrada de texto (emojis, IME)",
        NameEn = "Restart text input (emoji, IME)",
        DescriptionEs = "Para el panel de emojis (Win+.), el teclado táctil o el IME que no abren o escriben mal.",
        DescriptionEn = "For the emoji panel (Win+.), touch keyboard or IME that do not open or type wrongly.",
        TooltipEs = "Reinicia TextInputManagementService. No cambia la distribución del teclado ni ningún ajuste.",
        Category = OptimizationCategory.Performance,
        ExpectedImpact = PerformanceImpact.Tiny,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.Medium,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
        Reversible = false,
        Flags = OptimizationFlags.NotReversible,
    };
}
