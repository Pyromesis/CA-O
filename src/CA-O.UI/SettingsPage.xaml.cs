using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CAO.UI.Pages;

/// <summary>Settings (spec 80): expert mode with warning, theme and language.</summary>
public sealed partial class SettingsPage : Page
{
    private readonly ViewModels.SettingsViewModel _vm;

    public SettingsPage()
    {
        InitializeComponent();

        ExpertSwitch.Header = Localizer.Get("settings.expertMode");
        ExpertWarnBar.Message = Localizer.Get("optimize.expertWarning");
        ThemeLabel.Text = Localizer.Get("settings.theme");
        LanguageLabel.Text = Localizer.Get("settings.language");
        ServiceButton.Content = Localizer.Get("settings.serviceCheck");

        foreach (var language in Localizer.SupportedLanguages)
        {
            LanguageBox.Items.Add(language);
        }

        _vm = AppHost.Resolve<ViewModels.SettingsViewModel>();
        DataContext = _vm;
        ExpertSwitch.IsOn = _vm.ExpertMode;
        ExpertWarnBar.IsOpen = _vm.ExpertMode;
        Select(ThemeBox, _vm.Theme);
        Select(LanguageBox, _vm.Language);
        ServiceStatusText.Text = $"Servicio: {_vm.ServiceStatus}";
        VersionsText.Text = $"CA-O UI {CAO.Shared.AppVersion.Semantic} · Protocolo v{CAO.Shared.IPC.IpcProtocol.Version} · Settings: {CAO.Shared.CaOPaths.SettingsFile}";
        bool isAdmin = IsAdmin();
        PrivilegeText.Text = isAdmin
            ? "UI elevada (administrador, requireAdministrator) — toda escritura via Named Pipe tipado con ACL, nonce, expiración 30s y anti-replay. Ver docs/SECURITY.md."
            : "UI sin privilegios — toda escritura via Named Pipe tipado con ACL, nonce, expiración 30s y anti-replay. Ver docs/SECURITY.md.";
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ViewModels.SettingsViewModel.ServiceStatus))
                DispatcherQueue.TryEnqueue(() => ServiceStatusText.Text = $"Servicio: {_vm.ServiceStatus}");
        };
    }

    private void OnExpertToggled(object sender, RoutedEventArgs e)
    {
        _vm.ExpertMode = ExpertSwitch.IsOn;
        ExpertWarnBar.IsOpen = ExpertSwitch.IsOn;
    }

    private void OnThemeSelected(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeBox.SelectedItem is ComboBoxItem { Tag: string theme })
        {
            _vm.Theme = theme;
        }
    }

    private void OnLanguageSelected(object sender, SelectionChangedEventArgs e)
    {
        if (LanguageBox.SelectedItem is string language && language != _vm.Language)
        {
            _vm.Language = language;
        }
    }

    private async void OnServiceCheckClick(object sender, RoutedEventArgs e)
    {
        ServiceRing.IsActive = true;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await _vm.CheckServiceCommand.ExecuteAsync(null);
            ServiceStatusText.Text = _vm.ServiceStatus == "conectado" ? "Servicio privilegiado OK" : $"Servicio: {_vm.ServiceStatus}";
        }
        catch (Exception ex)
        {
            ServiceStatusText.Text = $"Servicio no disponible (normal si aún no está instalado): {ex.Message}";
        }
        finally { ServiceRing.IsActive = false; }
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
