using System.Text.Json;

namespace CAO.Shared.Updates;

/// <summary>
/// Manifiesto firmado de un release. Es la pieza que convierte el SHA-256 en
/// algo verificable: el hash no viaja en un sidecar que cualquiera con acceso
/// al release puede reescribir, sino dentro de un documento firmado con la
/// clave privada del mantenedor.
///
/// Los bytes exactos del fichero son los que se firman; el verificador no
/// re-serializa el JSON (asi no hay que canonicalizar nada).
/// </summary>
public sealed record ReleaseManifest
{
    public const int SupportedSchema = 1;
    public const string FileSuffix = ".manifest.json";
    public const string SignatureSuffix = ".manifest.sig";
    public const int MaxManifestBytes = 16 * 1024;
    public const int MaxSignatureBytes = 16 * 1024;
    public const long MaxPackageBytes = 4L * 1024 * 1024 * 1024;

    /// <summary>Version del formato del manifiesto (no del producto).</summary>
    public int Schema { get; init; }

    /// <summary>Version del producto, normalizada a 4 componentes.</summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>Nombre exacto del ZIP firmado (sin ruta).</summary>
    public string Asset { get; init; } = string.Empty;

    /// <summary>Tamano exacto del ZIP en bytes.</summary>
    public long Size { get; init; }

    /// <summary>SHA-256 del ZIP en hexadecimal minusculas.</summary>
    public string Sha256 { get; init; } = string.Empty;

    /// <summary>
    /// Parsea y valida el manifiesto. Rechaza campos desconocidos o duplicados:
    /// un documento con dos "sha256" es ambiguo aunque la firma sea valida.
    /// </summary>
    public static bool TryParse(
        ReadOnlyMemory<byte> utf8Json,
        out ReleaseManifest? manifest,
        out string failureCode)
    {
        manifest = null;
        failureCode = "manifest-invalid";

        if (utf8Json.IsEmpty || utf8Json.Length > MaxManifestBytes)
        {
            return false;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(utf8Json);
        }
        catch (JsonException)
        {
            return false;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            int? schema = null;
            string? version = null;
            string? asset = null;
            long? size = null;
            string? sha256 = null;

            foreach (var property in root.EnumerateObject())
            {
                if (!seen.Add(property.Name))
                {
                    return false;
                }

                switch (property.Name)
                {
                    case "schema":
                        if (property.Value.ValueKind != JsonValueKind.Number ||
                            !property.Value.TryGetInt32(out var parsedSchema))
                        {
                            return false;
                        }

                        schema = parsedSchema;
                        break;

                    case "version":
                        if (property.Value.ValueKind != JsonValueKind.String)
                        {
                            return false;
                        }

                        version = property.Value.GetString();
                        break;

                    case "asset":
                        if (property.Value.ValueKind != JsonValueKind.String)
                        {
                            return false;
                        }

                        asset = property.Value.GetString();
                        break;

                    case "size":
                        if (property.Value.ValueKind != JsonValueKind.Number ||
                            !property.Value.TryGetInt64(out var parsedSize))
                        {
                            return false;
                        }

                        size = parsedSize;
                        break;

                    case "sha256":
                        if (property.Value.ValueKind != JsonValueKind.String)
                        {
                            return false;
                        }

                        sha256 = property.Value.GetString();
                        break;

                    default:
                        return false;
                }
            }

            if (seen.Count != 5 || schema is null || version is null || asset is null || size is null || sha256 is null)
            {
                return false;
            }

            if (schema.Value != SupportedSchema)
            {
                return false;
            }

            if (!ReleaseVersion.TryNormalize(version, out var normalizedVersion))
            {
                return false;
            }

            if (asset.Length > 200 ||
                asset.IndexOfAny(['/', '\\']) >= 0 ||
                !TryParseAsset(asset, out var assetVersion) ||
                !string.Equals(assetVersion, normalizedVersion, StringComparison.Ordinal))
            {
                return false;
            }

            if (size.Value <= 0 || size.Value > MaxPackageBytes)
            {
                return false;
            }

            if (sha256.Length != 64 || !IsHex(sha256))
            {
                return false;
            }

            manifest = new ReleaseManifest
            {
                Schema = schema.Value,
                Version = normalizedVersion,
                Asset = asset,
                Size = size.Value,
                Sha256 = sha256.ToLowerInvariant(),
            };
            failureCode = "ok";
            return true;
        }
    }

    /// <summary>
    /// Comprueba que el manifiesto corresponde al asset y al release que se
    /// esta instalando. La firma dice "esto lo firmo el mantenedor"; esta
    /// comprobacion dice "y es el fichero que me pidieron descargar".
    /// </summary>
    public bool TryValidateExpected(string expectedAssetName, string? expectedVersion, out string failureCode)
    {
        if (!string.Equals(Asset, expectedAssetName, StringComparison.Ordinal))
        {
            failureCode = "asset-mismatch";
            return false;
        }

        if (expectedVersion is not null)
        {
            if (!ReleaseVersion.TryNormalize(expectedVersion, out var normalizedExpected) ||
                !string.Equals(Version, normalizedExpected, StringComparison.Ordinal))
            {
                failureCode = "version-mismatch";
                return false;
            }
        }

        failureCode = "ok";
        return true;
    }

    private static bool TryParseAsset(string asset, out string version)
    {
        version = string.Empty;
        const string prefix = "CA-O-";
        const string suffix = "-win-x64.zip";

        if (asset.Length <= prefix.Length + suffix.Length ||
            !asset.StartsWith(prefix, StringComparison.Ordinal) ||
            !asset.EndsWith(suffix, StringComparison.Ordinal))
        {
            return false;
        }

        var versionText = asset[prefix.Length..^suffix.Length];
        foreach (var c in versionText)
        {
            if (!char.IsAsciiDigit(c) && c != '.')
            {
                return false;
            }
        }

        return ReleaseVersion.TryNormalize(versionText, out version);
    }

    private static bool IsHex(string value)
    {
        foreach (var c in value)
        {
            if (!char.IsAsciiHexDigit(c))
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>Normalizacion de versiones para comparar tag, asset y manifiesto.</summary>
public static class ReleaseVersion
{
    /// <summary>
    /// "v2.2.22" y "2.2.22.0" producen la misma cadena; el sufijo de
    /// pre-release se ignora porque el paquete distribuido es la version final.
    /// </summary>
    public static bool TryNormalize(string? text, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var value = text.Trim();
        if (value.StartsWith('v') || value.StartsWith('V'))
        {
            value = value[1..];
        }

        var metadata = value.IndexOfAny(['-', '+']);
        if (metadata >= 0)
        {
            value = value[..metadata];
        }

        if (value.Length is 0 or > 32)
        {
            return false;
        }

        foreach (var c in value)
        {
            if (!char.IsAsciiDigit(c) && c != '.')
            {
                return false;
            }
        }

        if (!Version.TryParse(value, out var version) || version.Major < 1)
        {
            return false;
        }

        normalized = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}.{Math.Max(version.Revision, 0)}");
        return true;
    }
}
