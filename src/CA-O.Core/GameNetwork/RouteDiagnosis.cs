namespace CAO.Core.GameNetwork;

/// <summary>Un salto de la ruta: TTL, quién respondió y sus muestras.</summary>
public sealed record RouteHop(int Ttl, string? Address, LatencyStats Stats, bool IsDestination);

public enum RouteVerdictKind
{
    Healthy,
    LocalNetwork,
    PathLoss,
    LatencyJump,
    DestinationUnreachable,
    NoData,
}

public sealed record RouteVerdict(RouteVerdictKind Kind, int? HopTtl, string MessageEs);

/// <summary>
/// Dice DÓNDE se origina el lag a partir de un traceroute, con las dos
/// trampas clásicas resueltas: (1) un router intermedio que no contesta o
/// "pierde" pings pero deja pasar el tráfico NO es un problema (limitan el
/// ICMP hacia ellos mismos), así que la pérdida solo cuenta si continúa hasta
/// el destino; (2) la latencia de un salto intermedio solo importa si los
/// saltos siguientes la heredan.
/// </summary>
public static class RouteDiagnosis
{
    public const double LocalLatencyLimitMs = 20;
    public const double LocalJitterLimitMs = 10;
    public const double JumpLimitMs = 40;

    public static RouteVerdict Analyze(IReadOnlyList<RouteHop> hops)
    {
        ArgumentNullException.ThrowIfNull(hops);
        var responding = hops.Where(h => h.Stats.Received > 0).OrderBy(h => h.Ttl).ToList();
        if (responding.Count == 0)
        {
            return new RouteVerdict(RouteVerdictKind.NoData, null,
                "Ningún salto respondió. Puede que tu red o el destino bloqueen el traceroute.");
        }

        var destination = hops.FirstOrDefault(h => h.IsDestination);
        var first = hops.OrderBy(h => h.Ttl).First();

        // 1) Primer salto = tu router. Si ya va mal, el problema es local (Wi-Fi, cable, router saturado).
        if (first.Stats.Received > 0 &&
            ((first.Stats.MedianMs ?? 0) > LocalLatencyLimitMs ||
             (first.Stats.JitterMs ?? 0) > LocalJitterLimitMs ||
             (first.Stats.LossPercent > 0 && PersistsToEnd(hops, first.Ttl))))
        {
            return new RouteVerdict(RouteVerdictKind.LocalNetwork, first.Ttl,
                $"El problema empieza en tu red local (router {first.Address ?? "?"}: {Fmt(first.Stats.MedianMs)} de media, {Fmt(first.Stats.JitterMs)} de jitter). " +
                "Prueba con cable en vez de Wi-Fi, acércate al router o corta descargas en otros equipos.");
        }

        // 2) Pérdida que empieza en un salto y llega hasta el destino.
        foreach (var hop in responding)
        {
            if (hop.Stats.LossPercent >= 1 && PersistsToEnd(hops, hop.Ttl))
            {
                return new RouteVerdict(RouteVerdictKind.PathLoss, hop.Ttl,
                    $"Se pierden paquetes desde el salto {hop.Ttl} ({hop.Address ?? "?"}) y la pérdida llega hasta el destino. " +
                    (hop.Ttl <= 3
                        ? "Está muy cerca de ti: revisa router y cableado, o llama a tu proveedor."
                        : "Está en la red de tu proveedor o en un tránsito: es la ruta, no tu PC. Prueba otra región del juego o repórtalo a tu proveedor."));
            }
        }

        // Muchos servidores (y los endpoints de nube) no contestan al ping final:
        // entonces se razona con el último salto visible, avisándolo.
        var destinationResponds = destination is not null && destination.Stats.Received > 0;
        var end = destinationResponds ? destination! : responding[^1];
        var unreachableNote = destinationResponds
            ? string.Empty
            : "El destino no responde al traceroute (normal en servidores de juego); con los saltos visibles: ";

        // 3) Salto de latencia grande que el resto de la ruta hereda.
        RouteHop? worstFrom = null;
        double worstJump = 0;
        for (var i = 1; i < responding.Count; i++)
        {
            var previous = responding[i - 1].Stats.MedianMs ?? 0;
            var current = responding[i].Stats.MedianMs ?? 0;
            var jump = current - previous;
            var inherited = (end.Stats.MedianMs ?? 0) >= current - LocalLatencyLimitMs;
            if (jump > worstJump && inherited)
            {
                worstJump = jump;
                worstFrom = responding[i];
            }
        }

        if (worstFrom is not null && worstJump >= JumpLimitMs)
        {
            return new RouteVerdict(RouteVerdictKind.LatencyJump, worstFrom.Ttl,
                Join(unreachableNote,
                $"la mayor parte del ping (+{worstJump:0} ms) se suma en el salto {worstFrom.Ttl} ({worstFrom.Address ?? "?"}). " +
                "Suele ser un tramo largo (cable submarino o entre países) o un enlace saturado de tu proveedor. Una región más cercana lo evita."));
        }

        if (!destinationResponds)
        {
            return new RouteVerdict(RouteVerdictKind.DestinationUnreachable, null,
                unreachableNote + $"sin saltos anómalos hasta el salto {end.Ttl} ({Fmt(end.Stats.MedianMs)}). El ping real lo da el monitor en vivo.");
        }

        return new RouteVerdict(RouteVerdictKind.Healthy, null,
            $"Ruta sana: {Fmt(end.Stats.MedianMs)} hasta el destino, sin pérdida que llegue al final ni saltos anómalos.");
    }

    /// <summary>La pérdida vista en <paramref name="ttl"/> se mantiene en todos los saltos que responden después, destino incluido.</summary>
    private static bool PersistsToEnd(IReadOnlyList<RouteHop> hops, int ttl)
    {
        var after = hops.Where(h => h.Ttl > ttl && h.Stats.Received > 0).ToList();
        var destination = hops.FirstOrDefault(h => h.IsDestination);
        if (destination is null || destination.Stats.Received == 0) return false;
        return after.Count > 0 && after.All(h => h.Stats.LossPercent >= 1);
    }

    /// <summary>Une la nota previa con la frase; sin nota, la frase empieza en mayúscula.</summary>
    private static string Join(string note, string sentence) =>
        note.Length > 0 ? note + sentence : char.ToUpperInvariant(sentence[0]) + sentence[1..];

    private static string Fmt(double? ms) => ms is null ? "—" : $"{ms:0} ms";
}
