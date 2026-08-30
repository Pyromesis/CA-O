using System.Diagnostics;
using Microsoft.Win32;

// Log a %TEMP% para que el usuario vea por qué se cerró
var logFile = Path.Combine(Path.GetTempPath(), "CA-O-Setup.log");
try { File.AppendAllText(logFile, $"\n[{DateTime.Now:O}] Setup iniciado\n"); } catch { }
void Log(string msg) { Console.WriteLine(msg); try { File.AppendAllText(logFile, msg + "\n"); } catch { } }

Console.WriteLine("CA-O 2.0 Setup — Instalador con UAC (requireAdministrator)");
Console.WriteLine("==========================================================");
Log($"Log: {logFile}");

// Verificar admin (manifest ya exige, pero doble check)
if (!IsAdmin())
{
    Console.ForegroundColor = ConsoleColor.Red;
    var msg = "ERROR: Debe ejecutar como Administrador. El .exe debe pedir UAC automáticamente.\nSi no vio el prompt UAC, clic derecho > Ejecutar como administrador.";
    Console.WriteLine(msg);
    Log(msg);
    Console.ResetColor();
    ShowMessage(msg, "CA-O Setup — Error", isError: true);
    Console.WriteLine("Presione cualquier tecla para salir...");
    Console.ReadKey();
    return 1;
}

static void ShowMessage(string text, string title, bool isError = false)
{
    try
    {
        // Intenta MessageBox via WScript.Shell Popup (no requiere WinForms)
        var shell = Type.GetTypeFromProgID("WScript.Shell");
        if (shell != null)
        {
            dynamic wsh = Activator.CreateInstance(shell)!;
            wsh.Popup(text, 0, title, isError ? 0x10 : 0x40);
            return;
        }
    }
    catch { }
    // Fallback: PowerShell popup
    try { System.Diagnostics.Process.Start(new ProcessStartInfo("powershell", $"-Command \"Add-Type -AssemblyName System.Windows.Forms; [System.Windows.Forms.MessageBox]::Show('{text.Replace("'", "''")}', '{title}')\"") { UseShellExecute = false, CreateNoWindow = true }); } catch { }
}

var installDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "CA-O");
var serviceName = "CAO.Privileged";
var exeDir = AppContext.BaseDirectory; // donde IExpress extrae
// IExpress extrae todo a %TEMP%\IXP000.TMP — buscamos payload ui/service relativos
var payloadUi = Path.Combine(exeDir, "ui", "CA-O.UI.exe");
var payloadService = Path.Combine(exeDir, "service", "CA-O.Privileged.exe");

// Fallback: si estamos en desarrollo y payload no está al lado, buscar en artifacts/release-singlefile
if (!File.Exists(payloadUi))
{
    var dev = Path.GetFullPath(Path.Combine(exeDir, "..", "..", "..", "artifacts", "release-singlefile", "ui", "CA-O.UI.exe"));
    if (File.Exists(dev)) payloadUi = dev;
}
if (!File.Exists(payloadService))
{
    var dev2 = Path.GetFullPath(Path.Combine(exeDir, "..", "..", "..", "artifacts", "release-singlefile", "service", "CA-O.Privileged.exe"));
    if (File.Exists(dev2)) payloadService = dev2;
}

Log($"Origen UI: {payloadUi} {(File.Exists(payloadUi) ? "OK" : "NO ENCONTRADO")}");
Log($"Origen Service: {payloadService} {(File.Exists(payloadService) ? "OK" : "NO ENCONTRADO")}");
Log($"Destino (donde se instala la app): {installDir}");

