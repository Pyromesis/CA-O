using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CAO.InstallerGui;

public sealed partial class MainWindow : Window
{
    private readonly CancellationTokenSource _installCts = new();
    private readonly HttpClient _httpClient;

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        _httpClient = new HttpClient() { Timeout = TimeSpan.FromMinutes(15) };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("CA-O-Installer/2.0.17");
        _httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github.v3+json");
        _httpClient.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip");
    }

private async Task LoadPreviousAnalysisAsync()
{
    await Task.CompletedTask;
}

internal void OnInstallClick(object sender, RoutedEventArgs e)
{
    _ = InstallAsync();
}

private async Task InstallAsync()
    {
        if (!IsAdmin())
        {
            await ShowErrorAsync("Se requieren permisos de administrador", "El instalador debe ejecutarse con permisos de administrador. Haga clic derecho y seleccione 'Ejecutar como administrador'.");
            return;
        }

        InstallButton.IsEnabled = false;
        CancelButton.IsEnabled = true;
        ProgressCard.Visibility = Visibility.Visible;
        LogCard.Visibility = Visibility.Visible;
        ProgressBar.Value = 0;
        ProgressStatusText.Text = "Iniciando instalacion...";
        ProgressDetailText.Visibility = Visibility.Visible;
        Log("Iniciando instalacion como administrador...");

        var installDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "CA-O");
        var serviceName = "CAO.Privileged";
        var logFile = Path.Combine(Path.GetTempPath(), "CA-O-Setup-Gui.log");

        try { File.AppendAllText(logFile, $"[{DateTime.Now:O}] GUI Setup iniciado\n"); } catch { }

        var installCts = CancellationTokenSource.CreateLinkedTokenSource(_installCts.Token);
        installCts.CancelAfter(TimeSpan.FromMinutes(10));

        try
        {
            // Detectar instalación existente para actualización
            bool isUpdate = Directory.Exists(installDir) || Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\CA-O") != null;
            if (isUpdate)
            {
                Log("Instalación existente detectada — se realizará actualización (se sobrescribirán archivos).");
                try
                {
                    var existingVer = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\CA-O")?.GetValue("DisplayVersion") as string ?? "desconocida";
                    Log($"Versión instalada: {existingVer}");
                }
                catch { }
                // Cerrar UI en ejecución para liberar archivos
                try
                {
                    foreach (var p in Process.GetProcessesByName("CA-O.UI"))
                    {
                        try { p.Kill(); } catch { }
                    }
                    await Task.Delay(500);
                }
                catch { }
            }

            // Single-file: AppContext.BaseDirectory es temp de extracción, usar ProcessPath real
            var baseDir = Path.GetDirectoryName(Environment.ProcessPath ?? AppContext.BaseDirectory) ?? AppContext.BaseDirectory;
            var exeDir = AppContext.BaseDirectory;
            var payloadUi = Path.Combine(baseDir, "ui", "CA-O.UI.exe");
            var payloadService = Path.Combine(baseDir, "service", "CA-O.Privileged.exe");
            // Fallbacks para layout de build local
            if (!File.Exists(payloadUi))
                payloadUi = Path.Combine(exeDir, "ui", "CA-O.UI.exe");
            if (!File.Exists(payloadUi))
            {
                var alt1 = Path.Combine(baseDir, "..", "ui", "CA-O.UI.exe");
                var alt2 = Path.Combine(baseDir, "artifacts", "release", "ui", "CA-O.UI.exe");
                var dev = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "artifacts", "release", "ui", "CA-O.UI.exe"));
                if (File.Exists(alt1)) payloadUi = alt1;
                else if (File.Exists(alt2)) payloadUi = alt2;
                else if (File.Exists(dev)) payloadUi = dev;
            }
            if (!File.Exists(payloadService))
                payloadService = Path.Combine(exeDir, "service", "CA-O.Privileged.exe");
            if (!File.Exists(payloadService))
            {
                var alt1 = Path.Combine(baseDir, "..", "service", "CA-O.Privileged.exe");
                var alt2 = Path.Combine(baseDir, "artifacts", "release", "service", "CA-O.Privileged.exe");
                var dev2 = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "artifacts", "release", "service", "CA-O.Privileged.exe"));
                if (File.Exists(alt1)) payloadService = alt1;
                else if (File.Exists(alt2)) payloadService = alt2;
                else if (File.Exists(dev2)) payloadService = dev2;
            }
            Log($"BaseDir: {baseDir} | ExeDir: {exeDir}");
            Log($"Origen UI: {payloadUi} {(File.Exists(payloadUi) ? "OK" : "NO")}");
            Log($"Origen Service: {payloadService} {(File.Exists(payloadService) ? "OK" : "NO")}");
            Log($"Destino: {installDir}");

            if (!File.Exists(payloadUi) || !File.Exists(payloadService))
            {
                Log("Payload local no encontrado, intentando descarga...");
                var downloaded = await DownloadPayloadAsync(installCts.Token);
                payloadUi = downloaded.uiExe;
                payloadService = downloaded.svcExe;
                Log($"Payload descargado -> UI: {payloadUi} | Service: {payloadService}");
            }

            // Si es actualización, detener servicio antes de tocar archivos (evita file in use CA-O.Core.dll)
            if (isUpdate)
            {
                UpdateProgress(10, "Deteniendo servicio existente...", "Deteniendo CAO.Privileged para actualizar");
                Run("sc.exe", $"stop {serviceName}", true);
                // Esperar hasta que se detenga (hasta 8s) y matar proceso si persiste
                for (int i = 0; i < 16; i++)
                {
                    await Task.Delay(500);
                    var q = RunCapture("sc.exe", $"query {serviceName}");
                    if (q.Contains("STOPPED") || q.Contains("does not exist")) break;
                }
                try
                {
                    foreach (var p in Process.GetProcessesByName("CA-O.Privileged"))
                    {
                        try { p.Kill(); } catch { }
                    }
                    await Task.Delay(500);
                }
                catch { }
                Run("sc.exe", $"delete {serviceName}", true);
                await Task.Delay(800);
            }

            UpdateProgress(10, "Preparando instalacion...", "Creando directorio de instalacion");
            Directory.CreateDirectory(installDir);
            var destUi = Path.Combine(installDir, "ui");
            var destSvc = Path.Combine(installDir, "service");
            // Si es actualización, limpiar destinos para no dejar archivos obsoletos (con reintentos por file lock)
            if (Directory.Exists(destUi) && isUpdate)
            {
                for (int r = 0; r < 3; r++)
                {
                    try { Directory.Delete(destUi, true); break; } catch (Exception ex) { Log($"  Intento {r+1} limpiar {destUi}: {ex.Message}"); await Task.Delay(700); }
                }
            }
            if (Directory.Exists(destSvc) && isUpdate)
            {
                for (int r = 0; r < 3; r++)
                {
                    try { Directory.Delete(destSvc, true); break; } catch (Exception ex) { Log($"  Intento {r+1} limpiar {destSvc}: {ex.Message}"); await Task.Delay(700); }
                }
            }
            Directory.CreateDirectory(destUi);
            Directory.CreateDirectory(destSvc);
            // Copia con reintento si archivo bloqueado
            async Task CopyWithRetry(string src, string dst)
            {
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    try { CopyDirectory(src, dst, attempt == 0 ? p => UpdateProgress(10 + p / 5, "Copiando archivos de la aplicacion...", null) : null); return; }
                    catch (IOException ex) when (ex.Message.Contains("being used"))
                    {
                        Log($"  Reintento copia {attempt+1}: {ex.Message}");
                        await Task.Delay(800);
                    }
                }
                CopyDirectory(src, dst, null);
            }
            await CopyWithRetry(Path.GetDirectoryName(payloadUi)!, destUi);
            await CopyWithRetry(Path.GetDirectoryName(payloadService)!, destSvc);
            var installedExe = Path.Combine(destUi, "CA-O.UI.exe");
            Log($"Instalado en {installedExe} ({new FileInfo(installedExe).Length / 1024 / 1024} MB)");

            UpdateProgress(60, "Registrando servicio...", "Registrando servicio privilegiado CAO.Privileged");
            if (!isUpdate)
            {
                Run("sc.exe", $"stop {serviceName}", true);
                await Task.Delay(600);
                Run("sc.exe", $"delete {serviceName}", true);
                await Task.Delay(600);
            }
            Run("sc.exe", $"create {serviceName} binPath= \"{Path.Combine(destSvc, "CA-O.Privileged.exe")}\" start= demand DisplayName= \"CA-O Privileged Service\"");
            Run("sc.exe", $"failure {serviceName} reset= 86400 actions= restart/5000/restart/10000/reboot/60000");
            Run("sc.exe", $"description {serviceName} \"CA-O 2.0 servicio privilegiado - IPC Named Pipe con ACL + replay guard\"");

            UpdateProgress(80, "Creando accesos directos...", "Creando atajos en Menu Inicio y Escritorio");
            if (StartMenuShortcutCheck.IsChecked == true)
            {
                var start = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs", "CA-O.lnk");
                CreateShortcut(start, installedExe, "CA-O 2.0", Path.GetDirectoryName(installedExe)!);
                Log($"  Inicio: {start}");
            }
            if (DesktopShortcutCheck.IsChecked == true)
            {
                var common = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), "CA-O.lnk");
                var user = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "CA-O.lnk");
                bool ok = false;
                foreach (var d in new[] { common, user })
                {
                    try { CreateShortcut(d, installedExe, "CA-O 2.0", Path.GetDirectoryName(installedExe)!); Log($"  Escritorio: {d}"); ok = true; } catch (Exception ex) { Log($"  No {d}: {ex.Message}"); }
                }
                if (!ok) Log("  WARN: ningun atajo de escritorio creado");
            }

            UpdateProgress(82, "Registrando desinstalador...", "Creando entrada en Programas y características");
            // Copiar carpeta completa del desinstalador GUI (430 archivos) para que uninstall.exe tenga sus DLLs
            var uninstallSrcFolder = Path.GetDirectoryName(FindUninstallerPayload(baseDir, exeDir))!;
            var uninstallDestFolder = Path.Combine(installDir, "uninstall");
            if (Directory.Exists(uninstallSrcFolder))
            {
                try
                {
                    if (Directory.Exists(uninstallDestFolder)) Directory.Delete(uninstallDestFolder, true);
                    CopyDirectory(uninstallSrcFolder, uninstallDestFolder, null);
                    Log($"  Desinstalador carpeta: {uninstallDestFolder}");
                }
                catch (Exception ex) { Log($"  WARN no se pudo copiar carpeta desinstalador: {ex.Message}"); }
            }
            var uninstallSrc = FindUninstallerPayload(baseDir, exeDir);
            var uninstallDest = Path.Combine(uninstallDestFolder, "CA-O.Uninstaller.exe");
            // NO crear uninstall.exe en raiz - debe permanecer exclusivamente en uninstall\ por spec 1
            var uninstallRoot2 = Path.Combine(installDir, "uninstall.exe");
            try { if (File.Exists(uninstallRoot2)) File.Delete(uninstallRoot2); } catch { }
            var uninstallPs1Root = Path.Combine(installDir, "uninstall.ps1");
            try { if (File.Exists(uninstallPs1Root)) File.Delete(uninstallPs1Root); } catch { }
            if (File.Exists(uninstallSrc))
            {
                Log($"  Desinstalador: {uninstallDest} ({new FileInfo(uninstallDest).Length / 1024} KB)");
            }
            else
            {
                Log($"  WARN: desinstalador no encontrado en {uninstallSrc}");
            }
            CreateUninstallRegistryEntry(installDir, uninstallDest, installedExe);

            UpdateProgress(90, "Iniciando servicio...", "Iniciando servicio privilegiado");
            Run("sc.exe", $"start {serviceName}", true);
            await Task.Delay(800);

            UpdateProgress(95, "Verificando instalacion...", "Comprobando servicio y archivos");
            var qc = RunCapture("sc.exe", $"qc {serviceName}");
            Log(qc);

            UpdateProgress(100, "Instalacion completada", null);
            Log($"Instalado en {installDir}");
            await ShowSuccessDialogAsync(installedExe);
            try { Process.Start(new ProcessStartInfo(installedExe) { UseShellExecute = true }); } catch { }
            Close();
        }
        catch (OperationCanceledException)
        {
            Log("Instalacion cancelada por el usuario");
            await ShowErrorAsync("Instalacion cancelada", "La instalacion fue cancelada por el usuario.");
        }
        catch (Exception ex)
        {
            Log($"ERROR: {ex.Message}\n{ex.StackTrace}");
            await ShowErrorAsync("Error en la instalacion", $"Error instalando CA-O:\n{ex.Message}\n\nLog: {Path.Combine(Path.GetTempPath(), "CA-O-Setup-Gui.log")}\nDestino: {installDir}");
        }
        finally
        {
            InstallButton.IsEnabled = true;
            CancelButton.IsEnabled = false;
        }
    }

