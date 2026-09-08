namespace CAO.Shared.Networking;

/// <summary>
/// Pares DNS del mismo proveedor. Regla: nunca mezclar proveedores entre
/// primario y secundario (p. ej. nunca 2000.21.200.10 + 1.1.1.1).
/// Windows prioriza el primario y no siempre conmuta al secundario si el
/// primario responde pero no resuelve; mezclar un ISP lento con un público
/// rápido degrada latencia y estabilidad.
/// </summary>
public static class DnsResolverPairs
{
    /// <summary>Compañero canónico por IP conocida (ambas direcciones).</summary>
    private static readonly Dictionary<string, string> CompanionByIp = new(StringComparer.Ordinal)
    {
        // Cloudflare (recomendado)
        ["1.1.1.1"] = "1.0.0.1",
        ["1.0.0.1"] = "1.1.1.1",
        // Google (alternativa)
        ["8.8.8.8"] = "8.8.4.4",
        ["8.8.4.4"] = "8.8.8.8",
        // Quad9
        ["9.9.9.9"] = "149.112.112.112",
        ["149.112.112.112"] = "9.9.9.9",
        // OpenDNS
        ["208.67.222.222"] = "208.67.220.220",
        ["208.67.220.220"] = "208.67.222.222",
        // AdGuard
        ["94.140.14.14"] = "94.140.15.15",
        ["94.140.15.15"] = "94.140.14.14",
    };

    /// <summary>Nombre del proveedor para mensajes de UI.</summary>
    private static readonly Dictionary<string, string> ProviderByIp = new(StringComparer.Ordinal)
    {
        ["1.1.1.1"] = "Cloudflare",
        ["1.0.0.1"] = "Cloudflare",
        ["8.8.8.8"] = "Google",
        ["8.8.4.4"] = "Google",
        ["9.9.9.9"] = "Quad9",
        ["149.112.112.112"] = "Quad9",
        ["208.67.222.222"] = "OpenDNS",
        ["208.67.220.220"] = "OpenDNS",
        ["94.140.14.14"] = "AdGuard",
        ["94.140.15.15"] = "AdGuard",
    };

    public static bool IsKnownPublic(string ip) => CompanionByIp.ContainsKey(ip.Trim());

    public static string? GetCompanion(string primary)
    {
        var key = primary.Trim();
        return CompanionByIp.TryGetValue(key, out var companion) ? companion : null;
    }

    public static string GetProviderName(string ip)
    {
        var key = ip.Trim();
        if (ProviderByIp.TryGetValue(key, out var name)) return name;
        return "ISP/red local";
    }

    /// <summary>
    /// ¿Pertenecen ambas IP al mismo proveedor? Dos IP desconocidas (p. ej.
    /// 2000.21.200.10 y 2000.21.200.9 del mismo ISP) se consideran mismo
    /// proveedor; una conocida + una distinta nunca lo son.
    /// </summary>
    public static bool IsSameProvider(string a, string b)
    {
        a = a.Trim();
        b = b.Trim();
        if (a.Equals(b, StringComparison.Ordinal)) return true;
        if (CompanionByIp.TryGetValue(a, out var companion))
            return companion.Equals(b, StringComparison.Ordinal);
        if (CompanionByIp.TryGetValue(b, out _))
            return false; // b es pública conocida pero a no es su pareja
        return true; // ambas personalizadas/ISP: se asume misma red, no mezcla pública
    }

    /// <summary>
    /// Resuelve el par consistente para un primario dado.
    /// Conocido → su compañero canónico. Desconocido (ISP) → busca un hermano
    /// en <paramref name="candidates"/> (otro DNS del sistema que no sea
    /// público conocido, preferido mismo /24); si no hay, secundario null
    /// (un solo DNS antes que mezclar).
    /// </summary>
    public static (string Primary, string? Secondary) ResolvePair(
        string primary,
        IEnumerable<string>? candidates = null)
    {
        primary = primary.Trim();
        var companion = GetCompanion(primary);
        if (companion != null) return (primary, companion);

        // ISP / personalizado: buscar hermano en la misma red.
        if (candidates != null)
        {
            string? sameSlash24 = null;
            string? anyOtherCustom = null;
            foreach (var c in candidates)
            {
                var t = c.Trim();
                if (t.Equals(primary, StringComparison.Ordinal)) continue;
                if (IsKnownPublic(t)) continue; // nunca mezclar ISP + público
                anyOtherCustom ??= t;
                if (SameSlash24(primary, t)) { sameSlash24 = t; break; }
            }
            var sibling = sameSlash24 ?? anyOtherCustom;
            if (sibling != null) return (primary, sibling);
        }
        return (primary, null);
    }

    /// <summary>
    /// Normaliza un par (primario, secundario) a mismo proveedor. Si ya es
    /// consistente se devuelve tal cual; si está mezclado se corrige el
    /// secundario al compañero canónico (o al hermano ISP / null).
    /// Devuelve (primary, secondaryCorregido, fueCorregido).
    /// </summary>
    public static (string Primary, string? Secondary, bool Corrected) NormalizePair(
        string primary,
        string? secondary,
        IEnumerable<string>? candidates = null)
    {
        primary = primary.Trim();
        secondary = string.IsNullOrWhiteSpace(secondary) ? null : secondary.Trim();
        if (secondary == null) return (primary, ResolvePair(primary, candidates).Secondary, false);
        if (IsSameProvider(primary, secondary)) return (primary, secondary, false);
        var (_, fixedSecondary) = ResolvePair(primary, candidates);
        return (primary, fixedSecondary, true);
    }

    private static bool SameSlash24(string a, string b)
    {
        var pa = a.Split('.');
        var pb = b.Split('.');
        if (pa.Length != 4 || pb.Length != 4) return false;
        return pa[0] == pb[0] && pa[1] == pb[1] && pa[2] == pb[2];
    }
}
