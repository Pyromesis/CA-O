namespace CAO.Core.Abstractions;

/// <summary>
/// Enumeracion de ficheros para limpiezas recursivas.
/// CAO-BUG-2026-10-06: <c>SearchOption.AllDirectories</c> a pelo tiene dos
/// fallos que en un servicio privilegiado no tienen red de seguridad.
/// 1. Lanza en el primer directorio sin acceso, de modo que el resto del
///    arbol se pierde en silencio y la limpieza se queda a medias.
/// 2. Sigue los puntos de reanildo: un junction hacia el padre produce un
///    recorrido infinito, y aqui no hay ningun techo de tiempo que lo corte.
/// <see cref="RecursiveOptions"/> solve ambos: ignora los subarboles inaccesibles
/// y no entra en los directorios de reenlace.
/// </summary>
public static class SafeFileEnumeration
{
    /// <summary>Opciones de enumeracion recursiva segura. Visible para las pruebas.</summary>
    internal static readonly EnumerationOptions RecursiveOptions = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
    };

    /// <summary>
    /// Ficheros bajo <paramref name="directory"/> que casan con
    /// <paramref name="pattern"/>, en profundidad y sin entrar en reenlaces.
    /// </summary>
    public static IEnumerable<string> Files(string directory, string pattern = "*") =>
        Directory.EnumerateFiles(directory, pattern, RecursiveOptions);

    /// <summary>
    /// Suma de bytes de los ficheros bajo <paramref name="directory"/>. Un
    /// fichero que desaparece o se bloquea a mitad del recorrido se omite en
    /// lugar de abortar la cuenta.
    /// </summary>
    public static long BytesIn(string directory, string pattern = "*")
    {
        long bytes = 0;
        foreach (var path in Files(directory, pattern))
        {
            try { bytes += new FileInfo(path).Length; }
            catch { }
        }
        return bytes;
    }
}