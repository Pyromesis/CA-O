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
}
