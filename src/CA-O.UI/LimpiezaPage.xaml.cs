using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CAO.Shared.IPC;

namespace CAO.UI.Pages;

/// <summary>Limpieza rápida: temporales, papelera, DNS y timer resolution.</summary>
public sealed partial class LimpiezaPage : Page
{
    [DllImport("ntdll.dll")]
    private static extern int NtQueryTimerResolution(out uint max, out uint min, out uint current);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? rootPath, uint flags);

    private const uint SherbNoConfirmation = 0x1;
    private const uint SherbNoProgressUi = 0x2;
    private const uint SherbNoSound = 0x4;

    private static readonly string[] QuickCleanIds =
    [
        "cleanup-windows-temp",
        "cleanup-delivery-optimization-cache",
        "disk-cleanup-system-files",
        "stale-crash-dump-cleanup",
    ];

    public LimpiezaPage()
    {
        try { InitializeComponent(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"LimpiezaPage init failed: {ex}"); throw; }
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        Helpers.UiAnimations.PlayEntrance(PageContent);
        RefreshTimerDisplay();
    }

    private static string FormatMs(uint value100Ns) =>
        (value100Ns / 10000.0).ToString("0.###") + " ms";

    private void RefreshTimerDisplay()
    {
        try
        {
            if (NtQueryTimerResolution(out _, out _, out var current) == 0)
                TimerCurrentText.Text = $"Actual: {FormatMs(current)}";
            else
                TimerCurrentText.Text = "Actual: desconocido";
        }
        catch { TimerCurrentText.Text = "Actual: desconocido"; }
    }

    private async void OnCleanClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string id }) return;
        await RunCleanupAsync(id, CleanupStatusText, NetworkStatusText);
    }

    private async void OnCleanAllClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Limpieza rápida",
            Content = "Se limpiarán temporales de Windows, caché Delivery Optimization, restos de Windows Update y minidumps antiguos. ¿Continuar?",
            PrimaryButtonText = "Limpiar todo",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        var parts = new List<string>();
        foreach (var id in QuickCleanIds)
        {
            var message = await RunCleanupAsync(id, null, null);
            if (!string.IsNullOrEmpty(message)) parts.Add($"{id}: {message}");
        }
        CleanupStatusText.Text = parts.Count == 0 ? "Sin nada que limpiar." : string.Join("\n", parts);
    }

    /// <summary>Ejecuta una limpieza y devuelve el mensaje corto para resúmenes.</summary>
    private async Task<string?> RunCleanupAsync(string optimizationId, TextBlock? primary, TextBlock? secondary)
    {
        var target = optimizationId == "flush-dns-cache" ? secondary ?? primary : primary ?? secondary;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            var pipe = AppHost.Resolve<PrivilegedPipeClient>();
            var response = await pipe.ApplyAsync(optimizationId, cts.Token);
            var message = response is { Accepted: true }
                ? "✓ Completado."
                : $"Rechazado [{response?.ErrorCode}]: {response?.SafeMessage ?? "sin respuesta"}";
            if (target != null) target.Text = $"{optimizationId}: {message}";
            return message;
        }
        catch (Exception ex)
        {
            var message = $"Servicio no disponible: {ex.Message}";
            if (target != null) target.Text = $"{optimizationId}: {message}";
            App.WriteCrashLog(ex);
            return message;
        }
    }

    private async void OnEmptyRecycleBinClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Vaciar papelera",
            Content = "Se vaciarán todas las papeleras de reciclaje. No se puede deshacer. ¿Continuar?",
            PrimaryButtonText = "Vaciar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        try
        {
            var result = await Task.Run(() => SHEmptyRecycleBin(IntPtr.Zero, null,
                SherbNoConfirmation | SherbNoProgressUi | SherbNoSound));
            RecycleStatusText.Text = result == 0
                ? "✓ Papelera vaciada."
                : $"No se pudo vaciar (código 0x{result:X}).";
        }
        catch (Exception ex)
        {
            RecycleStatusText.Text = $"Error: {ex.Message}";
            App.WriteCrashLog(ex);
        }
    }

    private async void OnTimerClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tag } || !uint.TryParse(tag, out var resolution)) return;
        var label = resolution >= 156250 ? "predeterminado (15,625 ms)" : $"{resolution / 10000.0:0.###} ms";

        var dialog = new ContentDialog
        {
            Title = "Cambiar timer resolution",
            Content = $"Se fijará el timer del sistema a {label} mientras el servicio privilegiado corra. ¿Continuar?",
            PrimaryButtonText = "Aplicar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var pipe = AppHost.Resolve<PrivilegedPipeClient>();
            var response = await pipe.SetTimerResolutionAsync(resolution, cts.Token);
            TimerStatusText.Text = response is { Accepted: true }
                ? $"✓ Aplicado: {label}."
                : $"Rechazado [{response?.ErrorCode}]: {response?.SafeMessage ?? "sin respuesta"}";
        }
        catch (Exception ex)
        {
            TimerStatusText.Text = $"Servicio no disponible: {ex.Message}";
            App.WriteCrashLog(ex);
        }
        RefreshTimerDisplay();
    }
}
