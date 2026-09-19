namespace CAO.Infrastructure.Benchmarking;

/// <summary>Ventana de captura de fluidez (B ligero v1): frame-times + DPC%. Null = no disponible en este equipo.</summary>
public sealed record FrameCaptureResult(IReadOnlyList<double> FrameTimesMs, double DpcPercent, double InterruptPercent);

public interface IFrameCapture
{
    Task<FrameCaptureResult?> CaptureAsync(TimeSpan duration, CancellationToken ct);
}
