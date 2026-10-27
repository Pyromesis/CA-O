using CAO.Shared.Constants;

namespace CAO.Core.Security;

/// <summary>
/// Protección de los recursos propios de CA-O frente a optimizaciones
/// genéricas. Ninguna optimización puede retrasar, desactivar ni borrar el
/// servicio privilegiado ni las entradas de inicio de la propia app: ese
/// servicio es el canal SYSTEM que ejecuta todas las mutaciones, así que
/// perderlo deja la aplicación inutilizable.
///
/// La comprobación combina identidad (nombre canónico) y procedencia
/// (binario dentro de la instalación, ProgramData o el directorio de la app),
/// de modo que también cubre servicios/entradas renombrados o instalados en
/// una ruta de desarrollo.
/// </summary>
public static class CaoSelfProtection
{
    private static readonly string[] OwnExecutableNames =
    [
        BuildConstants.UiExecutable,
        BuildConstants.ServiceExecutable,
        BuildConstants.SetupExecutable,
        BuildConstants.UninstallerExecutable,
        BuildConstants.GuiInstallerExecutable,
    ];

    /// <summary>¿Es el servicio privilegiado de CA-O (por nombre o binario)?</summary>
    public static bool IsOwnService(string? serviceName, string? imagePath, string? displayName = null) =>
        IsOwnServiceIdentity(serviceName) ||
        IsOwnServiceIdentity(displayName) ||
        IsOwnExecutable(imagePath);

    /// <summary>¿Es una entrada Run de CA-O (por nombre, comando o fabricante)?</summary>
    public static bool IsOwnStartupEntry(string? name, string? command, string? company)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            if (name.Equals("CA-O", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("CAO", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("CA-O ", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("CAO.", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("CA-O", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return IsOwnPublisher(company) || IsOwnExecutable(command);
    }

    /// <summary>¿Es una tarea programada de CA-O (por nombre o acción)?</summary>
    public static bool IsOwnScheduledTask(string? taskName, string? taskToRun)
    {
        if (!string.IsNullOrWhiteSpace(taskName) &&
            (taskName.Contains("CA-O", StringComparison.OrdinalIgnoreCase) ||
             taskName.StartsWith(@"\CAO", StringComparison.OrdinalIgnoreCase) ||
             taskName.Contains(@"\CAO\", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return IsOwnExecutable(taskToRun);
    }

    /// <summary>¿El comando/ruta apunta a un ejecutable de CA-O?</summary>
    public static bool IsOwnExecutable(string? commandLine)
    {
        var path = ExtractExecutablePath(commandLine);
        if (path is null) return false;

        var fileName = Path.GetFileName(path);
        if (OwnExecutableNames.Any(n => n.Equals(fileName, StringComparison.OrdinalIgnoreCase)))
            return true;

        return IsOwnPath(path);
    }

    private static bool IsOwnServiceIdentity(string? name) =>
        !string.IsNullOrWhiteSpace(name) &&
        (name.Equals(BuildConstants.ServiceName, StringComparison.OrdinalIgnoreCase) ||
         name.Equals(BuildConstants.ServiceDisplayName, StringComparison.OrdinalIgnoreCase));

    private static bool IsOwnPublisher(string? company)
    {
        if (string.IsNullOrWhiteSpace(company)) return false;
        return company.Contains("CA-O", StringComparison.OrdinalIgnoreCase) ||
               company.StartsWith("CAO", StringComparison.OrdinalIgnoreCase) ||
               company.Contains(BuildConstants.Publisher, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOwnPath(string path)
    {
        var normalized = NormalizeFullPath(path);
        if (normalized is null) return false;

        foreach (var root in OwnRoots())
        {
            if (IsUnder(normalized, root)) return true;
        }
        return false;
    }

    private static IEnumerable<string> OwnRoots()
    {
        yield return NormalizeFullPath(BuildConstants.GetInstallDirectory())!;
        yield return NormalizeFullPath(BuildConstants.GetProgramDataRoot())!;
        yield return NormalizeFullPath(AppContext.BaseDirectory)!;
    }

    private static bool IsUnder(string path, string root)
    {
        if (string.IsNullOrEmpty(root)) return false;
        return path.Equals(root, StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Extrae la ruta del ejecutable de una línea de comando. Acepta rutas
    /// entre comillas y rutas sin comillas con espacios (el caso habitual de
    /// ImagePath en servicios: C:\Program Files\...\svc.exe -arg).
    /// </summary>
    internal static string? ExtractExecutablePath(string? commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine)) return null;
        try
        {
            var trimmed = Environment.ExpandEnvironmentVariables(commandLine.Trim());
            string candidate;
            if (trimmed.StartsWith('"'))
            {
                var end = trimmed.IndexOf('"', 1);
                candidate = end > 1 ? trimmed[1..end] : trimmed[1..];
            }
            else
            {
                var exeEnd = trimmed.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
                candidate = exeEnd >= 0
                    ? trimmed[..(exeEnd + 4)]
                    : trimmed.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries)[0];
            }

            candidate = candidate.Trim().Trim('"');
            return string.IsNullOrWhiteSpace(candidate) ? null : candidate;
        }
        catch
        {
            return null;
        }
    }

    private static string? NormalizeFullPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try
        {
            var expanded = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
            // Rutas nativas/NT que aparecen en ImagePath de algunos drivers.
            if (expanded.StartsWith(@"\\?\", StringComparison.Ordinal)) expanded = expanded[4..];
            else if (expanded.StartsWith(@"\??\", StringComparison.Ordinal)) expanded = expanded[4..];

            return Path.GetFullPath(expanded)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return null;
        }
    }
}
