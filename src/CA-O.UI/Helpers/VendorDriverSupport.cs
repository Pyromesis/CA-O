using CAO.Shared;

namespace CAO.UI.Helpers;

/// <summary>
/// Enlaces OFICIALES de drivers por fabricante (fase drivers 3). Solo
/// dominios del fabricante o de Microsoft; el serial solo se usa localmente
/// para construir el enlace de Dell y copiarlo bajo demanda del usuario.
/// Si el sistema miente ("Default string" en clónicos/VM), se recurre a la
/// placa base y a la BIOS antes de rendirse.
/// </summary>
public sealed record VendorSupport(string Vendor, string DriversUrl, string Note, string DetectedBy);

public static class VendorDriverSupport
{
    public const string MicrosoftCatalogUrl = "https://www.catalog.update.microsoft.com/";

    private static readonly string[] Placeholders =
    [
        "To be filled by O.E.M.", "Default string", "System manufacturer",
        "System Product Name", "System Name", "System Version",
        "None", "N/A", "Unknown", "Not Specified", "Not Available", "INVALID", "0",
    ];

    private static string Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var clean = value.Trim();
        return Placeholders.Any(p => clean.Equals(p, StringComparison.OrdinalIgnoreCase)) ? string.Empty : clean;
    }

    public static string MaskSerial(string? serial)
    {
        var clean = Clean(serial);
        if (string.IsNullOrEmpty(clean)) return "no disponible";
        return clean.Length <= 4 ? "••••" : new string('•', clean.Length - 4) + clean[^4..];
    }

    /// <summary>Resolución clásica solo con sistema (la usa el fallback y tests viejos).</summary>
    public static VendorSupport Resolve(string manufacturer, string model, string? serial) =>
        Resolve(new ComputerInfo(Clean(manufacturer), Clean(model), Clean(serial)));

    public static VendorSupport Resolve(ComputerInfo info)
    {
        var sysMaker = Clean(info.Manufacturer);
        var sysModel = Clean(info.Model);
        var boardMaker = Clean(info.BoardManufacturer);
        var boardModel = Clean(info.BoardProduct);
        var biosMaker = Clean(info.BiosManufacturer);
        var serial = Clean(info.SerialNumber);

        // Máquina virtual: los drivers los pone el anfitrión (guest tools).
        var everything = $"{sysMaker} {sysModel} {boardMaker} {boardModel} {biosMaker}".ToLowerInvariant();
        if (everything.Contains("vmware") || everything.Contains("virtualbox") || everything.Contains("qemu") ||
            everything.Contains("xen") || everything.Contains("hyper-v") || everything.Contains("hyperv") ||
            everything.Contains("virtual machine") || everything.Contains("parallels") || everything.Contains("utm") ||
            everything.Contains("kvm") || everything.Contains("bochs"))
        {
            return new("Máquina virtual", MicrosoftCatalogUrl,
                "Es una VM: los drivers los aportan las guest tools del anfitrión (VMware Tools / Guest Additions / Integration Services). Para el resto, catálogo Microsoft.",
                "virtualización");
        }

        // Fabricante: sistema, si no placa, si no BIOS. Modelo igual.
        var maker = FirstNonEmpty(sysMaker, boardMaker, biosMaker);
        var makerSource = !string.IsNullOrEmpty(sysMaker) ? "sistema"
            : !string.IsNullOrEmpty(boardMaker) ? "placa base" : "BIOS";
        var model = FirstNonEmpty(sysModel, boardModel);

        var match = MatchVendor(maker);
        if (match.HasValue)
        {
            var (vendor, url, note) = match.Value;
            if (vendor == "Dell" && !string.IsNullOrEmpty(serial))
                return new("Dell",
                    $"https://www.dell.com/support/home/es-es/product-support/servicetag/{Uri.EscapeDataString(serial)}",
                    $"Enlace directo a tu equipo por número de serie (detectado por {makerSource}).", makerSource);
            var modelSuffix = string.IsNullOrEmpty(model) ? string.Empty : $" Modelo: {model}.";
            return new(vendor, url, $"{note} (detectado por {makerSource}).{modelSuffix}", makerSource);
        }

        // Sin fabricante conocido: decir lo que SÍ se encontró, nunca callejón.
        var found = new List<string>();
        if (!string.IsNullOrEmpty(boardMaker) || !string.IsNullOrEmpty(boardModel))
            found.Add($"placa {JoinNonEmpty(boardMaker, boardModel)}");
        if (!string.IsNullOrEmpty(biosMaker)) found.Add($"BIOS {biosMaker}");
        if (!string.IsNullOrEmpty(sysModel)) found.Add($"modelo {sysModel}");
        var foundText = found.Count == 0
            ? "Ni sistema, ni placa, ni BIOS dicen quién lo fabricó."
            : $"Pistas: {string.Join(" · ", found)}.";
        return new("Fabricante no identificado", MicrosoftCatalogUrl,
            $"Busca cada dispositivo por su ID de hardware en el catálogo oficial de Microsoft. {foundText}",
            "sin datos útiles");
    }

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (var value in values)
            if (!string.IsNullOrEmpty(value)) return value;
        return string.Empty;
    }

    private static string JoinNonEmpty(params string[] values) =>
        string.Join(" ", values.Where(v => !string.IsNullOrEmpty(v)));

    private static (string Vendor, string Url, string Note)? MatchVendor(string maker)
    {
        var m = maker.ToLowerInvariant();
        if (m.Contains("dell")) return ("Dell", "https://www.dell.com/support/home/es-es", "Busca tu modelo o número de serie en la página oficial");
        if (m.Contains("lenovo")) return ("Lenovo", "https://pcsupport.lenovo.com/es/es/", "Detecta tu equipo o busca tu modelo en la página oficial");
        if (m.Contains("hp") || m.Contains("hewlett")) return ("HP", "https://support.hp.com/drivers", "Busca tu modelo en la página oficial de HP");
        if (m.Contains("asus") || m.Contains("asustek") || m.Contains("pegatron") || m.Contains("asrock")) return ("ASUS/ASRock", "https://www.asus.com/support/download-center/", "Busca tu placa/modelo en el centro de descargas oficial");
        if (m.Contains("acer")) return ("Acer", "https://www.acer.com/support", "Busca tu modelo en el soporte oficial");
        if (m.Contains("msi") || m.Contains("micro-star")) return ("MSI", "https://www.msi.com/support/download", "Busca tu modelo en el soporte oficial");
        if (m.Contains("gigabyte")) return ("Gigabyte", "https://www.gigabyte.com/Support/Consumer/Download", "Busca tu modelo en el soporte oficial");
        if (m.Contains("samsung")) return ("Samsung", "https://www.samsung.com/es/support/", "Busca tu modelo en el soporte oficial");
        if (m.Contains("microsoft")) return ("Microsoft Surface", "https://support.microsoft.com/surface", "Descarga el paquete oficial para tu Surface");
        if (m.Contains("huawei")) return ("Huawei", "https://consumer.huawei.com/support/", "Busca tu modelo en el soporte oficial");
        if (m.Contains("xiaomi")) return ("Xiaomi", "https://www.mi.com/global/support/", "Busca tu modelo en el soporte oficial");
        if (m.Contains("american megatrends") || m.Contains("ami")) return ("Placa genérica (BIOS AMI)", MicrosoftCatalogUrl, "Placa sin marca: usa el catálogo Microsoft por ID de hardware");
        if (m.Contains("phoenix")) return ("Placa genérica (BIOS Phoenix)", MicrosoftCatalogUrl, "Placa sin marca: usa el catálogo Microsoft por ID de hardware");
        if (m.Contains("insyde")) return ("Placa genérica (BIOS Insyde)", MicrosoftCatalogUrl, "Placa sin marca: usa el catálogo Microsoft por ID de hardware");
        return null;
    }
}
