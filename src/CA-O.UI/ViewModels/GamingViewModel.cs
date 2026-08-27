using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CAO.Core.Gaming;
using CAO.Infrastructure.Gaming;
using CAO.Shared;

namespace CAO.UI.ViewModels;

/// <summary>ViewModel para GamingPage (§5 MVVM real) — anti-cheat + perfiles por juego + estado térmico.</summary>
public sealed partial class GamingViewModel : ObservableObject
{
    private readonly UiState _state;

    public GamingViewModel(UiState state) => _state = state;

    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private IReadOnlyList<AntiCheatInfo> _antiCheats = Array.Empty<AntiCheatInfo>();
    [ObservableProperty] private IReadOnlyList<GameProfile> _gameProfiles = Array.Empty<GameProfile>();
    [ObservableProperty] private string _gpuSummary = string.Empty;
    [ObservableProperty] private string _vendorGuidance = string.Empty;
    [ObservableProperty] private bool _vanguardDetected;

    public UiState State => _state;

    [RelayCommand]
    private async Task ScanAsync(CancellationToken ct)
    {
        IsScanning = true;
        Status = "Escaneando…";
        try
        {
            var context = _state.Context ?? await AppServices.ContextProvider.GetAsync(ct);
            _state.Context = context;

            // Anti-cheat scan: solo lectura, sin mutación (allowlist, sin ejecución arbitraria §42-44)
            AntiCheats = await Task.Run(() => new AntiCheatScanProvider().Scan(), ct);
            // keep compat alias not needed
            VanguardDetected = context.VanguardDetected;

            var games = context.GamesDetected
                .Select(name => GameProfileCatalog.All.FirstOrDefault(p => p.DisplayName.Equals(name, StringComparison.OrdinalIgnoreCase)))
                .Where(p => p is not null)
                .Cast<GameProfile>()
                .ToList();
            GameProfiles = games;

            GpuSummary =
                $"GPU: {(string.IsNullOrWhiteSpace(context.GpuName) ? "desconocida" : context.GpuName)}\n" +
                $"Driver: {(string.IsNullOrWhiteSpace(context.GpuDriverVersion) ? "desconocido" : context.GpuDriverVersion)}\n" +
                $"Refresco: {(context.DisplayRefreshHz > 0 ? $"{context.DisplayRefreshHz} Hz" : "desconocido")}\n" +
                $"Térmico: {context.ThermalState}";

            VendorGuidance = context.GpuVendor switch
            {
                "NVIDIA" => "NVIDIA Reflex (y Reflex 2) es la vía correcta para latencia; prioridad sobre ULLM. Configure en el juego.",
                "AMD" => "AMD Anti-Lag / Anti-Lag 2 se controla desde Adrenalin o el juego. CA-O no aplica hacks de registro.",
                _ => "Use panel del fabricante para tecnologías nativas (Reflex, Anti-Lag).",
            };
            Status = "Escaneo completo.";
        }
        catch (OperationCanceledException)
        {
            Status = "Escaneo cancelado.";
        }
        catch (Exception ex)
        {
            Status = $"{ErrorCodes.UiGamingScanFailed}: escaneo fallido";
            App.WriteCrashLog(ex);
        }
        finally { IsScanning = false; }
    }
}
