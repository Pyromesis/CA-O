using System.Text.RegularExpressions;
using CAO.Core.Abstractions;
using CAO.Core.Optimization;
using CAO.Shared;
using CAO.Shared.Security;

namespace CAO.Core.Optimizations.Storage;

/// <summary>
/// Desfragmenta SOLO discos duros mecánicos: detecta el medio real de cada
/// volumen (HDD vía MSFT_PhysicalDisk), analiza fragmentación y ejecuta
/// defrag /D únicamente si ≥5%. Los SSD/SCM se omiten SIEMPRE (jamás /D en
/// un SSD) y los de medio desconocido también. Mantenimiento no reversible.
/// </summary>
public sealed class DefragmentHddOnly : IOptimization
{
    internal const double FragmentThresholdPercent = 5.0;

    private string? _lastSummary;
    private bool? _lastSuccess;

    public OptimizationDefinition Definition => new()
    {
        Id = "defragment-hdd-only",
        NameEs = "Desfragmentar HDD (nunca SSD)",
        NameEn = "Defragment HDDs (never SSDs)",
        DescriptionEs = "Analiza y desfragmenta solo discos mecánicos; los SSD se omiten siempre.",
        DescriptionEn = "Analyzes and defragments only mechanical drives; SSDs are always skipped.",
        TooltipEs = "Detecta HDD vs SSD por disco. Solo desfragmenta HDD con 5 por ciento o más fragmentado. Un SSD jamás recibe /D (para SSD usa ReTrim). Para un repaso rápido de C: existe Optimizar disco del sistema. Mantenimiento no reversible.",
        Category = OptimizationCategory.Storage,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
        Flags = OptimizationFlags.NotReversible,
    };

