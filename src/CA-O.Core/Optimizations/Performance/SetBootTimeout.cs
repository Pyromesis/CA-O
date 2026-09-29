using System.Diagnostics;
using System.Text.RegularExpressions;
using CAO.Core.Abstractions;
using CAO.Shared;
using CAO.Shared.Security;

namespace CAO.Core.Optimizations.Performance;

/// <summary>
/// Fija el timeout del menú de arranque a 5 s (bcdedit /timeout 5), SOLO
/// cuando hay más de un Windows arrancable. Con un solo Windows no hay
/// menú que abreviar: Detect informa Unknown ("no aplicable") y Apply se
/// niega a tocar el BCD. Reversible al timeout previo exacto.
/// </summary>
public sealed class SetBootTimeout : IOptimization
{
    internal const string TargetTimeout = "5";

    private int? _lastExitCode;

    public OptimizationDefinition Definition => new()
    {
        Id = "set-boot-timeout",
        NameEs = "Abreviar menú de arranque (5 s)",
        NameEn = "Shorten boot menu timeout (5 s)",
        DescriptionEs = "Fija el menú de arranque a 5 segundos, solo si hay varios Windows.",
        DescriptionEn = "Sets the boot menu to 5 seconds, only with multiple Windows entries.",
        TooltipEs = "bcdedit /timeout 5. Solo multiboot: con un solo Windows no se toca nada. Reversible al timeout previo.",
        Category = OptimizationCategory.Performance,
        ExpectedImpact = PerformanceImpact.Tiny,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Moderate,
        Compatibility = CompatibilityStatus.Conditional,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
    };

