using System.Diagnostics;
using Microsoft.Win32;

var logFile = Path.Combine(Path.GetTempPath(), "CA-O-Uninstall.log");
try { File.AppendAllText(logFile, $"\n[{DateTime.Now:O}] Uninstall iniciado\n"); } catch { }
void Log(string msg) { Console.WriteLine(msg); try { File.AppendAllText(logFile, msg + "\n"); } catch { } }

Console.WriteLine("CA-O 2.0 — Desinstalador");
Console.WriteLine("========================");

// Confirmación si no es silencioso
bool silent = args.Contains("/S") || args.Contains("--silent");
if (!silent)
{
    Console.Write("¿Desinstalar CA-O 2.0? (S/N): ");
    var key = Console.ReadKey(intercept: true);
    Console.WriteLine();
    if (key.KeyChar != 'S' && key.KeyChar != 's' && key.Key != ConsoleKey.Y)
    {
        Log("Desinstalación cancelada por el usuario.");
        return 0;
    }
}

if (!IsAdmin())
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("ERROR: Debe ejecutar como Administrador.");
    Console.ResetColor();
    try
    {
        var exe = Environment.ProcessPath ?? AppContext.BaseDirectory;
        var psi = new ProcessStartInfo(exe, string.Join(" ", args) + " /S") { UseShellExecute = true, Verb = "runas" };
        Process.Start(psi);
        return 0;
    }
    catch { }
    Console.ReadKey();
    return 1;
}

var installDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "CA-O");
var serviceName = "CAO.Privileged";

try
{
    Log("[1/5] Deteniendo servicio...");
    Run("sc.exe", $"stop {serviceName}", true);
    Thread.Sleep(1000);

    Log("[2/5] Eliminando servicio...");
    Run("sc.exe", $"delete {serviceName}", true);
    Thread.Sleep(800);

    Log("[3/5] Eliminando accesos directos...");
    foreach (var lnk in new[]
    {
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs", "CA-O.lnk"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), "CA-O.lnk"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "CA-O.lnk"),
    })
    {
        try { if (File.Exists(lnk)) File.Delete(lnk); Log($"  Eliminado: {lnk}"); } catch (Exception ex) { Log($"  No se pudo eliminar {lnk}: {ex.Message}"); }
        var dir = Path.GetDirectoryName(lnk);
        // Intentar eliminar carpeta CA-O del menú inicio si queda vacía
        if (lnk.Contains("Programs") && Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs", "CA-O")))
        {
            try { Directory.Delete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs", "CA-O"), true); } catch { }
        }
    }

    Log("[4/5] Eliminando entrada de Programas instalados...");
    try
    {
        Registry.LocalMachine.DeleteSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\CA-O", false);
        Log("  Registro desinstalador eliminado (64-bit)");
    }
    catch { }
    try
    {
        Registry.LocalMachine.DeleteSubKey(@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\CA-O", false);
        Log("  Registro desinstalador eliminado (32-bit)");
    }
    catch { }

    Log("[5/5] Eliminando archivos...");
    // No borrar historial por defecto
    foreach (var path in new[] { installDir })
    {
        if (Directory.Exists(path))
        {
            try
            {
                // No borrar si el desinstalador se está ejecutando desde dentro
                var self = Environment.ProcessPath ?? "";
                if (self.StartsWith(path, StringComparison.OrdinalIgnoreCase))
                {
                    // Copiar un batch que se auto-elimine tras salir
                    var batch = Path.Combine(Path.GetTempPath(), "CA-O-Delete.bat");
                    File.WriteAllText(batch, $"@echo off\r\ntimeout /t 2 >nul\r\n rmdir /s /q \"{path}\"\r\ndel \"%~f0\"\r\n");
                    Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{batch}\"") { CreateNoWindow = true, UseShellExecute = false });
                    Log($"  Programado borrado diferido de {path}");
                }
                else
                {
                    Directory.Delete(path, true);
                    Log($"  Eliminado: {path}");
                }
            }
            catch (Exception ex) { Log($"  No se pudo eliminar {path}: {ex.Message}"); }
        }
    }

    // Preguntar por historial
    if (!silent && !args.Contains("--keep-history"))
    {
        Console.Write("¿Borrar también historial y snapshots en %ProgramData%\\CA-O? (S/N): ");
        var k = Console.ReadKey(intercept: true);
        Console.WriteLine();
        if (k.KeyChar == 'S' || k.KeyChar == 's')
        {
            var data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CA-O");
            try { if (Directory.Exists(data)) Directory.Delete(data, true); Log($"  Historial eliminado: {data}"); } catch (Exception ex) { Log($"  No se pudo borrar historial: {ex.Message}"); }
        }
        else
        {
            Log("  Historial conservado en %ProgramData%\\CA-O");
        }
    }

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("\n✓ CA-O 2.0 desinstalado correctamente.");
    Console.ResetColor();
    Log("Desinstalación completada.");
    if (!silent) { Console.WriteLine("Presione cualquier tecla para salir..."); Console.ReadKey(); }
    return 0;
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"ERROR desinstalando: {ex.Message}\n{ex.StackTrace}");
    Console.ResetColor();
    Log($"ERROR: {ex}");
    if (!silent) Console.ReadKey();
    return 1;
}

static bool IsAdmin()
{
    using var id = System.Security.Principal.WindowsIdentity.GetCurrent();
    return new System.Security.Principal.WindowsPrincipal(id).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
}
static void Run(string file, string args, bool ignoreError = false)
{
    Console.WriteLine($"  > {file} {args}");
    var psi = new ProcessStartInfo(file, args) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
    using var p = Process.Start(psi)!;
    var stdout = p.StandardOutput.ReadToEnd();
    var stderr = p.StandardError.ReadToEnd();
    p.WaitForExit();
    if (stdout.Length > 0) Console.WriteLine(stdout.Trim());
    if (stderr.Length > 0) Console.WriteLine(stderr.Trim());
    if (p.ExitCode != 0 && !ignoreError) throw new Exception($"{file} {args} salió {p.ExitCode}: {stderr}");
}
