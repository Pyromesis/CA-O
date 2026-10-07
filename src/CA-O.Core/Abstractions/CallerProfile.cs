namespace CAO.Core.Abstractions;

/// <summary>
/// Resuelve las carpetas especiales del USUARIO que llamo al servicio.
/// </summary>
/// <remarks>
/// CAO-BUG-2026-10-06 (F1): con la suplantacion perdida tras el primer
/// <c>await</c> (vease <see cref="CallerContext"/>),
/// <c>Environment.GetFolderPath(SpecialFolder.LocalApplicationData)</c>
/// devolvia la ruta de SYSTEM y las limpiezas de caches de aplicaciones
/// apuntaban al perfil equivocado. La ruta del perfil de un SID no depende de
/// ningun token: Windows la publica en
/// <c>HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\&lt;sid&gt;\ProfileImagePath</c>.
/// Sin <see cref="CallerContext.CurrentUserSid"/> se devuelve
/// <see langword="null"/> y el llamante usa su propio
/// <c>GetFolderPath</c>, de modo que la UI y las pruebas no cambian de
/// comportamiento.
/// </remarks>
public static class CallerProfile
{
    private const string ProfileListKey =
        @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList";

    /// <summary>
    /// Ruta del perfil del llamante, o <see langword="null"/> si el llamante no
    /// es un usuario con perfil (o no hay SID de contexto).
    /// </summary>
    public static string? ProfileDirectory()
    {
        var sid = CallerContext.CurrentUserSid;
        if (sid is null || !OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                ProfileListKey + "\\" + sid);
            var path = key?.GetValue("ProfileImagePath") as string;
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            // El valor publicada como %SystemDrive%\Users\nombre y el formato
            // permite variables de entorno; ExpandEnvironmentVariables no hace
            // nada si no hay ninguna, asi que es seguro en ambos casos.
            return Environment.ExpandEnvironmentVariables(path);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException
                                      or System.Security.SecurityException
                                      or IOException)
        {
            return null;
        }
    }

    /// <summary>
    /// Carpeta especial del llamante, o del proceso cuando no hay llamante.
    /// </summary>
    public static string Folder(Environment.SpecialFolder folder)
    {
        var profile = ProfileDirectory();
        if (profile is null)
        {
            return Environment.GetFolderPath(folder);
        }

        var leaf = folder switch
        {
            Environment.SpecialFolder.ApplicationData => "AppData\\Roaming",
            Environment.SpecialFolder.LocalApplicationData => "AppData\\Local",
            Environment.SpecialFolder.Desktop => "Desktop",
            Environment.SpecialFolder.MyDocuments => "Documents",
            Environment.SpecialFolder.MyPictures => "Pictures",
            Environment.SpecialFolder.MyVideos => "Videos",
            Environment.SpecialFolder.MyMusic => "Music",
            _ => null,
        };

        return leaf is null
            ? Environment.GetFolderPath(folder)
            : Path.Combine(profile, leaf);
    }
}