if (!File.Exists(payloadUi) || !File.Exists(payloadService))
{
    Console.WriteLine("Payload no local — descargando desde GitHub Release v2.1.2...");
    var zipUrl = "https://github.com/Pyromesis/CA-O/releases/download/v2.1.2/CA-O-2.1.2-win-x64.zip";
    var fallbackUrl = "https://github.com/Pyromesis/CA-O/releases/download/v2.1.1/CA-O-2.1.1-win-x64.zip";
    var tmpZip = Path.Combine(Path.GetTempPath(), "CA-O-payload.zip");
    var tmpDir = Path.Combine(Path.GetTempPath(), "CA-O-payload");
    try
    {
        using var http = new HttpClient();
        http.Timeout = TimeSpan.FromMinutes(10);
        Console.WriteLine($"  Descargando {zipUrl} ...");
        var data = http.GetByteArrayAsync(zipUrl).GetAwaiter().GetResult();
        File.WriteAllBytes(tmpZip, data);
        Console.WriteLine($"  Descargado {tmpZip} ({data.Length/1024/1024} MB)");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  Falló descarga primaria: {ex.Message}, probando fallback {fallbackUrl}");
        try
        {
            using var http2 = new HttpClient();
            var data2 = http2.GetByteArrayAsync(fallbackUrl).GetAwaiter().GetResult();
            File.WriteAllBytes(tmpZip, data2);
            Console.WriteLine($"  Descargado fallback {data2.Length/1024/1024} MB");
        }
        catch (Exception ex2)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"No se pudo descargar payload: {ex2.Message}");
            Console.WriteLine("Descarga manual: https://github.com/Pyromesis/CA-O/releases/tag/v2.0.0");
            Console.ResetColor();
            return 1;
        }
    }
    try
    {
        if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, true);
        System.IO.Compression.ZipFile.ExtractToDirectory(tmpZip, tmpDir);
        // Buscar ui/service dentro del zip (puede estar en release/ui o ui/)
        var foundUi = Directory.GetFiles(tmpDir, "CA-O.UI.exe", SearchOption.AllDirectories).FirstOrDefault();
        var foundSvc = Directory.GetFiles(tmpDir, "CA-O.Privileged.exe", SearchOption.AllDirectories).FirstOrDefault();
        if (foundUi == null || foundSvc == null) throw new Exception("ZIP sin CA-O.UI.exe / CA-O.Privileged.exe");
        payloadUi = foundUi;
        payloadService = foundSvc;
        Console.WriteLine($"  Payload extraído: {payloadUi}");
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Error extrayendo ZIP: {ex.Message}");
        Console.ResetColor();
        return 1;
    }
}

