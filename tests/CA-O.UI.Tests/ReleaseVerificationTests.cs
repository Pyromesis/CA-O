using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using CAO.Shared.Updates;
using Xunit;

namespace CAO.UI.Tests;

/// <summary>
/// Cadena de confianza del updater: manifiesto firmado, ancla clavada y
/// verificacion fail-closed del ZIP. Cada test fija una forma de ataque que el
/// SHA-256 a secas no detectaba.
/// </summary>
public sealed class ReleaseVerificationTests
{
    private static readonly JsonSerializerOptions ManifestJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Fact]
    public void Manifest_ParsesAndNormalizesVersion()
    {
        var manifestBytes = CreateManifestBytes(new byte[] { 1, 2, 3, 4 }, "2.2.22");

        Assert.True(ReleaseManifest.TryParse(manifestBytes, out var manifest, out var code), code);
        Assert.NotNull(manifest);
        Assert.Equal("2.2.22.0", manifest.Version);
        Assert.Equal("CA-O-2.2.22-win-x64.zip", manifest.Asset);
        Assert.True(manifest.TryValidateExpected("CA-O-2.2.22-win-x64.zip", "v2.2.22", out var expectedCode), expectedCode);
        Assert.Equal("ok", expectedCode);
    }

    [Fact]
    public void Manifest_RejectsUnknownFields()
    {
        var valid = CreateManifestBytes(new byte[] { 1, 2, 3, 4 }, "2.2.22");
        var json = Encoding.UTF8.GetString(valid);
        var tampered = json[..^1] + ",\"extra\":1}";

        Assert.False(ReleaseManifest.TryParse(Encoding.UTF8.GetBytes(tampered), out _, out var code));
        Assert.Equal("manifest-invalid", code);
    }

    [Fact]
    public void Manifest_RejectsDuplicateFields()
    {
        // Dos "sha256": un parser y otro podrian leer hashes distintos. Se rechaza.
        var hash = new string('a', 64);
        var json = "{\"schema\":1,\"version\":\"2.2.22\",\"asset\":\"CA-O-2.2.22-win-x64.zip\"," +
                   $"\"size\":4,\"sha256\":\"{hash}\",\"sha256\":\"{hash}\"}}";

        Assert.False(ReleaseManifest.TryParse(Encoding.UTF8.GetBytes(json), out _, out var code));
        Assert.Equal("manifest-invalid", code);
    }

    [Fact]
    public void Manifest_RejectsAssetThatDoesNotMatchItsVersion()
    {
        var manifest = new ReleaseManifest
        {
            Schema = ReleaseManifest.SupportedSchema,
            Version = "2.2.22",
            Asset = "CA-O-2.2.23-win-x64.zip",
            Size = 4,
            Sha256 = new string('a', 64),
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(manifest, ManifestJson);

        Assert.False(ReleaseManifest.TryParse(bytes, out _, out _));
    }

    [Fact]
    public void SignedManifest_VerifiesWithTheMatchingKey()
    {
        using var key = RSA.Create(3072);
        using var certificate = CreateCertificate(key);
        var manifestBytes = CreateManifestBytes(new byte[] { 1, 2, 3, 4 }, "2.2.22");
        var signature = key.SignData(manifestBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        Assert.True(ReleaseSignatureVerifier.VerifyDetached(manifestBytes, signature, certificate));
    }

    [Fact]
    public void SignedManifest_RejectsTamperedBytes()
    {
        using var key = RSA.Create(3072);
        using var certificate = CreateCertificate(key);
        var manifestBytes = CreateManifestBytes(new byte[] { 1, 2, 3, 4 }, "2.2.22");
        var signature = key.SignData(manifestBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        manifestBytes[^2] ^= 0x01;

        Assert.False(ReleaseSignatureVerifier.VerifyDetached(manifestBytes, signature, certificate));
    }

    [Fact]
    public void SignedManifest_RejectsADifferentKey()
    {
        using var trustedKey = RSA.Create(3072);
        using var trustedCertificate = CreateCertificate(trustedKey);
        using var attackerKey = RSA.Create(3072);
        var manifestBytes = CreateManifestBytes(new byte[] { 1, 2, 3, 4 }, "2.2.22");
        var attackerSignature = attackerKey.SignData(manifestBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        Assert.False(ReleaseSignatureVerifier.VerifyDetached(manifestBytes, attackerSignature, trustedCertificate));
    }

    [Fact]
    public async Task VerifyDownload_AcceptsAZipThatMatchesTheSignedManifest()
    {
        var dir = CreateTempDir();
        try
        {
            var package = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
            var packagePath = Path.Combine(dir, "CA-O-2.2.22-win-x64.zip");
            await File.WriteAllBytesAsync(packagePath, package);

            using var key = RSA.Create(3072);
            using var certificate = CreateCertificate(key);
            using var http = CreateHttpForSignedFixture(package, "2.2.22", key);

            var result = await ReleaseUpdateVerifier.VerifyDownloadAsync(
                packagePath,
                "https://example.test/CA-O-2.2.22-win-x64.zip",
                expectedVersion: "v2.2.22",
                expectedBytes: package.Length,
                http: http,
                trustAnchors: [certificate]);

            Assert.True(result.IsValid, $"{result.Code}: {result.Detail}");
            Assert.Equal("ok", result.Code);
        }
        finally
        {
            TryDeleteDirectory(dir);
        }
    }

    [Fact]
    public async Task VerifyDownload_RejectsATamperedZipEvenWithAValidManifest()
    {
        var dir = CreateTempDir();
        try
        {
            var package = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
            var packagePath = Path.Combine(dir, "CA-O-2.2.22-win-x64.zip");
            await File.WriteAllBytesAsync(packagePath, package);

            using var key = RSA.Create(3072);
            using var certificate = CreateCertificate(key);
            using var http = CreateHttpForSignedFixture(package, "2.2.22", key);

            // Mismo tamano, contenido distinto: solo el hash lo detecta.
            package[0] ^= 0xFF;
            await File.WriteAllBytesAsync(packagePath, package);

            var result = await ReleaseUpdateVerifier.VerifyDownloadAsync(
                packagePath,
                "https://example.test/CA-O-2.2.22-win-x64.zip",
                http: http,
                trustAnchors: [certificate]);

            Assert.False(result.IsValid);
            Assert.Equal("hash-mismatch", result.Code);
        }
        finally
        {
            TryDeleteDirectory(dir);
        }
    }

    [Fact]
    public async Task VerifyDownload_RejectsMissingSignature()
    {
        var dir = CreateTempDir();
        try
        {
            var package = new byte[] { 1, 2, 3, 4 };
            var packagePath = Path.Combine(dir, "CA-O-2.2.22-win-x64.zip");
            await File.WriteAllBytesAsync(packagePath, package);

            using var key = RSA.Create(3072);
            using var certificate = CreateCertificate(key);
            var manifestBytes = CreateManifestBytes(package, "2.2.22");
            var handler = new StubHandler()
                .Add("https://example.test/CA-O-2.2.22-win-x64.zip.manifest.json", manifestBytes);
            using var http = new HttpClient(handler);

            var result = await ReleaseUpdateVerifier.VerifyDownloadAsync(
                packagePath,
                "https://example.test/CA-O-2.2.22-win-x64.zip",
                http: http,
                trustAnchors: [certificate]);

            Assert.False(result.IsValid);
            Assert.Equal("signature-unavailable", result.Code);
        }
        finally
        {
            TryDeleteDirectory(dir);
        }
    }

    [Fact]
    public async Task VerifyDownload_RejectsASignatureFromAnotherKey()
    {
        var dir = CreateTempDir();
        try
        {
            var package = new byte[] { 1, 2, 3, 4 };
            var packagePath = Path.Combine(dir, "CA-O-2.2.22-win-x64.zip");
            await File.WriteAllBytesAsync(packagePath, package);

            using var trustedKey = RSA.Create(3072);
            using var trustedCertificate = CreateCertificate(trustedKey);
            using var attackerKey = RSA.Create(3072);
            using var http = CreateHttpForSignedFixture(package, "2.2.22", attackerKey);

            var result = await ReleaseUpdateVerifier.VerifyDownloadAsync(
                packagePath,
                "https://example.test/CA-O-2.2.22-win-x64.zip",
                http: http,
                trustAnchors: [trustedCertificate]);

            Assert.False(result.IsValid);
            Assert.Equal("signature-invalid", result.Code);
        }
        finally
        {
            TryDeleteDirectory(dir);
        }
    }

    [Fact]
    public async Task VerifyDownload_RejectsAnotherAssetWithAValidSignature()
    {
        var dir = CreateTempDir();
        try
        {
            var package = new byte[] { 1, 2, 3, 4 };
            var packagePath = Path.Combine(dir, "CA-O-2.2.23-win-x64.zip");
            await File.WriteAllBytesAsync(packagePath, package);

            using var key = RSA.Create(3072);
            using var certificate = CreateCertificate(key);
            using var http = CreateHttpForSignedFixture(package, "2.2.22", key, requestVersion: "2.2.23");

            var result = await ReleaseUpdateVerifier.VerifyDownloadAsync(
                packagePath,
                "https://example.test/CA-O-2.2.23-win-x64.zip",
                http: http,
                trustAnchors: [certificate]);

            Assert.False(result.IsValid);
            Assert.Equal("asset-mismatch", result.Code);
        }
        finally
        {
            TryDeleteDirectory(dir);
        }
    }

    [Fact]
    public async Task VerifyDownload_RejectsAnotherVersionThanTheOneRequested()
    {
        var dir = CreateTempDir();
        try
        {
            var package = new byte[] { 1, 2, 3, 4 };
            var packagePath = Path.Combine(dir, "CA-O-2.2.22-win-x64.zip");
            await File.WriteAllBytesAsync(packagePath, package);

            using var key = RSA.Create(3072);
            using var certificate = CreateCertificate(key);
            using var http = CreateHttpForSignedFixture(package, "2.2.22", key);

            var result = await ReleaseUpdateVerifier.VerifyDownloadAsync(
                packagePath,
                "https://example.test/CA-O-2.2.22-win-x64.zip",
                expectedVersion: "v2.2.23",
                http: http,
                trustAnchors: [certificate]);

            Assert.False(result.IsValid);
            Assert.Equal("version-mismatch", result.Code);
        }
        finally
        {
            TryDeleteDirectory(dir);
        }
    }

    [Fact]
    public async Task VerifyDownload_RejectsNonHttpsUrls()
    {
        var dir = CreateTempDir();
        try
        {
            var packagePath = Path.Combine(dir, "CA-O-2.2.22-win-x64.zip");
            await File.WriteAllBytesAsync(packagePath, new byte[] { 1 });

            var result = await ReleaseUpdateVerifier.VerifyDownloadAsync(
                packagePath,
                "http://example.test/CA-O-2.2.22-win-x64.zip");

            Assert.False(result.IsValid);
            Assert.Equal("url-insecure", result.Code);
        }
        finally
        {
            TryDeleteDirectory(dir);
        }
    }

    [Fact]
    public void SignedManifest_VerifiesWithTheRecoveryAnchor()
    {
        using var primaryKey = RSA.Create(3072);
        using var primaryCertificate = CreateCertificate(primaryKey);
        using var recoveryKey = RSA.Create(3072);
        using var recoveryCertificate = CreateCertificate(recoveryKey);
        var manifestBytes = CreateManifestBytes(new byte[] { 1, 2, 3, 4 }, "2.2.22");
        var signature = recoveryKey.SignData(manifestBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        Assert.True(ReleaseSignatureVerifier.VerifyDetachedAny(
            manifestBytes, signature, [primaryCertificate, recoveryCertificate], out var matched));
        Assert.Equal(recoveryCertificate.Thumbprint, matched);
    }

    [Fact]
    public async Task VerifyDownload_AcceptsASignatureFromTheRecoveryAnchor()
    {
        var dir = CreateTempDir();
        try
        {
            var package = new byte[] { 1, 2, 3, 4 };
            var packagePath = Path.Combine(dir, "CA-O-2.2.22-win-x64.zip");
            await File.WriteAllBytesAsync(packagePath, package);

            using var primaryKey = RSA.Create(3072);
            using var primaryCertificate = CreateCertificate(primaryKey);
            using var recoveryKey = RSA.Create(3072);
            using var recoveryCertificate = CreateCertificate(recoveryKey);
            using var http = CreateHttpForSignedFixture(package, "2.2.22", recoveryKey);

            var result = await ReleaseUpdateVerifier.VerifyDownloadAsync(
                packagePath,
                "https://example.test/CA-O-2.2.22-win-x64.zip",
                http: http,
                trustAnchors: [primaryCertificate, recoveryCertificate]);

            Assert.True(result.IsValid, $"{result.Code}: {result.Detail}");
            Assert.Equal(recoveryCertificate.Thumbprint, result.KeyThumbprint);
        }
        finally
        {
            TryDeleteDirectory(dir);
        }
    }

    [Fact]
    public void EmbeddedTrustAnchors_AreTwoDistinctRsa3072Certificates()
    {
        // Si alguien deja el placeholder, pega un certificado de 1024 bits o
        // duplica la misma clave como "recuperacion", el updater quedaria sin
        // una segunda raiz real. Esto lo detecta en CI.
        using var primary = ReleaseTrust.LoadSigningCertificate();
        using var recovery = ReleaseTrust.LoadRecoveryCertificate();
        using var primaryRsa = primary.GetRSAPublicKey();
        using var recoveryRsa = recovery.GetRSAPublicKey();

        Assert.NotNull(primaryRsa);
        Assert.NotNull(recoveryRsa);
        Assert.True(primaryRsa.KeySize >= ReleaseSignatureVerifier.MinimumKeySizeBits);
        Assert.True(recoveryRsa.KeySize >= ReleaseSignatureVerifier.MinimumKeySizeBits);
        Assert.Contains("CA-O Release Signing", primary.Subject, StringComparison.Ordinal);
        Assert.Contains("CA-O Release Recovery", recovery.Subject, StringComparison.Ordinal);
        Assert.NotEqual(primary.Thumbprint, recovery.Thumbprint);
        Assert.Equal(2, ReleaseTrust.Anchors.Count);
        Assert.False(string.IsNullOrWhiteSpace(ReleaseTrust.GetSigningCertificateSha256()));
        Assert.False(string.IsNullOrWhiteSpace(ReleaseTrust.GetRecoveryCertificateSha256()));
    }

    private static HttpClient CreateHttpForSignedFixture(
        byte[] package,
        string version,
        RSA signingKey,
        string? requestVersion = null)
    {
        var manifestBytes = CreateManifestBytes(package, version);
        var signature = signingKey.SignData(manifestBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var baseUrl = $"https://example.test/CA-O-{requestVersion ?? version}-win-x64.zip";

        var handler = new StubHandler()
            .Add(baseUrl + ReleaseManifest.FileSuffix, manifestBytes)
            .Add(baseUrl + ReleaseManifest.SignatureSuffix, Encoding.UTF8.GetBytes(Convert.ToBase64String(signature)));
        return new HttpClient(handler);
    }

    private static byte[] CreateManifestBytes(byte[] package, string version)
    {
        var manifest = new ReleaseManifest
        {
            Schema = ReleaseManifest.SupportedSchema,
            Version = version,
            Asset = $"CA-O-{version}-win-x64.zip",
            Size = package.Length,
            Sha256 = Convert.ToHexString(SHA256.HashData(package)).ToLowerInvariant(),
        };
        return JsonSerializer.SerializeToUtf8Bytes(manifest, ManifestJson);
    }

    private static X509Certificate2 CreateCertificate(RSA key)
    {
        var request = new CertificateRequest(
            "CN=CA-O Test Release, O=CA-O",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddYears(1));
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cao-verify-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void TryDeleteDirectory(string dir)
    {
        try { Directory.Delete(dir, recursive: true); } catch { }
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, byte[]> _responses = new(StringComparer.Ordinal);

        public StubHandler Add(string url, byte[] body)
        {
            _responses[url] = body;
            return this;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.AbsoluteUri;
            var response = _responses.TryGetValue(url, out var body)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) }
                : new HttpResponseMessage(HttpStatusCode.NotFound);
            return Task.FromResult(response);
        }
    }
}
