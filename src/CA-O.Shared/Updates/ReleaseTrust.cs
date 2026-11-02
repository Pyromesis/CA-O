using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace CAO.Shared.Updates;

/// <summary>
/// Ancla de confianza del updater: el certificado publico (DER en base64) de
/// la clave que firma los manifiestos de release.
///
/// Es autofirmado a proposito: no hace falta una CA comercial. La confianza
/// no viene de una cadena de certificados, viene de que este valor esta
/// compilado dentro de CA-O y el sector privado del par nunca sale del equipo
/// del mantenedor (<c>%USERPROFILE%\.cao\release-signing</c>, fuera del repo).
///
/// Rotar la clave = publicar una version nueva con este valor actualizado.
/// Las versiones ya instaladas seguiran confiando en el ancla vieja, asi que
/// la rotacion se hace con solapamiento (firmar con ambas) o aceptando que
/// esas versiones necesitaran descarga manual.
/// </summary>
public static class ReleaseTrust
{
    /// <summary>
    /// Certificado publico "CN=CA-O Release Signing 2026" (RSA-3072).
    /// Generado con <c>scripts/generate-release-key.ps1</c>; el .pfx NO esta
    /// en el repositorio.
    /// </summary>
    public const string SigningCertificateBase64 =
        "MIIEVzCCAr+gAwIBAgIJAJz2E+I8cTftMA0GCSqGSIb3DQEBCwUAMEAxCzAJBgNVBAYTAkVTMQ0wCwYDVQQKEwRDQS1PMSIwIAYDVQQDExlDQS1PIFJlbGVhc2UgU2lnbmluZyAyMDI2MB4XDTI2MTEwMjAzNTIxMVoXDTQ2MTEwMjAzNTcxMVowQDELMAkGA1UEBhMCRVMxDTALBgNVBAoTBENBLU8xIjAgBgNVBAMTGUNBLU8gUmVsZWFzZSBTaWduaW5nIDIwMjYwggGiMA0GCSqGSIb3DQEBAQUAA4IBjwAwggGKAoIBgQCthN8lVCwisSM53Ixh3UHZRiM4kXSUQ0ucP9dNe1Z49KOfP6PAcdXaksAaofNzv7JuQWdYfaNBhpQT+cantZqs7Xo7tqNSnA9q5MZGn8NL31IywkfeEdy1CAtL5O0Y7zMi1zoOqJEH9IWAwpQSFPxlrQwxWMmVdctfbdq5P+cmiXHs+TH2vCyyFqqmecIBlHPfMiZoFGgPOuTEUZ2mlgxoDm9lxFDZ2IxgRxzPVh+6Ygw4WFyXq4mUMJl3qHvx6dMTRVepnZQkhNtX9G5d4M6IvokVbvYNz07N54ZEQ1g7sQarEt9Q4eT6e4GwXBgREjXuRMTkc4iLyh+L1JDhyToiig+F4F2tdDugJNjavaQPc7fK8Qm9sFx2uISSmhKKa6M1wBtYMuokJ0QSToRxi2sjjOmuZIyfJH37+rJy7B9Q0kM2cbntNGlPzu/xgtvsFoADPv/41bamkXKg5QcOYi9tggD4gjp4MaryR5mrZWakcYNUEsolPvjSSXg6a+eTQvECAwEAAaNUMFIwDgYDVR0PAQH/BAQDAgeAMBMGA1UdJQQMMAoGCCsGAQUFBwMDMAwGA1UdEwEB/wQCMAAwHQYDVR0OBBYEFGilQxFNiCD26DMOr7Gk3/lhO2uvMA0GCSqGSIb3DQEBCwUAA4IBgQA/Hx3JrxtOIDdVWUIB7jvuB46ocmDZQAEFfbgIERndRBgZHWBiRiG3CCQjOvSUUfMINGUVQjBscTJ7onmwXB8ZvQg2de8LFtA/JkicZlyhFDTOAFHgF920A0/gcWuvvz5/j4IO7hBqdXzUWiPykfM50qdq7FbaYbcGJGwCgysYzHnVn+OVCEH/JgPVIgzHp8quOA/GQ+W+DUrow1tyWd4seF+B1vUh7n1WFVjypgvsL2Yakp4mhDJRJ/MkFhjPNy2jphknhkmQU0CB2VugrjYzeukSVm16J2rx+8+NLXIIVq4+j3aizVjVsrPun5mpOOV+OLIr+CxcYxxJYwN8ItYqxYWBOHtmtpMEK8ckg2V9S3NgFr0XPgZSyHPWaizeVpMioLeI7BbZGLPwNd1JtjIZ0LhQCSV2gztdOVrHPVsbLUY/HQBmeeXhvtHSxR3rZlIdnzlV6IKaRBCD6JdM9NDFHrKYyRzFBTgwzHdfGJyNIz1xYBmtJVgBCZAzk7dpR3U=";

    /// <summary>Carga el certificado publico clavado. Solo contiene la clave publica.</summary>
    public static X509Certificate2 LoadSigningCertificate() =>
        X509CertificateLoader.LoadCertificate(Convert.FromBase64String(SigningCertificateBase64));

    /// <summary>Huella SHA-256 del certificado, para logs y verificacion manual.</summary>
    public static string GetSigningCertificateSha256()
    {
        using var certificate = LoadSigningCertificate();
        return Convert.ToHexString(SHA256.HashData(certificate.RawData));
    }
}
