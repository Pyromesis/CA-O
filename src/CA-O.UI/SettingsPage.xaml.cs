using System.Diagnostics;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CAO.UI.Helpers;

namespace CAO.UI.Pages;

/// <summary>Settings (spec 80): expert mode with warning, theme and language.</summary>
public sealed partial class SettingsPage : Page
{
    private readonly ViewModels.SettingsViewModel _vm;
    private ViewModels.UiState? _uiState;
    private bool _autoCheckAttempted;
    private bool _updateInProgress;

    public SettingsPage()
    {
        try { InitializeComponent(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"SettingsPage InitializeComponent failed: {ex}"); throw; }

        try
        {
            foreach (var language in Localizer.SupportedLanguages)
            {
                LanguageBox.Items.Add(language);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SettingsPage language load failed: {ex}");
        }

        try
        {
            _vm = AppHost.Resolve<ViewModels.SettingsViewModel>();
            DataContext = _vm;
            ExpertSwitch.IsOn = _vm.ExpertMode;
            ExpertWarnBar.IsOpen = _vm.ExpertMode;
            Select(ThemeBox, _vm.Theme);
            Select(LanguageBox, _vm.Language);
            
            _uiState = AppHost.Resolve<ViewModels.UiState>();
            // El estado verificado vive en UiState (memoria de sesión): la página lo refleja sin pedir otro clic.
            SyncViewModelFromSharedState();
            RenderServiceState();
            
            VersionsText.Text = $"CA-O UI {CAO.Shared.AppVersion.Semantic} · Protocolo v{CAO.Shared.IPC.IpcProtocol.Version} · Settings: {CAO.Shared.CaOPaths.SettingsFile}";
            bool isAdmin = IsAdmin();
            PrivilegeText.Text = isAdmin
                ? "UI elevada (administrador, requireAdministrator) — toda escritura via Named Pipe tipado con ACL, nonce, expiración 30s y anti-replay. Ver docs/SECURITY.md."
                : "UI sin privilegios — toda escritura via Named Pipe tipado con ACL, nonce, expiración 30s y anti-replay. Ver docs/SECURITY.md.";
            _vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ViewModels.SettingsViewModel.ServiceStatus) ||
                    e.PropertyName == nameof(ViewModels.SettingsViewModel.ServiceCheckedUtc))
                    DispatcherQueue.TryEnqueue(RenderServiceState);
            };
            _uiState.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ViewModels.UiState.ServiceStatus) ||
                    e.PropertyName == nameof(ViewModels.UiState.ServiceCheckedUtc))
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        SyncViewModelFromSharedState();
                        RenderServiceState();
                    });
                if (e.PropertyName == nameof(ViewModels.UiState.UpdateAvailable) ||
                    e.PropertyName == nameof(ViewModels.UiState.LatestVersion))
                    DispatcherQueue.TryEnqueue(RenderUpdateState);
            };

            _uiState.LanguageChanged += (_, __) => DispatcherQueue.TryEnqueue(ApplyTexts);
            
            DispatcherQueue.TryEnqueue(ApplyTexts);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SettingsPage initialization failed: {ex}");
            throw;
        }
    }

    private void ApplyTexts()
    {
        try
        {
            if (ThemeLabel != null) ThemeLabel.Text = Localizer.Get("settings.theme") ?? "Tema";
            if (LanguageLabel != null) LanguageLabel.Text = Localizer.Get("settings.language") ?? "Idioma";
            if (ExpertSwitch != null) ExpertSwitch.Header = Localizer.Get("settings.expertMode") ?? "Modo Expert";
            if (ExpertWarnBar != null) ExpertWarnBar.Message = Localizer.Get("optimize.expertWarning") ?? "Modo Expert habilitado";
            if (ServiceInstallButton != null) ServiceInstallButton.Content = Localizer.Get("settings.serviceInstall") ?? "Instalar ahora";
            if (ServiceExplanationBar != null)
            {
                ServiceExplanationBar.Title = Localizer.Get("settings.serviceExplanationTitle") ?? "¿Por qué instalar el servicio privilegiado?";
                ServiceExplanationBar.Message = Localizer.Get("settings.serviceExplanationMessage") ?? "";
            }
            try { LocalizationHelper.LocalizeTree(this.Content as DependencyObject ?? this); } catch { }
            RenderServiceState();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ApplyTexts failed: {ex}");
        }
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ApplyTexts();
        SyncViewModelFromSharedState();
        RenderServiceState();
        RenderUpdateState();
        Helpers.UiAnimations.PlayEntrance(PageContent);
        // Si aún no hay verificación en memoria, comprobar una sola vez al entrar (sin pedir clic).
        _ = AutoCheckServiceIfNeededAsync();
    }

    private void SyncViewModelFromSharedState()
    {
        if (_vm is null || _uiState is null) return;
        if (_vm.ServiceStatus != _uiState.ServiceStatus)
            _vm.ServiceStatus = _uiState.ServiceStatus;
        if (_vm.ServiceCheckedUtc != _uiState.ServiceCheckedUtc)
            _vm.ServiceCheckedUtc = _uiState.ServiceCheckedUtc;
    }

    private static bool IsConnected(string? status) =>
        status is "connected" or "conectado";

    private static bool NeedsVerification(string? status) =>
        status is null or "unknown" or "unavailable" or "no disponible";

    private void RenderServiceState()
    {
        if (ServiceStatusText is null || ServiceDetailText is null) return;
        var status = _uiState?.ServiceStatus ?? _vm?.ServiceStatus ?? "unknown";
        var checkedUtc = _uiState?.ServiceCheckedUtc ?? _vm?.ServiceCheckedUtc;
        var lastCheck = checkedUtc.HasValue
            ? Localizer.Format("settings.serviceLastChecked", checkedUtc.Value.ToLocalTime().ToString("g"))
            : string.Empty;

        if (IsConnected(status))
        {
            var serviceVersion = _uiState?.ServiceVersion ?? string.Empty;
            if (Helpers.ServiceVersionProbe.IsStale(serviceVersion))
            {
                var appVersion = Helpers.AppUpdater.CurrentVersion;
                ServiceStatusText.Text = $"⚠ Servicio desactualizado ({serviceVersion})";
                ServiceDetailText.Text = $"La app es {appVersion} pero el servicio instalado es {serviceVersion}: los últimos fixes no están activos. Pulsa 'Instalar ahora' para actualizarlo.";
                if (ServiceInstallButton != null) ServiceInstallButton.Visibility = Visibility.Visible;
                if (ServiceCheckButton != null) ServiceCheckButton.Content = Localizer.Get("settings.serviceCheck");
            }
            else
            {
                ServiceStatusText.Text = "✓ Servicio activo y conectado";
                ServiceDetailText.Text = string.IsNullOrEmpty(lastCheck)
                    ? "El servicio privilegiado está funcionando correctamente."
                    : $"El servicio privilegiado está funcionando correctamente. {lastCheck}";
                if (ServiceInstallButton != null) ServiceInstallButton.Visibility = Visibility.Collapsed;
                if (ServiceCheckButton != null) ServiceCheckButton.Content = Localizer.Get("settings.serviceStateOk");
            }
        }
        else
        {
            ServiceStatusText.Text = "⚠ Servicio no disponible";
            ServiceDetailText.Text = string.IsNullOrEmpty(lastCheck)
                ? "Haz clic en 'Instalar ahora' para configurar el servicio."
                : $"Haz clic en 'Instalar ahora' para configurar el servicio. {lastCheck}";
            if (ServiceInstallButton != null) ServiceInstallButton.Visibility = Visibility.Visible;
            if (ServiceCheckButton != null) ServiceCheckButton.Content = Localizer.Get("settings.serviceCheck");
        }
    }

    private async Task AutoCheckServiceIfNeededAsync()
    {
        try
        {
            if (_autoCheckAttempted || _vm is null || _uiState is null) return;
            if (_vm.IsCheckingService) return;
            if (!NeedsVerification(_uiState.ServiceStatus)) return;
            if (_uiState.ServiceCheckedUtc.HasValue &&
                DateTime.UtcNow - _uiState.ServiceCheckedUtc.Value < TimeSpan.FromMinutes(5))
                return;
            _autoCheckAttempted = true;
            await RefreshServiceStatusAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AutoCheckServiceIfNeededAsync failed: {ex}");
        }
    }

    private async Task RefreshServiceStatusAsync()
    {
        if (_vm is null) return;
        ServiceRing.IsActive = true;
        ServiceStatusText.Text = "Verificando servicio...";
        ServiceDetailText.Text = "";
        try
        {
            // Tras un reinicio el servicio (start=demand) queda detenido: el ping
            // falla y parece "perdido para siempre". Intento best-effort de
            // levantarlo antes de reportar, la UI corre elevada (requireAdministrator).
            if (IsAdmin())
            {
                try { TryStartServiceBestEffort(); } catch { }
                if (!IsServiceRunning())
                    await Task.Delay(800);
            }
            await _vm.CheckServiceCommand.ExecuteAsync(null);
            SyncViewModelFromSharedState();
            RenderServiceState();
            // Si sigue no disponible pero el binario existe, guiar hacia reparación.
            if (!IsConnected(_vm.ServiceStatus) && ResolveInstalledServiceExe() is not null)
            {
                ServiceDetailText.Text = string.IsNullOrWhiteSpace(ServiceDetailText.Text)
                    ? "El servicio está detenido o sin registrar. Pulsa 'Instalar ahora' para repararlo (no necesitas el repo)."
                    : ServiceDetailText.Text + " Pulsa 'Instalar ahora' para repararlo.";
            }
        }
        catch (Exception ex)
        {
            ServiceStatusText.Text = "⚠ No se pudo verificar el servicio";
            ServiceDetailText.Text = $"Error: {ex.Message}";
            if (ServiceInstallButton != null) ServiceInstallButton.Visibility = Visibility.Visible;
        }
        finally { ServiceRing.IsActive = false; }
    }

    private static string ScExe() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "sc.exe");
    private static string PowerShellExe() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe");

    /// <summary>
    /// Best-effort, sin lanzar: si el servicio existe pero está detenido, arrancarlo.
    /// No crea nada; la creación/reparación vive en el flujo de instalación.
    /// </summary>
    private static void TryStartServiceBestEffort()
    {
        try
        {
            using var q = Process.Start(new ProcessStartInfo(ScExe(), "query CAO.Privileged")
            {
                UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true,
            });
            if (q is null) return;
            var out1 = q.StandardOutput.ReadToEnd();
            q.WaitForExit(5000);
            if (out1.Contains("does not exist", StringComparison.OrdinalIgnoreCase)) return;
            if (out1.Contains("RUNNING", StringComparison.OrdinalIgnoreCase)) return;
            try
            {
                using var s = Process.Start(new ProcessStartInfo(ScExe(), "start CAO.Privileged")
                {
                    UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true,
                });
                s?.WaitForExit(8000);
            }
            catch { }
        }
        catch { }
    }

    private static bool IsServiceRunning()
    {
        try
        {
            using var q = Process.Start(new ProcessStartInfo(ScExe(), "query CAO.Privileged")
            {
                UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true,
            });
            if (q is null) return false;
            var o = q.StandardOutput.ReadToEnd();
            q.WaitForExit(5000);
            return o.Contains("RUNNING", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private void OnExpertToggled(object sender, RoutedEventArgs e)
    {
        if (_vm is null || ExpertSwitch is null) return;
        _vm.ExpertMode = ExpertSwitch.IsOn;
        ExpertWarnBar.IsOpen = ExpertSwitch.IsOn;
    }

    private void OnThemeSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_vm is null || ThemeBox is null) return;
        if (ThemeBox.SelectedItem is ComboBoxItem { Tag: string theme })
        {
            _vm.Theme = theme;
        }
    }

    private void OnLanguageSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_vm is null || LanguageBox is null) return;
        if (LanguageBox.SelectedItem is string language && language != _vm.Language)
        {
            _vm.Language = language;
        }
    }

    private async void OnServiceCheckClick(object sender, RoutedEventArgs e)
    {
        _autoCheckAttempted = true;
        await RefreshServiceStatusAsync();
    }

    private async void OnServiceInstallClick(object sender, RoutedEventArgs e)
    {
        ServiceRing.IsActive = true;
        ServiceStatusText.Text = "Instalando servicio...";
        ServiceDetailText.Text = "Se requieren permisos administrativos.";
        ServiceInstallButton.IsEnabled = false;
        ServiceCheckButton.IsEnabled = false;

        string? wrapperScript = null;
        try
        {
            // Crear script wrapper para instalación con auto-reinicio
            wrapperScript = await CreateInstallWrapperScriptAsync();
            if (string.IsNullOrWhiteSpace(wrapperScript))
            {
                var expectedExe = ResolveInstalledServiceExe()
                    ?? CAO.Shared.Constants.BuildConstants.GetServiceExecutablePath();
                ServiceStatusText.Text = "❌ Error: No se pudo crear el script de instalación";
                ServiceDetailText.Text = $"No se encontró ni scripts/install-privileged-service.ps1 (modo dev) ni el binario instalado ({expectedExe}). Reinstala la app desde el instalador.";
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = PowerShellExe(),
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{wrapperScript}\"",
                UseShellExecute = true,
                Verb = IsAdmin() ? "" : "runas",
                CreateNoWindow = false,
            };

            ServiceStatusText.Text = "Abriendo ventana de instalación...";
            ServiceDetailText.Text = "Sigue los pasos en la ventana que se abrirá.";

            var process = Process.Start(startInfo);
            if (process is null)
            {
                ServiceStatusText.Text = "❌ No se pudo elevar permisos";
                ServiceDetailText.Text = "Intenta nuevamente o ejecuta manualmente.";
                return;
            }

            ServiceStatusText.Text = "⏳ Instalación en progreso...";
            ServiceDetailText.Text = "Por favor espera a que termine la instalación.";

            // Esperar a que termine la instalación
            await Task.Run(() => process.WaitForExit(60000)); // 60 segundos máximo
            
            await Task.Delay(2000); // Esperar a que el servicio se registre

            // Verificar si se instaló correctamente
            await VerifyServiceInstalledAsync();

            if (IsConnected(_vm.ServiceStatus))
            {
                ServiceStatusText.Text = "✓ ¡Instalación completada!";
                ServiceDetailText.Text = "El servicio está listo. Reiniciando aplicación...";
                ServiceInstallButton.Visibility = Visibility.Collapsed;

                // Auto-reiniciar la app
                await Task.Delay(1500);
                RestartApplication();
            }
        }
        catch (Exception ex)
        {
            ServiceStatusText.Text = "❌ Error en la instalación";
            ServiceDetailText.Text = ex.Message;
        }
        finally
        {
            // El wrapper vive en %TEMP% con el binario embebido: borrarlo siempre
            // para no dejar scripts Bypass firmables por terceros.
            if (!string.IsNullOrWhiteSpace(wrapperScript))
            {
                try { File.Delete(wrapperScript); } catch { }
            }
            ServiceRing.IsActive = false;
            ServiceInstallButton.IsEnabled = true;
            ServiceCheckButton.IsEnabled = true;
        }
    }

    private void RenderUpdateState()
    {
        if (UpdateStatusText is null || _uiState is null) return;
        var current = Helpers.AppUpdater.CurrentVersion;
        if (_uiState.UpdateAvailable && !string.IsNullOrWhiteSpace(_uiState.LatestVersion))
        {
            UpdateStatusText.Text = $"Nueva versión disponible: {_uiState.LatestVersion} (instalada: {current})";
            if (UpdateDownloadButton != null) UpdateDownloadButton.Visibility = Visibility.Visible;
        }
        else
        {
            UpdateStatusText.Text = $"Versión instalada: {current} (al día)";
            if (UpdateDownloadButton != null && UpdateProgressBar.Visibility != Visibility.Visible)
                UpdateDownloadButton.Visibility = Visibility.Collapsed;
        }
    }

    private async void OnUpdateCheckClick(object sender, RoutedEventArgs e)
    {
        if (_uiState is null) return;
        UpdateCheckButton.IsEnabled = false;
        UpdateDetailText.Text = "Buscando actualizaciones...";
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25));
            var release = await Helpers.AppUpdater.CheckAsync(cts.Token);
            if (release is null)
            {
                _uiState.UpdateAvailable = false;
                UpdateDetailText.Text = "Ya tienes la última versión.";
            }
            else
            {
                _uiState.LatestVersion = release.Tag;
                _uiState.LatestAssetUrl = release.ZipUrl ?? string.Empty;
                _uiState.LatestAssetBytes = release.ZipBytes;
                _uiState.UpdateAvailable = true;
                UpdateDetailText.Text = string.IsNullOrWhiteSpace(release.ZipUrl)
                    ? $"Disponible {release.Tag}, pero sin paquete descargable: ábrelo en GitHub."
                    : $"Disponible {release.Tag}. Pulsa Descargar e instalar.";
            }
            RenderUpdateState();
        }
        catch (Exception ex)
        {
            UpdateDetailText.Text = $"No se pudo comprobar: {ex.Message}";
        }
        finally { UpdateCheckButton.IsEnabled = true; }
    }

    private async void OnUpdateDownloadClick(object sender, RoutedEventArgs e)
    {
        if (_uiState is null) return;
        if (!_uiState.UpdateAvailable || string.IsNullOrWhiteSpace(_uiState.LatestAssetUrl))
        {
            UpdateDetailText.Text = "No hay paquete descargable para esta versión.";
            return;
        }

        if (_updateInProgress) return;
        _updateInProgress = true;
        try
        {
            var dialog = new ContentDialog
            {
                Title = $"Instalar {_uiState.LatestVersion}",
                Content = "Se descargará el paquete completo (~450 MB) con su progreso, se extraerá (~1700 archivos, varios minutos con progreso) y se abrirá el instalador (pide UAC). La app solo se cerrará cuando confirmes que el instalador ya está abierto. ¿Continuar?",
                PrimaryButtonText = "Descargar e instalar",
                CloseButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = Content.XamlRoot,
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

            // La URL y el tag vienen de la API de GitHub: re-validar aquí
            // (https + host GitHub, tag con forma de versión) antes de
            // descargar o interpolar en una URL del navegador.
            var downloadUrl = _uiState.LatestAssetUrl;
            if (!IsTrustedReleaseTag(_uiState.LatestVersion))
            {
                UpdateDetailText.Text = "Versión remota no válida.";
                return;
            }
            if (!IsTrustedDownloadUrl(downloadUrl))
            {
                UpdateDetailText.Text = "URL de descarga no válida (solo HTTPS de GitHub).";
                return;
            }

            if (!IsInstalledLocation())
            {
                // Modo portable: abrir la release en el navegador para descarga manual.
                Process.Start(new ProcessStartInfo($"https://github.com/Pyromesis/CA-O/releases/tag/{_uiState.LatestVersion}") { UseShellExecute = true });
                UpdateDetailText.Text = "Modo portable: descarga el ZIP desde el navegador.";
                return;
            }

            UpdateCheckButton.IsEnabled = false;
            UpdateDownloadButton.IsEnabled = false;
            UpdateProgressBar.Visibility = Visibility.Visible;
            UpdateProgressBar.IsIndeterminate = false;
            UpdateProgressBar.Value = 0;
            UpdateDetailText.Text = "Descargando...";

            try
            {
                var updateDir = Path.Combine(Path.GetTempPath(), "CA-O-update");
                Directory.CreateDirectory(updateDir);
                var zipPath = Path.Combine(updateDir, $"CA-O-{_uiState.LatestVersion}-win-x64.zip");
                var progress = new Progress<double>(v => DispatcherQueue.TryEnqueue(() => UpdateProgressBar.Value = v * 100));
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(30));
                // Descarga fuera del hilo UI: con ConfigureAwait(false) dentro y
                // progreso limitado, la ventana sigue respondiendo durante los
                // varios minutos que tarda un paquete de ~400 MB.
                // Sin ConfigureAwait(false): lo que sigue toca UI (TextBlock,
                // diálogos) y debe continuar en el hilo UI. El trabajo pesado
                // vive dentro (Task.Run + ConfigureAwait(false) internos).
                // Tope anti disk-fill: tamaño anunciado + 64 MB, techo 2 GB.
                var announcedBytes = _uiState.LatestAssetBytes;
                var maxBytes = announcedBytes > 0
                    ? Math.Min(2L * 1024 * 1024 * 1024, announcedBytes + 64L * 1024 * 1024)
                    : 2L * 1024 * 1024 * 1024;
                await Task.Run(() => Helpers.AppUpdater.DownloadAsync(downloadUrl, zipPath, progress, cts.Token, maxBytes), cts.Token);

                // Verifica integridad: el tamaño debe coincidir con el anunciado por el release.
                var expectedBytes = _uiState.LatestAssetBytes;
                var actualBytes = new FileInfo(zipPath).Length;
                if (expectedBytes > 0 && actualBytes != expectedBytes)
                {
                    try { File.Delete(zipPath); } catch { }
                    throw new InvalidOperationException($"Descarga incompleta ({actualBytes} de {expectedBytes} bytes). Reintenta.");
                }
                // Verifica hash SHA-256 contra el sidecar publicado junto al
                // asset (detecta corrupción o sustitución en tránsito).
                // FAIL-CLOSED: sin sidecar no se instala nada (antes se
                // seguía con "solo tamaño + aviso": un release comprometido
                // colaba cualquier binario). Borra y manda a descarga manual.
                UpdateDetailText.Text = "Verificando hash SHA-256...";
                var expectedHash = await Task.Run(() => Helpers.AppUpdater.TryFetchExpectedHashAsync(downloadUrl, cts.Token), cts.Token);
                if (string.IsNullOrWhiteSpace(expectedHash))
                {
                    try { File.Delete(zipPath); } catch { }
                    throw new InvalidOperationException("Esta versión no publica hash SHA-256: no se puede verificar la descarga. Borrada por seguridad — descarga el ZIP a mano desde GitHub.");
                }
                var hashOk = await Task.Run(() => Helpers.AppUpdater.VerifyFileHash(zipPath, expectedHash), cts.Token);
                if (!hashOk)
                {
                    try { File.Delete(zipPath); } catch { }
                    throw new InvalidOperationException("El hash SHA-256 no coincide: descarga corrupta o manipulada. Borrada por seguridad, reintenta.");
                }
                // Quita Mark-of-the-Web del ZIP para que lo extraído no lo herede
                // (SmartScreen frenaba el instalador auto-lanzado en silencio).
                Helpers.AppUpdater.UnblockTree(zipPath);

                var payloadDir = Path.Combine(updateDir, "payload");
                // Borrado + extracción fuera del hilo UI con progreso REAL
                // (archivo x de y) y reintentos: el antivirus suele bloquear
                // el ZIP recién descargado unos segundos.
                UpdateDetailText.Text = "Limpiando descarga anterior...";
                await Task.Run(() =>
                {
                    if (Directory.Exists(payloadDir)) Directory.Delete(payloadDir, recursive: true);
                }, cts.Token);
                var extractProgress = new Progress<(double Ratio, int Done, int Total)>(p => DispatcherQueue.TryEnqueue(() =>
                {
                    UpdateProgressBar.IsIndeterminate = false;
                    UpdateProgressBar.Value = p.Ratio * 100;
                    UpdateDetailText.Text = $"Extrayendo paquete: {p.Done} de {p.Total} archivos ({p.Ratio * 100:0}%)...";
                }));
                await Helpers.AppUpdater.ExecuteWithRetryAsync(
                    () => Helpers.AppUpdater.ExtractWithProgressAsync(zipPath, payloadDir, extractProgress, cts.Token),
                    ct: cts.Token);
                // Los archivos pueden traer Zone.Identifier aunque el ZIP se
                // desbloqueara (cachés/proxies lo re-marcan): limpiar el árbol.
                UpdateDetailText.Text = "Quitando bloqueos de Windows (SmartScreen)...";
                await Task.Run(() => Helpers.AppUpdater.UnblockTree(payloadDir), cts.Token);

                var installer = Path.Combine(payloadDir, "gui-installer", "CA-O.InstallerGui.exe");
                if (!File.Exists(installer))
                {
                    UpdateDetailText.Text = $"El paquete no trae instalador. Puedes ejecutarlo a mano desde: {payloadDir}";
                    return;
                }
                // El payload vive en un directorio escribible por el usuario:
                // un symlink pre-plantado (payload/gui-installer -> otro sitio)
                // redirigiría File.Exists al binario de un atacante. Exigir
                // ruta canónica bajo payloadDir antes de confiar.
                string canonicalInstaller;
                try
                {
                    canonicalInstaller = Path.GetFullPath(installer);
                    var payloadRoot = Path.GetFullPath(payloadDir) + Path.DirectorySeparatorChar;
                    if (!canonicalInstaller.StartsWith(payloadRoot, StringComparison.OrdinalIgnoreCase))
                    {
                        UpdateDetailText.Text = "Ruta del instalador fuera del paquete (posible symlink). Abortado por seguridad.";
                        return;
                    }
                }
                catch
                {
                    UpdateDetailText.Text = "Ruta del instalador no válida. Abortado por seguridad.";
                    return;
                }
                // El instalador se lanza elevado: exigir firma Authenticode
                // válida ANTES del UAC. Sin firma (release comprometido o
                // binario sustituido en %TEMP%) no se ejecuta nada.
                UpdateDetailText.Text = "Verificando firma del instalador...";
                var signatureOk = await Task.Run(() => Helpers.AppUpdater.HasValidAuthenticodeSignature(canonicalInstaller), cts.Token);
                if (!signatureOk)
                {
                    UpdateDetailText.Text = $"El instalador no tiene firma Authenticode válida y no se ejecutará. Descarga el instalador a mano desde GitHub y verifica su hash: {canonicalInstaller}";
                    return;
                }

                // Handoff con confirmación: antes la app se cerraba sola y si
                // el instalador no aparecía (UAC cancelado, crash) el usuario
                // se quedaba sin nada. Ahora se verifica que siga vivo y
                // solo se cierra al confirmar.
                var installerLog = Path.Combine(Path.GetTempPath(), "CA-O-Setup-Gui.log");
                UpdateDetailText.Text = "Abriendo instalador...";
                Process? installerProcess;
                try
                {
                    installerProcess = Process.Start(new ProcessStartInfo(canonicalInstaller)
                    {
                        UseShellExecute = true,
                        Arguments = $"--auto-update --payload-dir=\"{payloadDir}\"",
                        WorkingDirectory = Path.GetDirectoryName(canonicalInstaller)!,
                    });
                }
                catch (Exception startEx)
                {
                    UpdateDetailText.Text = $"No se pudo abrir el instalador ({startEx.Message}). Ejecútalo a mano desde: {canonicalInstaller}";
                    return;
                }
                if (installerProcess is null)
                {
                    UpdateDetailText.Text = $"El instalador no arrancó. Ejecútalo a mano desde: {canonicalInstaller}";
                    return;
                }
                // Liveness REAL: no basta con que el proceso viva (atascado tras
                // SmartScreen también "vive") — se exige ventana principal hasta
                // 25 s. Sin ventana: avisar en vez de cerrar la app a ciegas.
                UpdateDetailText.Text = "Esperando la ventana del instalador...";
                var hasWindow = false;
                var exitedEarly = false;
                for (var i = 0; i < 25; i++)
                {
                    try
                    {
                        installerProcess.Refresh();
                        if (installerProcess.HasExited) { exitedEarly = true; break; }
                        if (installerProcess.MainWindowHandle != IntPtr.Zero) { hasWindow = true; break; }
                    }
                    catch { break; }
                    try { await Task.Delay(TimeSpan.FromSeconds(1), cts.Token); } catch { break; }
                }
                if (exitedEarly)
                {
                    int code;
                    try { code = installerProcess.ExitCode; } catch { code = -1; }
                    UpdateDetailText.Text = $"El instalador se cerró solo (código {code}) y no aplicó nada. Revisa el log: {installerLog} — o ejecútalo a mano desde: {canonicalInstaller}";
                    return;
                }
                if (!hasWindow)
                {
                    UpdateDetailText.Text = $"El instalador arrancó pero no muestra ventana (¿lo frenó SmartScreen? Busca su aviso y acepta). Si no aparece, ejecútalo a mano desde: {canonicalInstaller} — log: {installerLog}";
                    return;
                }
                var handoff = new ContentDialog
                {
                    Title = "Instalador en marcha",
                    Content = $"El instalador está abierto y trabajando (acepta el UAC y SmartScreen si te lo piden).\nSi lo pierdes de vista, su log está en:\n{installerLog}\nManual en:\n{payloadDir}\n\n¿Cerrar esta app para continuar en el instalador?",
                    PrimaryButtonText = "Cerrar app y continuar",
                    CloseButtonText = "Ahora no",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = Content.XamlRoot,
                };
                if (await handoff.ShowAsync() == ContentDialogResult.Primary)
                    Application.Current.Exit();
                else
                    UpdateDetailText.Text = $"Instalador disponible en: {payloadDir} (la app sigue abierta).";
            }
            catch (Exception ex)
            {
                UpdateDetailText.Text = $"Falló la actualización: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"Update failed: {ex}");
            }
            finally
            {
                UpdateProgressBar.IsIndeterminate = false;
                UpdateProgressBar.Visibility = Visibility.Collapsed;
                UpdateCheckButton.IsEnabled = true;
                UpdateDownloadButton.IsEnabled = true;
            }
        }
        finally
        {
            _updateInProgress = false;
        }
    }

    private static bool IsInstalledLocation()
    {
        try
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            return AppContext.BaseDirectory.StartsWith(programFiles, StringComparison.OrdinalIgnoreCase);
        }
        catch { return true; }
    }

    /// <summary>
    /// Tag de release confiable: forma de versión (v2.1.32, 2.1.32, con
    /// opcional sufijo). Se interpola en URLs y nombres de archivo: cualquier
    /// otra forma se rechaza antes de usarse.
    /// </summary>
    private static bool IsTrustedReleaseTag(string? tag) =>
        !string.IsNullOrWhiteSpace(tag) &&
        System.Text.RegularExpressions.Regex.IsMatch(
            tag.Trim(), @"^v?\d+\.\d+\.\d+([-.+][0-9A-Za-z.\-]+)?$");

    /// <summary>
    /// URL de descarga confiable: solo HTTPS servido por GitHub (la URL del
    /// asset puede redirigir a objetos de githubusercontent). Todo lo demás
    /// (http, hosts ajenos) se rechaza antes de descargar.
    /// </summary>
    private static bool IsTrustedDownloadUrl(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        if (!uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)) return false;
        return uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
               uri.Host.Equals("githubusercontent.com", StringComparison.OrdinalIgnoreCase) ||
               uri.Host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string?> CreateInstallWrapperScriptAsync()
    {
        try
        {
            var script = FindPrivilegedSetupScript();

            // Caso dev/repo: usar el script del repositorio si existe.
            if (!string.IsNullOrWhiteSpace(script) && File.Exists(script))
            {
                var scriptDir = Path.GetDirectoryName(script) ?? "";
                var repoPath = scriptDir.Contains("scripts", StringComparison.OrdinalIgnoreCase)
                    ? Path.GetDirectoryName(scriptDir)
                    : scriptDir;
                // Escapar comilla simple PS: ' -> '' para evitar inyección.
                var repoEsc = (repoPath ?? "").Replace("'", "''");
                var scriptEsc = script.Replace("'", "''");

                var wrapperContent = @$"
$ErrorActionPreference = 'Stop'

# Verificar si es admin
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)

if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {{
    Write-Host '[X] Este script requiere permisos de administrador.' -ForegroundColor Red
    Read-Host 'Presiona Enter para salir'
    exit 1
}}

Write-Host '[*] CA-O: Instalando servicio privilegiado...' -ForegroundColor Cyan

try {{
    Push-Location '{repoEsc}'
    & '{scriptEsc}'
    Pop-Location
    Write-Host '[OK] Servicio instalado exitosamente.' -ForegroundColor Green
    Write-Host '[*] Esperando a que se registre en el sistema...' -ForegroundColor Gray
    Start-Sleep -Seconds 2
    
    Write-Host '[*] Iniciando servicio...' -ForegroundColor Cyan
    sc.exe start CAO.Privileged | Out-Null
    Start-Sleep -Seconds 2
    
    Write-Host '[OK] Servicio iniciado.' -ForegroundColor Green
}}
catch {{
    Write-Host ('[X] Error: ' + $_) -ForegroundColor Red
    Read-Host 'Presiona Enter para salir'
    exit 1
}}

Write-Host '[OK] La app se reiniciara automaticamente.' -ForegroundColor Green
Read-Host 'Presiona Enter para continuar'
exit 0
";

                // Escritura endurecida (anti-TOCTOU): %TEMP% es escribible por
                // cualquier proceso del usuario y el wrapper se eleva con runas.
                return await WriteSecureWrapperAsync(wrapperContent);
            }

            // Caso app instalada (C:\Program Files\CA-O): no hay scripts/*.ps1.
            // Instalar/reparar directamente con sc.exe usando el binario instalado.
            var svcExe = ResolveInstalledServiceExe();
            if (string.IsNullOrWhiteSpace(svcExe) || !File.Exists(svcExe))
            {
                return null;
            }

            return await CreateDirectInstallWrapperAsync(svcExe);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Localiza CA-O.Privileged.exe en una instalación real (Program Files\CA-O\service)
    /// o junto a la UI en layouts dev/payload. No depende del repo.
    /// </summary>
    private static string? ResolveInstalledServiceExe()
    {
        var candidates = new List<string>();
        try { candidates.Add(CAO.Shared.Constants.BuildConstants.GetServiceExecutablePath()); } catch { }
        try
        {
            var baseDir = AppContext.BaseDirectory;
            candidates.Add(Path.Combine(baseDir, "service", "CA-O.Privileged.exe"));
            var parent = Directory.GetParent(baseDir.TrimEnd(Path.DirectorySeparatorChar));
            if (parent is not null)
            {
                candidates.Add(Path.Combine(parent.FullName, "service", "CA-O.Privileged.exe"));
                // Layout UI en ui\: <install>\ui -> <install>\service
                if (parent.Name.Equals("ui", StringComparison.OrdinalIgnoreCase) && parent.Parent is not null)
                    candidates.Add(Path.Combine(parent.Parent.FullName, "service", "CA-O.Privileged.exe"));
            }
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            candidates.Add(Path.Combine(programFiles, "CA-O", "service", "CA-O.Privileged.exe"));
        }
        catch { }

        foreach (var c in candidates)
        {
            try
            {
                var full = Path.GetFullPath(c);
                if (File.Exists(full)) return full;
            }
            catch { }
        }
        return null;
    }

    /// <summary>
    /// Wrapper que recrea CAO.Privileged con sc.exe (stop/delete/create/failure/start)
    /// sin necesitar install-privileged-service.ps1. Sobrevive a reinicios porque el
    /// binario vive en Program Files y el servicio se re-registra si fue borrado.
    /// El servicio es start=demand por diseño: aquí además se arranca (sc start) para
    /// que tras un reinicio (servicio detenido) "Instalar ahora" lo levante de nuevo.
    /// </summary>
    private static async Task<string> CreateDirectInstallWrapperAsync(string svcExe)
    {
        var version = CAO.Shared.AppVersion.Semantic;
        var svcEsc = svcExe.Replace("'", "''");
        var wrapperContent = @$"
$ErrorActionPreference = 'Stop'
$serviceName = 'CAO.Privileged'
$svcExe = '{svcEsc}'

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {{
    Write-Host '[X] Este script requiere permisos de administrador.' -ForegroundColor Red
    Read-Host 'Presiona Enter para salir'
    exit 1
}}

Write-Host '[*] CA-O: Reparando servicio privilegiado...' -ForegroundColor Cyan
Write-Host ('[*] Binario: ' + $svcExe) -ForegroundColor Gray
if (-not (Test-Path $svcExe)) {{
    Write-Host ('[X] No existe el binario del servicio: ' + $svcExe) -ForegroundColor Red
    Read-Host 'Presiona Enter para salir'
    exit 1
}}

# Si el servicio existe pero está detenido (caso típico tras reinicio: start=demand),
# basta con arrancarlo.
$qc = sc.exe query $serviceName 2>&1 | Out-String
$exists = $LASTEXITCODE -eq 0 -or ($qc -notmatch 'does not exist' -and $qc -match 'CAO')
if ($exists) {{
    Write-Host '[*] Servicio existente: intentando arranque...' -ForegroundColor Cyan
    sc.exe start $serviceName 2>&1 | Out-Host
    Start-Sleep -Seconds 2
    $q2 = sc.exe query $serviceName 2>&1 | Out-String
    if ($q2 -match 'RUNNING') {{
        Write-Host '[OK] Servicio arrancado.' -ForegroundColor Green
        exit 0
    }}
    Write-Host '[*] Re-creando servicio (estaba corrupto o detenido)...' -ForegroundColor Yellow
    sc.exe stop $serviceName 2>&1 | Out-Null
    Start-Sleep -Seconds 1
    sc.exe delete $serviceName 2>&1 | Out-Null
    Start-Sleep -Seconds 1
}}

sc.exe create $serviceName binPath= ""$svcExe"" start= demand DisplayName= ""CA-O Privileged Service"" | Out-Host
if ($LASTEXITCODE -ne 0) {{ throw 'sc create falló' }}
sc.exe failure $serviceName reset= 86400 actions= restart/5000/restart/10000/reboot/60000 | Out-Host
sc.exe description $serviceName ""CA-O {version} servicio privilegiado"" | Out-Host
sc.exe start $serviceName | Out-Host
Start-Sleep -Seconds 2
$q = sc.exe query $serviceName 2>&1 | Out-String
Write-Host $q
if ($q -notmatch 'RUNNING') {{
    Write-Host '[!] El servicio se registró pero no está en RUNNING. Revisa el Visor de eventos.' -ForegroundColor Yellow
}} else {{
    Write-Host '[OK] Servicio instalado e iniciado.' -ForegroundColor Green
}}
Read-Host 'Presiona Enter para continuar'
exit 0
";
        // Igual que el wrapper de repo: escritura endurecida anti-TOCTOU.
        return await WriteSecureWrapperAsync(wrapperContent);
    }

    /// <summary>
    /// Escribe un wrapper de instalación fuera del alcance de otros procesos
    /// del mismo usuario: directorio bajo %ProgramData%\CA-O con herencia
    /// cortada (solo SYSTEM + Administradores), creación exclusiva
    /// (CreateNew: no se sobrescribe un fichero pre-plantado) y
    /// re-lectura de verificación. Sin esto, un malware del mismo usuario
    /// podía sustituir el .ps1 entre la escritura y el runas y heredar la
    /// elevación UAC consentida como "CA-O".
    /// </summary>
    private static async Task<string> WriteSecureWrapperAsync(string content)
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "CA-O", "install-scripts");
        Directory.CreateDirectory(dir);
        HardenAdminOnly(dir);
        var path = Path.Combine(dir, $"cao-install-{Guid.NewGuid():N}.ps1");
        await using (var fs = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await using var writer = new StreamWriter(fs, System.Text.Encoding.UTF8);
            await writer.WriteAsync(content);
        }
        HardenAdminOnly(path);
        var roundTrip = await File.ReadAllTextAsync(path);
        if (!roundTrip.Equals(content, StringComparison.Ordinal))
            throw new IOException("Wrapper de instalación alterado tras escribir. Abortado por seguridad.");
        return path;
    }

    /// <summary>
    /// Corta la herencia y deja solo SYSTEM + Administradores (SID, funciona
    /// en Windows no ingleses). Best-effort: si falla se intenta seguir con
    /// el ACL por defecto (el CreateNew + verificación siguen valiendo).
    /// </summary>
    private static void HardenAdminOnly(string path)
    {
        try
        {
            var system = new System.Security.Principal.SecurityIdentifier(
                System.Security.Principal.WellKnownSidType.LocalSystemSid, null);
            var admins = new System.Security.Principal.SecurityIdentifier(
                System.Security.Principal.WellKnownSidType.BuiltinAdministratorsSid, null);
            if (File.Exists(path))
            {
                var security = new System.Security.AccessControl.FileSecurity();
                security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
                security.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(
                    system, System.Security.AccessControl.FileSystemRights.FullControl,
                    System.Security.AccessControl.AccessControlType.Allow));
                security.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(
                    admins, System.Security.AccessControl.FileSystemRights.FullControl,
                    System.Security.AccessControl.AccessControlType.Allow));
                new FileInfo(path).SetAccessControl(security);
            }
            else
            {
                var security = new System.Security.AccessControl.DirectorySecurity();
                security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
                security.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(
                    system, System.Security.AccessControl.FileSystemRights.FullControl,
                    System.Security.AccessControl.InheritanceFlags.ContainerInherit |
                    System.Security.AccessControl.InheritanceFlags.ObjectInherit,
                    System.Security.AccessControl.PropagationFlags.None,
                    System.Security.AccessControl.AccessControlType.Allow));
                security.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(
                    admins, System.Security.AccessControl.FileSystemRights.FullControl,
                    System.Security.AccessControl.InheritanceFlags.ContainerInherit |
                    System.Security.AccessControl.InheritanceFlags.ObjectInherit,
                    System.Security.AccessControl.PropagationFlags.None,
                    System.Security.AccessControl.AccessControlType.Allow));
                new DirectoryInfo(path).SetAccessControl(security);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"HardenAdminOnly falló en {path}: {ex.Message}");
        }
    }

    private async Task VerifyServiceInstalledAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await _vm.CheckServiceCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"VerifyServiceInstalledAsync failed: {ex}");
        }
    }

    private void RestartApplication()
    {
        try
        {
            var currentPath = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrWhiteSpace(currentPath))
            {
                Process.Start(currentPath);
                Application.Current.Exit();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RestartApplication failed: {ex}");
        }
    }

    private static string? FindPrivilegedSetupScript()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var script = Path.Combine(dir.FullName, "scripts", "install-privileged-service.ps1");
            if (File.Exists(script)) return script;
            var repoScript = Path.Combine(dir.FullName, "install-privileged-service.ps1");
            if (File.Exists(repoScript)) return repoScript;
            dir = dir.Parent;
        }
        
        // Fallback: buscar en ubicaciones conocidas
        var knownPaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "scripts", "install-privileged-service.ps1"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "scripts", "install-privileged-service.ps1"),
            Path.Combine(Environment.CurrentDirectory, "scripts", "install-privileged-service.ps1"),
            Path.Combine(Environment.CurrentDirectory, "install-privileged-service.ps1"),
        };
        
        foreach (var path in knownPaths)
        {
            var fullPath = Path.GetFullPath(path);
            if (File.Exists(fullPath)) return fullPath;
        }
        
        return null;
    }

    private static void Select(ComboBox box, string value)
    {
        foreach (var item in box.Items)
        {
            if ((item is ComboBoxItem comboItem ? comboItem.Tag?.ToString() : item as string) == value)
            {
                box.SelectedItem = item;
                return;
            }
        }
    }

    private static bool IsAdmin()
    {
        using var id = System.Security.Principal.WindowsIdentity.GetCurrent();
        return new System.Security.Principal.WindowsPrincipal(id).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }
}
