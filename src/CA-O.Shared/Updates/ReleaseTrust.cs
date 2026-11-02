using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace CAO.Shared.Updates;

/// <summary>
/// Anclas de confianza del updater: los certificados publicos (DER en base64)
/// de las dos claves que pueden firmar manifiestos de release.
///
/// <para>
/// <c>Primary</c> es la clave del dia a dia. <c>Recovery</c> es una segunda
/// raiz cuya clave privada se guarda offline: si la Primary se pierde o se
/// compromete, permite publicar una version de transicion firmada por
/// Recovery. Aceptar cualquiera de las dos no elimina el riesgo de que una
/// clave comprometida firme una version superior antes de que llegue la
/// transicion; por eso la Recovery debe estar fuera del equipo habitual.
/// </para>
///
/// <para>
/// Son autofirmadas a proposito: no hace falta una CA comercial. La confianza
/// no viene de una cadena de certificados, viene de que estos valores estan
/// compilados dentro de CA-O. Rotar una clave = publicar una version nueva
/// con el ancla actualizada; las versiones ya instaladas seguiran confiando
/// en las anclas viejas.
/// </para>
/// </summary>
public static class ReleaseTrust
{
    public const string PrimaryKeyId = "primary";
    public const string RecoveryKeyId = "recovery";

    /// <summary>
    /// Certificado publico "CN=CA-O Release Signing 2026" (RSA-3072).
    /// Generado con <c>scripts/generate-release-key.ps1 -Role Primary</c>;
    /// el .pfx NO esta en el repositorio.
    /// </summary>
    public const string SigningCertificateBase64 =
        "MIIEVzCCAr+gAwIBAgIJAJz2E+I8cTftMA0GCSqGSIb3DQEBCwUAMEAxCzAJBgNVBAYTAkVTMQ0wCwYDVQQKEwRDQS1PMSIwIAYDVQQDExlDQS1PIFJlbGVhc2UgU2lnbmluZyAyMDI2MB4XDTI2MTEwMjAzNTIxMVoXDTQ2MTEwMjAzNTcxMVowQDELMAkGA1UEBhMCRVMxDTALBgNVBAoTBENBLU8xIjAgBgNVBAMTGUNBLU8gUmVsZWFzZSBTaWduaW5nIDIwMjYwggGiMA0GCSqGSIb3DQEBAQUAA4IBjwAwggGKAoIBgQCthN8lVCwisSM53Ixh3UHZRiM4kXSUQ0ucP9dNe1Z49KOfP6PAcdXaksAaofNzv7JuQWdYfaNBhpQT+cantZqs7Xo7tqNSnA9q5MZGn8NL31IywkfeEdy1CAtL5O0Y7zMi1zoOqJEH9IWAwpQSFPxlrQwxWMmVdctfbdq5P+cmiXHs+TH2vCyyFqqmecIBlHPfMiZoFGgPOuTEUZ2mlgxoDm9lxFDZ2IxgRxzPVh+6Ygw4WFyXq4mUMJl3qHvx6dMTRVepnZQkhNtX9G5d4M6IvokVbvYNz07N54ZEQ1g7sQarEt9Q4eT6e4GwXBgREjXuRMTkc4iLyh+L1JDhyToiig+F4F2tdDugJNjavaQPc7fK8Qm9sFx2uISSmhKKa6M1wBtYMuokJ0QSToRxi2sjjOmuZIyfJH37+rJy7B9Q0kM2cbntNGlPzu/xgtvsFoADPv/41bamkXKg5QcOYi9tggD4gjp4MaryR5mrZWakcYNUEsolPvjSSXg6a+eTQvECAwEAAaNUMFIwDgYDVR0PAQH/BAQDAgeAMBMGA1UdJQQMMAoGCCsGAQUFBwMDMAwGA1UdEwEB/wQCMAAwHQYDVR0OBBYEFGilQxFNiCD26DMOr7Gk3/lhO2uvMA0GCSqGSIb3DQEBCwUAA4IBgQA/Hx3JrxtOIDdVWUIB7jvuB46ocmDZQAEFfbgIERndRBgZHWBiRiG3CCQjOvSUUfMINGUVQjBscTJ7onmwXB8ZvQg2de8LFtA/JkicZlyhFDTOAFHgF920A0/gcWuvvz5/j4IO7hBqdXzUWiPykfM50qdq7FbaYbcGJGwCgysYzHnVn+OVCEH/JgPVIgzHp8quOA/GQ+W+DUrow1tyWd4seF+B1vUh7n1WFVjypgvsL2Yakp4mhDJRJ/MkFhjPNy2jphknhkmQU0CB2VugrjYzeukSVm16J2rx+8+NLXIIVq4+j3aizVjVsrPun5mpOOV+OLIr+CxcYxxJYwN8ItYqxYWBOHtmtpMEK8ckg2V9S3NgFr0XPgZSyHPWaizeVpMioLeI7BbZGLPwNd1JtjIZ0LhQCSV2gztdOVrHPVsbLUY/HQBmeeXhvtHSxR3rZlIdnzlV6IKaRBCD6JdM9NDFHrKYyRzFBTgwzHdfGJyNIz1xYBmtJVgBCZAzk7dpR3U=";