    /// <summary>
    /// Cuenta entradas de cargador ("Windows Boot Loader" / "Cargador de arranque de Windows",
    /// bcdedit localiza sus cabeceras).
    /// </summary>
    internal static int CountBootLoaders(string output)
    {
        if (string.IsNullOrEmpty(output))
            return 0;
        return Regex.Count(output,
            @"(?:Windows Boot Loader|Cargador de arranque de Windows)",
            RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Lee el timeout actual ("timeout 30" / "Tiempo de espera 30").
    /// null si no se encuentra.
    /// </summary>
    internal static string? ParseTimeout(string output)
    {
        if (string.IsNullOrEmpty(output))
            return null;
        var match = Regex.Match(output,
            @"^(?:timeout|tiempo de espera)\s+(\d+)\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string? QueryBcd()
    {
        // Solo lectura por ruta fija de System32. bcdedit exige elevación:
        // sin ella (UI sin privilegios) falla → null → Unknown honesto.
        try
        {
            var bcdedit = Path.Combine(
                Environment.ExpandEnvironmentVariables(@"%SystemRoot%\System32"), "bcdedit.exe");
            using var process = new Process();
            process.StartInfo.FileName = bcdedit;
            process.StartInfo.ArgumentList.Add("/enum");
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;
            process.Start();
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            var readAll = Task.WhenAll(stdoutTask, stderrTask);
            if (Task.WhenAny(readAll, Task.Delay(8000)).GetAwaiter().GetResult() != readAll)
            {
                try { process.Kill(); } catch { }
                return null;
            }
            if (!process.WaitForExit(8000))
            {
                try { process.Kill(); } catch { }
                return null;
            }
            if (process.ExitCode != 0)
                return null;
            var stdout = stdoutTask.GetAwaiter().GetResult();
            _ = stderrTask.GetAwaiter().GetResult();
            return stdout;
        }
        catch
        {
            return null;
        }
    }

    public OptimizationState Detect(IRegistryAccessor registry)
    {
        var output = QueryBcd();
        if (output is null)
            return OptimizationState.Unknown;
        // OptimizationState no tiene NotApplicable: con un solo Windows se
        // informa Unknown ("no aplicable, nada que abreviar") para no
        // declarar ni éxito ni pendiente falsos.
        if (CountBootLoaders(output) < 2)
            return OptimizationState.Unknown;
        return ParseTimeout(output) == TargetTimeout
            ? OptimizationState.AppliedByCao
            : OptimizationState.NotApplied;
    }

    public OptimizationSnapshot Capture(IRegistryAccessor registry)
    {
        var snapshot = new OptimizationSnapshot();
        snapshot.RawNotes.Add($"boot-timeout={ParseTimeout(QueryBcd() ?? string.Empty) ?? "unknown"}");
        return snapshot;
    }

    public async Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (context.Executor is null)
            return OperationResult.Fail("Ejecutor no disponible.", "CAO-SEC-010");

        var list = await context.Executor.ExecuteAsync(
            SystemCommandKey.BcdEditEnum, ["/enum"], ct);
        if (!list.Success)
            return OperationResult.Fail("No se pudo leer el BCD.", list.StdErr);

        if (CountBootLoaders(list.StdOut) < 2)
        {
            // Un solo Windows: no hay menú que abreviar. Fallar ANTES de
            // mutar, no "éxito" sobre un cambio que no debía existir.
            return OperationResult.Fail(
                "Un solo Windows arrancable: no hay menú que abreviar y no se toca el BCD.",
                "single-boot");
        }

        var set = await context.Executor.ExecuteAsync(
            SystemCommandKey.BcdEditSetTimeout, ["/timeout", TargetTimeout], ct);
        _lastExitCode = set.ExitCode;
        return set.Success
            ? OperationResult.Ok("Menú de arranque fijado a 5 segundos.")
            : OperationResult.Fail("bcdedit no pudo fijar el timeout.", set.StdErr);
    }

    public async Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default)
    {
        // La política solo permite /timeout 5: un timeout previo distinto
        // (p. ej. 30) no se puede reescribir por el funnel. Se informa en
        // vez de inventar una escritura fuera de política.
        var note = snapshot.RawNotes.FirstOrDefault(n => n.StartsWith("boot-timeout=", StringComparison.Ordinal));
        var previous = note?["boot-timeout=".Length..];
        if (string.IsNullOrWhiteSpace(previous) || previous == "unknown")
            return OperationResult.Fail("Sin timeout previo registrado; nada que restaurar.", "no-previous-timeout");
        if (previous == TargetTimeout)
            return OperationResult.Ok("Ya estaba en 5 segundos; nada que restaurar.");
        return OperationResult.Fail(
            $"El timeout previo era {previous} s y la política solo permite fijar 5 s: restáurelo a mano con bcdedit /timeout {previous} en un CMD elevado.",
            "revert-out-of-policy");
    }

    public async Task<VerificationResult> VerifyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (context.Executor is null)
            return VerificationResult.Unknown(OptimizationState.Unknown, "Ejecutor no disponible.");
        if (_lastExitCode is null)
        {
            return VerificationResult.Unknown(OptimizationState.Unknown,
                "Sin evidencia de ejecución en esta sesión.");
        }
        if (_lastExitCode != 0)
            return VerificationResult.Failed(OptimizationState.Unknown, $"bcdedit terminó con exit={_lastExitCode}.");

        var list = await context.Executor.ExecuteAsync(
            SystemCommandKey.BcdEditEnum, ["/enum"], ct);
        if (!list.Success)
            return VerificationResult.Unknown(OptimizationState.Unknown, "No se pudo releer el BCD: " + list.StdErr);
        return ParseTimeout(list.StdOut) == TargetTimeout
            ? VerificationResult.Passed(OptimizationState.AppliedByCao, "Timeout de arranque verificado en 5 s.")
            : VerificationResult.Failed(OptimizationState.NotApplied, "El timeout no quedó en 5 s tras aplicar.");
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
                    Target = "bcdedit /timeout 5",
                    Before = "timeout actual (se lee del BCD al aplicar)",
                    After = "timeout 5 (solo si hay varios Windows)",
                },
            ],
            Risk = Definition.Risk,
            SecurityImpact = Definition.SecurityImpact,
            Flags = Definition.Flags,
        });
}
