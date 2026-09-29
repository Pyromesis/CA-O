using System.Text.RegularExpressions;
using CAO.Core.Abstractions;
using CAO.Shared;
using CAO.Shared.Security;

namespace CAO.Core.Optimizations.Storage;

/// <summary>
/// Diagnóstico del almacén de componentes (DISM /AnalyzeComponentStore):
/// informa del tamaño de WinSxS y si conviene limpiar o ResetBase, sin
/// mutar nada. Solo lectura: alimenta la decisión de limpieza.
/// </summary>
public sealed class AnalyzeComponentStore : IOptimization
{
    private string? _lastSummary;

    public OptimizationDefinition Definition => new()
    {
        Id = "analyze-component-store",
        NameEs = "Analizar almacén de componentes",
        NameEn = "Analyze component store",
        DescriptionEs = "Diagnostica WinSxS con DISM (tamaño y recomendación de limpieza). Solo informa.",
        DescriptionEn = "Diagnoses WinSxS with DISM (size and cleanup recommendation). Reports only.",
        TooltipEs = "Ejecuta DISM /Online /Cleanup-Image /AnalyzeComponentStore. Solo diagnóstico: no modifica ni borra nada.",
        Category = OptimizationCategory.Storage,
        ExpectedImpact = PerformanceImpact.DiagnosticOnly,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Safe,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
    };

    /// <summary>
    /// Extrae el tamaño reportado del almacén (p. ej. "Actual Size of Component
    /// Store : 8.41 GB" / "Tamaño real del almacén de componentes : 8,41 GB").
    /// null si no se encuentra (salida inesperada o localizada distinto).
    /// </summary>
    internal static string? ParseStoreSize(string output)
    {
        if (string.IsNullOrEmpty(output))
            return null;
        var match = Regex.Match(output,
            @"(?:Actual Size of Component Store|Tamaño real del almac[eé]n de componentes)\s*:\s*([0-9][0-9.,]*\s*[KMG]B)",
            RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    /// <summary>
    /// true si DISM recomienda limpiar (línea "Component Store Cleanup
    /// Recommended : Yes" / "Limpieza... : Sí").
    /// </summary>
    internal static bool ParseCleanupRecommended(string output)
    {
        if (string.IsNullOrEmpty(output))
            return false;
        return Regex.IsMatch(output,
            @"(?:Component Store Cleanup Recommended|Limpieza del almac[eé]n de componentes recomendada)\s*:\s*(Yes|Sí|Si)",
            RegexOptions.IgnoreCase);
    }

    // Diagnóstico con ejecutor: sin Apply previo no hay nada observable.
    public OptimizationState Detect(IRegistryAccessor registry) => OptimizationState.Unknown;

    public OptimizationSnapshot Capture(IRegistryAccessor registry) => new();

    public async Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (context.Executor is null)
            return OperationResult.Fail("Ejecutor no disponible.", "CAO-SEC-010");

        var result = await context.Executor.ExecuteAsync(
            SystemCommandKey.DismAnalyzeComponentStore,
            ["/Online", "/Cleanup-Image", "/AnalyzeComponentStore"], ct);
        if (!result.Success)
            return OperationResult.Fail("DISM no pudo analizar el almacén.", result.StdErr);

        var size = ParseStoreSize(result.StdOut) ?? "tamaño no extraído";
        var recommended = ParseCleanupRecommended(result.StdOut);
        _lastSummary = $"WinSxS: {size}. " + (recommended
            ? "DISM recomienda limpiar: use windows-component-store-cleanup."
            : "DISM no recomienda limpiar por ahora.");
        return OperationResult.Ok("Análisis del almacén completado. " + _lastSummary);
    }

    public Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default) =>
        Task.FromResult(OperationResult.Ok("El análisis no tiene cambios que revertir."));

    public Task<VerificationResult> VerifyAsync(OptimizationContext context, CancellationToken ct = default) =>
        Task.FromResult(_lastSummary is null
            ? VerificationResult.Unknown(OptimizationState.Unknown, "Sin evidencia de ejecución en esta sesión.")
            : VerificationResult.Passed(OptimizationState.AppliedByCao, "Verificado: " + _lastSummary));

    public Task<OptimizationPreview> PreviewAsync(IRegistryAccessor registry, CancellationToken ct = default) =>
        Task.FromResult(new OptimizationPreview
        {
            OptimizationId = Definition.Id,
            Lines =
            [
                new PreviewLine
                {
                    Kind = "Command",
                    Target = "DISM /Online /Cleanup-Image /AnalyzeComponentStore",
                    Before = "estado del almacén desconocido",
                    After = "informe de tamaño + recomendación (sin cambios)",
                },
            ],
            Risk = Definition.Risk,
            SecurityImpact = Definition.SecurityImpact,
            Flags = Definition.Flags,
        });
}
