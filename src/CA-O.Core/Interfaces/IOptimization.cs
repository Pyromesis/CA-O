using CAO.Shared;

namespace CAO.Core.Abstractions;

/// <summary>Result of one optimization lifecycle operation.</summary>
public sealed record OperationResult(bool Success, string MessageEs, string? Error = null)
{
    public static OperationResult Ok(string messageEs) => new(true, messageEs);

    public static OperationResult Fail(string messageEs, string? error = null) => new(false, messageEs, error);
}

/// <summary>
/// Contract implemented by every optimization in the catalog (spec 8).
///
/// Transactional mapping used by the engine (spec 123):
///   PRECHECK -> CheckPreconditionsAsync
///   SNAPSHOT -> CaptureSnapshotAsync (Capture)
///   APPLY    -> ApplyAsync
///   VERIFY   -> VerifyAsync
///   ROLLBACK -> RollbackAsync / RevertAsync
/// Implementations MUST capture state into a snapshot before mutating and
/// MUST verify after applying; Detect must never mutate anything.
/// </summary>
public interface IOptimization
{
    OptimizationDefinition Definition { get; }

    /// <summary>Reads live Windows state without changing it.</summary>
    OptimizationState Detect(IRegistryAccessor registry);

    /// <summary>Captures the current state so Apply can be reverted later.</summary>
    OptimizationSnapshot Capture(IRegistryAccessor registry);

    /// <summary>Applies the change. Assumes Capture already ran.</summary>
    Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default);

    /// <summary>Restores the exact state captured earlier.</summary>
    Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default);

    // ------------------------------------------------------------------
    // Lifecycle extensions (v2). Default implementations keep the simple
    // registry-backed catalog honest without forcing every optimization to
    // re-implement boilerplate; overrides exist where real checks matter.
    // ------------------------------------------------------------------

    /// <summary>
    /// PRECHECK phase: optimization-specific gates only (e.g. observed
    /// service state). Context-level rules live in the COMPATIBILITY
    /// transaction phase so they are always enforced, even for overrides.
    /// </summary>
    Task<PreconditionResult> CheckPreconditionsAsync(SystemContext context, CancellationToken ct = default)
    {
        return Task.FromResult(PreconditionResult.Ok("Sin precondiciones específicas de esta optimización."));
    }

    /// <summary>VERIFY phase: re-detects live state and compares with intent.</summary>
    Task<VerificationResult> VerifyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        var observed = Detect(context.Registry);
        return Task.FromResult(observed switch
        {
            OptimizationState.AppliedByCao =>
                VerificationResult.Passed(observed, "El estado aplicado se ha verificado en el sistema."),
            OptimizationState.PendingReboot =>
                VerificationResult.Passed(observed, "El cambio requiere reinicio para completarse."),
            OptimizationState.Unknown =>
                VerificationResult.Passed(observed, "El cambio no es verificable por lectura directa; se acepta."),
            _ => VerificationResult.Failed(observed, "El sistema no refleja el estado esperado tras aplicar."),
        });
    }

    /// <summary>Benchmark hook; null means this change has no measurable path.</summary>
    Task<BenchmarkResult?> BenchmarkAsync(CancellationToken ct = default) =>
        Task.FromResult<BenchmarkResult?>(null);

    /// <summary>ROLLBACK phase: revert against a freshly captured fallback snapshot.</summary>
    async Task<RollbackResult> RollbackAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default)
    {
        try
        {
            var result = await RevertAsync(context, snapshot, ct);
            return result.Success
                ? RollbackResult.Ok(result.MessageEs)
                : RollbackResult.Fail(result.MessageEs, result.Error);
        }
        catch (Exception ex)
        {
            return RollbackResult.Fail("La reversión falló.", ex.Message);
        }
    }
}

/// <summary>Runtime services handed to optimizations.</summary>
public sealed class OptimizationContext
{
    public required IRegistryAccessor Registry { get; init; }

    /// <summary>Runs external tools (powercfg/netsh/bcdedit/Optimize-Volume). Null in unit tests.</summary>
    public IProcessRunner? Process { get; init; }

    /// <summary>Windows services manager. Null in unit tests that don't touch services.</summary>
    public IServiceManager? Services { get; init; }
}

/// <summary>Elevated process execution abstraction (powercfg, netsh...).</summary>
public interface IProcessRunner
{
    Task<(int ExitCode, string Output)> RunAsync(string fileName, string arguments, CancellationToken ct = default);
}

/// <summary>Windows service control abstraction.</summary>
public interface IServiceManager
{
    string? GetStartType(string serviceName);

    void SetStartType(string serviceName, string startType);

    Task StopAsync(string serviceName, CancellationToken ct = default);

    Task StartAsync(string serviceName, CancellationToken ct = default);

    bool Exists(string serviceName);
}
