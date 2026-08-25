namespace CAO.Shared;

public enum DiagnosticSeverity
{
    Information,
    Warning,
    Critical,
}

public enum HealthDimension
{
    System,
    Gaming,
    Security,
    Network,
    Storage,
    Drivers,
    Thermals,
    Input,
    Stability,
    Startup,
}

public sealed record DiagnosticFinding(
    HealthDimension Dimension,
    DiagnosticSeverity Severity,
    string Code,
    string MessageEs);

public sealed record HealthScore(
    HealthDimension Dimension,
    int? Score,
    bool IsMeasured,
    string ReasonEs);

public sealed record SystemDiagnosticReport(
    IReadOnlyList<HealthScore> Scores,
    IReadOnlyList<DiagnosticFinding> Findings);