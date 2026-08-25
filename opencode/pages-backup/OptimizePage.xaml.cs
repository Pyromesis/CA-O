using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CAO.Core.Engine;
using CAO.Shared;

namespace CAO.UI.Pages;

/// <summary>
/// Optimization cards (spec 79): name, description, current vs target state,
/// evidence, risk, security and anti-cheat impact, reboot requirement,
/// rollback availability. Apply is restricted to Recommended unless Expert
/// mode is on, and every apply travels through the privileged service.
/// </summary>
public sealed partial class OptimizePage : Page
{
    /// <summary>Row view-model binding the card template.</summary>
    public sealed record RecommendationRow(
        string OptimizationId,
        string NameEs,
        string DescriptionEs,
        string BucketLabel,
        string RebootLabel,
        string Evidence,
        string Risk,
        string SecurityImpact,
        string Compatibility,
        string CurrentState,
        string ScoreLabel,
        string ReasonMessage,
        RecommendationBucket Bucket);

    public OptimizePage()
    {
        InitializeComponent();
        ApplyTexts();
    }

    private void ApplyTexts()
    {
        ApplyRecommendedButton.Content = Localizer.Get("optimize.applyRecommended");
        ExpertBar.Message = Localizer.Get("optimize.expertWarning");
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        Render();
    }

    private void Render()
    {
        var expert = AppServices.State.ExpertMode;
        ExpertBar.IsOpen = expert;

        var rows = AppServices.State.Recommendations
            .OrderBy(row => row.Bucket switch
            {
                RecommendationBucket.Recommended => 0,
                RecommendationBucket.Optional => 1,
                RecommendationBucket.Experimental => 2,
                RecommendationBucket.SecuritySensitive => 3,
                _ => 4,
            })
            .Select(recommendation => new RecommendationRow(
                recommendation.OptimizationId,
                recommendation.NameEs,
                recommendation.DescriptionEs,
                recommendation.Bucket.ToString(),
                recommendation.RequiresReboot ? "requiere reinicio" : string.Empty,
                recommendation.Evidence.ToString(),
                recommendation.Risk.ToString(),
                recommendation.SecurityImpact.ToString(),
                recommendation.Compatibility.ToString(),
                recommendation.CurrentState.ToString(),
                recommendation.Score?.ToString() ?? "n/a",
                recommendation.Reason.MessageEs,
                recommendation.Bucket))
            .ToList();

        RecommendationsList.ItemsSource = rows;
    }

    private async void OnApplyClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string id }) return;
        if (!CanOperate(id))
        {
            StatusText.Text = "Sólo se aplican cambios Recomendados (o Expert con confirmación).";
            return;
        }

        await RunOperationAsync(PrivilegedOperation.ApplyOptimization, id);
    }

    private async void OnRevertClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string id }) return;
        await RunOperationAsync(PrivilegedOperation.RevertOptimization, id);
    }

    private bool CanOperate(string optimizationId) =>
        AppServices.State.Recommendations.Any(recommendation =>
            recommendation.OptimizationId == optimizationId &&
            (recommendation.Bucket == RecommendationBucket.Recommended || AppServices.State.ExpertMode));

    private async Task RunOperationAsync(PrivilegedOperation operation, string optimizationId)
    {
        if (AppServices.State.ExpertMode)
        {
            var dialog = new ContentDialog
            {
                Title = "Confirmar operación",
                Content = $"Se ejecutará '{operation}' sobre '{optimizationId}'. Se creará un snapshot previo.",
                PrimaryButtonText = "Continuar",
                CloseButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = Content.XamlRoot,
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }
        }

        BusyRing.IsActive = true;
        try
        {
            var response = await AppServices.Pipe.SendAsync(operation, optimizationId);
            AppServices.State.ServiceStatus = response is { Accepted: true } ? "connected" : "rejected";
            StatusText.Text = response is { Accepted: true }
                ? $"OK: {response.MessageEs}"
                : $"Rechazado: {response?.Error ?? "sin respuesta del servicio"}";

            await RefreshRecommendationsAsync();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Servicio no disponible: {ex.Message}";
        }
        finally
        {
            BusyRing.IsActive = false;
        }
    }

    private async void OnApplyRecommendedClick(object sender, RoutedEventArgs e)
    {
        var recommended = AppServices.State.Recommendations
            .Where(recommendation => recommendation.Bucket == RecommendationBucket.Recommended &&
                                     recommendation.CurrentState != OptimizationState.AppliedByCao)
            .Select(recommendation => recommendation.OptimizationId)
            .ToList();

        if (recommended.Count == 0)
        {
            StatusText.Text = "No hay recomendaciones pendientes; ejecute el análisis primero.";
            return;
        }

        BusyRing.IsActive = true;
        var failures = new List<string>();
        try
        {
            foreach (var id in recommended)
            {
                var response = await AppServices.Pipe.SendAsync(PrivilegedOperation.ApplyOptimization, id);
                if (response is not { Accepted: true })
                {
                    failures.Add($"{id}: {response?.Error ?? "sin respuesta"}");
                    break; // stop the batch on first failure (spec 124)
                }
            }

            await RefreshRecommendationsAsync();
            StatusText.Text = failures.Count == 0
                ? $"Aplicados {recommended.Count} cambios recomendados."
                : $"Lote detenido: {string.Join("; ", failures)}";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Servicio no disponible: {ex.Message}";
        }
        finally
        {
            BusyRing.IsActive = false;
        }
    }

    private async Task RefreshRecommendationsAsync()
    {
        var context = AppServices.State.Context ?? await AppServices.ContextProvider.GetAsync();
        AppServices.State.Context = context;
        AppServices.State.Recommendations = RecommendationEngine.BuildAll(AppServices.Catalog, AppServices.Registry, context);
        Render();
    }
}
