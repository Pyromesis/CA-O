using System.Diagnostics;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CAO.Shared;
using CAO.Shared.Updates;
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
            MinimizeToTraySwitch.IsOn = _vm.MinimizeToTray;
            CloseToTraySwitch.IsOn = _vm.CloseToTray;
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

    /// <summary>Boton del tour de esta pagina: texto localizado y lanzamiento con guarda. Nunca lanza.</summary>
    private void ApplyPageTourText()
    {
        try
        {
            PageTourButton.Content = Localizer.Get("tour.page.button");
            ToolTipService.SetToolTip(PageTourButton, Localizer.Get("tour.page.tip"));
        }
        catch { }
    }

    private void OnPageTourClick(object sender, RoutedEventArgs e)
    {
        if (Tutorial.TourCoordinator.IsActive) return;
        MainWindow.Current?.StartPageTour("settings");
    }

    private void ApplyTexts()
    {
        try
        {
            if (ThemeLabel != null) ThemeLabel.Text = Localizer.Get("settings.theme") ?? "Tema";
            if (LanguageLabel != null) LanguageLabel.Text = Localizer.Get("settings.language") ?? "Idioma";
            if (ExpertSwitch != null) ExpertSwitch.Header = Localizer.Get("settings.expertMode") ?? "Modo Expert";
            if (TrayTitleText != null) TrayTitleText.Text = Localizer.Get("settings.trayTitle") ?? "Bandeja del sistema";
            if (TrayDescText != null) TrayDescText.Text = Localizer.Get("settings.trayDesc") ?? "CA-O sigue ejecutándose en segundo plano";
            if (MinimizeToTraySwitch != null) MinimizeToTraySwitch.Header = Localizer.Get("settings.minimizeToTray") ?? "Minimizar a la bandeja";
            if (MinimizeToTrayDescText != null) MinimizeToTrayDescText.Text = Localizer.Get("settings.minimizeToTrayDesc") ?? "Al minimizar, la ventana se oculta en la bandeja.";
            if (CloseToTraySwitch != null) CloseToTraySwitch.Header = Localizer.Get("settings.closeToTray") ?? "Cerrar a la bandeja";
            if (CloseToTrayDescText != null) CloseToTrayDescText.Text = Localizer.Get("settings.closeToTrayDesc") ?? "Al cerrar con la X, la app se oculta en la bandeja en vez de salir.";
            if (ExpertWarnBar != null) ExpertWarnBar.Message = Localizer.Get("optimize.expertWarning") ?? "Modo Expert habilitado";
            if (ServiceInstallButton != null) ServiceInstallButton.Content = Localizer.Get("settings.serviceInstall") ?? "Instalar ahora";
            ApplyPageTourText();
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
            if (_vm is null || _uiState is null) return;
            if (_vm.IsCheckingService) return;
            var status = _uiState.ServiceStatus;
            if (!ServiceStatusMapper.NeedsVerification(status)) return;
            // unavailable reintenta aunque ya hubiera un intento previo
            // (el servicio puede volver tras una limpieza o un arranque
            // lento): throttle 60 s; el resto mantiene 5 min.
            if (_autoCheckAttempted && !ServiceStatusMapper.IsUnavailable(status)) return;
            var throttle = ServiceStatusMapper.AutoCheckThrottle(status);
            if (_uiState.ServiceCheckedUtc.HasValue &&
                DateTime.UtcNow - _uiState.ServiceCheckedUtc.Value < throttle)
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
            // Tras un reinicio el servicio (delayed-auto) puede seguir en
            // arranque: el ping falla y parece "perdido para siempre".
            // Intento best-effort de levantarlo + espera de readiness con
            // ping al pipe (hasta 8 s) antes de reportar. La UI corre
            // elevada (requireAdministrator).
            if (IsAdmin())
            {
                // CAO-BUG-2026-10-06: ambos ayudantes lanzan sc.exe y leen la
                // salida de forma sincrona (ReadToEnd + WaitForExit hasta 8 s).
                // Con el servicio parado esa espera congelaba la ventana entera
                // al abrir Ajustes. Se ejecutan fuera del hilo de UI y en el
                // mismo paso, para no lanzar sc query dos veces.
                var serviceRunning = await Task.Run(() =>
                {
                    TryStartServiceBestEffort();
                    return IsServiceRunning();
                });
                if (!serviceRunning)
                    await WaitForServiceReadyAsync();
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

    /// <summary>
    /// Readiness tras sc start: ping al pipe hasta 8 s (cada 500 ms) en vez
    /// del delay fijo. Async sin bloquear la UI; nunca lanza.
    /// </summary>
    private static async Task WaitForServiceReadyAsync()
    {
        try
        {
            var pipe = AppHost.Resolve<PrivilegedPipeClient>();
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(8);
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    var resp = await pipe.PingAsync(cts.Token);
                    if (resp is { Accepted: true }) return;
                }
                catch { }
                try { await Task.Delay(500); } catch { }
            }
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

    private void OnMinimizeToTrayToggled(object sender, RoutedEventArgs e)
    {
        if (_vm is null || MinimizeToTraySwitch is null) return;
        _vm.MinimizeToTray = MinimizeToTraySwitch.IsOn;
    }

    private void OnCloseToTrayToggled(object sender, RoutedEventArgs e)
    {
        if (_vm is null || CloseToTraySwitch is null) return;
        _vm.CloseToTray = CloseToTraySwitch.IsOn;
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
            if (await UiDialogs.ShowAsync(dialog) != ContentDialogResult.Primary) return;

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

                // Verificación fail-closed: el ZIP debe coincidir con el
                // manifiesto firmado del release (RSA-3072, ancla clavada en
                // CA-O). El SHA-256 solo ya no basta: un atacante con acceso
                // al release podía sustituir ZIP y sidecar a la vez. Sin
                // manifiesto, sin firma válida o con hash distinto no se
                // instala nada y el paquete se borra.
                UpdateDetailText.Text = "Verificando firma del release...";
                var verification = await ReleaseUpdateVerifier.VerifyDownloadAsync(
                    zipPath,
                    downloadUrl,
                    _uiState.LatestVersion,
                    _uiState.LatestAssetBytes > 0 ? _uiState.LatestAssetBytes : null,
                    cancellationToken: cts.Token);
                if (!verification.IsValid)
                {
                    try { File.Delete(zipPath); } catch { }
                    throw new InvalidOperationException(DescribeReleaseVerificationFailure(verification));
                }
                UpdateDetailText.Text = "Paquete verificado (manifiesto firmado + SHA-256).";
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
                // El instalador se lanza elevado. La confianza ya está tomada:
                // manifiesto firmado + SHA-256 del ZIP. Authenticode se informa
                // como señal extra, pero no es obligatorio (el proyecto no
                // compra un certificado comercial).
                UpdateDetailText.Text = "Verificando instalador...";
                var signatureOk = await Task.Run(() => Helpers.AppUpdater.HasValidAuthenticodeSignature(canonicalInstaller), cts.Token);
                UpdateDetailText.Text = signatureOk
                    ? "Instalador verificado (manifiesto firmado + SHA-256 + Authenticode)."
                    : "Instalador verificado (manifiesto firmado + SHA-256; sin Authenticode, no es obligatorio).";

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
                if (await UiDialogs.ShowAsync(handoff) == ContentDialogResult.Primary)
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

    private static string DescribeReleaseVerificationFailure(ReleaseVerificationResult result) =>
        result.Code switch
        {
            "manifest-unavailable" =>
                "Este release no publica un manifiesto firmado: no se puede verificar la descarga. Borrada por seguridad — descarga el ZIP a mano desde GitHub.",
            "signature-unavailable" =>
                "El manifiesto del release no trae firma: no se puede verificar la descarga. Borrada por seguridad — descarga el ZIP a mano desde GitHub.",
            "manifest-invalid" =>
                "El manifiesto del release no es válido. Descarga abortada por seguridad.",
            "signature-invalid" =>
                "La firma del manifiesto no es válida: el paquete no procede de CA-O. Descarga borrada por seguridad.",
            "asset-mismatch" =>
                "El manifiesto firmado corresponde a otro archivo. Descarga abortada por seguridad.",
            "version-mismatch" =>
                "El manifiesto firmado corresponde a otra versión. Descarga abortada por seguridad.",
            "size-mismatch" =>
                "El tamaño del paquete no coincide con el manifiesto firmado. Descarga borrada; reintenta.",
            "hash-mismatch" =>
                "El hash SHA-256 no coincide con el manifiesto firmado: descarga corrupta o manipulada. Borrada por seguridad.",
            "package-missing" =>
                "No se encontró el paquete descargado. Reintenta.",
            "url-insecure" =>
                "La URL de descarga no es HTTPS. Actualización abortada.",
            _ =>
                $"No se pudo verificar el release ({result.Code}). Descarga abortada por seguridad.",
        };

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
    /// Wrapper que repara CAO.Privileged con sc.exe (start/config/failure;
    /// delete/create solo como último recurso si la clave está corrupta)
    /// sin necesitar install-privileged-service.ps1. Sobrevive a reinicios
    /// porque el binario vive en Program Files y el servicio se re-registra
    /// si fue borrado. Primero intenta arrancarlo y espera a que delayed-auto
    /// termine: no se borra un servicio sano solo porque tarde en arrancar.
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

# Si el servicio existe pero está detenido (caso típico tras reinicio si aún arranca: delayed-auto),
# basta con arrancarlo. Nunca se borra por el simple hecho de tardar: delayed-auto
# puede necesitar más de dos segundos y borrarlo dejaba la app sin su canal SYSTEM.
$qc = sc.exe query $serviceName 2>&1 | Out-String
$exists = $LASTEXITCODE -eq 0 -or ($qc -notmatch 'does not exist' -and $qc -match 'CAO')
if ($exists) {{
    Write-Host '[*] Servicio existente: intentando arranque...' -ForegroundColor Cyan
    sc.exe start $serviceName 2>&1 | Out-Host
    $running = $false
    for ($i = 0; $i -lt 12; $i++) {{
        Start-Sleep -Seconds 1
        $q2 = sc.exe query $serviceName 2>&1 | Out-String
        if ($q2 -match 'RUNNING') {{ $running = $true; break }}
    }}
    if ($running) {{
        Write-Host '[OK] Servicio arrancado.' -ForegroundColor Green
        exit 0
    }}

    # Reparación en sitio: sc config no borra el servicio ni su clave. Si la
    # configuración está corrupta, fallará y solo entonces se recrea.
    Write-Host '[*] Servicio detenido: reparando configuración sin borrarlo...' -ForegroundColor Cyan
    sc.exe config $serviceName binPath= ""$svcExe"" start= delayed-auto DisplayName= ""CA-O Privileged Service"" 2>&1 | Out-Host
    if ($LASTEXITCODE -eq 0) {{
        sc.exe failure $serviceName reset= 86400 actions= restart/5000/restart/10000/reboot/60000 | Out-Null
        sc.exe description $serviceName ""CA-O {version} servicio privilegiado"" | Out-Null
        sc.exe start $serviceName 2>&1 | Out-Host
        $repaired = $false
        for ($i = 0; $i -lt 12; $i++) {{
            Start-Sleep -Seconds 1
            $q3 = sc.exe query $serviceName 2>&1 | Out-String
            if ($q3 -match 'RUNNING') {{ $repaired = $true; break }}
        }}
        if ($repaired) {{
            Write-Host '[OK] Servicio reparado y arrancado.' -ForegroundColor Green
            exit 0
        }}
    }}

    Write-Host '[*] sc config no bastó; recreando el servicio como último recurso...' -ForegroundColor Yellow
    sc.exe stop $serviceName 2>&1 | Out-Null
    Start-Sleep -Seconds 1
    sc.exe delete $serviceName 2>&1 | Out-Null
    # sc delete marca la clave para borrado; hay que esperar antes de crear.
    for ($i = 0; $i -lt 10; $i++) {{
        Start-Sleep -Seconds 1
        sc.exe query $serviceName 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) {{ break }}
    }}
}}

$created = $false
for ($i = 0; $i -lt 5; $i++) {{
    sc.exe create $serviceName binPath= ""$svcExe"" start= delayed-auto DisplayName= ""CA-O Privileged Service"" | Out-Host
    if ($LASTEXITCODE -eq 0) {{ $created = $true; break }}
    Start-Sleep -Seconds 2
}}
if (-not $created) {{ throw 'sc create falló' }}
sc.exe failure $serviceName reset= 86400 actions= restart/5000/restart/10000/reboot/60000 | Out-Host
sc.exe description $serviceName ""CA-O {version} servicio privilegiado"" | Out-Host
sc.exe start $serviceName | Out-Host
$q = ''
$running = $false
for ($i = 0; $i -lt 12; $i++) {{
    Start-Sleep -Seconds 1
    $q = sc.exe query $serviceName 2>&1 | Out-String
    if ($q -match 'RUNNING') {{ $running = $true; break }}
}}
Write-Host $q
if (-not $running) {{
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
