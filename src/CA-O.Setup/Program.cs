using System.Diagnostics;
using Microsoft.Win32;
using System.Windows.Forms;

// Instalador CA-O de un solo exe: ventana con progreso (sin consola).
// Descarga el paquete, deriva al setup gráfico bonito y sale; si no hay
// GUI, instala por aquí con la misma ventana de progreso.
var productVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "2.1.23";
var logFile = Path.Combine(Path.GetTempPath(), "CA-O-Setup.log");
try { File.AppendAllText(logFile, $"\n[{DateTime.Now:O}] Setup iniciado\n"); } catch { }

if (!IsAdmin())
{
    MessageBox.Show("Debe ejecutar como Administrador.\nSi no vio el aviso de Windows (UAC), clic derecho > Ejecutar como administrador.",
        "CA-O Setup — Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    return 1;
}

using var cts = new CancellationTokenSource();
using var ui = new SetupForm(productVersion, cts);
var uiThread = new Thread(() => Application.Run(ui));
uiThread.SetApartmentState(ApartmentState.STA);
uiThread.IsBackground = true;
uiThread.Start();
for (var i = 0; i < 200 && !ui.Ready && uiThread.IsAlive; i++) await Task.Delay(100);

int exitCode;
try
{
    exitCode = await RunInstallAsync(ui, productVersion, logFile, cts.Token);
}
catch (OperationCanceledException)
{
    ui.Report(0, "Instalación cancelada por el usuario.", "Cancelado por el usuario.", done: false, failed: false);
    exitCode = 2;
}
catch (Exception ex)
{
    var err = $"ERROR instalando: {ex.Message}\nLog: {logFile}";
    try { File.AppendAllText(logFile, err + "\n" + ex.StackTrace + "\n"); } catch { }
    ui.Report(0, "Error en la instalación.", err, done: true, failed: true);
    exitCode = 1;
    await WaitForCloseAsync(ui);
    return exitCode;
}

if (exitCode == 0)
{
    // Cierre automático tras unos segundos salvo que haya fallado algo.
    await Task.Delay(TimeSpan.FromSeconds(6));
    ui.BeginClose();
}
else
{
    await WaitForCloseAsync(ui);
}
return exitCode;

static async Task WaitForCloseAsync(SetupForm ui)
{
    while (!ui.CloseRequested) await Task.Delay(250);
}

static bool IsAdmin()
{
    using var id = System.Security.Principal.WindowsIdentity.GetCurrent();
    return new System.Security.Principal.WindowsPrincipal(id).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
}

static async Task<int> RunInstallAsync(SetupForm ui, string productVersion, string logFile, CancellationToken ct)
{
    void Log(string msg)
    {
        try { File.AppendAllText(logFile, msg + "\n"); } catch { }
        ui.Report(null, null, msg);
    }
    Log($"Log: {logFile}");

    var installDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "CA-O");
    var serviceName = "CAO.Privileged";
    var exeDir = AppContext.BaseDirectory;
    var payloadUi = Path.Combine(exeDir, "ui", "CA-O.UI.exe");
    var payloadService = Path.Combine(exeDir, "service", "CA-O.Privileged.exe");

    if (!File.Exists(payloadUi) || !File.Exists(payloadService))
    {
        ui.Report(2, "Descargando paquete (~450 MB)...", null);
        Log($"Payload no local — descargando v{productVersion}...");
        var tmpZip = Path.Combine(Path.GetTempPath(), "CA-O-payload.zip");
        var tmpDir = Path.Combine(Path.GetTempPath(), "CA-O-payload");
        var candidates = new List<string>
        {
            $"https://github.com/Pyromesis/CA-O/releases/download/v{productVersion}/CA-O-{productVersion}-win-x64.zip",
        };
        var latestAsset = GetLatestFullAssetUrl(msg => ui.Report(null, null, msg));
        if (latestAsset != null) candidates.Add(latestAsset);
        Exception? lastError = null;
        var downloaded = false;
        foreach (var url in candidates)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                ui.Report(3, "Descargando paquete (~450 MB)...", $"Descargando {url}");
                await DownloadStreamingAsync(url, tmpZip, (mb, total) =>
                    ui.Report(total > 0 ? (int)(mb * 55.0 / total) + 2 : null,
                        "Descargando paquete (~450 MB)...",
                        total > 0 ? $"{mb} / {total} MB" : $"{mb} MB descargados"), ct);
                Log($"Descargado {tmpZip} ({new FileInfo(tmpZip).Length / 1024 / 1024} MB)");
                downloaded = true;
                break;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                Log($"Falló descarga: {ex.Message}");
                lastError = ex;
            }
        }
        if (!downloaded)
            throw new InvalidOperationException($"No se pudo descargar el paquete: {lastError?.Message}\nDescarga manual: https://github.com/Pyromesis/CA-O/releases/latest");
        ct.ThrowIfCancellationRequested();
        ui.Report(58, "Extrayendo paquete...", "Extrayendo ~1700 archivos, puede tardar minutos...");
        await Task.Run(() =>
        {
            if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, true);
            System.IO.Compression.ZipFile.ExtractToDirectory(tmpZip, tmpDir);
        }, ct);
        var foundUi = Directory.GetFiles(tmpDir, "CA-O.UI.exe", SearchOption.AllDirectories).FirstOrDefault();
        var foundSvc = Directory.GetFiles(tmpDir, "CA-O.Privileged.exe", SearchOption.AllDirectories).FirstOrDefault();
        if (foundUi == null || foundSvc == null) throw new InvalidOperationException("ZIP sin CA-O.UI.exe / CA-O.Privileged.exe");
        payloadUi = foundUi;
        payloadService = foundSvc;
        Log($"Payload extraído: {payloadUi}");
    }

    Log($"Origen UI: {payloadUi} | Origen Service: {payloadService} | Destino: {installDir}");

    // Handoff al setup gráfico bonito: deriva y termina.
    {
        var uiDir = Path.GetDirectoryName(payloadUi);
        var payloadRoot = uiDir != null ? Path.GetDirectoryName(uiDir) : null;
        var guiExe = payloadRoot != null ? Path.Combine(payloadRoot, "gui-installer", "CA-O.InstallerGui.exe") : null;
        if (guiExe != null && File.Exists(guiExe))
        {
            ui.Report(70, "Abriendo instalador gráfico...", "Quitando bloqueos y abriendo el instalador...");
            UnblockTree(payloadRoot!);
            try
            {
                Process.Start(new ProcessStartInfo(guiExe)
                {
                    UseShellExecute = true,
                    Arguments = $"--auto-update --payload-dir=\"{payloadRoot}\"",
                    WorkingDirectory = Path.GetDirectoryName(guiExe)!,
                });
                Log("Handoff a InstallerGui; descargador termina.");
                ui.Report(100, "Instalador gráfico en marcha.", "Continúa en la ventana del instalador.", done: true, failed: false);
                return 0;
            }
            catch (Exception ex)
            {
                Log($"Handoff GUI falló, fallback consola: {ex.Message}");
                ui.Report(null, "Abriendo instalador gráfico falló, sigo por aquí...", null);
            }
        }
        else
        {
            Log("Sin instalador gráfico en payload; instalación por esta ventana.");
        }
    }

    // Instalación por esta ventana (fallback cuando no hay GUI).
    var isUpdateSetup = Directory.Exists(installDir);
    if (isUpdateSetup)
    {
        ui.Report(62, "Deteniendo versión anterior...", "Cerrando app y deteniendo servicio...");
        Log("Actualización: cerrando app y deteniendo servicio...");
        foreach (var p in Process.GetProcessesByName("CA-O.UI"))
        {
            try { p.Kill(); } catch { }
        }
        Run("sc.exe", $"stop {serviceName}", ignoreError: true);
        for (int i = 0; i < 16; i++)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(500, ct);
            var q = RunCapture("sc.exe", $"query {serviceName}");
            if (q.Contains("STOPPED") || q.Contains("does not exist")) break;
        }
        foreach (var p in Process.GetProcessesByName("CA-O.Privileged"))
        {
            try { p.Kill(); } catch { }
        }
        await Task.Delay(800, ct);
    }

    ui.Report(66, "Copiando archivos...", "Copiando aplicación y servicio...");
    Log("Copiando archivos...");
    Directory.CreateDirectory(installDir);
    var destUi = Path.Combine(installDir, "ui");
    var destSvc = Path.Combine(installDir, "service");
    await CopyDirectoryRetryAsync(Path.GetDirectoryName(payloadUi)!, destUi,
        p => ui.Report(66 + p * 20 / 100, "Copiando archivos...", null), ct);
    await CopyDirectoryRetryAsync(Path.GetDirectoryName(payloadService)!, destSvc, null, ct);
    var installedExe = Path.Combine(destUi, "CA-O.UI.exe");
    Log($"Instalado en {installedExe}");

    ui.Report(88, "Registrando servicio...", "Creando CAO.Privileged...");
    Run("sc.exe", $"stop {serviceName}", ignoreError: true);
    await Task.Delay(600, ct);
    Run("sc.exe", $"delete {serviceName}", ignoreError: true);
    await Task.Delay(600, ct);
    var svcExe = Path.Combine(destSvc, "CA-O.Privileged.exe");
    Run("sc.exe", $"create {serviceName} binPath= \"{svcExe}\" start= demand DisplayName= \"CA-O Privileged Service\"");
    Run("sc.exe", $"failure {serviceName} reset= 86400 actions= restart/5000/restart/10000/reboot/60000");
    Run("sc.exe", $"description {serviceName} \"CA-O {productVersion} servicio privilegiado\"");

    ui.Report(92, "Creando accesos directos...", "Menú Inicio y Escritorio...");
    var startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs", "CA-O.lnk");
    CreateShortcut(startMenu, installedExe, $"CA-O {productVersion}", Path.GetDirectoryName(installedExe)!);
    var commonDesktop = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), "CA-O.lnk");
    var userDesktop = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "CA-O.lnk");
    bool desktopOk = false;
    foreach (var desktop in new[] { commonDesktop, userDesktop })
    {
        try { CreateShortcut(desktop, installedExe, $"CA-O {productVersion}", Path.GetDirectoryName(installedExe)!); desktopOk = true; break; }
        catch (Exception ex) { Log($"No {desktop}: {ex.Message}"); }
    }
    if (!desktopOk) Log("WARN: ningún atajo de escritorio creado");

    ui.Report(95, "Copiando desinstalador...", "Registrando en Programas y características...");
    var uninstallSrcFolder = Path.GetDirectoryName(FindUninstallerPayload(exeDir))!;
    var uninstallDestFolderSetup = Path.Combine(installDir, "uninstall");
    if (Directory.Exists(uninstallSrcFolder))
    {
        try
        {
            if (Directory.Exists(uninstallDestFolderSetup)) Directory.Delete(uninstallDestFolderSetup, true);
            await CopyDirectoryRetryAsync(uninstallSrcFolder, uninstallDestFolderSetup, null, ct);
        }
        catch (Exception ex) { Log($"WARN desinstalador: {ex.Message}"); }
    }
    var uninstallSrc2 = FindUninstallerPayload(exeDir);
    var uninstallDestExe = Path.Combine(uninstallDestFolderSetup, "CA-O.Uninstaller.exe");
    if (!File.Exists(uninstallDestExe) && File.Exists(uninstallSrc2))
    {
        try { Directory.CreateDirectory(uninstallDestFolderSetup); File.Copy(uninstallSrc2, uninstallDestExe, true); } catch { }
    }
    CreateUninstallRegistryEntry(installDir, uninstallDestExe, installedExe, productVersion);

    ui.Report(97, "Iniciando servicio...", "Arrancando CAO.Privileged...");
    Run("sc.exe", $"start {serviceName}", ignoreError: true);
    await Task.Delay(800, ct);

    var qc = RunCapture("sc.exe", $"qc {serviceName}");
    if (!qc.Contains("CA-O.Privileged.exe")) Log("WARN: sc qc sin exe esperado");

    ui.Report(100, "Instalación completada.", $"CA-O {productVersion} listo en {installDir}. Abriendo la app...", done: true, failed: false);
    Log($"Instalado en {installDir}");
    try { Process.Start(new ProcessStartInfo(installedExe) { UseShellExecute = true }); }
    catch (Exception ex) { Log($"No se pudo abrir CA-O: {ex.Message}"); }
    return 0;
}

