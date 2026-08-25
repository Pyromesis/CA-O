using CAO.Shared;

namespace CAO.Core.Abstractions;

/// <summary>Result of one optimization lifecycle operation.</summary>
public sealed record OperationResult(bool Success, string MessageEs, string? Error = null)
{
    public static OperationResult Ok(string messageEs) => new(true, messageEs);

    public static OperationResult Fail(string messageEs, string? error = null) => new(false, messageEs, error);
}

/// <summary>
/// Contract implemented by every optimization in the catalog.
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
