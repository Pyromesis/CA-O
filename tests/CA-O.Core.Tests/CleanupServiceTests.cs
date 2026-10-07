using System.Diagnostics;

using CAO.Core.Services;

using Xunit;

namespace CAO.Core.Tests;

/// <summary>
/// CAO-BUG-2026-10-06: <c>CleanupService.DeleteTree</c> recorria directorios sin
/// ningun filtro de puntos de reanalisis. Un junction colgado dentro de la raiz
/// (hay junctions reales bajo %TEMP%) se seguia como si fuera un directorio
/// normal, de modo que un enlace a un ancestro provocaba recursion infinita y
/// un StackOverflowException, que en .NET apaga el proceso sin poder capturarse.
/// El servicio privilegiado trabaja como SYSTEM, asi que ahi seria el servicio
/// entero el que cae. Ademas la enumeracion cruda se detenia en el primer
/// subarbol sin acceso y el resto de la entrada se abandonaba en silencio.
/// </summary>
public sealed class CleanupServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "cao-clean-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            // Borra el junction antes del contenido: entrar por el enlace
            // durante la propia limpieza del test seria un falso positivo.
            foreach (var dir in Directory.GetDirectories(_root, "*", SearchOption.AllDirectories))
                DeleteLink(dir);
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
        catch { }
    }

    private static void DeleteLink(string dir)
    {
        try
        {
            if ((File.GetAttributes(dir) & FileAttributes.ReparsePoint) == 0) return;
            Directory.Delete(dir, recursive: false);
        }
        catch { }
    }

    private static void MakeJunction(string linkPath, string targetPath)
    {
        var psi = new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{linkPath}\" \"{targetPath}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var proc = Process.Start(psi);
        proc!.WaitForExit(15_000);
        Assert.True(proc.ExitCode == 0, "No se pudo crear el junction de prueba: " + proc.StandardError.ReadToEnd());
    }

    private static string WriteFile(string dir, string name, int bytes)
    {
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, name);
        File.WriteAllBytes(path, new byte[bytes]);
        return path;
    }

    [Fact]
    public async Task CleanAsync_DoesNotFollowJunctions_AndStillDeletesRealContent()
    {
        Directory.CreateDirectory(_root);
        WriteFile(_root, "soltado.tmp", 4 * 1024);
        WriteFile(Path.Combine(_root, "sub"), "anidado.tmp", 6 * 1024);

        // Junction a un subdirectorio ya recorrido: al seguirlo se vuelve a
        // entrar en un contenido que ya no existe y cada entrada cuenta como
        // error. Se usa un destino NO ciclico a proposito: un junction a la
        // propia raiz provocaria recursion infinita y un StackOverflowException
        // que apagaria el proceso de pruebas entero antes de poder informar.
        var link = Path.Combine(_root, "bucle");
        MakeJunction(link, Path.Combine(_root, "sub"));

        var service = new CleanupService(new[] { _root });
        var (freed, errors) = await service.CleanAsync();

        Assert.Equal(0, errors);
        Assert.Equal(10 * 1024, freed);
        Assert.False(File.Exists(Path.Combine(_root, "soltado.tmp")));
        Assert.False(File.Exists(Path.Combine(_root, "sub", "anidado.tmp")));
        // El enlace si se borra: ocupa cero bytes y no tiene destino propio.
        Assert.False(Directory.Exists(link));
    }

    [Fact]
    public void EstimateBytes_IgnoresJunctions_SoItDoesNotDoubleCount()
    {
        Directory.CreateDirectory(_root);
        WriteFile(_root, "soltado.tmp", 4 * 1024);
        MakeJunction(Path.Combine(_root, "bucle"), _root);

        var service = new CleanupService(new[] { _root });
        var estimated = service.EstimateBytes();

        Assert.Equal(4 * 1024, estimated);
    }
}