try
{
    Log("\n[1/5] Creando directorio de instalación...");
    Log($"  Carpeta de instalación: {installDir}");
    Directory.CreateDirectory(installDir);
    var destUi = Path.Combine(installDir, "ui");
    var destSvc = Path.Combine(installDir, "service");
    CopyDirectory(Path.GetDirectoryName(payloadUi)!, destUi);
    CopyDirectory(Path.GetDirectoryName(payloadService)!, destSvc);
    var installedExe = Path.Combine(destUi, "CA-O.UI.exe");
    Log($"  Instalado en {installedExe} ({new FileInfo(installedExe).Length / 1024 / 1024} MB)");
    Log($"  La app se ha descargado/instalado en: {installDir}");

    Log("[2/5] Registrando servicio privilegiado...");
    Run("sc.exe", $"stop {serviceName}", ignoreError: true);
    Thread.Sleep(800);
    Run("sc.exe", $"delete {serviceName}", ignoreError: true);
    Thread.Sleep(800);
    var svcExe = Path.Combine(destSvc, "CA-O.Privileged.exe");
    Run("sc.exe", $"create {serviceName} binPath= \"{svcExe}\" start= demand DisplayName= \"CA-O Privileged Service\"");
    Run("sc.exe", $"failure {serviceName} reset= 86400 actions= restart/5000/restart/10000/reboot/60000");
    Run("sc.exe", $"description {serviceName} \"CA-O 2.0 servicio privilegiado — IPC Named Pipe con ACL + replay guard\"");

    Log("[3/5] Creando accesos directos...");
    var startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs", "CA-O.lnk");
    CreateShortcut(startMenu, installedExe, "CA-O 2.0 — Optimizador Windows 11", Path.GetDirectoryName(installedExe)!);
    // Escritorio: intentar Common Desktop y como fallback User Desktop
    var commonDesktop = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), "CA-O.lnk");
    var userDesktop = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "CA-O.lnk");
    bool desktopOk = false;
    foreach (var desktop in new[] { commonDesktop, userDesktop })
    {
        try { CreateShortcut(desktop, installedExe, "CA-O 2.0", Path.GetDirectoryName(installedExe)!); Log($"  Atajo creado en escritorio: {desktop}"); desktopOk = true; }
        catch (Exception ex) { Log($"  No se pudo crear atajo en {desktop}: {ex.Message}"); }
    }
    if (!desktopOk) Log("  ADVERTENCIA: No se pudo crear atajo en ningún escritorio.");

    Log("[4/5] Registrando desinstalador...");
    var uninstallSrcFolder = Path.GetDirectoryName(FindUninstallerPayload(exeDir))!;
    var uninstallDestFolderSetup = Path.Combine(installDir, "uninstall");
    if (Directory.Exists(uninstallSrcFolder))
    {
        try
        {
            if (Directory.Exists(uninstallDestFolderSetup)) Directory.Delete(uninstallDestFolderSetup, true);
            CopyDirectory(uninstallSrcFolder, uninstallDestFolderSetup);
            Log($"  Desinstalador carpeta: {uninstallDestFolderSetup}");
        }
        catch (Exception ex) { Log($"  WARN no se pudo copiar carpeta desinstalador: {ex.Message}"); }
    }
    var uninstallSrc2 = FindUninstallerPayload(exeDir);
    var uninstallDestExe = Path.Combine(uninstallDestFolderSetup, "CA-O.Uninstaller.exe");
    if (!File.Exists(uninstallDestExe) && File.Exists(uninstallSrc2))
    {
        try { Directory.CreateDirectory(uninstallDestFolderSetup); File.Copy(uninstallSrc2, uninstallDestExe, true); } catch { }
    }
    // NO crear uninstall.exe en raiz - debe permanecer exclusivamente en uninstall\
    var legacyRoot = Path.Combine(installDir, "uninstall.exe");
    try { if (File.Exists(legacyRoot)) File.Delete(legacyRoot); } catch { }
    var legacyPs1 = Path.Combine(installDir, "uninstall.ps1");
    try { if (File.Exists(legacyPs1)) File.Delete(legacyPs1); } catch { }
    CreateUninstallRegistryEntry(installDir, uninstallDestExe, installedExe);

    Console.WriteLine("[4/5] Iniciando servicio...");
    Run("sc.exe", $"start {serviceName}", ignoreError: true);

    Console.WriteLine("[5/5] Verificando instalación...");
    var qc = RunCapture("sc.exe", $"qc {serviceName}");
    if (!qc.Contains("CA-O.Privileged.exe")) Console.WriteLine("  WARN: sc qc no contiene exe esperado: " + qc);

    Console.ForegroundColor = ConsoleColor.Green;
    var successMsg = $"✓ CA-O 2.0 instalado correctamente.\n\nCarpeta: {installDir}\nEjecutable: {installedExe}\nAtajos: Escritorio y Menú Inicio > CA-O\nServicio: {serviceName} (demand start)\n\nPara desinstalar: sc.exe stop {serviceName} && sc.exe delete {serviceName} && rmdir /s \"{installDir}\"";
    Console.WriteLine("\n" + successMsg);
    Log(successMsg);
    Console.ResetColor();
    ShowMessage(successMsg, "CA-O 2.0 — Instalación completada");
    Log("Presione cualquier tecla para lanzar CA-O...");
    Console.WriteLine("\nPresione cualquier tecla para lanzar CA-O...");
    Console.WriteLine($"Log guardado en: {logFile}");
    // Esperar 3 seg y lanzar
    Thread.Sleep(1200);
    try { Process.Start(new ProcessStartInfo(installedExe) { UseShellExecute = true }); } catch (Exception ex) { Log($"No se pudo lanzar CA-O: {ex.Message}"); }
    Console.WriteLine("Instalador permanecerá abierto 10 segundos...");
    Thread.Sleep(10000);
    return 0;
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    var err = $"ERROR instalando: {ex.Message}\n{ex.StackTrace}\nLog: {logFile}";
    Console.WriteLine(err);
    Log(err);
    Console.ResetColor();
    ShowMessage(err, "CA-O Setup — Error", isError: true);
    Console.WriteLine("Presione cualquier tecla para salir...");
    Console.ReadKey();
    return 1;
}

