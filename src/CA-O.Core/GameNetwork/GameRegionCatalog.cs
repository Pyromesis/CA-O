namespace CAO.Core.GameNetwork;

/// <summary>Una región de nube donde se alojan servidores de juego, medible por TCP.</summary>
public sealed record GameRegion(string Id, string NameEs, string Area, string Host, int Port = 443);

/// <summary>
/// Regiones de AWS: Fortnite, PUBG y buena parte de los juegos online alojan
/// sus servidores ahí, así que la latencia a cada región es la latencia
/// realista a esos servidores. Se mide con el endpoint regional de DynamoDB
/// (siempre vivo, responde en 443, uno por región). No sustituye a medir el
/// servidor exacto del juego: para eso la página acepta una IP/host propia.
/// </summary>
public static class GameRegionCatalog
{
    private static GameRegion Aws(string code, string name, string area) =>
        new(code, name, area, $"dynamodb.{code}.amazonaws.com");

    public static IReadOnlyList<GameRegion> All { get; } =
    [
        Aws("us-east-1", "EE. UU. Este (Virginia)", "Norteamérica"),
        Aws("us-east-2", "EE. UU. Centro (Ohio)", "Norteamérica"),
        Aws("us-west-1", "EE. UU. Oeste (California)", "Norteamérica"),
        Aws("us-west-2", "EE. UU. Oeste (Oregón)", "Norteamérica"),
        Aws("ca-central-1", "Canadá (Montreal)", "Norteamérica"),
        Aws("mx-central-1", "México (Querétaro)", "Latinoamérica"),
        Aws("sa-east-1", "Brasil (São Paulo)", "Latinoamérica"),
        Aws("eu-south-2", "España (Aragón)", "Europa"),
        Aws("eu-west-1", "Irlanda", "Europa"),
        Aws("eu-west-2", "Reino Unido (Londres)", "Europa"),
        Aws("eu-west-3", "Francia (París)", "Europa"),
        Aws("eu-central-1", "Alemania (Fráncfort)", "Europa"),
        Aws("eu-north-1", "Suecia (Estocolmo)", "Europa"),
        Aws("me-central-1", "Emiratos (Dubái)", "Oriente Medio"),
        Aws("af-south-1", "Sudáfrica (Ciudad del Cabo)", "África"),
        Aws("ap-south-1", "India (Bombay)", "Asia-Pacífico"),
        Aws("ap-east-1", "Hong Kong", "Asia-Pacífico"),
        Aws("ap-northeast-1", "Japón (Tokio)", "Asia-Pacífico"),
        Aws("ap-northeast-2", "Corea (Seúl)", "Asia-Pacífico"),
        Aws("ap-southeast-1", "Singapur", "Asia-Pacífico"),
        Aws("ap-southeast-2", "Australia (Sídney)", "Asia-Pacífico"),
    ];

    public static GameRegion? Find(string? id) =>
        id is null ? null : All.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Valida un destino escrito por el usuario (IP o nombre de host, puerto
    /// opcional). Solo caracteres de host: nada que pueda acabar en un comando.
    /// </summary>
    public static bool TryParseCustomTarget(string? text, out string host, out int port)
    {
        host = string.Empty;
        port = 443;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var value = text.Trim();
        if (value.Length > 253) return false;

        var colon = value.LastIndexOf(':');
        if (colon > 0 && value.IndexOf(':') == colon)
        {
            if (!int.TryParse(value[(colon + 1)..], System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out port) || port is < 1 or > 65535)
            {
                return false;
            }
            value = value[..colon];
        }

        if (value.Length == 0) return false;
        if (System.Net.IPAddress.TryParse(value, out _))
        {
            host = value;
            return true;
        }

        foreach (var c in value)
        {
            if (!(char.IsAsciiLetterOrDigit(c) || c is '-' or '.')) return false;
        }
        if (value.StartsWith('.') || value.EndsWith('-') || value.Contains("..", StringComparison.Ordinal)) return false;
        host = value;
        return true;
    }
}
