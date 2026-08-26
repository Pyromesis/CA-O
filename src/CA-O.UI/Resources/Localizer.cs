namespace CAO.UI;

/// <summary>
/// Typed UI strings (spec 84): es-ES and en-US are first-class. No raw text
/// scattered through pages; adding a language means adding one dictionary.
/// All strings flow through Localizer.Get(key); technical identifiers are never translated.
/// </summary>
public static class Localizer
{
    public static IReadOnlyList<string> SupportedLanguages { get; } = ["es-ES", "en-US"];

    private static readonly Dictionary<string, string> Spanish = new()
    {
        ["app.title"] = "CA-O",
        ["app.subtitle"] = "Plataforma de rendimiento para Windows",
        ["nav.dashboard"] = "Panel",
        ["nav.analyze"] = "Analizar",
        ["nav.optimize"] = "Optimizar",
        ["nav.gaming"] = "Gaming",
        ["nav.diagnostics"] = "Diagnóstico",
        ["nav.benchmark"] = "Benchmark",
        ["nav.restore"] = "Restaurar",
        ["nav.history"] = "Historial",
        ["nav.settings"] = "Ajustes",
        ["dashboard.analyze"] = "Analizar sistema",
        ["dashboard.analyzing"] = "Analizando…",
        ["dashboard.lastAnalysis"] = "Último análisis",
        ["dashboard.never"] = "nunca",
        ["dashboard.recommended"] = "Recomendados",
        ["dashboard.optional"] = "Opcionales",
        ["dashboard.experimental"] = "Experimentales",
        ["dashboard.securitySensitive"] = "Seguridad sensible",
        ["dashboard.notApplicable"] = "No aplicables",
        ["dashboard.health"] = "Salud del sistema",
        ["dashboard.findings"] = "Hallazgos",
        ["dashboard.noClaims"] = "Sin promesas numéricas: solo mediciones.",
        ["analyze.run"] = "Ejecutar diagnósticos",
        ["analyze.diagnosticsFirst"] = "Diagnósticos primero",
        ["analyze.diagnosticsFirstMessage"] = "Cada área se mide antes de recomendar cualquier cambio. Nada aquí modifica el sistema.",
        ["optimize.applyRecommended"] = "Aplicar recomendados",
        ["optimize.apply"] = "Aplicar",
        ["optimize.revert"] = "Revertir",
        ["optimize.preview"] = "Ver cambios",
        ["optimize.expertWarning"] = "Modo Expert activo: los cambios experimentales y sensibles a seguridad quedan visibles pero siguen requiriendo confirmación.",
        ["benchmark.baseline"] = "Medir línea base",
        ["benchmark.after"] = "Medir tras cambio",
        ["benchmark.running"] = "Midiendo…",
        ["restore.pointNote"] = "CA-O crea snapshots propios antes de cada cambio; el punto de restauración de Windows se solicita una vez por sesión cuando el sistema lo permite.",
        ["settings.expertMode"] = "Modo Expert",
        ["settings.theme"] = "Tema",
        ["settings.language"] = "Idioma",
        ["settings.serviceCheck"] = "Comprobar servicio privilegiado",
        ["common.workloadDependent"] = "Depende de la carga de trabajo",
        ["common.noClaims"] = "Sin promesas numéricas: solo mediciones.",
        ["common.connected"] = "Conectado",
        ["common.disconnected"] = "No disponible",
        ["common.healthy"] = "Correcto",
        ["common.warning"] = "Atención",
        ["common.attention"] = "Requiere atención",
        ["common.systemStatus"] = "Estado del sistema",
        ["common.serviceStatus"] = "Servicio",
        ["common.lastScan"] = "Último análisis",
    };

    private static readonly Dictionary<string, string> English = new()
    {
        ["app.title"] = "CA-O",
        ["app.subtitle"] = "Windows performance platform",
        ["nav.dashboard"] = "Dashboard",
        ["nav.analyze"] = "Analyze",
        ["nav.optimize"] = "Optimize",
        ["nav.gaming"] = "Gaming",
        ["nav.diagnostics"] = "Diagnostics",
        ["nav.benchmark"] = "Benchmark",
        ["nav.restore"] = "Restore",
        ["nav.history"] = "History",
        ["nav.settings"] = "Settings",
        ["dashboard.analyze"] = "Analyze system",
        ["dashboard.analyzing"] = "Analyzing…",
        ["dashboard.lastAnalysis"] = "Last analysis",
        ["dashboard.never"] = "never",
        ["dashboard.recommended"] = "Recommended",
        ["dashboard.optional"] = "Optional",
        ["dashboard.experimental"] = "Experimental",
        ["dashboard.securitySensitive"] = "Security sensitive",
        ["dashboard.notApplicable"] = "Not applicable",
        ["dashboard.health"] = "System health",
        ["dashboard.findings"] = "Findings",
        ["dashboard.noClaims"] = "No numeric promises: measurements only.",
        ["analyze.run"] = "Run diagnostics",
        ["analyze.diagnosticsFirst"] = "Diagnostics first",
        ["analyze.diagnosticsFirstMessage"] = "Each area is measured before any change is recommended. Nothing here modifies the system.",
        ["optimize.applyRecommended"] = "Apply recommended",
        ["optimize.apply"] = "Apply",
        ["optimize.revert"] = "Revert",
        ["optimize.preview"] = "Preview changes",
        ["optimize.expertWarning"] = "Expert mode active: experimental and security-sensitive changes become visible but still require confirmation.",
        ["benchmark.baseline"] = "Measure baseline",
        ["benchmark.after"] = "Measure after change",
        ["benchmark.running"] = "Measuring…",
        ["restore.pointNote"] = "CA-O takes its own snapshots before every change; a Windows restore point is requested once per session when the system allows it.",
        ["settings.expertMode"] = "Expert mode",
        ["settings.theme"] = "Theme",
        ["settings.language"] = "Language",
        ["settings.serviceCheck"] = "Check privileged service",
        ["common.workloadDependent"] = "Workload dependent",
        ["common.noClaims"] = "No numeric promises: measurements only.",
        ["common.connected"] = "Connected",
        ["common.disconnected"] = "Unavailable",
        ["common.healthy"] = "Healthy",
        ["common.warning"] = "Warning",
        ["common.attention"] = "Attention required",
        ["common.systemStatus"] = "System status",
        ["common.serviceStatus"] = "Service",
        ["common.lastScan"] = "Last scan",
    };

    private static Dictionary<string, string> Current { get; set; } = Spanish;

    public static void SetLanguage(string language)
    {
        Current = language.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? English : Spanish;
    }

    public static string Get(string key) =>
        Current.TryGetValue(key, out var value) ? value : key;
}
