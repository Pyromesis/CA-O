using CommunityToolkit.Mvvm.ComponentModel;
using CAO.Shared;

namespace CAO.UI.ViewModels;

/// <summary>
/// Central UI state shared across pages (MVVM view-model). Holds the
/// measured SystemContext, computed recommendations and user preferences.
/// Nothing here mutates the system directly.
/// </summary>
public sealed class UiState : ObservableObject
{
    private SystemContext? _context;
    private IReadOnlyList<Recommendation> _recommendations = Array.Empty<Recommendation>();
    private bool _expertMode;
    private string _theme = "system";
    private string _language = "es-ES";
    private DateTime? _lastAnalysisUtc;
    private string _serviceStatus = "unknown";
    private DateTime? _serviceCheckedUtc;
    private IReadOnlyList<string> _recoveryCandidates = Array.Empty<string>();
    private string _freshnessLabel = string.Empty;
    private string _staleReason = string.Empty;
    private string _analysisAgeLabel = string.Empty;

    public SystemContext? Context
    {
        get => _context;
        set => SetProperty(ref _context, value);
    }

    public IReadOnlyList<Recommendation> Recommendations
    {
        get => _recommendations;
        set => SetProperty(ref _recommendations, value);
    }

    public bool ExpertMode
    {
        get => _expertMode;
        set => SetProperty(ref _expertMode, value);
    }

    public string Theme
    {
        get => _theme;
        set { if (SetProperty(ref _theme, value)) ThemeChanged?.Invoke(this, value); }
    }

    public string Language
    {
        get => _language;
        set { if (SetProperty(ref _language, value)) LanguageChanged?.Invoke(this, value); }
    }

    public DateTime? LastAnalysisUtc
    {
        get => _lastAnalysisUtc;
        set => SetProperty(ref _lastAnalysisUtc, value);
    }

    public string ServiceStatus
    {
        get => _serviceStatus;
        set => SetProperty(ref _serviceStatus, value);
    }

    private bool _updateAvailable;
    private string _latestVersion = string.Empty;
    private string _latestAssetUrl = string.Empty;

    /// <summary>Hay un release más nuevo que la versión instalada.</summary>
    public bool UpdateAvailable
    {
        get => _updateAvailable;
        set => SetProperty(ref _updateAvailable, value);
    }

    /// <summary>Tag del último release (p. ej. "v2.1.6").</summary>
    public string LatestVersion
    {
        get => _latestVersion;
        set => SetProperty(ref _latestVersion, value);
    }

    /// <summary>URL del ZIP completo del último release.</summary>
    public string LatestAssetUrl
    {
        get => _latestAssetUrl;
        set => SetProperty(ref _latestAssetUrl, value);
    }

    private long _latestAssetBytes;

    /// <summary>Tamaño esperado del ZIP (0 si se desconoce); sirve para verificar la descarga.</summary>
    public long LatestAssetBytes
    {
        get => _latestAssetBytes;
        set => SetProperty(ref _latestAssetBytes, value);
    }

    /// <summary>
    /// Última vez que se verificó el servicio privilegiado (UTC).
    /// Memoria de sesión: evita pedir al usuario que pulse "Comprobar" en cada página.
    /// </summary>
    public DateTime? ServiceCheckedUtc
    {
        get => _serviceCheckedUtc;
        set => SetProperty(ref _serviceCheckedUtc, value);
    }

    /// <summary>Optimization ids left Incomplete by a previous crash (spec 13).</summary>
    public IReadOnlyList<string> RecoveryCandidates
    {
        get => _recoveryCandidates;
        set => SetProperty(ref _recoveryCandidates, value);
    }

    public string FreshnessLabel
    {
        get => _freshnessLabel;
        set => SetProperty(ref _freshnessLabel, value);
    }

    public string StaleReason
    {
        get => _staleReason;
        set => SetProperty(ref _staleReason, value);
    }

    public string AnalysisAgeLabel
    {
        get => _analysisAgeLabel;
        set => SetProperty(ref _analysisAgeLabel, value);
    }

    /// <summary>
    /// Ids aplicados con éxito en esta sesión. Cubre one-shots sin estado
    /// persistente (flush-dns, trim, DISM...): tras aplicarlos la tarjeta
    /// muestra "Aplicado ✓" y no se pueden volver a aplicar hasta reiniciar.
    /// Se limpia al revertir.
    /// </summary>
    public HashSet<string> AppliedThisSession { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Foto de lo renderizado en Analizar (textos DNS/DPC/gaming/diagnósticos
    /// y filas de barras). Sobrevive a cambios de pestaña en sesión y se
    /// persiste a disco al cerrar cada análisis completo.
    /// </summary>
    public AnalysisDisplaySnapshot? DisplaySnapshot { get; set; }

    public event EventHandler<string>? ThemeChanged;
    public event EventHandler<string>? LanguageChanged;
}
