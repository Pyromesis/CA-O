using System.Diagnostics;
using Microsoft.Win32;

namespace CAO.Uninstaller;

internal static class UninstallService
{
    private static readonly string LogFile = Path.Combine(Path.GetTempPath(), "CA-O-Uninstall.log");

    public static void SilentUninstall()
    {
        try { File.AppendAllText(LogFile, $"\n[{DateTime.Now:O}] Silent uninstall iniciado\n"); } catch { }
        DoUninstall(deleteHistory: false, log: s => { try { File.AppendAllText(LogFile, s + "\n"); } catch { } });
    }

    public static void DoUninstall(bool deleteHistory, Action<string> log)
    {
        var installDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "CA-O");
        var serviceName = "CAO.Privileged";

        log("[1/5] Deteniendo servicio...");
        Run("sc.exe", $"stop {serviceName}", true, log);
        Thread.Sleep(1000);

        log("[2/5] Eliminando servicio...");
        Run("sc.exe", $"delete {serviceName}", true, log);
        Thread.Sleep(800);

        log("[3/5] Eliminando accesos directos...");
        foreach (var lnk in new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs", "CA-O.lnk"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), "CA-O.lnk"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "CA-O.lnk"),
        })
        {
            try { if (File.Exists(lnk)) File.Delete(lnk); log($"  Eliminado: {lnk}"); } catch (Exception ex) { log($"  No se pudo eliminar {lnk}: {ex.Message}"); }
        }
        var startMenuFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs", "CA-O");
        if (Directory.Exists(startMenuFolder))
        {
            try { Directory.Delete(startMenuFolder, true); } catch { }
        }

        log("[4/5] Eliminando entrada de Programas instalados...");
        try { Registry.LocalMachine.DeleteSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\CA-O", false); log("  Registro eliminado (64-bit)"); } catch { }
        try { Registry.LocalMachine.DeleteSubKey(@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\CA-O", false); log("  Registro eliminado (32-bit)"); } catch { }

        log("[5/5] Eliminando archivos...");
        if (Directory.Exists(installDir))
        {
            try
            {
                var self = Environment.ProcessPath ?? "";
                if (self.StartsWith(installDir, StringComparison.OrdinalIgnoreCase))
                {
                    var batch = Path.Combine(Path.GetTempPath(), "CA-O-Delete.bat");
                    File.WriteAllText(batch, $"@echo off\r\ntimeout /t 2 >nul\r\n rmdir /s /q \"{installDir}\"\r\ndel \"%~f0\"\r\n");
                    Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{batch}\"") { CreateNoWindow = true, UseShellExecute = false });
                    log($"  Programado borrado diferido de {installDir}");
                }
                else
                {
                    Directory.Delete(installDir, true);
                    log($"  Eliminado: {installDir}");
                }
            }
            catch (Exception ex) { log($"  No se pudo eliminar {installDir}: {ex.Message}"); }
        }

        if (deleteHistory)
        {
            var data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CA-O");
            try { if (Directory.Exists(data)) Directory.Delete(data, true); log($"  Historial eliminado: {data}"); } catch (Exception ex) { log($"  No se pudo borrar historial: {ex.Message}"); }
        }
        else
        {
            log("  Historial conservado en %ProgramData%\\CA-O");
        }

        log("Desinstalación completada.");
    }

    private static void Run(string file, string args, bool ignoreError, Action<string> log)
    {
        log($"  > {file} {args}");
        var psi = new ProcessStartInfo(file, args) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        using var p = Process.Start(psi)!;
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();
        if (stdout.Length > 0) log(stdout.Trim());
        if (stderr.Length > 0) log(stderr.Trim());
        if (p.ExitCode != 0 && !ignoreError) throw new InvalidOperationException($"{file} {args} salió {p.ExitCode}: {stderr}");
    }

    public static bool IsAdmin()
    {
        using var id = System.Security.Principal.WindowsIdentity.GetCurrent();
        return new System.Security.Principal.WindowsPrincipal(id).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }
}
