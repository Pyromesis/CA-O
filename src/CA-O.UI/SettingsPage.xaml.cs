using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CAO.UI.Pages;

/// <summary>Settings (spec 80): expert mode with warning, theme and language.</summary>
public sealed partial class SettingsPage : Page
{
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

        var state = AppServices.State;
        ExpertSwitch.IsOn = state.ExpertMode;
        ExpertWarnBar.IsOpen = state.ExpertMode;
        Select(ThemeBox, state.Theme);
        Select(LanguageBox, state.Language);

        ServiceStatusText.Text = $"Servicio: {state.ServiceStatus}";
        VersionsText.Text = $"CA-O UI {CAO.Shared.AppVersion.Semantic} · Protocolo v{CAO.Shared.IPC.IpcProtocol.Version} · Settings: {CAO.Shared.CaOPaths.SettingsFile}";
    }

    private void OnExpertToggled(object sender, RoutedEventArgs e)
    {
        AppServices.State.ExpertMode = ExpertSwitch.IsOn;
        ExpertWarnBar.IsOpen = ExpertSwitch.IsOn;
    }

    private void OnThemeSelected(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeBox.SelectedItem is ComboBoxItem { Tag: string theme })
        {
            AppServices.State.Theme = theme;
            MainWindow.ApplyThemeGlobally?.Invoke(theme);
        }
    }

    private void OnLanguageSelected(object sender, SelectionChangedEventArgs e)
    {
        if (LanguageBox.SelectedItem is string language && language != AppServices.State.Language)
        {
            AppServices.State.Language = language; // triggers shell re-localization
        }
    }

    private async void OnServiceCheckClick(object sender, RoutedEventArgs e)
    {
        ServiceRing.IsActive = true;
        try
        {
            var response = await AppServices.Pipe.DetectAsync("disable-transparency");
            AppServices.State.ServiceStatus = response is { Accepted: true } ? "conectado" : "rechazado";
            ServiceStatusText.Text = response is { Accepted: true }
                ? "Servicio privilegiado OK"
                : $"Servicio respondió rechazo: {response?.ErrorCode ?? "?"}";
        }
        catch (Exception ex)
        {
            AppServices.State.ServiceStatus = "no disponible";
            ServiceStatusText.Text = $"Servicio no disponible (normal si aún no está instalado): {ex.Message}";
        }
        finally
        {
            ServiceRing.IsActive = false;
        }
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
}
