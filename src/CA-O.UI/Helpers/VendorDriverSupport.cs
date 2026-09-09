namespace CAO.UI.Helpers;

/// <summary>
/// Enlaces OFICIALES de drivers por fabricante (fase drivers 3). Solo
/// dominios del fabricante o de Microsoft; el serial solo se usa localmente
/// para construir el enlace de Dell y copiarlo bajo demanda del usuario.
/// </summary>
public sealed record VendorSupport(string Vendor, string DriversUrl, string Note);

public static class VendorDriverSupport
{
    public const string MicrosoftCatalogUrl = "https://www.catalog.update.microsoft.com/";

    private static bool IsPlaceholderSerial(string? serial) =>
        string.IsNullOrWhiteSpace(serial) ||
        serial.Trim() is "To be filled by O.E.M." or "Default string" or "0" or "None" or "N/A";

    public static string MaskSerial(string? serial)
    {
        if (IsPlaceholderSerial(serial)) return "no disponible";
        var clean = serial!.Trim();
        return clean.Length <= 4 ? "••••" : new string('•', clean.Length - 4) + clean[^4..];
    }

    public static VendorSupport Resolve(string manufacturer, string model, string? serial)
    {
        var maker = (manufacturer ?? string.Empty).ToLowerInvariant();
        var hasSerial = !IsPlaceholderSerial(serial);
        if (maker.Contains("dell"))
            return hasSerial
                ? new("Dell", $"https://www.dell.com/support/home/es-es/product-support/servicetag/{Uri.EscapeDataString(serial!.Trim())}",
                    "Enlace directo a tu equipo por número de serie.")
                : new("Dell", "https://www.dell.com/support/home/es-es",
                    "Busca tu modelo o número de serie en la página oficial.");
        if (maker.Contains("lenovo"))
            return new("Lenovo", "https://pcsupport.lenovo.com/es/es/",
                "Detecta tu equipo o busca tu modelo en la página oficial.");
        if (maker.Contains("hp") || maker.Contains("hewlett"))
            return new("HP", "https://support.hp.com/drivers",
                "Busca tu modelo en la página oficial de HP.");
        if (maker.Contains("asus") || maker.Contains("asustek"))
            return new("ASUS", "https://www.asus.com/support/download-center/",
                "Busca tu modelo en el centro de descargas oficial.");
        if (maker.Contains("acer"))
            return new("Acer", "https://www.acer.com/support",
                "Busca tu modelo en el soporte oficial.");
        if (maker.Contains("msi") || maker.Contains("micro-star"))
            return new("MSI", "https://www.msi.com/support/download",
                "Busca tu modelo en el soporte oficial.");
        if (maker.Contains("gigabyte"))
            return new("Gigabyte", "https://www.gigabyte.com/Support/Consumer/Download",
                "Busca tu modelo en el soporte oficial.");
        if (maker.Contains("samsung"))
            return new("Samsung", "https://www.samsung.com/es/support/",
                "Busca tu modelo en el soporte oficial.");
        if (maker.Contains("microsoft"))
            return new("Microsoft Surface", "https://support.microsoft.com/surface",
                "Descarga el paquete oficial para tu Surface.");
        if (maker.Contains("huawei"))
            return new("Huawei", "https://consumer.huawei.com/support/",
                "Busca tu modelo en el soporte oficial.");
        if (maker.Contains("xiaomi"))
            return new("Xiaomi", "https://www.mi.com/global/support/",
                "Busca tu modelo en el soporte oficial.");
        return new("Fabricante no identificado", MicrosoftCatalogUrl,
            "Catálogo oficial de Microsoft Update: busca el hardware por nombre.");
    }
}
