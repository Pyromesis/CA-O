using System.Diagnostics;
using System.IO.Compression;

public sealed class SetupForm : Form
{
    private readonly Label _title = new() { Text = "CA-O 2.0 — Instalador", Font = new Font("Segoe UI", 18, FontStyle.Bold), AutoSize = true };
    private readonly Label _subtitle = new() { Text = "Optimizador Windows 11 · Transacciones seguras · Benchmark honesto", ForeColor = Color.DimGray, AutoSize = true };
    private readonly Label _destLabel = new() { Text = @"Se instalará en: C:\Program Files\CA-O", AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
    private readonly CheckBox _desktopCheck = new() { Text = "Crear acceso directo en el escritorio", Checked = true, AutoSize = true };
    private readonly CheckBox _startMenuCheck = new() { Text = "Crear acceso en Menú Inicio", Checked = true, AutoSize = true };
    private readonly ProgressBar _progress = new() { Style = ProgressBarStyle.Continuous, Minimum = 0, Maximum = 100, Height = 22, Dock = DockStyle.Fill };
    private readonly Label _status = new() { Text = "Listo para instalar.", AutoSize = true, ForeColor = Color.DimGray };
    private readonly Button _installBtn = new() { Text = "Instalar", Height = 36, Width = 140, BackColor = Color.FromArgb(0, 120, 212), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
    private readonly Button _closeBtn = new() { Text = "Cerrar", Height = 36, Width = 100, Enabled = false };
    private readonly TextBox _logBox = new() { Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true, Height = 110, Font = new Font("Consolas", 8) };

    public SetupForm()
    {
        Text = "CA-O 2.0 Setup";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(620, 520);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        Icon = SystemIcons.Shield;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), RowCount = 8, ColumnCount = 1 };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var header = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        header.Controls.Add(new PictureBox { Image = SystemIcons.Shield.ToBitmap(), SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(48, 48), Margin = new Padding(0,0,12,0) });
        var headerText = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true };
        headerText.Controls.Add(_title);
        headerText.Controls.Add(_subtitle);
        header.Controls.Add(headerText);

        var destPanel = new Panel { Height = 70, Dock = DockStyle.Fill, BackColor = Color.FromArgb(243, 243, 243), Padding = new Padding(12) };
        destPanel.Controls.Add(_destLabel);
        var loc2 = new Label { Text = "Requiere permisos de administrador (UAC) · Servicio CAO.Privileged se registrará automáticamente", AutoSize = true, ForeColor = Color.DimGray, Font = new Font("Segoe UI", 8) };
        loc2.Location = new Point(12, 32);
        destPanel.Controls.Add(loc2);

