using System.Runtime.InteropServices;
using CAO.Shared;

namespace CAO.Infrastructure.SystemInterop;

/// <summary>
/// Mapeo puro de ofertas de Windows Update (WUApi COM) a DTOs + diagnóstico
/// de fallos. Sin llamadas COM aquí: testeable sin Windows Update.
/// </summary>
public static class WindowsUpdateDrivers
{
    public const int MaxUpdates = 50;

    public static DriverUpdateInfo MapUpdateInfo(dynamic update)
    {
        string id = (string)update.Identity.UpdateID;
        string title = ((string?)update.Title ?? string.Empty).Trim();
        string kb = "";
        try
        {
            var kbs = new List<string>();
            foreach (var kbid in update.KBArticleIDs) kbs.Add("KB" + kbid.ToString());
            kb = string.Join(" ", kbs);
        }
        catch { }
        long size = 0;
        try { size = (long)update.MaxDownloadSize; } catch { }
        bool reboot = false;
        try { reboot = (bool)update.RebootRequired; } catch { }
        return new DriverUpdateInfo(id, title, kb, size, reboot);
    }

    public static bool AnyRebootRequired(dynamic collection)
    {
        for (int i = 0; i < collection.Count; i++)
        {
            try { if ((bool)collection[i].RebootRequired) return true; }
            catch { }
        }
        return false;
    }

    public static string DescribeFailure(Exception ex)
    {
        if (ex is COMException com)
        {
            return com.HResult switch
            {
                unchecked((int)0x80070005) => "Windows Update denegó el acceso (0x80070005). Ejecuta como administrador.",
                unchecked((int)0x80072EE7) or unchecked((int)0x80072EFE) or unchecked((int)0x8024402C)
                    => $"Sin conexión con Windows Update (0x{com.HResult:X8}). Revisa internet e inténtalo de nuevo.",
                _ => $"Windows Update falló (0x{com.HResult:X8}): {com.Message.Trim()}",
            };
        }
        return $"Windows Update falló ({ex.GetType().Name}): {ex.Message.Trim()}";
    }
}
