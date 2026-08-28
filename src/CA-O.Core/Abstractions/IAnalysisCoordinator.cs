using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CAO.Shared;

namespace CAO.Core.Abstractions;

public sealed record ModuleResult(string Module, bool Success, TimeSpan Duration, string? Value, string? Warning, string? ErrorCode);

public sealed record AnalysisReport(
    string AnalysisState,
    IReadOnlyList<ModuleResult> Modules,
    SystemContext? Context,
    SystemDiagnosticReport? Health,
    IReadOnlyList<Recommendation> Recommendations,
    TimeSpan TotalDuration,
    IReadOnlyList<string> Warnings,
    string? CorrelationId
);

public interface IAnalysisCoordinator
{
    Task<AnalysisReport> RunAsync(CancellationToken ct = default);
}