    /// <summary>Lee "% fragmentado" del `defrag /A` (inglés y español).</summary>
    internal static double? ParseFragmentationPercent(string output)
    {
        if (string.IsNullOrWhiteSpace(output)) return null;
        foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var lowered = line.ToLowerInvariant();
            if (!lowered.Contains("fragment")) continue;
            var match = Regex.Match(line, @"(\d+(?:[.,]\d+)?)\s*%");
            if (!match.Success) continue;
            if (double.TryParse(match.Groups[1].Value.Replace(',', '.'),
                    global::System.Globalization.NumberStyles.Float,
                    global::System.Globalization.CultureInfo.InvariantCulture, out var percent))
                return percent;
        }
        return null;
    }

    public OptimizationState Detect(IRegistryAccessor registry) => OptimizationState.Unknown;

    public OptimizationSnapshot Capture(IRegistryAccessor registry)
    {
        var snapshot = new OptimizationSnapshot();
        snapshot.RawNotes.Add("defrag-hdd-only=mantenimiento puntual (sin estado previo que guardar)");
        return snapshot;
    }

    public async Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (context.Executor is null)
            return OperationResult.Fail("Ejecutor no disponible.", "CAO-SEC-010");

        var volumes = DriveInfo.GetDrives()
            .Where(d => d.DriveType == DriveType.Fixed && d.IsReady)
            .Select(d => d.Name[..2].ToUpperInvariant())
            .Distinct()
            .ToList();
        if (volumes.Count == 0)
            return Finish(true, "Sin volúmenes fijos que optimizar.");

        var defragged = new List<string>();
        var alreadyFine = new List<string>();
        var skippedSsd = new List<string>();
        var skippedUnknown = new List<string>();
        var failed = new List<string>();
        foreach (var volume in volumes)
        {
            ct.ThrowIfCancellationRequested();
            var media = DiskMediaDetector.ResolveVolumeMedia(volume);
            if (media is DiskMedia.Ssd or DiskMedia.Scm) { skippedSsd.Add(volume); continue; }
            if (media != DiskMedia.Hdd) { skippedUnknown.Add(volume); continue; }

            var analyze = await context.Executor.ExecuteAsync(
                SystemCommandKey.DefragAnalyze, [volume, "/A"], ct);
            if (!analyze.Success) { failed.Add($"{volume} (análisis)"); continue; }
            var percent = ParseFragmentationPercent(analyze.StdOut);
            if (percent is null) { skippedUnknown.Add($"{volume} (análisis ilegible)"); continue; }
            if (percent < FragmentThresholdPercent) { alreadyFine.Add($"{volume} ({percent:0.#}%)"); continue; }

            var defrag = await context.Executor.ExecuteAsync(
                SystemCommandKey.DefragHdd, [volume, "/D"], ct);
            if (defrag.Success) defragged.Add($"{volume} ({percent:0.#}%→0%)");
            else failed.Add($"{volume} (exit {defrag.ExitCode})");
        }

        var parts = new List<string>();
        if (defragged.Count > 0) parts.Add($"desfragmentados: {string.Join(", ", defragged)}");
        if (alreadyFine.Count > 0) parts.Add($"ya bien: {string.Join(", ", alreadyFine)}");
        if (skippedSsd.Count > 0) parts.Add($"SSD omitidos (jamás /D): {string.Join(", ", skippedSsd)}");
        if (skippedUnknown.Count > 0) parts.Add($"omitidos por medio desconocido: {string.Join(", ", skippedUnknown)}");
        if (failed.Count > 0) parts.Add($"fallaron: {string.Join(", ", failed)}");
        var summary = parts.Count == 0 ? "Sin volúmenes que procesar." : string.Join(". ", parts) + ".";
        return Finish(failed.Count == 0, summary);
    }

    private OperationResult Finish(bool success, string summary)
    {
        _lastSummary = summary;
        _lastSuccess = success;
        return success ? OperationResult.Ok(summary) : OperationResult.Fail(summary, "partial");
    }

    public Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default) =>
        Task.FromResult(OperationResult.Ok("La desfragmentación no se revierte (mantenimiento)."));

    public Task<VerificationResult> VerifyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (_lastSummary is null || _lastSuccess is null)
        {
            return Task.FromResult(VerificationResult.Unknown(OptimizationState.Unknown,
                "Sin evidencia de ejecución en esta sesión."));
        }
        return Task.FromResult(_lastSuccess.Value
            ? VerificationResult.Passed(OptimizationState.Unknown, "Verificado: " + _lastSummary)
            : VerificationResult.Failed(OptimizationState.Unknown, "Fallos: " + _lastSummary));
    }

    public Task<OptimizationPreview> PreviewAsync(IRegistryAccessor registry, CancellationToken ct = default)
    {
        var lines = DriveInfo.GetDrives()
            .Where(d => d.DriveType == DriveType.Fixed && d.IsReady)
            .Select(d => d.Name[..2].ToUpperInvariant())
            .Distinct()
            .Select(volume =>
            {
                var media = DiskMediaDetector.ResolveVolumeMedia(volume);
                var after = media == DiskMedia.Hdd
                    ? "analizar y desfragmentar si ≥5%"
                    : media is DiskMedia.Ssd or DiskMedia.Scm
                        ? "omitido SIEMPRE (SSD)"
                        : "omitido (medio desconocido)";
                return new PreviewLine
                {
                    Kind = "Defrag",
                    Target = $"{volume} ({media})",
                    Before = "fragmentación actual",
                    After = after,
                };
            }).ToList();
        if (lines.Count == 0)
        {
            lines.Add(new PreviewLine
            {
                Kind = "Defrag",
                Target = "(sin volúmenes fijos)",
                Before = "—",
                After = "sin cambios",
            });
        }
        return Task.FromResult(new OptimizationPreview
        {
            OptimizationId = Definition.Id,
            Lines = lines,
            Risk = Definition.Risk,
            SecurityImpact = Definition.SecurityImpact,
            Flags = Definition.Flags,
        });
    }
}
