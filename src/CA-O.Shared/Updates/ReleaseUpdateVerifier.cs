using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace CAO.Shared.Updates;

/// <summary>Resultado de verificar un paquete contra su manifiesto firmado.</summary>
public sealed record ReleaseVerificationResult(
    bool IsValid,
    string Code,
    string? Detail = null,
    string? KeyThumbprint = null)
{
    public static ReleaseVerificationResult Valid { get; } = new(true, "ok");

    public static ReleaseVerificationResult ValidWith(string keyThumbprint) =>
        new(true, "ok", null, keyThumbprint);

    public static ReleaseVerificationResult Fail(string code, string? detail = null) =>
        new(false, code, detail);
}

/// <summary>
/// Verificacion fail-closed de un update: descarga el manifiesto y su firma
/// desde el mismo release que el ZIP, valida la firma contra las anclas
/// clavadas (Primary o Recovery), comprueba que el manifiesto corresponde al
/// asset/version pedidos y solo entonces compara tamano y SHA-256 del fichero
/// descargado.
///
/// El orden importa: nada del manifiesto se usa como dato de confianza antes
/// de verificar la firma. Y si cualquier paso falla, no se instala.
/// </summary>
public static class ReleaseUpdateVerifier
{
    public static async Task<ReleaseVerificationResult> VerifyDownloadAsync(
        string packagePath,
        string packageUrl,
        string? expectedVersion = null,
        long? expectedBytes = null,
        HttpClient? http = null,
        IReadOnlyList<X509Certificate2>? trustAnchors = null,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(packageUrl, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return ReleaseVerificationResult.Fail("url-insecure");
        }

        var assetName = Path.GetFileName(uri.AbsolutePath);
        if (string.IsNullOrEmpty(assetName))
        {
            return ReleaseVerificationResult.Fail("url-insecure");
        }

        if (!File.Exists(packagePath))
        {
            return ReleaseVerificationResult.Fail("package-missing");
        }

        using var ownedHttp = http is null ? CreateHttpClient() : null;
        var client = http ?? ownedHttp!;

        var manifestBytes = await FetchAsync(
            client,
            packageUrl + ReleaseManifest.FileSuffix,
            ReleaseManifest.MaxManifestBytes,
            cancellationToken).ConfigureAwait(false);
        if (manifestBytes is null)
        {
            return ReleaseVerificationResult.Fail("manifest-unavailable");
        }

        var signatureBytes = await FetchAsync(
            client,
            packageUrl + ReleaseManifest.SignatureSuffix,
            ReleaseManifest.MaxSignatureBytes,
            cancellationToken).ConfigureAwait(false);
        if (signatureBytes is null)
        {
            return ReleaseVerificationResult.Fail("signature-unavailable");
        }

        string signatureText;
        try
        {
            signatureText = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetString(signatureBytes)
                .Trim();
        }
        catch (DecoderFallbackException)
        {
            return ReleaseVerificationResult.Fail("signature-invalid");
        }

        byte[] signature;
        try
        {
            signature = Convert.FromBase64String(signatureText);
        }
        catch (FormatException)
        {
            return ReleaseVerificationResult.Fail("signature-invalid");
        }

        if (trustAnchors is { Count: > 0 })
        {
            return await VerifyCoreAsync(
                packagePath, manifestBytes, signature, assetName, expectedVersion, expectedBytes,
                trustAnchors, cancellationToken).ConfigureAwait(false);
        }

        // Produccion: Primary o Recovery. Cualquiera de las dos es una raiz
        // valida; la Recovery existe para no quedarse sin poder firmar.
        var pinned = ReleaseTrust.LoadTrustAnchors();
        try
        {
            return await VerifyCoreAsync(
                packagePath, manifestBytes, signature, assetName, expectedVersion, expectedBytes,
                pinned, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            foreach (var certificate in pinned)
            {
                certificate.Dispose();
            }
        }
    }

    private static async Task<ReleaseVerificationResult> VerifyCoreAsync(
        string packagePath,
        byte[] manifestBytes,
        byte[] signature,
        string assetName,
        string? expectedVersion,
        long? expectedBytes,
        IReadOnlyList<X509Certificate2> trustAnchors,
        CancellationToken cancellationToken)
    {
        if (!ReleaseManifest.TryParse(manifestBytes, out var manifest, out var parseFailure))
        {
            return ReleaseVerificationResult.Fail(parseFailure);
        }

        if (!ReleaseSignatureVerifier.VerifyDetachedAny(
                manifestBytes, signature, trustAnchors, out var matchedThumbprint))
        {
            return ReleaseVerificationResult.Fail("signature-invalid");
        }

        if (!manifest!.TryValidateExpected(assetName, expectedVersion, out var expectedFailure))
        {
            return ReleaseVerificationResult.Fail(expectedFailure);
        }

        if (expectedBytes is > 0 && manifest.Size != expectedBytes.Value)
        {
            return ReleaseVerificationResult.Fail(
                "size-mismatch",
                $"release={expectedBytes.Value} manifest={manifest.Size}");
        }

        var info = new FileInfo(packagePath);
        if (!info.Exists)
        {
            return ReleaseVerificationResult.Fail("package-missing");
        }

        if (info.Length != manifest.Size)
        {
            return ReleaseVerificationResult.Fail(
                "size-mismatch",
                $"file={info.Length} manifest={manifest.Size}");
        }

        await using var stream = File.OpenRead(packagePath);
        var actualHash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        var actualHex = Convert.ToHexString(actualHash).ToLowerInvariant();
        if (!string.Equals(actualHex, manifest.Sha256, StringComparison.Ordinal))
        {
            return ReleaseVerificationResult.Fail("hash-mismatch");
        }

        return ReleaseVerificationResult.ValidWith(matchedThumbprint!);
    }

    private static async Task<byte[]?> FetchAsync(
        HttpClient http,
        string url,
        int maxBytes,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http
                .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                return null;
            }

            if (response.Content.Headers.ContentLength is { } announced && announced > maxBytes)
            {
                return null;
            }

            await using var network = await response.Content
                .ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            using var memory = new MemoryStream();
            var buffer = new byte[4096];
            int read;
            while ((read = await network.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                if (memory.Length + read > maxBytes)
                {
                    return null;
                }

                memory.Write(buffer, 0, read);
            }

            return memory.ToArray();
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("CA-O-ReleaseVerifier");
        return client;
    }
}
