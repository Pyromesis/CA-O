using System.Diagnostics;
using Microsoft.Win32;

// Log a %TEMP% para que el usuario vea por qué se cerró
var logFile = Path.Combine(Path.GetTempPath(), "CA-O-Setup.log");
try { File.AppendAllText(logFile, $"\n[{DateTime.Now:O}] Setup iniciado\n"); } catch { }
void Log(string msg) { Console.WriteLine(msg); try { File.AppendAllText(logFile, msg + "\n"); } catch { } }

var productVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "2.1.5";
Console.WriteLine($"CA-O {productVersion} Setup — Instalador con UAC (requireAdministrator)");
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
    Console.WriteLine($"Payload no local — descargando desde GitHub Release v{productVersion}...");
    var tmpZip = Path.Combine(Path.GetTempPath(), "CA-O-payload.zip");
    var tmpDir = Path.Combine(Path.GetTempPath(), "CA-O-payload");
    var candidates = new List<string>
    {
        $"https://github.com/Pyromesis/CA-O/releases/download/v{productVersion}/CA-O-{productVersion}-win-x64.zip",
    };
    var latestAsset = GetLatestFullAssetUrl();
    if (latestAsset != null) candidates.Add(latestAsset);
    Exception? lastError = null;
    var downloaded = false;
    foreach (var url in candidates)
    {
        try
        {
            Console.WriteLine($"  Descargando {url} ...");
            DownloadStreaming(url, tmpZip);
            Console.WriteLine($"  Descargado {tmpZip} ({new FileInfo(tmpZip).Length/1024/1024} MB)");
            downloaded = true;
            break;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Falló: {ex.Message}");
            lastError = ex;
        }
    }
    if (!downloaded)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"No se pudo descargar payload: {lastError?.Message}");
        Console.WriteLine($"Descarga manual: https://github.com/Pyromesis/CA-O/releases/latest");
        Console.ResetColor();
        return 1;
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
    Run("sc.exe", $"description {serviceName} \"CA-O {productVersion} servicio privilegiado — IPC Named Pipe con ACL + replay guard\"");

    Log("[3/5] Creando accesos directos...");
    var startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs", "CA-O.lnk");
    CreateShortcut(startMenu, installedExe, $"CA-O {productVersion} — Optimizador Windows 11", Path.GetDirectoryName(installedExe)!);
    // Escritorio: intentar Common Desktop y como fallback User Desktop
    var commonDesktop = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), "CA-O.lnk");
    var userDesktop = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "CA-O.lnk");
    bool desktopOk = false;
    foreach (var desktop in new[] { commonDesktop, userDesktop })
    {
        try { CreateShortcut(desktop, installedExe, $"CA-O {productVersion}", Path.GetDirectoryName(installedExe)!); Log($"  Atajo creado en escritorio: {desktop}"); desktopOk = true; }
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
    CreateUninstallRegistryEntry(installDir, uninstallDestExe, installedExe, productVersion);

    Console.WriteLine("[4/5] Iniciando servicio...");
    Run("sc.exe", $"start {serviceName}", ignoreError: true);

    Console.WriteLine("[5/5] Verificando instalación...");
    var qc = RunCapture("sc.exe", $"qc {serviceName}");
    if (!qc.Contains("CA-O.Privileged.exe")) Console.WriteLine("  WARN: sc qc no contiene exe esperado: " + qc);

    Console.ForegroundColor = ConsoleColor.Green;
    var successMsg = $"✓ CA-O {productVersion} instalado correctamente.\n\nCarpeta: {installDir}\nEjecutable: {installedExe}\nAtajos: Escritorio y Menú Inicio > CA-O\nServicio: {serviceName} (demand start)\n\nPara desinstalar: sc.exe stop {serviceName} && sc.exe delete {serviceName} && rmdir /s \"{installDir}\"";
    Console.WriteLine("\n" + successMsg);
    Log(successMsg);
    Console.ResetColor();
    ShowMessage(successMsg, $"CA-O {productVersion} — Instalación completada");
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
// Descarga por streaming con progreso (nunca carga el ZIP entero en memoria:
// 452 MB en RAM reventaban con OutOfMemory/EndOfCentralDirectory).
static void DownloadStreaming(string url, string dest)
{
    using var http = new HttpClient() { Timeout = TimeSpan.FromMinutes(30) };
    http.DefaultRequestHeaders.UserAgent.ParseAdd("CA-O-Setup");
    using var resp = http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
    resp.EnsureSuccessStatusCode();
    var total = resp.Content.Headers.ContentLength;
    using var net = resp.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
    using var file = File.Create(dest);
    var buffer = new byte[81920];
    long read = 0, lastShown = -1;
    int n;
    while ((n = net.Read(buffer, 0, buffer.Length)) > 0)
    {
        file.Write(buffer, 0, n);
        read += n;
        var mb = read / 1024 / 1024;
        if (mb - lastShown >= 10)
        {
            lastShown = mb;
            Console.WriteLine(total.HasValue
                ? $"  ... {mb} / {total.Value / 1024 / 1024} MB"
                : $"  ... {mb} MB");
        }
    }
}
// Último recurso: el asset completo del release latest vía API de GitHub.
static string? GetLatestFullAssetUrl()
{
    try
    {
        using var http = new HttpClient() { Timeout = TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("CA-O-Setup");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github.v3+json");
        var json = http.GetStringAsync("https://api.github.com/repos/Pyromesis/CA-O/releases/latest").GetAwaiter().GetResult();
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        foreach (var asset in doc.RootElement.GetProperty("assets").EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            var dl = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() ?? "" : "";
            if (name.EndsWith("-win-x64.zip", StringComparison.OrdinalIgnoreCase) &&
                !name.StartsWith("CA-O-Setup-GUI", StringComparison.OrdinalIgnoreCase) &&
                Uri.TryCreate(dl, UriKind.Absolute, out _))
                return dl;
        }
        return null;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  No se pudo consultar latest: {ex.Message}");
        return null;
    }
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
static void CreateUninstallRegistryEntry(string installDir, string uninstallExe, string mainExe, string productVersion)
{
    try
    {
        var keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\CA-O";
        using var key = Registry.LocalMachine.CreateSubKey(keyPath);
        if (key == null) throw new InvalidOperationException("No se pudo crear clave de registro");
        key.SetValue("DisplayName", $"CA-O {productVersion}", RegistryValueKind.String);
        key.SetValue("DisplayVersion", productVersion, RegistryValueKind.String);
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
