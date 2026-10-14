namespace CAO.Core.GameNetwork;

/// <summary>Calidad de conexión para jugar, derivada solo de lo medido.</summary>
public enum ConnectionGrade
{
    Excellent,
    Good,
    Fair,
    Poor,
    Unreachable,
}

/// <summary>
/// Resumen de una serie de muestras de RTT (null = paquete perdido):
/// mediana, p95, jitter (media de diferencias entre muestras consecutivas,
/// como el jitter que muestran los juegos) y pérdida.
/// </summary>
public sealed record LatencyStats(
    int Sent,
    int Received,
    double? MedianMs,
    double? P95Ms,
    double? MinMs,
    double? JitterMs,
    double LossPercent)
{
    public static LatencyStats Empty { get; } = new(0, 0, null, null, null, null, 0);

    public static LatencyStats From(IReadOnlyList<double?> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (samples.Count == 0) return Empty;

        var received = samples.Where(s => s.HasValue).Select(s => s!.Value).ToList();
        var loss = Math.Round((samples.Count - received.Count) * 100.0 / samples.Count, 1);
        if (received.Count == 0)
        {
            return new LatencyStats(samples.Count, 0, null, null, null, null, loss);
        }

        double? jitter = null;
        if (received.Count >= 2)
        {
            double sum = 0;
            for (var i = 1; i < received.Count; i++) sum += Math.Abs(received[i] - received[i - 1]);
            jitter = Math.Round(sum / (received.Count - 1), 1);
        }

        var sorted = received.OrderBy(v => v).ToList();
        return new LatencyStats(
            samples.Count,
            received.Count,
            Math.Round(Percentile(sorted, 0.5), 1),
            Math.Round(Percentile(sorted, 0.95), 1),
            Math.Round(sorted[0], 1),
            jitter,
            loss);
    }

    /// <summary>Percentil con interpolación lineal sobre una lista ya ordenada.</summary>
    internal static double Percentile(IReadOnlyList<double> sorted, double p)
    {
        if (sorted.Count == 1) return sorted[0];
        var rank = p * (sorted.Count - 1);
        var low = (int)Math.Floor(rank);
        var high = (int)Math.Ceiling(rank);
        return sorted[low] + (sorted[high] - sorted[low]) * (rank - low);
    }

    /// <summary>
    /// Umbrales para juego online: por encima de ~150 ms, 30 ms de jitter o
    /// 5 de cada 100 paquetes perdidos la partida se nota mal en cualquier género.
    /// </summary>
    public ConnectionGrade Grade
    {
        get
        {
            if (Received == 0 || MedianMs is null) return ConnectionGrade.Unreachable;
            var jitter = JitterMs ?? 0;
            if (LossPercent >= 5 || MedianMs >= 150 || jitter >= 30) return ConnectionGrade.Poor;
            if (LossPercent >= 1 || MedianMs >= 80 || jitter >= 15) return ConnectionGrade.Fair;
            if (MedianMs >= 40 || jitter >= 5) return ConnectionGrade.Good;
            return ConnectionGrade.Excellent;
        }
    }

    public static string GradeLabelEs(ConnectionGrade grade) => grade switch
    {
        ConnectionGrade.Excellent => "Excelente",
        ConnectionGrade.Good => "Buena",
        ConnectionGrade.Fair => "Aceptable",
        ConnectionGrade.Poor => "Mala",
        _ => "Sin respuesta",
    };
}
