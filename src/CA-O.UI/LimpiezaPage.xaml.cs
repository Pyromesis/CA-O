using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CAO.Shared.IPC;
using CAO.UI.Controls;

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
        "cleanup-windows-update-cache",
        "stale-crash-dump-cleanup",
        "cleanup-app-caches",
    ];

    private static readonly HashSet<string> HeavyIds = new(StringComparer.Ordinal)
    {
        "windows-component-store-cleanup",
        "windows-component-store-resetbase",
        "optimize-system-drive",
        "retrim-system-ssd",
        "cleanup-app-caches",
    };

    private static TimeSpan TimeoutFor(string id) =>
        HeavyIds.Contains(id) ? TimeSpan.FromMinutes(20) : TimeSpan.FromSeconds(90);

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
        if (!await ConfirmIfNeededAsync(id)) return;
        var target = TargetFor(id);
        await RunCleanupAsync(id, target, null);
    }

    private TextBlock? TargetFor(string id) => id switch
    {
        "flush-dns-cache" => NetworkStatusText,
        "windows-component-store-cleanup" or "windows-component-store-resetbase" => MaintenanceStatusText,
        "ensure-trim-enabled" or "retrim-system-ssd" or "optimize-system-drive"
            or "restore-system-managed-pagefile"
            or "disable-hibernate" => DiskStatusText,
        "enable-storage-sense" or "storage-sense-temp-cleanup"
            or "storage-sense-recycle-bin-policy" => SenseStatusText,
        "free-low-storage-space" => SpaceStatusText,
        "cleanup-prefetch-stale" or "cleanup-cbs-logs" or "cleanup-crash-dumps-extended"
            or "cleanup-outlook-cache" or "cleanup-browser-code-cache" => DeepStatusText,
        _ => CleanupStatusText,
    };

    private async Task<bool> ConfirmIfNeededAsync(string id)
    {
        var (title, content) = id switch
        {
            "windows-component-store-cleanup" => ("Limpiar WinSxS",
                "Ejecuta DISM /StartComponentCleanup. Tarda varios minutos y no se puede cancelar a la mitad. ¿Continuar?"),
            "windows-component-store-resetbase" => ("ResetBase irreversible",
                "IRREVERSIBLE: elimina la posibilidad de desinstalar actualizaciones de Windows. Solo para expertos con copia de seguridad. ¿Continuar?"),
            "optimize-system-drive" => ("Optimizar unidad",
                "Ejecuta desfragmentado/TRIM en C:. Tarda varios minutos y es mejor no usar el disco mientras tanto. ¿Continuar?"),
            "disable-hibernate" => ("Desactivar hibernación",
                "Libera varios GB (hiberfil.sys) pero desactiva hibernación e inicio rápido. Reversible. ¿Continuar?"),
            "cleanup-crash-dumps-extended" => ("Volcados extendidos",
                "Borra LiveKernelReports, MEMORY.DMP y CrashDumps de usuario de más de 30 días. Dificulta depurar fallos antiguos y no se puede deshacer. ¿Continuar?"),
            _ => (null, null),
        };
        if (title is null) return true;

        if (id == "windows-component-store-resetbase")
        {
            var expert = AppHost.Resolve<ViewModels.UiState>().ExpertMode;
            if (!expert)
            {
                var warn = new ContentDialog
                {
                    Title = "Requiere Modo Expert",
                    Content = "ResetBase es irreversible y de alto riesgo. Activa Modo Expert en Ajustes para confirmar que entiendes el riesgo.",
                    CloseButtonText = "Entendido",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = Content.XamlRoot,
                };
                await warn.ShowAsync();
                return false;
            }
        }

        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            PrimaryButtonText = "Continuar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot,
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async void OnCleanAllClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Limpieza rápida",
            Content = "Se limpiarán temporales de Windows, caché Delivery Optimization, restos de Windows Update, minidumps antiguos y cachés de apps (Discord, Spotify, Slack; con cada app cerrada). ¿Continuar?",
            PrimaryButtonText = "Limpiar todo",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        Mascot.Set("Working");
        ShowCleanupProgress("Limpieza rápida en curso…", 0);
        var parts = new List<string>();
        var okCount = 0;
        try
        {
            for (var i = 0; i < QuickCleanIds.Length; i++)
            {
                var id = QuickCleanIds[i];
                var pct = (double)i / QuickCleanIds.Length * 100;
                ShowCleanupProgress($"Limpiando {id} ({i + 1}/{QuickCleanIds.Length})…", pct);
                var message = await RunCleanupAsync(id, null, null);
                if (!string.IsNullOrEmpty(message)) parts.Add($"{id}: {message}");
                if (message?.StartsWith('✓') == true) okCount++;
            }
            CleanupStatusText.Text = parts.Count == 0 ? "Sin nada que limpiar." : string.Join("\n", parts);
            ShowCleanupProgress(okCount == QuickCleanIds.Length ? "✓ Limpieza rápida completada." : $"Limpieza rápida: {okCount}/{QuickCleanIds.Length} completadas.", 100);
            Mascot.CelebrateThenIdle(DispatcherQueue);
        }
        catch (Exception ex)
        {
            // Nunca se relanza desde un handler async void: se informa en la
            // propia página en vez de dejarlo caer a UnhandledException.
            Mascot.Set("Warn");
            CleanupStatusText.Text = $"No se pudo completar la limpieza: {ex.Message}";
            try { App.WriteCrashLog(ex); } catch { }
        }
        finally
        {
            try { await Task.Delay(1500); } catch { }
            HideCleanupProgress();
        }
    }

    private async Task<string?> RunCleanupAsync(string optimizationId, TextBlock? primary, TextBlock? secondary)
    {
        var target = optimizationId == "flush-dns-cache" ? secondary ?? primary : primary ?? secondary;
        Mascot.Set("Working");
        var batchRunning = CleanupProgressCard.Visibility == Visibility.Visible && CleanupRing.IsActive;
        if (!batchRunning)
            ShowCleanupProgress($"Limpiando {optimizationId}…", null);
        try
        {
            using var cts = new CancellationTokenSource(TimeoutFor(optimizationId));
            var pipe = AppHost.Resolve<PrivilegedPipeClient>();
            if (target != null && HeavyIds.Contains(optimizationId)) target.Text = $"{optimizationId}: en curso (puede tardar minutos)...";
            var response = await pipe.ApplyAsync(optimizationId, cts.Token);
            var ok = response is { Accepted: true };
            var message = ok
                ? "✓ Completado."
                : $"Rechazado [{response?.ErrorCode}]: {response?.SafeMessage ?? "sin respuesta"}";
            if (target != null) target.Text = $"{optimizationId}: {message}";
            if (!batchRunning)
            {
                ShowCleanupProgress(ok ? $"✓ {optimizationId} completado." : $"{optimizationId}: {message}", 100);
                if (ok) Mascot.CelebrateThenIdle(DispatcherQueue);
                else Mascot.Set("Warn");
                try { await Task.Delay(1500); } catch { }
                HideCleanupProgress();
            }
            return message;
        }
        catch (Exception ex)
        {
            var message = $"Servicio no disponible: {ex.Message}";
            if (target != null) target.Text = $"{optimizationId}: {message}";
            App.WriteCrashLog(ex);
            if (!batchRunning)
            {
                Mascot.Set("Warn");
                ShowCleanupProgress(message, 100);
                try { await Task.Delay(1500); } catch { }
                HideCleanupProgress();
            }
            return message;
        }
    }

    /// <summary>Muestra la tarjeta global de progreso. pct null = indeterminado. Nunca lanza.</summary>
    private void ShowCleanupProgress(string text, double? pct)
    {
        try
        {
            CleanupProgressCard.Visibility = Visibility.Visible;
            CleanupRing.IsActive = true;
            CleanupProgressText.Text = text;
            if (pct.HasValue)
            {
                CleanupProgressBar.IsIndeterminate = false;
                CleanupProgressBar.Value = Math.Clamp(pct.Value, 0, 100);
                CleanupPercentText.Text = $"{CleanupProgressBar.Value:0}%";
            }
            else
            {
                CleanupProgressBar.IsIndeterminate = true;
                CleanupPercentText.Text = string.Empty;
            }
        }
        catch { }
    }

    private void HideCleanupProgress()
    {
        try { CleanupProgressCard.Visibility = Visibility.Collapsed; CleanupRing.IsActive = false; } catch { }
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

        Mascot.Set("Working");
        ShowCleanupProgress("Vaciando papelera…", null);
        try
        {
            // SHEmptyRecycleBin exige hilo STA: en MTA (Task.Run) devuelve
            // E_UNEXPECTED (0x8000FFFF). Hilo STA dedicado sin UI.
            var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var thread = new Thread(() =>
            {
                try
                {
                    tcs.SetResult(SHEmptyRecycleBin(IntPtr.Zero, null,
                        SherbNoConfirmation | SherbNoProgressUi | SherbNoSound));
                }
                catch (Exception ex) { tcs.SetException(ex); }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            var result = await tcs.Task;
            var ok = result == 0;
            RecycleStatusText.Text = ok
                ? "✓ Papelera vaciada."
                : $"No se pudo vaciar (código 0x{result:X8}). Si persiste, abra la Papelera y vacíela a mano: puede estar dañada.";
            ShowCleanupProgress(ok ? "✓ Papelera vaciada." : "No se pudo vaciar la papelera.", 100);
            if (ok) Mascot.CelebrateThenIdle(DispatcherQueue);
            else Mascot.Set("Warn");
        }
        catch (Exception ex)
        {
            RecycleStatusText.Text = $"Error: {ex.Message}";
            Mascot.Set("Warn");
            App.WriteCrashLog(ex);
        }
        finally
        {
            try { await Task.Delay(1500); } catch { }
            HideCleanupProgress();
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

        Mascot.Set("Working");
        ShowCleanupProgress($"Aplicando timer {label}…", null);
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var pipe = AppHost.Resolve<PrivilegedPipeClient>();
            var response = await pipe.SetTimerResolutionAsync(resolution, cts.Token);
            var ok = response is { Accepted: true };
            TimerStatusText.Text = ok
                ? $"✓ Aplicado: {label}."
                : $"Rechazado [{response?.ErrorCode}]: {response?.SafeMessage ?? "sin respuesta"}";
            ShowCleanupProgress(ok ? $"✓ Timer aplicado: {label}." : TimerStatusText.Text, 100);
            if (ok) Mascot.CelebrateThenIdle(DispatcherQueue);
            else Mascot.Set("Warn");
        }
        catch (Exception ex)
        {
            TimerStatusText.Text = $"Servicio no disponible: {ex.Message}";
            Mascot.Set("Warn");
            App.WriteCrashLog(ex);
        }
        finally
        {
            try { await Task.Delay(1500); } catch { }
            HideCleanupProgress();
        }
        RefreshTimerDisplay();
    }
}
