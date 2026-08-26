namespace CAO.Core.Benchmark;

/// <summary>
/// Single source of truth for benchmark methodology (FASE 21/29): the noise
/// floor is 3% everywhere — docs, runner and analyzer read THIS value, so
/// the contradiction "docs 3% vs analyzer 1%" cannot reappear.
/// </summary>
public static class BenchmarkPolicy
{
    /// <summary>Deltas below this percent are declared NoMeasurableImprovement.</summary>
    public const double MinimumEffectPercent = 3.0;

    /// <summary>Measured trials per run (median reported).</summary>
    public const int MeasuredRuns = 3;

    /// <summary>Discarded warmup passes before measuring.</summary>
    public const int WarmupRuns = 1;
}