    /// <summary>
    /// Certificado publico "CN=CA-O Release Recovery 2026" (RSA-3072).
    /// Clave de recuperacion: su .pfx debe guardarse offline, no en el equipo
    /// habitual. Generado con <c>scripts/generate-release-key.ps1 -Role Recovery</c>.
    /// </summary>
    public const string RecoveryCertificateBase64 =
        "MIIEWDCCAsCgAwIBAgIIRA1+i4w44IUwDQYJKoZIhvcNAQELBQAwQTELMAkGA1UEBhMCRVMxDTALBgNVBAoTBENBLU8xIzAhBgNVBAMTGkNBLU8gUmVsZWFzZSBSZWNvdmVyeSAyMDI2MB4XDTI2MTEwMjA0MTAyMFoXDTQ2MTEwMjA0MTUyMFowQTELMAkGA1UEBhMCRVMxDTALBgNVBAoTBENBLU8xIzAhBgNVBAMTGkNBLU8gUmVsZWFzZSBSZWNvdmVyeSAyMDI2MIIBojANBgkqhkiG9w0BAQEFAAOCAY8AMIIBigKCAYEAq/vK02ruKbRSrS6EQbSNNrK/BiXZpA+JQ4GLtkv34Q6x/8d4XX9jcMGmyVGyH/LZF7xJNcosFuynDkUvQWURCRuWmxNFjRES+LtqzcJl8M9Bov0T1ohOhhBx8o18gmIGHBfGSPkoIfsnPRrAzcs4Zt4Inm3wMS3PY88r1BneSEu2Z8g4BXsfYlqYtO558hCcz+vHXw2B49aC0GY4CQ6fEIKVxcJyS3eC955jGcemldBbYsNMXgrLcz/5EldfIJh9E3JAg0nR4Hzz4qg4lby8Hhz+0puZvRspQq9MJefpwjQLpx7EYeGSTRT2fUPstUdxl8U7aB5wOxS/4EP9XFIuHTTerRiPPyl0NlNyY3kEadNce0F10FWayQ3b7bjf4leBG+s3WASUmeoPoxN0y45G4tCFsJlJSfeDdNjkQZNFu4OtjVvTW2SXIS51d18fmESns8zf5fI9TCesYVqfN7HTVeQu5svPaa5fFSrXAneMeWIQjHEIE/EpDcbc1GHLpF3FAgMBAAGjVDBSMA4GA1UdDwEB/wQEAwIHgDATBgNVHSUEDDAKBggrBgEFBQcDAzAMBgNVHRMBAf8EAjAAMB0GA1UdDgQWBBRpe8o8Ldcq+8ME3cGPU/NZ1wK8ijANBgkqhkiG9w0BAQsFAAOCAYEADQClS9ZqSgbBa5pi8DY1OKXMCOQ6uwds5pqh50CS+Sw1r9SSPkyhG42BaQT4O/rj5L+GpyEZnxjYBOwmCyPomjWNFM7LVNQfyjkPn5Rl0NElxZaJN2ve/mBbiuivXcPCPilclLT4S5DZiA0FRze+dczNxl/O8mQBww8uSf50qawsIfuW7gqpa1Mvr0utPtf3XiU4Zkk1zTH9hX4tcEdTwl3Ybr/PnSJlO7iOS5AimRm5HHg08bKJ3iZ9aCAzirjGu0qQszTgLEelKzXG/QIuIObuSw+3vmMjtS4cYNjplayBj53c4tVWlhOUcOpnu4X6qrAhvDafBmG+HunRRo1flrAi6Fl5V2qK/w6OkRPxDPY9QbXKjp6adI4RzjvlULZZxLa108pB0HrmijhaTqBw4Ktgqb1ty9CDbIM+5d+qVsIJVMUboi6ICYNl0r+dACPjkwLQFx82miGZCzpZbIGFA839y89MuBKfMSV+r2P7x/lTK/Ghm/ifILQT3vTakWDt";

    /// <summary>Todas las anclas compiladas, con su identificador estable.</summary>
    public static IReadOnlyList<ReleaseTrustAnchor> Anchors { get; } =
    [
        new(PrimaryKeyId, SigningCertificateBase64),
        new(RecoveryKeyId, RecoveryCertificateBase64),
    ];

    /// <summary>Carga el certificado publico Primary. Solo contiene la clave publica.</summary>
    public static X509Certificate2 LoadSigningCertificate() => LoadAnchor(PrimaryKeyId);

    /// <summary>Carga el certificado publico Recovery. Solo contiene la clave publica.</summary>
    public static X509Certificate2 LoadRecoveryCertificate() => LoadAnchor(RecoveryKeyId);

    /// <summary>
    /// Carga todas las anclas. El llamante es responsable de disponer cada
    /// certificado (por ejemplo, con un <c>foreach</c> y <c>Dispose</c>).
    /// </summary>
    public static IReadOnlyList<X509Certificate2> LoadTrustAnchors() =>
        Anchors
            .Select(anchor => X509CertificateLoader.LoadCertificate(Convert.FromBase64String(anchor.CertificateBase64)))
            .ToArray();

    /// <summary>Huella SHA-256 del certificado Primary, para logs y verificacion manual.</summary>
    public static string GetSigningCertificateSha256() => GetCertificateSha256(PrimaryKeyId);

    /// <summary>Huella SHA-256 del certificado Recovery, para logs y verificacion manual.</summary>
    public static string GetRecoveryCertificateSha256() => GetCertificateSha256(RecoveryKeyId);

    private static X509Certificate2 LoadAnchor(string keyId)
    {
        var anchor = Anchors.Single(a => string.Equals(a.KeyId, keyId, StringComparison.Ordinal));
        return X509CertificateLoader.LoadCertificate(Convert.FromBase64String(anchor.CertificateBase64));
    }

    private static string GetCertificateSha256(string keyId)
    {
        using var certificate = LoadAnchor(keyId);
        return Convert.ToHexString(SHA256.HashData(certificate.RawData));
    }
}

/// <summary>Ancla publica y su identificador estable (<c>primary</c>/<c>recovery</c>).</summary>
public sealed record ReleaseTrustAnchor(string KeyId, string CertificateBase64);
