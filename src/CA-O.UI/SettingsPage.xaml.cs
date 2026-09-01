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
            
            // Inicializar estado del servicio
            ServiceStatusText.Text = $"Estado: {_vm.ServiceStatus}";
            ServiceDetailText.Text = _vm.ServiceStatus == "conectado" 
                ? "El servicio está funcionando correctamente."
                : "Haz clic en 'Instalar ahora' para configurar el servicio.";
            
            VersionsText.Text = $"CA-O UI {CAO.Shared.AppVersion.Semantic} · Protocolo v{CAO.Shared.IPC.IpcProtocol.Version} · Settings: {CAO.Shared.CaOPaths.SettingsFile}";
            bool isAdmin = IsAdmin();
            PrivilegeText.Text = isAdmin
                ? "UI elevada (administrador, requireAdministrator) — toda escritura via Named Pipe tipado con ACL, nonce, expiración 30s y anti-replay. Ver docs/SECURITY.md."
                : "UI sin privilegios — toda escritura via Named Pipe tipado con ACL, nonce, expiración 30s y anti-replay. Ver docs/SECURITY.md.";
            _vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ViewModels.SettingsViewModel.ServiceStatus))
                    DispatcherQueue.TryEnqueue(() => 
                    {
                        ServiceStatusText.Text = $"Estado: {_vm.ServiceStatus}";
                        ServiceDetailText.Text = _vm.ServiceStatus == "conectado" 
                            ? "El servicio está funcionando correctamente."
                            : "Haz clic en 'Instalar ahora' para configurar el servicio.";
                    });
            };
            
            var uiState = AppHost.Resolve<ViewModels.UiState>();
            uiState.LanguageChanged += (_, __) => DispatcherQueue.TryEnqueue(ApplyTexts);
            
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
            if (ServiceCheckButton != null) ServiceCheckButton.Content = Localizer.Get("settings.serviceCheck") ?? "Verificar servicio";
            if (ServiceInstallButton != null) ServiceInstallButton.Content = Localizer.Get("settings.serviceInstall") ?? "Instalar ahora";
            if (ServiceExplanationBar != null)
            {
                ServiceExplanationBar.Title = Localizer.Get("settings.serviceExplanationTitle") ?? "¿Por qué instalar el servicio privilegiado?";
                ServiceExplanationBar.Message = Localizer.Get("settings.serviceExplanationMessage") ?? "";
            }
            try { LocalizationHelper.LocalizeTree(this.Content as DependencyObject ?? this); } catch { }
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
        ServiceRing.IsActive = true;
        ServiceStatusText.Text = "Verificando servicio...";
        ServiceDetailText.Text = "";
        
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await _vm.CheckServiceCommand.ExecuteAsync(null);

            if (_vm.ServiceStatus == "conectado")
            {
                ServiceStatusText.Text = "✓ Servicio activo y conectado";
                ServiceDetailText.Text = "El servicio privilegiado está funcionando correctamente.";
                ServiceInstallButton.Visibility = Visibility.Collapsed;
                ServiceCheckButton.Content = "Estado OK";
                return;
            }

            ServiceStatusText.Text = "⚠ Servicio no disponible";
            ServiceDetailText.Text = "Haz clic en 'Instalar ahora' para configurar el servicio.";
            ServiceInstallButton.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            ServiceStatusText.Text = "⚠ No se pudo verificar el servicio";
            ServiceDetailText.Text = $"Error: {ex.Message}";
            ServiceInstallButton.Visibility = Visibility.Visible;
        }
        finally { ServiceRing.IsActive = false; }
    }

    private async void OnServiceInstallClick(object sender, RoutedEventArgs e)
    {
        ServiceRing.IsActive = true;
        ServiceStatusText.Text = "Instalando servicio...";
        ServiceDetailText.Text = "Se requieren permisos administrativos.";
        ServiceInstallButton.IsEnabled = false;
        ServiceCheckButton.IsEnabled = false;

        try
        {
            // Crear script wrapper para instalación con auto-reinicio
            var wrapperScript = await CreateInstallWrapperScriptAsync();
            if (string.IsNullOrWhiteSpace(wrapperScript))
            {
                ServiceStatusText.Text = "❌ Error: No se pudo crear el script de instalación";
                ServiceDetailText.Text = "Verifica que exista install-privileged-service.ps1";
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
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

            if (_vm.ServiceStatus == "conectado")
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
            ServiceRing.IsActive = false;
            ServiceInstallButton.IsEnabled = true;
            ServiceCheckButton.IsEnabled = true;
        }
    }

    private async Task<string?> CreateInstallWrapperScriptAsync()
    {
        try
        {
            var script = FindPrivilegedSetupScript();
            if (string.IsNullOrWhiteSpace(script) || !File.Exists(script))
            {
                return null;
            }

            // Obtener ruta del repositorio desde la ruta del script
            var scriptDir = Path.GetDirectoryName(script) ?? "";
            var repoPath = scriptDir.Contains("scripts", StringComparison.OrdinalIgnoreCase) 
                ? Path.GetDirectoryName(scriptDir) 
                : scriptDir;

            // Crear script wrapper temporal con auto-reinicio
            var tempScript = Path.Combine(Path.GetTempPath(), $"cao-install-{Guid.NewGuid()}.ps1");
            
            var wrapperContent = @$"
$ErrorActionPreference = 'Stop'

# Verificar si es admin
\$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
\$principal = [Security.Principal.WindowsPrincipal]::new(\$identity)

if (-not \$principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {{
    Write-Host '[X] Este script requiere permisos de administrador.' -ForegroundColor Red
    Read-Host 'Presiona Enter para salir'
    exit 1
}}

Write-Host '[*] CA-O: Instalando servicio privilegiado...' -ForegroundColor Cyan

try {{
    Push-Location '{repoPath}'
    & '{script}'
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
    Write-Host ('[X] Error: ' + \$_ ) -ForegroundColor Red
    Read-Host 'Presiona Enter para salir'
    exit 1
}}

Write-Host '[OK] La app se reiniciará automáticamente.' -ForegroundColor Green
exit 0
";

            await File.WriteAllTextAsync(tempScript, wrapperContent);
            return tempScript;
        }
        catch
        {
            return null;
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
            @"C:\Users\berna\OneDrive\Documentos\CA-O\scripts\install-privileged-service.ps1",
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