        var checks = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true };
        checks.Controls.Add(_desktopCheck);
        checks.Controls.Add(_startMenuCheck);

        var btnPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, AutoSize = true };
        btnPanel.Controls.Add(_closeBtn);
        btnPanel.Controls.Add(_installBtn);
        _closeBtn.Click += (_, _) => Close();
        _installBtn.Click += async (_, _) => await RunInstallAsync();

        _progress.Value = 0;

        root.Controls.Add(header, 0, 0);
        root.Controls.Add(destPanel, 0, 1);
        root.Controls.Add(checks, 0, 2);
        root.Controls.Add(new Label { Text = "Progreso", AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold), Margin = new Padding(0,12,0,4) }, 0, 3);
        root.Controls.Add(_progress, 0, 4);
        root.Controls.Add(_status, 0, 5);
        root.Controls.Add(_logBox, 0, 6);
        root.Controls.Add(btnPanel, 0, 7);

        Controls.Add(root);

        // Mostrar destino exacto
        _destLabel.Text = $@"Se instalará en: {Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "CA-O")}";
    }

    private async Task RunInstallAsync()
    {
        _installBtn.Enabled = false;
        _status.Text = "Instalando...";
        _progress.Value = 5;
        Log("Iniciando instalación como administrador...");

        var installDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "CA-O");
        var serviceName = "CAO.Privileged";
        var logFile = Path.Combine(Path.GetTempPath(), "CA-O-Setup-Gui.log");
        try { File.AppendAllText(logFile, $"[{DateTime.Now:O}] GUI Setup iniciado\n"); } catch { }

        try
        {
            // Resolver payload
            var exeDir = AppContext.BaseDirectory;
            var payloadUi = Path.Combine(exeDir, "ui", "CA-O.UI.exe");
            var payloadService = Path.Combine(exeDir, "service", "CA-O.Privileged.exe");
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
            Log($"Origen UI: {payloadUi} {(File.Exists(payloadUi)?"OK":"NO")}");
            Log($"Origen Service: {payloadService} {(File.Exists(payloadService)?"OK":"NO")}");
            Log($"Destino: {installDir}");

            if (!File.Exists(payloadUi) || !File.Exists(payloadService))
            {
                Log("Payload no local — descargando desde GitHub Release v2.0.1...");
                _status.Text = "Descargando...";
                _progress.Value = 10;
                var zipUrl = "https://github.com/Pyromesis/CA-O/releases/download/v2.0.1/CA-O-2.0.0-20260826-1958-win-x64-selfcontained-singlefile.zip";
                var tmpZip = Path.Combine(Path.GetTempPath(), "CA-O-payload.zip");
                var tmpDir = Path.Combine(Path.GetTempPath(), "CA-O-payload-gui");
                using var http = new HttpClient();
                http.Timeout = TimeSpan.FromMinutes(10);
                var data = await http.GetByteArrayAsync(zipUrl);
                await File.WriteAllBytesAsync(tmpZip, data);
                Log($"Descargado {tmpZip} ({data.Length/1024/1024} MB)");
                _progress.Value = 40;
                if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, true);
                ZipFile.ExtractToDirectory(tmpZip, tmpDir);
                var foundUi = Directory.GetFiles(tmpDir, "CA-O.UI.exe", SearchOption.AllDirectories).FirstOrDefault() ?? throw new Exception("ZIP sin CA-O.UI.exe");
                var foundSvc = Directory.GetFiles(tmpDir, "CA-O.Privileged.exe", SearchOption.AllDirectories).FirstOrDefault() ?? throw new Exception("ZIP sin service");
                payloadUi = foundUi; payloadService = foundSvc;
                Log($"Payload extraído: {payloadUi}");
            }

            _status.Text = "Copiando archivos...";
            _progress.Value = 50;
            Log($"[1/5] Creando {installDir}");
            Directory.CreateDirectory(installDir);
            var destUi = Path.Combine(installDir, "ui");
            var destSvc = Path.Combine(installDir, "service");
            CopyDirectory(Path.GetDirectoryName(payloadUi)!, destUi, p => { _progress.Value = 50 + p/4; });
            CopyDirectory(Path.GetDirectoryName(payloadService)!, destSvc, null);
            var installedExe = Path.Combine(destUi, "CA-O.UI.exe");
            Log($"Instalado en {installedExe} ({new FileInfo(installedExe).Length/1024/1024} MB)");
            _progress.Value = 70;

            _status.Text = "Registrando servicio...";
            Log("[2/5] Servicio CAO.Privileged");
            Run("sc.exe", $"stop {serviceName}", true);
            await Task.Delay(600);
            Run("sc.exe", $"delete {serviceName}", true);
            await Task.Delay(600);
            Run("sc.exe", $"create {serviceName} binPath= \"{Path.Combine(destSvc, "CA-O.Privileged.exe")}\" start= demand DisplayName= \"CA-O Privileged Service\"");
            Run("sc.exe", $"failure {serviceName} reset= 86400 actions= restart/5000/restart/10000/reboot/60000");
            _progress.Value = 80;

            _status.Text = "Creando accesos directos...";
            Log("[3/5] Atajos");
            if (_startMenuCheck.Checked)
            {
                var start = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs", "CA-O.lnk");
                CreateShortcut(start, installedExe, "CA-O 2.0", Path.GetDirectoryName(installedExe)!);
                Log($"  Inicio: {start}");
            }
            if (_desktopCheck.Checked)
            {
                var common = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), "CA-O.lnk");
                var user = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "CA-O.lnk");
                bool ok = false;
                foreach (var d in new[] { common, user })
                {
                    try { CreateShortcut(d, installedExe, "CA-O 2.0", Path.GetDirectoryName(installedExe)!); Log($"  Escritorio: {d}"); ok = true; } catch (Exception ex) { Log($"  No {d}: {ex.Message}"); }
                }
                if (!ok) Log("  WARN: ningún atajo de escritorio creado");
            }
            _progress.Value = 85;

            _status.Text = "Iniciando servicio...";
            Log("[4/5] Iniciando servicio");
            Run("sc.exe", $"start {serviceName}", true);
            await Task.Delay(800);
            _progress.Value = 95;

            _status.Text = "Verificando...";
            Log("[5/5] Verificando");
            var qc = RunCapture("sc.exe", $"qc {serviceName}");
            Log(qc);

            _progress.Value = 100;
            _status.Text = "¡Instalación completada!";
            Log($"✓ Instalado en {installDir}");
            MessageBox.Show($"CA-O 2.0 instalado correctamente.\n\nCarpeta: {installDir}\nEjecutable: {installedExe}\nEscritorio: {(_desktopCheck.Checked? "Sí" : "No")}\n\nPulsa Aceptar para abrir CA-O.", "CA-O Setup", MessageBoxButtons.OK, MessageBoxIcon.Information);
            try { Process.Start(new ProcessStartInfo(installedExe) { UseShellExecute = true }); } catch { }
            _closeBtn.Enabled = true;
        }
        catch (Exception ex)
        {
            Log($"ERROR: {ex.Message}\n{ex.StackTrace}");
            _status.Text = "Error en la instalación";
            _progress.Value = 0;
            MessageBox.Show($"Error instalando CA-O:\n{ex.Message}\n\nLog: {Path.Combine(Path.GetTempPath(), "CA-O-Setup-Gui.log")}\nDestino: {installDir}", "CA-O Setup — Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _installBtn.Enabled = true;
        }
    }

    private void Log(string msg)
    {
        _logBox.AppendText(msg + Environment.NewLine);
        try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "CA-O-Setup-Gui.log"), msg + "\n"); } catch { }
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
            progress?.Invoke((int)((i+1)/(double)files.Length*100));
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
}
