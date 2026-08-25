using CommunityToolkit.Mvvm.ComponentModel;
using CAO.Shared;
using Microsoft.UI.Xaml;

namespace CAO.UI;

/// <summary>
/// Central UI state shared across pages. Holds the measured SystemContext,
/// computed recommendations and user preferences (expert mode, theme,
/// language). Nothing here mutates the system directly.
/// </summary>
public sealed class UiState : ObservableObject
{
    private SystemContext? _context;
    private IReadOnlyList<Recommendation> _recommendations = [];
    private bool _expertMode;
    private string _theme = "system";
    private string _language = "es-ES";
    private DateTime? _lastAnalysisUtc;
    private string _serviceStatus = "unknown";

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

    public event EventHandler<string>? ThemeChanged;
    public event EventHandler<string>? LanguageChanged;
}

/// <summary>Composition root for UI-side services (no service locator magic).</summary>
internal static class AppServices
{
    public static UiState State { get; } = new();

    public static Infrastructure.SystemInterop.SystemContextProvider ContextProvider { get; } = new();

    public static Infrastructure.SystemInterop.WmiSystemInfoProvider SystemInfo { get; } = new();

    public static Infrastructure.Logging.JsonHistoryLogger History { get; } = new();

    public static Infrastructure.Persistence.FileSnapshotStore Snapshots { get; } = new();

    public static PrivilegedPipeClient Pipe { get; } = new();

    public static Core.Services.RegistryAccessor Registry { get; } = new();

    /// <summary>Catalog instances used read-only by the UI for detection.</summary>
    public static IReadOnlyList<Core.Abstractions.IOptimization> Catalog { get; } =
        Core.Catalog.OptimizationCatalog.All;
}