static bool IsAdmin()
{
    using var id = System.Security.Principal.WindowsIdentity.GetCurrent();
    return new System.Security.Principal.WindowsPrincipal(id).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
}
static void CopyDirectory(string src, string dst)
{
    Directory.CreateDirectory(dst);
    foreach (var file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
    {
        var rel = Path.GetRelativePath(src, file);
        var dest = Path.Combine(dst, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        File.Copy(file, dest, true);
    }
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
static string RunCapture(string file, string args)
{
    var psi = new ProcessStartInfo(file, args) { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true };
    using var p = Process.Start(psi)!;
    var stdout = p.StandardOutput.ReadToEnd();
    p.WaitForExit();
    return stdout;
}
static void CreateShortcut(string lnkPath, string target, string desc, string workDir)
{
    var dir = Path.GetDirectoryName(lnkPath)!;
    Directory.CreateDirectory(dir);
    var shell = Type.GetTypeFromProgID("WScript.Shell")!;
    dynamic wsh = Activator.CreateInstance(shell)!;
    var sc = wsh.CreateShortcut(lnkPath);
    sc.TargetPath = target;
    sc.WorkingDirectory = workDir;
    sc.Description = desc;
    sc.Save();
    Console.WriteLine($"  Atajo: {lnkPath}");
}
static string FindUninstallerPayload(string exeDir)
{
    var candidates = new[]
    {
        Path.Combine(exeDir, "uninstall", "CA-O.Uninstaller.exe"),
        Path.Combine(exeDir, "CA-O.Uninstaller.exe"),
        Path.Combine(AppContext.BaseDirectory, "uninstall", "CA-O.Uninstaller.exe"),
        Path.GetFullPath(Path.Combine(exeDir, "..", "..", "..", "artifacts", "release", "uninstall", "CA-O.Uninstaller.exe")),
    };
    foreach (var c in candidates)
    {
        var full = Path.GetFullPath(c);
        if (File.Exists(full)) return full;
    }
    return Path.Combine(exeDir, "uninstall", "CA-O.Uninstaller.exe");
}
static void CreateUninstallRegistryEntry(string installDir, string uninstallExe, string mainExe)
{
    try
    {
        var keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\CA-O";
        using var key = Registry.LocalMachine.CreateSubKey(keyPath);
        if (key == null) throw new InvalidOperationException("No se pudo crear clave de registro");
        key.SetValue("DisplayName", "CA-O 2.0", RegistryValueKind.String);
        key.SetValue("DisplayVersion", "2.0.1", RegistryValueKind.String);
        key.SetValue("Publisher", "CA-O", RegistryValueKind.String);
        key.SetValue("InstallLocation", installDir, RegistryValueKind.String);
        key.SetValue("DisplayIcon", mainExe, RegistryValueKind.String);
        key.SetValue("UninstallString", $"\"{uninstallExe}\"", RegistryValueKind.String);
        key.SetValue("QuietUninstallString", $"\"{uninstallExe}\" /S", RegistryValueKind.String);
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        key.SetValue("EstimatedSize", GetDirectorySizeKb(installDir), RegistryValueKind.DWord);
        key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"), RegistryValueKind.String);
        key.SetValue("HelpLink", "https://github.com/Pyromesis/CA-O", RegistryValueKind.String);
        Console.WriteLine($"  Registro desinstalador creado: HKLM\\{keyPath}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  WARN: No se pudo registrar desinstalador: {ex.Message}");
    }
}
static int GetDirectorySizeKb(string dir)
{
    try
    {
        long bytes = 0;
        foreach (var f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
            bytes += new FileInfo(f).Length;
        return (int)(bytes / 1024);
    }
    catch { return 0; }
}
