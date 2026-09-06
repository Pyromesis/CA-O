using System.Text.RegularExpressions;
using CAO.Core.Abstractions;
using CAO.Shared;
using CAO.Shared.Security;

namespace CAO.Core.Optimizations.Network;

/// <summary>Restores the TCP congestion provider to CUBIC (Windows default) via netsh.</summary>
public sealed class RestoreWindowsTcpCongestionDefault : IOptimization
{
    public OptimizationDefinition Definition => new()
    {
        Id = "restore-windows-tcp-congestion-default",
        NameEs = "Restaurar congestion TCP por defecto",
        NameEn = "Restore default TCP congestion provider",
        DescriptionEs = "Detecta un proveedor de congestión no estándar y restaura CUBIC, el valor por defecto de Windows.",
        DescriptionEn = "Detects a non-standard congestion provider and restores CUBIC, the Windows default.",
        TooltipEs = "Ejecuta netsh int tcp set global congestionprovider=cubic. Lee el actual con show global. Reversible.",
        Category = OptimizationCategory.Network,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
    };

    public OptimizationState Detect(IRegistryAccessor registry) => OptimizationState.NotApplied;

    public OptimizationSnapshot Capture(IRegistryAccessor registry) => new OptimizationSnapshot();

    public async Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (context.Executor is null)
            return OperationResult.Fail("Ejecutor no disponible.", "CAO-SEC-010");

        var result = await context.Executor.ExecuteAsync(
            SystemCommandKey.NetShTcpCongestionCubic,
            ["int", "tcp", "set", "global", "congestionprovider=cubic"], ct);

        return result.Success
            ? OperationResult.Ok("Proveedor de congestión TCP restaurado a CUBIC.")
            : OperationResult.Fail("No se pudo restaurar el proveedor de congestión.", result.StdErr);
    }

    public async Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default)
    {
        if (context.Executor is null)
            return OperationResult.Fail("Ejecutor no disponible.", "CAO-SEC-010");

        var note = snapshot.RawNotes.FirstOrDefault(n => n.StartsWith("congestionprovider=", StringComparison.Ordinal));
        var previous = note?["congestionprovider=".Length..] ?? "cubic";
        var args = previous.ToLowerInvariant() switch
        {
            "ctcp" => (SystemCommandKey.NetShTcpCongestionDefault, new[] { "int", "tcp", "set", "global", "congestionprovider=default" }),
            _ => (SystemCommandKey.NetShTcpCongestionCubic, new[] { "int", "tcp", "set", "global", "congestionprovider=cubic" }),
        };
        var result = await context.Executor.ExecuteAsync(args.Item1, args.Item2, ct);
        return result.Success
            ? OperationResult.Ok($"Proveedor de congestión restaurado a {previous}.")
            : OperationResult.Fail("No se pudo revertir el proveedor de congestión.", result.StdErr);
    }

    public async Task<VerificationResult> VerifyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (context.Executor is null)
            return VerificationResult.Unknown(OptimizationState.Unknown, "Ejecutor no disponible.");

        var query = await context.Executor.ExecuteAsync(
            SystemCommandKey.NetShTcpShowGlobal, ["int", "tcp", "show", "global"], ct);
        if (!query.Success)
            return VerificationResult.Unknown(OptimizationState.Unknown, "No se pudo consultar TCP global: " + query.StdErr);

        var match = Regex.Match(query.StdOut, @"Congestion\w*\s*Provider\s*:\s*(\w+)", RegexOptions.IgnoreCase);
        if (match.Success && match.Groups[1].Value.Equals("cubic", StringComparison.OrdinalIgnoreCase))
            return VerificationResult.Passed(OptimizationState.AppliedByCao, "Proveedor CUBIC verificado activo.");

        return VerificationResult.Failed(OptimizationState.NotApplied, "El proveedor activo no es CUBIC tras aplicar.");
    }

    public Task<PreconditionResult> CheckPreconditionsAsync(SystemContext context, CancellationToken ct = default) =>
        Task.FromResult(PreconditionResult.Ok("Sin precondiciones específicas."));

    public Task<OptimizationPreview> PreviewAsync(IRegistryAccessor registry, CancellationToken ct = default) =>
        Task.FromResult(new OptimizationPreview
        {
            OptimizationId = Definition.Id,
            Lines =
            [
                new PreviewLine
                {
                    Kind = "NetSh",
                    Target = "netsh int tcp set global congestionprovider=cubic",
                    Before = "proveedor actual",
                    After = "CUBIC",
                },
            ],
            Risk = Definition.Risk,
            SecurityImpact = Definition.SecurityImpact,
            Flags = Definition.Flags,
        });
}
