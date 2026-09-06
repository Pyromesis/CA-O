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
        _serviceCheckedUtc = state.ServiceCheckedUtc;
    }

    [ObservableProperty] private bool _expertMode;
    [ObservableProperty] private string _theme;
    [ObservableProperty] private string _language;
    [ObservableProperty] private string _serviceStatus;
    [ObservableProperty] private DateTime? _serviceCheckedUtc;
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
        if (IsCheckingService) return;
        IsCheckingService = true;
        try
        {
            var response = await _pipe.DetectAsync("disable-transparency", ct);
            // Estados canónicos en inglés: los muestra MainWindow/Dashboard/Settings sin bifurcar por idioma.
            ServiceStatus = response is { Accepted: true } ? "connected" : "rejected";
            ServiceCheckedUtc = DateTime.UtcNow;
            _state.ServiceStatus = ServiceStatus;
            _state.ServiceCheckedUtc = ServiceCheckedUtc;
        }
        catch (Exception)
        {
            ServiceStatus = "unavailable";
            ServiceCheckedUtc = DateTime.UtcNow;
            _state.ServiceStatus = ServiceStatus;
            _state.ServiceCheckedUtc = ServiceCheckedUtc;
        }
        finally { IsCheckingService = false; }
    }
}
