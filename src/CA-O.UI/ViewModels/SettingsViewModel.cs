using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CAO.Shared;

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
            var response = await _pipe.PingAsync(ct);
            // Estados canónicos en inglés: los muestra MainWindow/Dashboard/Settings sin bifurcar por idioma.
            // Un pipe inalcanzable (servicio detenido/no instalado) devuelve
            // rejection CAO-IPC-007/008: eso es "unavailable", no "rejected".
            if (response is { Accepted: true })
                ServiceStatus = "connected";
            else if (response is { ErrorCode: ErrorCodes.IpcPipeNotFound or ErrorCodes.IpcTimeout })
                ServiceStatus = "unavailable";
            else
                ServiceStatus = "rejected";
            ServiceCheckedUtc = DateTime.UtcNow;
            _state.ServiceStatus = ServiceStatus;
            _state.ServiceCheckedUtc = ServiceCheckedUtc;
            if (ServiceStatus == "connected")
                _state.ServiceVersion = await Helpers.ServiceVersionProbe.FetchAsync(_pipe, ct) ?? string.Empty;
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
