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
    private IReadOnlyList<string> _recoveryCandidates = Array.Empty<string>();

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

    /// <summary>Optimization ids left Incomplete by a previous crash (spec 13).</summary>
    public IReadOnlyList<string> RecoveryCandidates
    {
        get => _recoveryCandidates;
        set => SetProperty(ref _recoveryCandidates, value);
    }

    public event EventHandler<string>? ThemeChanged;
    public event EventHandler<string>? LanguageChanged;
}
