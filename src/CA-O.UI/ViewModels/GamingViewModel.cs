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

    private readonly CAO.Infrastructure.SystemInterop.SystemContextProvider _contextProvider;

    public GamingViewModel(UiState state, CAO.Infrastructure.SystemInterop.SystemContextProvider contextProvider)
    {
        _state = state;
        _contextProvider = contextProvider;
    }

    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private IReadOnlyList<AntiCheatInfo> _antiCheats = Array.Empty<AntiCheatInfo>();
    [ObservableProperty] private IReadOnlyList<GameProfile> _gameProfiles = Array.Empty<GameProfile>();
    [ObservableProperty] private string _gpuSummary = string.Empty;
    [ObservableProperty] private string _vendorGuidance = string.Empty;
    [ObservableProperty] private bool _vanguardDetected;
    [ObservableProperty] private int _blockedCount;
    [ObservableProperty] private int _allowedCount;
    [ObservableProperty] private int _reviewCount;

    public UiState State => _state;

    [RelayCommand]
    private async Task ScanAsync(CancellationToken ct)
    {
        IsScanning = true;
        Status = "Escaneando…";
        try
        {
            var context = _state.Context ?? await _contextProvider.GetAsync(ct);
            _state.Context = context;

            AntiCheats = await Task.Run(() => new AntiCheatScanProvider().Scan(), ct);
            VanguardDetected = context.VanguardDetected;

            var games = context.GamesDetected
                .Select(name => GameProfileCatalog.All.FirstOrDefault(p => p.DisplayName.Equals(name, StringComparison.OrdinalIgnoreCase)))
                .Where(p => p is not null)
                .Cast<GameProfile>()
                .ToList();
            GameProfiles = games;

            // Matriz gaming §27: bloqueadas/permitidas/revisión
            var candidates = CAO.Core.Catalog.OptimizationCatalog.All.Select(o => o.Definition.Id).ToList();
            BlockedCount = candidates.Count(id => GameCompatibilityPolicy.IsBlocked(id, context));
            AllowedCount = candidates.Count(id => GameCompatibilityPolicy.Evaluate(id, context).Compatibility == GameCompatibility.Safe);
            ReviewCount = candidates.Count - BlockedCount - AllowedCount;

            GpuSummary =
                $"GPU: {(string.IsNullOrWhiteSpace(context.GpuName) ? "desconocida" : context.GpuName)}\n" +
                $"Driver: {(string.IsNullOrWhiteSpace(context.GpuDriverVersion) ? "desconocido" : context.GpuDriverVersion)}\n" +
                $"Refresco: {(context.DisplayRefreshHz > 0 ? $"{context.DisplayRefreshHz} Hz" : "desconocido")}\n" +
                $"Térmico: {context.ThermalState}\n" +
                $"Modo protegido: {(VanguardDetected ? "Activo" : "No")} — {BlockedCount} bloqueadas, {AllowedCount} permitidas, {ReviewCount} revisión";

            VendorGuidance = context.GpuVendor switch
            {
                "NVIDIA" => "NVIDIA Reflex (y Reflex 2) es la vía correcta para latencia; prioridad sobre ULLM. Configure en el juego.",
                "AMD" => "AMD Anti-Lag / Anti-Lag 2 se controla desde Adrenalin o el juego. CA-O no aplica hacks de registro.",
                _ => "Use panel del fabricante para tecnologías nativas (Reflex, Anti-Lag).",
            };
            Status = $"Escaneo completo — {BlockedCount} bloqueadas, {AllowedCount} permitidas.";
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
