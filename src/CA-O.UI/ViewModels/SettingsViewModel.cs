using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.UI.ViewModels;

/// <summary>ViewModel para SettingsPage — preferencias con binding y validación.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly UiState _state;
    private readonly PrivilegedPipeClient _pipe;
    private readonly ISettingsStore _store;

    public SettingsViewModel(UiState state, PrivilegedPipeClient pipe, ISettingsStore store)
    {
        _state = state;
        _pipe = pipe;
        _store = store;
        try
        {
            var persisted = _store.Load();
            state.MinimizeToTray = persisted.Ui.MinimizeToTray;
            state.CloseToTray = persisted.Ui.CloseToTray;
        }
        catch { }
        _expertMode = state.ExpertMode;
        _theme = state.Theme;
        _language = state.Language;
        _minimizeToTray = state.MinimizeToTray;
        _closeToTray = state.CloseToTray;
        _serviceStatus = state.ServiceStatus;
        _serviceCheckedUtc = state.ServiceCheckedUtc;
    }

    [ObservableProperty] private bool _expertMode;
    [ObservableProperty] private string _theme;
    [ObservableProperty] private string _language;
    [ObservableProperty] private bool _minimizeToTray;
    [ObservableProperty] private bool _closeToTray;
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

    partial void OnMinimizeToTrayChanged(bool value)
    {
        _state.MinimizeToTray = value;
        PersistTrayPrefs();
    }

    partial void OnCloseToTrayChanged(bool value)
    {
        _state.CloseToTray = value;
        PersistTrayPrefs();
    }

    private void PersistTrayPrefs()
    {
        try
        {
            var settings = _store.Load();
            settings.Ui.MinimizeToTray = _state.MinimizeToTray;
            settings.Ui.CloseToTray = _state.CloseToTray;
            _store.Save(settings);
        }
        catch (Exception ex)
        {
            try { App.WriteCrashLog(ex); } catch { }
        }
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
            ServiceStatus = ServiceStatusMapper.FromPing(response is { Accepted: true }, response?.ErrorCode);
            ServiceCheckedUtc = DateTime.UtcNow;
            _state.ServiceStatus = ServiceStatus;
            _state.ServiceCheckedUtc = ServiceCheckedUtc;
            if (ServiceStatus == "connected")
                _state.ServiceVersion = await Helpers.ServiceVersionProbe.FetchAsync(_pipe, ct) ?? string.Empty;
        }
        catch (Exception ex)
        {
            ServiceStatus = "unavailable";
            ServiceCheckedUtc = DateTime.UtcNow;
            _state.ServiceStatus = ServiceStatus;
            _state.ServiceCheckedUtc = ServiceCheckedUtc;
            try { App.WriteCrashLog(ex); } catch { }
        }
        finally { IsCheckingService = false; }
    }
}
