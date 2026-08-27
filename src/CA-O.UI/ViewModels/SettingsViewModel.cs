using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CAO.UI.ViewModels;

/// <summary>ViewModel para SettingsPage — preferencias con binding y validación.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly UiState _state;
    private readonly PrivilegedPipeClient _pipe;

    public SettingsViewModel(UiState state, PrivilegedPipeClient pipe)
    {
        _state = state;
        _pipe = pipe;
        _expertMode = state.ExpertMode;
        _theme = state.Theme;
        _language = state.Language;
        _serviceStatus = state.ServiceStatus;
    }

    [ObservableProperty] private bool _expertMode;
    [ObservableProperty] private string _theme;
    [ObservableProperty] private string _language;
    [ObservableProperty] private string _serviceStatus;
    [ObservableProperty] private bool _isCheckingService;

    partial void OnExpertModeChanged(bool value)
    {
        _state.ExpertMode = value;
    }

    partial void OnThemeChanged(string value)
    {
        _state.Theme = value;
        MainWindow.ApplyThemeGlobally?.Invoke(value);
    }

    partial void OnLanguageChanged(string value)
    {
        if (value != _state.Language) _state.Language = value;
    }

    [RelayCommand]
    private async Task CheckServiceAsync(CancellationToken ct)
    {
        IsCheckingService = true;
        try
        {
            var response = await _pipe.DetectAsync("disable-transparency", ct);
            ServiceStatus = response is { Accepted: true } ? "conectado" : "rechazado";
            _state.ServiceStatus = ServiceStatus;
        }
        catch (Exception)
        {
            ServiceStatus = "no disponible";
            _state.ServiceStatus = ServiceStatus;
        }
        finally { IsCheckingService = false; }
    }
}
