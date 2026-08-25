using CAO.Shared;

namespace CAO.Core.Engine;

public static class BenchmarkAnalyzer
{
    public static FrameTimeStatistics AnalyzeFrameTimes(IEnumerable<double> frameTimesMs)
    {
        var samples = frameTimesMs
            .Where(sample => double.IsFinite(sample) && sample > 0)
            .OrderBy(sample => sample)
            .ToArray();

        if (samples.Length == 0)
        {
            return new FrameTimeStatistics(0, 0, 0, 0, 0, 0, 0, 0);
        }

        var averageFrameTime = samples.Average();
        var variance = samples.Average(sample => Math.Pow(sample - averageFrameTime, 2));
        var onePercentLow = Percentile(samples, 99);
        var zeroPointOnePercentLow = Percentile(samples, 99.9);

        return new FrameTimeStatistics(
            samples.Length,
            1000d / averageFrameTime,
            1000d / onePercentLow,
            1000d / zeroPointOnePercentLow,
            Percentile(samples, 50),
            Percentile(samples, 95),
            Percentile(samples, 99),
            variance);
    }

    public static BenchmarkComparison Compare(
        FrameTimeStatistics baseline,
        FrameTimeStatistics candidate,
        double significanceThresholdPercent = 1.0)
    {
        if (baseline.SampleCount == 0 || candidate.SampleCount == 0)
        {
            return new(BenchmarkVerdict.InsufficientData, 0, 0, 0, "Faltan muestras válidas para comparar.");
        }

        var averageFpsChange = PercentChange(baseline.AverageFps, candidate.AverageFps);
        var onePercentLowChange = PercentChange(baseline.OnePercentLowFps, candidate.OnePercentLowFps);
        var p99FrameTimeChange = PercentChange(baseline.P99FrameTimeMs, candidate.P99FrameTimeMs);

        if (averageFpsChange >= significanceThresholdPercent &&
            onePercentLowChange >= significanceThresholdPercent &&
            p99FrameTimeChange <= -significanceThresholdPercent)
        {
            return new(BenchmarkVerdict.Improvement, averageFpsChange, onePercentLowChange, p99FrameTimeChange, "La mejora supera el umbral en rendimiento y frame-time.");
        }

        if (averageFpsChange <= -significanceThresholdPercent ||
            onePercentLowChange <= -significanceThresholdPercent ||
            p99FrameTimeChange >= significanceThresholdPercent)
        {
            return new(BenchmarkVerdict.Regression, averageFpsChange, onePercentLowChange, p99FrameTimeChange, "El resultado muestra una regresión medible.");
        }

        return new(BenchmarkVerdict.NoMeasurableImprovement, averageFpsChange, onePercentLowChange, p99FrameTimeChange, "No hay una mejora medible consistente.");
    }

    private static double Percentile(double[] sortedSamples, double percentile)
    {
        var position = (sortedSamples.Length - 1) * percentile / 100d;
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        if (lower == upper) return sortedSamples[lower];
        var fraction = position - lower;
        return sortedSamples[lower] + (sortedSamples[upper] - sortedSamples[lower]) * fraction;
    }

    private static double PercentChange(double baseline, double candidate) =>
        baseline == 0 ? 0 : (candidate - baseline) / baseline * 100d;
}