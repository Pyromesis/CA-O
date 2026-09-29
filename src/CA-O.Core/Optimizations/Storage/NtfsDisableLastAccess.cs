using System.Diagnostics;
using System.Text.RegularExpressions;
using CAO.Core.Abstractions;
using CAO.Shared;
using CAO.Shared.Security;

namespace CAO.Core.Optimizations.Storage;

/// <summary>
/// Desactiva la actualización de last access en NTFS
/// (fsutil behavior set disablelastaccess 1): cada lectura deja de
/// provocar una escritura de metadatos, lo que recorta I/O en discos con
/// muchas lecturas. Acción única (OneShot): el Detect posterior lee el
/// estado vivo con fsutil. Requiere reinicio para efecto completo.
/// </summary>
public sealed class NtfsDisableLastAccess : IOptimization
{
    public OptimizationDefinition Definition => new()
    {
        Id = "ntfs-disable-last-access",
        NameEs = "Desactivar last access en NTFS",
        NameEn = "Disable NTFS last access updates",
        DescriptionEs = "Evita que cada lectura escriba la fecha de último acceso (menos I/O en disco).",
        DescriptionEn = "Stops every read from writing last-access timestamps (less disk I/O).",
        TooltipEs = "Ejecuta fsutil behavior set disablelastaccess 1. Requiere reinicio. Reversible al valor previo exacto (0-3). Acción única.",
        Category = OptimizationCategory.Storage,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
        Flags = OptimizationFlags.OneShot | OptimizationFlags.RequiresReboot,
    };

    /// <summary>
    /// true solo si alguna línea es `DisableLastAccessUpdate = 1`. El nombre
    /// del valor no se localiza, así que vale en ES y EN.
    /// </summary>
    internal static bool ParseLastAccessDisabled(string output)
    {
        if (string.IsNullOrEmpty(output))
            return false;
        var match = Regex.Match(output, @"DisableLastAccessUpdate\s*=\s*([0-3])",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);
        return match.Success && match.Groups[1].Value == "1";
    }

    internal static string? ParseLastAccessValue(string output)
    {
        if (string.IsNullOrEmpty(output))
            return null;
        var match = Regex.Match(output, @"DisableLastAccessUpdate\s*=\s*([0-3])",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string? QueryLiveValue()
    {
        // Solo lectura por ruta fija de System32 (mismo patrón que
        // EnsureTrimEnabled): Detect no tiene executor por firma.
        // Nunca lanza → null.
        try
        {
            var fsutil = Path.Combine(
                Environment.ExpandEnvironmentVariables(@"%SystemRoot%\System32"), "fsutil.exe");
            using var process = new Process();
            process.StartInfo.FileName = fsutil;
            process.StartInfo.ArgumentList.Add("behavior");
            process.StartInfo.ArgumentList.Add("query");
            process.StartInfo.ArgumentList.Add("disablelastaccess");
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;
            process.Start();
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            var readAll = Task.WhenAll(stdoutTask, stderrTask);
            if (Task.WhenAny(readAll, Task.Delay(5000)).GetAwaiter().GetResult() != readAll)
            {
                try { process.Kill(); } catch { }
                return null;
            }
            if (!process.WaitForExit(5000))
            {
                try { process.Kill(); } catch { }
                return null;
            }
            var stdout = stdoutTask.GetAwaiter().GetResult();
            _ = stderrTask.GetAwaiter().GetResult();
            return ParseLastAccessValue(stdout);
        }
        catch
        {
            return null;
        }
    }

    public OptimizationState Detect(IRegistryAccessor registry)
    {
        var current = QueryLiveValue();
        if (current is null)
            return OptimizationState.Unknown;
        return current == "1" ? OptimizationState.AppliedByCao : OptimizationState.NotApplied;
    }

    public OptimizationSnapshot Capture(IRegistryAccessor registry)
    {
        var snapshot = new OptimizationSnapshot();
        snapshot.RawNotes.Add($"lastaccess={QueryLiveValue() ?? "unknown"}");
        return snapshot;
    }

    public async Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (context.Executor is null)
            return OperationResult.Fail("Ejecutor no disponible.", "CAO-SEC-010");

        var result = await context.Executor.ExecuteAsync(
            SystemCommandKey.FsutilLastAccessSet,
            ["behavior", "set", "disablelastaccess", "1"], ct);
        return result.Success
            ? OperationResult.Ok("Last access desactivado (disablelastaccess 1). Reinicia para efecto completo.")
            : OperationResult.Fail("No se pudo desactivar last access.", result.StdErr);
    }

    public async Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default)
    {
        if (context.Executor is null)
            return OperationResult.Fail("Ejecutor no disponible.", "CAO-SEC-010");

        var note = snapshot.RawNotes.FirstOrDefault(n => n.StartsWith("lastaccess=", StringComparison.Ordinal));
        var previous = note?["lastaccess=".Length..];
        if (string.IsNullOrWhiteSpace(previous) || previous is not ("0" or "1" or "2" or "3"))
            return OperationResult.Fail("Sin valor previo registrado; nada que restaurar.", "no-previous-value");

        var result = await context.Executor.ExecuteAsync(
            SystemCommandKey.FsutilLastAccessSet,
            ["behavior", "set", "disablelastaccess", previous], ct);
        return result.Success
            ? OperationResult.Ok($"Last access restaurado a {previous}.")
            : OperationResult.Fail("No se pudo restaurar last access.", result.StdErr);
    }

    public async Task<VerificationResult> VerifyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (context.Executor is null)
            return VerificationResult.Unknown(OptimizationState.Unknown, "Ejecutor no disponible.");

        var query = await context.Executor.ExecuteAsync(
            SystemCommandKey.FsutilLastAccessQuery,
            ["behavior", "query", "disablelastaccess"], ct);
        if (!query.Success)
            return VerificationResult.Unknown(OptimizationState.Unknown, "No se pudo consultar last access: " + query.StdErr);

        return ParseLastAccessDisabled(query.StdOut)
            ? VerificationResult.Passed(OptimizationState.AppliedByCao, "Last access verificado desactivado.")
            : VerificationResult.Failed(OptimizationState.NotApplied, "Last access no quedó desactivado tras aplicar.");
    }

    public Task<OptimizationPreview> PreviewAsync(IRegistryAccessor registry, CancellationToken ct = default) =>
        Task.FromResult(new OptimizationPreview
        {
            OptimizationId = Definition.Id,
            Lines =
            [
                new PreviewLine
                {
                    Kind = "Command",
                    Target = "fsutil behavior set disablelastaccess 1",
                    Before = "valor actual (se lee con fsutil al aplicar)",
                    After = "DisableLastAccessUpdate = 1",
                },
            ],
            Risk = Definition.Risk,
            SecurityImpact = Definition.SecurityImpact,
            Flags = Definition.Flags,
        });
}
