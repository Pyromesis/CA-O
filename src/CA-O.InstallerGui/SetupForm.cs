using System.Diagnostics;
using System.IO.Compression;

public sealed class SetupForm : Form
{
    private readonly ProgressBar _progress = new() { Style = ProgressBarStyle.Continuous, Minimum = 0, Maximum = 100, Height = 20, Dock = DockStyle.Fill };
    private readonly Label _status = new() { Text = "Listo para instalar.", AutoSize = true, ForeColor = Color.FromArgb(80,80,80), Font = new Font("Segoe UI", 9) };
    private readonly Button _installBtn = new() { Text = "Instalar", Height = 38, Width = 160, BackColor = Color.FromArgb(0, 103, 192), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
    private readonly Button _closeBtn = new() { Text = "Cerrar", Height = 36, Width = 110, Enabled = false, Font = new Font("Segoe UI", 9) };
    private readonly CheckBox _desktopCheck = new() { Text = "Crear acceso directo en el escritorio", Checked = true, AutoSize = true, Font = new Font("Segoe UI", 9) };
    private readonly CheckBox _startMenuCheck = new() { Text = "Crear acceso en el Menú Inicio", Checked = true, AutoSize = true, Font = new Font("Segoe UI", 9) };
    private readonly TextBox _logBox = new() { Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true, Font = new Font("Consolas", 8), BackColor = Color.FromArgb(248,248,248), Dock = DockStyle.Fill };

    public SetupForm()
    {
        Text = "CA-O 2.0 Setup";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(680, 620);
        MinimumSize = new Size(660, 600);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        BackColor = Color.White;
        Icon = SystemIcons.Shield;
        Font = new Font("Segoe UI", 9);
        _installBtn.FlatAppearance.BorderSize = 0;
        _closeBtn.FlatAppearance.BorderSize = 1;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(28, 24, 28, 20), RowCount = 6, ColumnCount = 1 };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // header
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // card
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // checks
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // progreso
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // log
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // botones

        // Header con icono y textos
        var header = new TableLayoutPanel { ColumnCount = 2, RowCount = 2, AutoSize = true, Dock = DockStyle.Fill };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var icon = new PictureBox { Image = SystemIcons.Shield.ToBitmap(), SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(52, 52), Margin = new Padding(0,0,12,0) };
        var title = new Label { Text = "CA-O 2.0 — Instalador", Font = new Font("Segoe UI", 17, FontStyle.Bold), ForeColor = Color.FromArgb(18,18,18), AutoSize = true, Margin = new Padding(0,2,0,0) };
        var subtitle = new Label { Text = "Optimizador para Windows 11 · Seguro · Transaccional · Con benchmark real", ForeColor = Color.FromArgb(95,95,95), Font = new Font("Segoe UI", 9), AutoSize = true, MaximumSize = new Size(560,0) };
        header.Controls.Add(icon, 0, 0);
        header.SetRowSpan(icon, 2);
        header.Controls.Add(title, 1, 0);
        header.Controls.Add(subtitle, 1, 1);

        // Card destino
        var card = new Panel { BackColor = Color.FromArgb(243, 244, 246), Padding = new Padding(16, 14, 16, 14), Margin = new Padding(0,18,0,0), AutoSize = true, Dock = DockStyle.Fill };
        card.Paint += (s,e) => { using var pen = new Pen(Color.FromArgb(225,225,225)); e.Graphics.DrawRectangle(pen, 0,0, card.Width-1, card.Height-1); };
        var destTitle = new Label { Text = @"Se instalará en:", Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = Color.FromArgb(30,30,30), AutoSize = true, Dock = DockStyle.Top };
        var destPath = new Label { Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "CA-O"), Font = new Font("Consolas", 9), ForeColor = Color.FromArgb(0,103,192), AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(0,6,0,0) };
        var destNote = new Label { Text = "Requiere permisos de administrador (UAC). El servicio CAO.Privileged se registrará automáticamente.", ForeColor = Color.FromArgb(110,110,110), Font = new Font("Segoe UI", 8), AutoSize = true, MaximumSize = new Size(600,0), Dock = DockStyle.Top, Padding = new Padding(0,8,0,0) };
        card.Controls.Add(destNote);
        card.Controls.Add(destPath);
        card.Controls.Add(destTitle);

        // Checks
        var checks = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = new Padding(0,16,0,0) };
        checks.Controls.Add(_desktopCheck);
        checks.Controls.Add(_startMenuCheck);

        // Progreso
        var progPanel = new TableLayoutPanel { RowCount = 3, ColumnCount = 1, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0,16,0,0) };
        var progTitle = new Label { Text = "Progreso", Font = new Font("Segoe UI", 9, FontStyle.Bold), AutoSize = true };
        var statusMargin = new Padding(0,6,0,0);
        _status.Margin = statusMargin;
        _progress.Margin = new Padding(0,6,0,0);
        progPanel.Controls.Add(progTitle, 0, 0);
        progPanel.Controls.Add(_progress, 0, 1);
        progPanel.Controls.Add(_status, 0, 2);

        // Log con borde
        var logPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0,8,0,0) };
        var logBorder = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(1) };
        logBorder.Paint += (s,e) => { using var pen = new Pen(Color.FromArgb(220,220,220)); e.Graphics.DrawRectangle(pen,0,0,logBorder.Width-1, logBorder.Height-1); };
        logBorder.Controls.Add(_logBox);
        logPanel.Controls.Add(logBorder);

        // Botones
        var btnPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, AutoSize = true, Margin = new Padding(0,16,0,0) };
        btnPanel.Controls.Add(_closeBtn);
        btnPanel.Controls.Add(_installBtn);
        _closeBtn.Click += (_, _) => Close();
        _installBtn.Click += async (_, _) => await RunInstallAsync();

        root.Controls.Add(header, 0, 0);
        root.Controls.Add(card, 0, 1);
        root.Controls.Add(checks, 0, 2);
        root.Controls.Add(progPanel, 0, 3);
        root.Controls.Add(logPanel, 0, 4);
        root.Controls.Add(btnPanel, 0, 5);

        Controls.Add(root);
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
                _status.Text = "Descargando (127 MB)...";
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