// Descarga por streaming con progreso en MB (nunca el ZIP entero en RAM).
static async Task DownloadStreamingAsync(string url, string dest, Action<long, long> progress, CancellationToken ct)
{
    using var http = new HttpClient() { Timeout = TimeSpan.FromMinutes(30) };
    http.DefaultRequestHeaders.UserAgent.ParseAdd("CA-O-Setup");
    using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
    resp.EnsureSuccessStatusCode();
    var total = resp.Content.Headers.ContentLength;
    await using var net = await resp.Content.ReadAsStreamAsync(ct);
    await using var file = File.Create(dest);
    var buffer = new byte[81920];
    long read = 0, lastShown = -1;
    int n;
    while ((n = await net.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
    {
        await file.WriteAsync(buffer.AsMemory(0, n), ct);
        read += n;
        var mb = read / 1024 / 1024;
        if (mb - lastShown >= 10)
        {
            lastShown = mb;
            progress(mb, (total ?? 0) / 1024 / 1024);
        }
    }
}

// Último recurso: el asset completo del release latest vía API de GitHub.
static string? GetLatestFullAssetUrl(Action<string> log)
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
        log($"No se pudo consultar latest: {ex.Message}");
        return null;
    }
}

// Quita Mark-of-the-Web heredado del ZIP. Best-effort, nunca lanza.
static void UnblockTree(string dir)
{
    try
    {
        if (!Directory.Exists(dir)) return;
        foreach (var file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
        {
            try { File.Delete(file + ":Zone.Identifier"); }
            catch { }
        }
    }
    catch { }
}

static async Task CopyDirectoryRetryAsync(string src, string dst, Action<int>? progress, CancellationToken ct)
{
    for (int attempt = 0; ; attempt++)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            Directory.CreateDirectory(dst);
            var files = Directory.GetFiles(src, "*", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                ct.ThrowIfCancellationRequested();
                var rel = Path.GetRelativePath(src, files[i]);
                var dest = Path.Combine(dst, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(files[i], dest, true);
                if (i % 25 == 0) progress?.Invoke((int)((i + 1) / (double)files.Length * 100));
            }
            progress?.Invoke(100);
            return;
        }
        catch (OperationCanceledException) { throw; }
        catch (IOException ex) when (attempt < 3)
        {
            await Task.Delay(1500, ct);
            _ = ex;
        }
    }
}

