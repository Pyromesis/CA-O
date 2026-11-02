using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace CAO.Shared.Updates;

/// <summary>
/// Verificacion de firmas RSA-3072 PKCS#1/SHA-256 sobre los bytes exactos del
/// manifiesto. No valida cadenas de certificados: el ancla esta clavada en
/// <see cref="ReleaseTrust"/>, que es mas fuerte que depender de una CA.
/// </summary>
public static class ReleaseSignatureVerifier
{
    /// <summary>Minimo aceptado: 3072 bits (el ancla actual usa 3072).</summary>
    public const int MinimumKeySizeBits = 3072;

    public static bool VerifyDetached(
        ReadOnlySpan<byte> data,
        ReadOnlySpan<byte> signature,
        X509Certificate2 certificate) =>
        VerifyDetached(data, signature, certificate, DateTimeOffset.UtcNow);

    public static bool VerifyDetached(
        ReadOnlySpan<byte> data,
        ReadOnlySpan<byte> signature,
        X509Certificate2 certificate,
        DateTimeOffset now)
    {
        using var rsa = certificate.GetRSAPublicKey();
        if (rsa is null || rsa.KeySize < MinimumKeySizeBits)
        {
            return false;
        }

        // Una firma PKCS#1 mide exactamente el tamano del modulo. Rechazar
        // cualquier otra longitud antes de tocar el verificador.
        if (signature.Length != rsa.KeySize / 8)
        {
            return false;
        }

        var instant = now.UtcDateTime;
        if (instant < certificate.NotBefore.ToUniversalTime() ||
            instant > certificate.NotAfter.ToUniversalTime())
        {
            return false;
        }

        return rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }

    /// <summary>
    /// Prueba la firma contra varias anclas (Primary y Recovery). Devuelve el
    /// thumbprint del certificado que valido; <c>null</c> si ninguna.
    /// </summary>
    public static bool VerifyDetachedAny(
        ReadOnlySpan<byte> data,
        ReadOnlySpan<byte> signature,
        IReadOnlyList<X509Certificate2> certificates,
        out string? matchedThumbprint) =>
        VerifyDetachedAny(data, signature, certificates, DateTimeOffset.UtcNow, out matchedThumbprint);

    /// <summary>
    /// Prueba la firma contra varias anclas con un instante explicito, para
    /// tests de validez temporal.
    /// </summary>
    public static bool VerifyDetachedAny(
        ReadOnlySpan<byte> data,
        ReadOnlySpan<byte> signature,
        IReadOnlyList<X509Certificate2> certificates,
        DateTimeOffset now,
        out string? matchedThumbprint)
    {
        foreach (var certificate in certificates)
        {
            if (VerifyDetached(data, signature, certificate, now))
            {
                matchedThumbprint = certificate.Thumbprint;
                return true;
            }
        }

        matchedThumbprint = null;
        return false;
    }
}
