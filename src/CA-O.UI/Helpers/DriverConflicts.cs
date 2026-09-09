using CAO.Shared;

namespace CAO.UI.Helpers;

/// <summary>
/// Lectura honesta de conflictos de controladores (ConfigManagerErrorCode de
/// Win32_PnPSignedDriver). Solo describe lo medido; no diagnostica causas.
/// </summary>
public static class DriverConflicts
{
    public static bool IsProblem(DriverDiagnostic driver) => driver.ProblemCode != 0;

    public static bool IsMissing(DriverDiagnostic driver) => driver.ProblemCode == 28;

    public static bool IsUnsigned(DriverDiagnostic driver) => driver.IsSigned == false;

    public static string SignedLabel(bool? signed) => signed switch
    {
        true => "Firmado",
        false => "Sin firmar",
        null => "Firma desconocida",
    };

    /// <summary>Convierte fecha WMI (aaaammddHHMMSS...) a dd/MM/aaaa; tal cual si no parsea.</summary>
    public static string FormatDriverDate(string? wmiDate)
    {
        if (string.IsNullOrWhiteSpace(wmiDate) || wmiDate.Length < 8) return "Fecha desconocida";
        if (DateTime.TryParseExact(wmiDate[..8], "yyyyMMdd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var date))
            return date.ToString("dd/MM/yyyy");
        return wmiDate;
    }

    /// <summary>Significado en español de ConfigManagerErrorCode (códigos CM_PROB de Windows).</summary>
    public static string DescribeProblem(int code) => code switch
    {
        0 => "Correcto",
        1 => "Código 1: sin configurar. Completa la instalación del dispositivo.",
        10 => "Código 10: el dispositivo no puede iniciar. Reinstala su controlador.",
        14 => "Código 14: necesita reinicio para terminar de funcionar.",
        18 => "Código 18: reinstala el controlador de este dispositivo.",
        19 => "Código 19: registro dañado. Desinstala y reinstala el dispositivo.",
        21 => "Código 21: Windows lo va a quitar.",
        22 => "Código 22: deshabilitado. Habilítalo en Administrador de dispositivos.",
        24 => "Código 24: ausente o no responde. Revisa la conexión física.",
        28 => "Código 28: sin controlador instalado. Instala el original del fabricante.",
        29 => "Código 29: deshabilitado (a veces desde el firmware/BIOS).",
        31 => "Código 31: no funciona correctamente. Reinstala su controlador.",
        32 => "Código 32: controlador deshabilitado; el alternativo tomó el control.",
        38 => "Código 38: no se pudo cargar el controlador (versión anterior en uso).",
        39 => "Código 39: controlador ausente o dañado. Reinstálalo.",
        40 => "Código 40: entrada de registro incompleta. Reinstala el dispositivo.",
        43 => "Código 43: Windows lo detuvo por notificar problemas.",
        45 => "Código 45: actualmente desconectado.",
        47 => "Código 47: preparado para extracción segura.",
        48 => "Código 48: bloqueado por Windows (incompatible o vulnerable).",
        52 => "Código 52: sin firma digital válida. Instala el original firmado.",
        _ => $"Código {code} de Windows: míralo en Administrador de dispositivos.",
    };
}
