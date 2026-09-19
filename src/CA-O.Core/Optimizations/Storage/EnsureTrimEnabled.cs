using System.Diagnostics;
using System.Text.RegularExpressions;
using CAO.Core.Abstractions;
using CAO.Shared;
using CAO.Shared.Security;

namespace CAO.Core.Optimizations.Storage;

/// <summary>Ensures TRIM is enabled (fsutil DisableDeleteNotify 0).</summary>
public sealed class EnsureTrimEnabled : IOptimization
{
    public OptimizationDefinition Definition => new()
    {
        Id = "ensure-trim-enabled",
        NameEs = "Asegurar TRIM habilitado en SSD",
        NameEn = "Ensure TRIM enabled on SSD",
        DescriptionEs = "Activa TRIM con fsutil para mantener el rendimiento del SSD al liberar bloques.",
        DescriptionEn = "Enables TRIM via fsutil to keep SSD performance when freeing blocks.",
        TooltipEs = "Ejecuta fsutil behavior set DisableDeleteNotify 0. Reversible exacto.",
        Category = OptimizationCategory.Storage,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
    };

    /// <summary>
    /// true solo si alguna línea es `DisableDeleteNotify = 0` (NTFS y/o
    /// ReFS). El nombre del valor no se localiza, así que vale en ES y EN.
    /// </summary>
    internal static bool ParseDeleteNotifyOff(string output)
    {
        if (string.IsNullOrEmpty(output))
            return false;
        return Regex.IsMatch(output, @"DisableDeleteNotify\s*=\s*0",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);
    }

    public OptimizationState Detect(IRegistryAccessor registry)
    {
        // Solo lectura: `fsutil behavior query` por ruta fija de System32
        // (sin PATH, sin PowerShell, sin executor: Detect no tiene executor
        // por firma). Nunca lanza → Unknown.
        try
        {
            var fsutil = Path.Combine(
                Environment.ExpandEnvironmentVariables(@"%SystemRoot%\System32"), "fsutil.exe");
            using var process = new Process();
            process.StartInfo.FileName = fsutil;
            process.StartInfo.ArgumentList.Add("behavior");
            process.StartInfo.ArgumentList.Add("query");
            process.StartInfo.ArgumentList.Add("DisableDeleteNotify");
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;
            process.Start();
            // Patrón mínimo anti-deadlock: leer stdout antes de WaitForExit
            // (salida minúscula, stderr se drena después solo por higiene).
            var stdout = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(5000))
            {
                try { process.Kill(); } catch { }
                return OptimizationState.Unknown;
            }
            _ = process.StandardError.ReadToEnd();
            return ParseDeleteNotifyOff(stdout)
                ? OptimizationState.AppliedByCao
                : OptimizationState.NotApplied;
        }
        catch
        {
            return OptimizationState.Unknown;
        }
    }

    public OptimizationSnapshot Capture(IRegistryAccessor registry) => new OptimizationSnapshot();

    public async Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (context.Executor is null)
            return OperationResult.Fail("Ejecutor no disponible.", "CAO-SEC-010");

        var result = await context.Executor.ExecuteAsync(
            SystemCommandKey.FsutilDisableDeleteNotifyOff,
            ["behavior", "set", "DisableDeleteNotify", "0"], ct);

        return result.Success
            ? OperationResult.Ok("TRIM habilitado (DisableDeleteNotify 0).")
            : OperationResult.Fail("No se pudo habilitar TRIM.", result.StdErr);
    }

    public async Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default)
    {
        if (context.Executor is null)
            return OperationResult.Fail("Ejecutor no disponible.", "CAO-SEC-010");

        var result = await context.Executor.ExecuteAsync(
            SystemCommandKey.FsutilDisableDeleteNotifyOn,
            ["behavior", "set", "DisableDeleteNotify", "1"], ct);

        return result.Success
            ? OperationResult.Ok("TRIM devuelto a deshabilitado.")
            : OperationResult.Fail("No se pudo revertir TRIM.", result.StdErr);
    }

    public async Task<VerificationResult> VerifyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (context.Executor is null)
            return VerificationResult.Unknown(OptimizationState.Unknown, "Ejecutor no disponible.");

        var query = await context.Executor.ExecuteAsync(
            SystemCommandKey.FsutilQueryDeleteNotify,
            ["behavior", "query", "DisableDeleteNotify"], ct);
        if (!query.Success)
            return VerificationResult.Unknown(OptimizationState.Unknown, "No se pudo consultar TRIM: " + query.StdErr);

        return ParseDeleteNotifyOff(query.StdOut)
            ? VerificationResult.Passed(OptimizationState.AppliedByCao, "TRIM verificado habilitado.")
            : VerificationResult.Failed(OptimizationState.NotApplied, "TRIM no está habilitado tras aplicar.");
    }

    public Task<PreconditionResult> CheckPreconditionsAsync(SystemContext context, CancellationToken ct = default) =>
        Task.FromResult(PreconditionResult.Ok("Requiere SSD."));

    public Task<OptimizationPreview> PreviewAsync(IRegistryAccessor registry, CancellationToken ct = default) =>
        Task.FromResult(new OptimizationPreview
        {
            OptimizationId = Definition.Id,
            Lines =
            [
                new PreviewLine
                {
                    Kind = "Command",
                    Target = "fsutil behavior set DisableDeleteNotify 0",
                    Before = "desconocido",
                    After = "DisableDeleteNotify = 0",
                },
            ],
            Risk = Definition.Risk,
            SecurityImpact = Definition.SecurityImpact,
            Flags = Definition.Flags,
        });
}