internal void OnCancelClick(object sender, RoutedEventArgs e)
{
    _installCts.Cancel();
    Log("Cancelando instalacion...");
}

    private async Task<(string uiExe, string svcExe)> DownloadPayloadAsync(CancellationToken ct)
    {
        UpdateProgress(5, "Descargando payload (394 MB)...", "Descargando desde GitHub Release v2.1.0");
        var zipUrl = "https://github.com/Pyromesis/CA-O/releases/download/v2.1.0/CA-O-2.1.0-win-x64.zip";
        var fallbackUrl = "https://github.com/Pyromesis/CA-O/releases/latest/download/CA-O-2.1.0-win-x64.zip";
        var fallbackOld = "https://github.com/Pyromesis/CA-O/releases/download/v2.0.17/CA-O-2.0.17-win-x64.zip";
        var tmpZip = Path.Combine(Path.GetTempPath(), "CA-O-payload.zip");
        var tmpDir = Path.Combine(Path.GetTempPath(), "CA-O-payload-gui");

        // Descarga por streaming para 300+ MB (evita ByteArray truncado/OOM y EndOfCentralDirectory)
        async Task DownloadToFileAsync(string url, string dest, CancellationToken cts)
        {
            using var resp = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts);
            resp.EnsureSuccessStatusCode();
            var total = resp.Content.Headers.ContentLength;
            await using var net = await resp.Content.ReadAsStreamAsync(cts);
            await using var file = File.Create(dest);
            var buffer = new byte[81920];
            long read = 0;
            int n;
            while ((n = await net.ReadAsync(buffer.AsMemory(0, buffer.Length), cts)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, n), cts);
                read += n;
                if (total.HasValue && read % (10 * 1024 * 1024) < 81920) Log($"  Descargando... {read / 1024 / 1024} / {total.Value / 1024 / 1024} MB");
            }
            Log($"Descargado {dest} ({new FileInfo(dest).Length / 1024 / 1024} MB)");
        }

        if (File.Exists(tmpZip)) try { File.Delete(tmpZip); } catch { }
        // Intentar descarga con fallbacks y API latest si todo 404 (releases antiguos borrados)
        async Task<bool> TryDownload(string url)
        {
            try { await DownloadToFileAsync(url, tmpZip, ct); return true; }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound) { Log($"404 en {url}"); return false; }
        }
        if (!await TryDownload(zipUrl))
        {
            Log($"Probando {fallbackUrl}...");
            if (!await TryDownload(fallbackUrl))
            {
                Log($"Probando {fallbackOld}...");
                if (!await TryDownload(fallbackOld))
                {
                    // Último recurso: consultar API GitHub para tag latest y construir URL
                    try
                    {
                        Log("Consultando API GitHub para latest tag...");
                        using var apiResp = await _httpClient.GetAsync("https://api.github.com/repos/Pyromesis/CA-O/releases/latest", ct);
                        apiResp.EnsureSuccessStatusCode();
                        var json = await apiResp.Content.ReadAsStringAsync(ct);
                        var tag = System.Text.Json.JsonDocument.Parse(json).RootElement.GetProperty("tag_name").GetString();
                        if (!string.IsNullOrWhiteSpace(tag))
                        {
                            var apiUrl = $"https://github.com/Pyromesis/CA-O/releases/download/{tag}/CA-O-{tag}-win-x64.zip";
                            // fallback a nombre genérico si el tag no sigue patrón CA-O-2.x
                            var altUrl = $"https://github.com/Pyromesis/CA-O/releases/download/{tag}/CA-O-2.0.15-win-x64.zip";
                            Log($"Probando API latest {tag} -> {apiUrl}");
                            if (!await TryDownload(apiUrl))
                            {
                                Log($"Probando alt {altUrl}");
                                await DownloadToFileAsync(altUrl, tmpZip, ct);
                            }
                        }
                        else throw new InvalidOperationException("Tag vacío en API");
                    }
                    catch (Exception ex2)
                    {
                        throw new InvalidOperationException($"No se pudo descargar payload (todas las URLs 404). Último error: {ex2.Message}. Descarga manualmente CA-O-2.0.15-win-x64.zip o usa el ZIP completo offline.", ex2);
                    }
                }
            }
        }
        if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, true);
        ZipFile.ExtractToDirectory(tmpZip, tmpDir);
        var foundUi = Directory.GetFiles(tmpDir, "CA-O.UI.exe", SearchOption.AllDirectories).FirstOrDefault() ?? throw new InvalidOperationException("ZIP sin CA-O.UI.exe");
        var foundSvc = Directory.GetFiles(tmpDir, "CA-O.Privileged.exe", SearchOption.AllDirectories).FirstOrDefault() ?? throw new InvalidOperationException("ZIP sin service");
        Log($"Payload extraido: {foundUi}");
        return (foundUi, foundSvc);
    }

    internal void UpdateProgress(int value, string status, string? detail = null)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ProgressBar.Value = value;
            ProgressStatusText.Text = status;
            if (detail != null)
            {
                ProgressDetailText.Text = detail;
                ProgressDetailText.Visibility = Visibility.Visible;
            }
        });
    }

    internal void Log(string msg)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {msg}";
        DispatcherQueue.TryEnqueue(() =>
        {
            LogTextBox.Text += line + "\n";
            var scrollViewer = GetScrollViewer(LogTextBox);
            scrollViewer?.ScrollToVerticalOffset(LogTextBox.ActualHeight);
        });
        try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "CA-O-Setup-Gui.log"), line + "\n"); } catch { }
    }

    private ScrollViewer? GetScrollViewer(DependencyObject element)
    {
        if (element is ScrollViewer sv) return sv;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
        {
            var child = VisualTreeHelper.GetChild(element, i);
            var result = GetScrollViewer(child);
            if (result != null) return result;
        }
        return null;
    }

    private static void CopyDirectory(string src, string dst, Action<int>? progress)
    {
        var files = Directory.GetFiles(src, "*", SearchOption.AllDirectories);
        Directory.CreateDirectory(dst);
        for (int i = 0; i < files.Length; i++)
        {
            var rel = Path.GetRelativePath(src, files[i]);
            var dest = Path.Combine(dst, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(files[i], dest, true);
            progress?.Invoke((int)((i + 1) / (double)files.Length * 100));
        }
    }

    private static void Run(string file, string args, bool ignoreError = false)
    {
        var psi = new ProcessStartInfo(file, args) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        using var p = Process.Start(psi)!;
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();
        if (p.ExitCode != 0 && !ignoreError) throw new InvalidOperationException($"{file} {args} -> {p.ExitCode}: {stderr} {stdout}");
    }

    private static string RunCapture(string file, string args)
    {
        var psi = new ProcessStartInfo(file, args) { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true };
        using var p = Process.Start(psi)!;
        var stdout = p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        return stdout;
    }

    private static void CreateShortcut(string lnk, string target, string desc, string workDir)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(lnk)!);
        var shell = Type.GetTypeFromProgID("WScript.Shell")!;
        dynamic wsh = Activator.CreateInstance(shell)!;
        var sc = wsh.CreateShortcut(lnk);
        sc.TargetPath = target;
        sc.WorkingDirectory = workDir;
        sc.Description = desc;
        sc.IconLocation = target;
        sc.Save();
    }

    internal async Task<ContentDialogResult> ShowSuccessDialogAsync(string installedExe)
    {
        var dialog = new ContentDialog
        {
            Title = "CA-O 2.0 instalado correctamente",
            Content = new TextBlock
            {
                Text = $"CA-O 2.0 se ha instalado correctamente.\n\nCarpeta: {Path.GetDirectoryName(installedExe)!}\nEjecutable: {Path.GetFileName(installedExe)}\nEscritorio: {(DesktopShortcutCheck.IsChecked == true ? "Si" : "No")}\n\nPulsa Aceptar para abrir CA-O.",
                TextWrapping = TextWrapping.Wrap,
            },
            PrimaryButtonText = "Abrir CA-O",
            CloseButtonText = "Cerrar",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };
        return await dialog.ShowAsync();
    }

    internal async Task ShowErrorAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
            },
            CloseButtonText = "Aceptar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private static string FindUninstallerPayload(string baseDir, string exeDir)
    {
        var candidates = new[]
        {
            Path.Combine(baseDir, "uninstall", "CA-O.Uninstaller.exe"),
            Path.Combine(baseDir, "CA-O.Uninstaller.exe"),
            Path.Combine(exeDir, "uninstall", "CA-O.Uninstaller.exe"),
            Path.Combine(baseDir, "..", "uninstall", "CA-O.Uninstaller.exe"),
            Path.Combine(baseDir, "artifacts", "release", "uninstall", "CA-O.Uninstaller.exe"),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "artifacts", "release", "uninstall", "CA-O.Uninstaller.exe")),
        };
        foreach (var c in candidates)
        {
            var full = Path.GetFullPath(c);
            if (File.Exists(full)) return full;
        }
        return Path.Combine(baseDir, "uninstall", "CA-O.Uninstaller.exe");
    }

    private static void CreateUninstallRegistryEntry(string installDir, string uninstallExe, string mainExe)
    {
        try
        {
            var keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\CA-O";
            using var key = Registry.LocalMachine.CreateSubKey(keyPath);
            if (key == null) throw new InvalidOperationException("No se pudo crear clave de registro");
            key.SetValue("DisplayName", "CA-O 2.0", RegistryValueKind.String);
            var version = typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "2.1.0";
            // Normalizar a 3 partes
            if (version == "2.0.0.0") version = "2.1.0";
            key.SetValue("DisplayVersion", version, RegistryValueKind.String);
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
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"No se pudo registrar desinstalador: {ex.Message}", ex);
        }
    }

    private static int GetDirectorySizeKb(string dir)
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

    internal static bool IsAdmin()
    {
        using var id = System.Security.Principal.WindowsIdentity.GetCurrent();
        return new System.Security.Principal.WindowsPrincipal(id).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }
}