static void Run(string file, string args, bool ignoreError = false)
{
    var psi = new ProcessStartInfo(file, args) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
    using var p = Process.Start(psi)!;
    var stdout = p.StandardOutput.ReadToEnd();
    var stderr = p.StandardError.ReadToEnd();
    p.WaitForExit();
    if (p.ExitCode != 0 && !ignoreError) throw new InvalidOperationException($"{file} {args} salió {p.ExitCode}: {stderr}");
    _ = stdout;
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
        key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"), RegistryValueKind.String);
        key.SetValue("HelpLink", "https://github.com/Pyromesis/CA-O", RegistryValueKind.String);
    }
    catch (Exception ex)
    {
        throw new InvalidOperationException($"No se pudo registrar desinstalador: {ex.Message}", ex);
    }
}

/// <summary>Ventana del instalador: progreso, estado, registro y botón Cerrar/Cancelar.</summary>
sealed class SetupForm : Form
{
    private readonly ProgressBar _bar = new() { Minimum = 0, Maximum = 100, Height = 26, Dock = DockStyle.Top };
    private readonly Label _status = new() { AutoSize = false, Height = 26, Dock = DockStyle.Top, TextAlign = System.Drawing.ContentAlignment.MiddleLeft };
    private readonly TextBox _log = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, Font = new System.Drawing.Font("Consolas", 8.5f) };
    private readonly Button _close = new() { Text = "Cancelar", Dock = DockStyle.Bottom, Height = 34 };
    private readonly CancellationTokenSource _cts;
    private volatile bool _running = true;

    public bool Ready { get; private set; }
    public bool CloseRequested { get; private set; }

    public SetupForm(string version, CancellationTokenSource cts)
    {
        _cts = cts;
        Text = $"CA-O {version} — Instalador";
        Width = 580;
        Height = 430;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        try { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        var title = new Label
        {
            Text = $"Instalando CA-O {version}...",
            Dock = DockStyle.Top,
            Height = 34,
            Font = new System.Drawing.Font(Font.FontFamily, 12f, System.Drawing.FontStyle.Bold),
            TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
            Padding = new Padding(10, 6, 10, 0),
        };
        var pad = new Panel { Dock = DockStyle.Top, Height = 10 };
        var logLabel = new Label { Text = "Detalle:", Dock = DockStyle.Top, Height = 20, Padding = new Padding(10, 0, 0, 0) };
        Controls.Add(_log);
        Controls.Add(logLabel);
        Controls.Add(_bar);
        Controls.Add(_status);
        Controls.Add(pad);
        Controls.Add(title);
        Controls.Add(_close);
        _close.Click += (_, _) =>
        {
            if (_running)
            {
                try { _cts.Cancel(); } catch { }
                _close.Enabled = false;
                _close.Text = "Cancelando...";
            }
            else
            {
                CloseRequested = true;
                Close();
            }
        };
        FormClosing += (_, e) =>
        {
            if (_running)
            {
                try { _cts.Cancel(); } catch { }
                CloseRequested = true;
            }
            else CloseRequested = true;
        };
        Shown += (_, _) => Ready = true;
    }

    public void BeginClose()
    {
        try
        {
            if (InvokeRequired) BeginInvoke(new Action(BeginClose));
            else Close();
        }
        catch { CloseRequested = true; }
    }

    /// <summary>Reporta progreso. pct null = no tocar barra.</summary>
    public void Report(int? pct, string? status, string? logLine, bool done = false, bool failed = false)
    {
        void Apply()
        {
            if (pct.HasValue) _bar.Value = Math.Clamp(pct.Value, 0, 100);
            if (status != null) _status.Text = status;
            if (logLine != null) _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {logLine}\r\n");
            if (done)
            {
                _running = false;
                _bar.Value = failed ? _bar.Value : 100;
                _close.Text = "Cerrar";
                _close.Enabled = true;
            }
        }
        try
        {
            if (InvokeRequired) Invoke(new Action(Apply));
            else Apply();
        }
        catch { }
    }
}
