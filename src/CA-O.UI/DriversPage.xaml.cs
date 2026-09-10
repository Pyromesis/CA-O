using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CAO.Infrastructure.SystemInterop;
using CAO.Shared;
using CAO.UI.Helpers;

namespace CAO.UI.Pages;

/// <summary>Pestaña Controladores: inventario total SetupAPI + WMI, conflictos y fixes. Solo lectura salvo fix/inf.</summary>
public sealed partial class DriversPage : Page
{
    private sealed record DriverRow(string Title, string Detail);

    private IReadOnlyList<DriverDiagnostic> _drivers = Array.Empty<DriverDiagnostic>();
    private CancellationTokenSource? _cts;
    private string _vendorUrl = string.Empty;

    public DriversPage()
    {
        try { InitializeComponent(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"DriversPage init failed: {ex}"); throw; }
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        Helpers.UiAnimations.PlayEntrance(PageContent);
    }

    private async void OnScanClick(object sender, RoutedEventArgs e)
    {
        try { _cts?.Cancel(); } catch { }
        _cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        ScanButton.IsEnabled = false;
        ScanRing.IsActive = true;
        ScanRing.Visibility = Visibility.Visible;
        ScanStatusText.Text = "Leyendo todos los dispositivos (SetupAPI + WMI)...";
        try
        {
            var report = await new DriverDiagnosticsProvider().MeasureAsync(_cts.Token);
            _drivers = report.Drivers
                .Where(d => !string.IsNullOrWhiteSpace(d.Name))
                .OrderBy(d => d.Name)
                .ToList();
            RenderSummary();
            RenderConflicts();
            RenderInventory();
            await RenderOemAsync(_cts.Token);
            ScanStatusText.Text = $"Escaneo completo: {_drivers.Count} dispositivos ({report.TimestampUtc.ToLocalTime():g}). Incluye ocultos y sin controlador. Solo lectura.";
        }
        catch (OperationCanceledException)
        {
            ScanStatusText.Text = "Escaneo cancelado (30 s sin respuesta). Reintenta.";
        }
        catch (Exception ex)
        {
            ScanStatusText.Text = $"No se pudo escanear: {ex.Message}";
            App.WriteCrashLog(ex);
        }
        finally
        {
            ScanRing.IsActive = false;
            ScanRing.Visibility = Visibility.Collapsed;
            ScanButton.IsEnabled = true;
        }
    }

    private void RenderSummary()
    {
        TotalText.Text = _drivers.Count.ToString();
        ProblemsText.Text = _drivers.Count(DriverConflicts.IsProblem).ToString();
        UnsignedText.Text = _drivers.Count(DriverConflicts.IsUnsigned).ToString();
        MissingText.Text = _drivers.Count(DriverConflicts.IsMissing).ToString();
    }

    private sealed record ConflictRow(string InstanceId, string Title, string Detail, string FixLabel, string FixAction, bool CanFix);

    private static (string Label, string Action) SuggestFix(DriverDiagnostic d) => d.ProblemCode switch
    {
        22 => ("Habilitar", CAO.Shared.IPC.FixDriverActions.Enable),
        28 => ("Re-detectar", CAO.Shared.IPC.FixDriverActions.Rescan),
        _ => ("Reinstalar", CAO.Shared.IPC.FixDriverActions.Reinstall),
    };

    private void RenderConflicts()
    {
        var problems = _drivers.Where(DriverConflicts.IsProblem).ToList();
        if (problems.Count == 0)
        {
            ConflictsText.Visibility = Visibility.Visible;
            ConflictsText.Text = _drivers.Count == 0
                ? "Sin escanear: pulsa Escanear para detectar conflictos."
                : "✓ Sin conflictos: todos los dispositivos arrancan bien.";
            ConflictsList.Visibility = Visibility.Collapsed;
            return;
        }
        ConflictsText.Visibility = Visibility.Collapsed;
        ConflictsList.Visibility = Visibility.Visible;
        ConflictsList.ItemsSource = problems.Select(d =>
        {
            var (label, action) = SuggestFix(d);
            var provider = string.IsNullOrWhiteSpace(d.Provider) ? d.Manufacturer : d.Provider;
            return new ConflictRow(
                d.PnpDeviceId,
                string.IsNullOrWhiteSpace(d.DeviceClass) ? d.Name : $"{d.Name} [{d.DeviceClass}]",
                ($"{DriverConflicts.DescribeProblem(d.ProblemCode)} · {provider} v{d.Version} · {d.InfName}".Trim(' ', '·') +
                 DriverIdsLine(d)).Trim(),
                label, action,
                CanFix: !string.IsNullOrWhiteSpace(d.PnpDeviceId));
        }).ToList();
    }

    private async void OnFixDriverClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ConflictRow row } || !row.CanFix) return;
        var explain = row.FixAction switch
        {
            "enable" => "Se habilitará el dispositivo. Inmediato y reversible.",
            "rescan" => "Se re-detectará el hardware (pnputil /scan-devices). No borra nada.",
            _ => "Se desinstalará el dispositivo y se re-detectará para recargar su controlador de la tienda de drivers. Podría pedir reinicio.",
        };
        var confirm = new ContentDialog
        {
            Title = $"{row.FixLabel}: {row.Title}",
            Content = new TextBlock { Text = $"{explain}\n¿Continuar?", TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = row.FixLabel,
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot,
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

        FixStatusText.Visibility = Visibility.Visible;
        FixStatusText.Text = $"Corrigiendo {row.Title}...";
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            var pipe = AppHost.Resolve<PrivilegedPipeClient>();
            var response = await pipe.FixDriverAsync(row.InstanceId, row.FixAction, cts.Token);
            FixStatusText.Text = response is { Accepted: true }
                ? $"✓ {row.Title}: corregido y verificado (ya no reporta problema)."
                : $"{row.Title}: rechazado [{response?.ErrorCode}]: {response?.SafeMessage ?? "sin respuesta"}";
        }
        catch (Exception ex)
        {
            FixStatusText.Text = $"{row.Title}: servicio no disponible ({ex.Message})";
            App.WriteCrashLog(ex);
        }
        // Re-escanear para refrescar contadores y listas con el estado real.
        OnScanClick(ScanButton, new RoutedEventArgs());
    }

    private async Task RenderOemAsync(CancellationToken ct)
    {
        try
        {
            var info = await new DriverDiagnosticsProvider().GetComputerInfoAsync(ct);
            var support = Helpers.VendorDriverSupport.Resolve(info);
            _vendorUrl = support.DriversUrl;
            var board = string.IsNullOrWhiteSpace(info.BoardProduct) ? info.BoardManufacturer
                : $"{info.BoardManufacturer} {info.BoardProduct}".Trim();
            var sysModel = string.IsNullOrWhiteSpace(info.Model) ? "" : $" {info.Model}";
            OemText.Text = $"Equipo: {info.Manufacturer}{sysModel} · Placa: {(string.IsNullOrWhiteSpace(board) ? "—" : board)} · " +
                $"Serie: {Helpers.VendorDriverSupport.MaskSerial(info.SerialNumber)} · {support.Vendor} (por {support.DetectedBy})";
            VendorLinkText.Text = $"{support.Note}\n{support.DriversUrl}";
            CopyVendorLinkButton.IsEnabled = true;
        }
        catch
        {
            OemText.Text = "No se pudo identificar el equipo.";
            CopyVendorLinkButton.IsEnabled = false;
        }
    }

    private void OnCopyVendorLinkClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_vendorUrl)) return;
        try
        {
            var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
            package.SetText(_vendorUrl);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
            OemText.Text += " (enlace copiado)";
        }
        catch (Exception ex) { App.WriteCrashLog(ex); }
    }

    private async Task<string?> PickInfAsync()
    {
        try
        {
            var window = CAO.UI.MainWindow.Current;
            if (window is null) return null;
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(window));
            picker.FileTypeFilter.Add(".inf");
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Downloads;
            var file = await picker.PickSingleFileAsync();
            return file?.Path;
        }
        catch (Exception ex)
        {
            InfStatusText.Visibility = Visibility.Visible;
            InfStatusText.Text = $"No se pudo abrir el selector: {ex.Message}";
            App.WriteCrashLog(ex);
            return null;
        }
    }

    private async void OnPickInfClick(object sender, RoutedEventArgs e)
    {
        var path = await PickInfAsync();
        if (string.IsNullOrWhiteSpace(path)) return;
        await InstallInfAsync(path, string.Empty, null);
    }

    private async void OnRowInfClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ConflictRow row } || !row.CanFix) return;
        var path = await PickInfAsync();
        if (string.IsNullOrWhiteSpace(path)) return;
        await InstallInfAsync(path, row.InstanceId, row.Title);
    }

    private async Task InstallInfAsync(string infPath, string instanceId, string? deviceTitle)
    {
        var target = string.IsNullOrWhiteSpace(deviceTitle) ? "el dispositivo" : deviceTitle;
        var confirm = new ContentDialog
        {
            Title = "Instalar controlador original",
            Content = new TextBlock
            {
                Text = $"Se instalará:\n{infPath}\n\nPara: {target}\nSolo .inf descargados de la web oficial del fabricante. Podría pedir reinicio. ¿Continuar?",
                TextWrapping = TextWrapping.Wrap,
            },
            PrimaryButtonText = "Instalar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot,
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

        InfStatusText.Visibility = Visibility.Visible;
        InfStatusText.Text = $"Instalando {Path.GetFileName(infPath)}...";
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            var pipe = AppHost.Resolve<PrivilegedPipeClient>();
            var response = await pipe.InstallDriverAsync(infPath, instanceId, cts.Token);
            InfStatusText.Text = response is { Accepted: true }
                ? $"✓ {Path.GetFileName(infPath)}: instalado y verificado."
                : $"Rechazado [{response?.ErrorCode}]: {response?.SafeMessage ?? "sin respuesta"}";
        }
        catch (Exception ex)
        {
            InfStatusText.Text = $"Servicio no disponible ({ex.Message})";
            App.WriteCrashLog(ex);
        }
        OnScanClick(ScanButton, new RoutedEventArgs());
    }

    /// <summary>Segunda línea con los IDs uno a uno (instancia, hardware, INF, proveedor).</summary>
    private static string DriverIdsLine(DriverDiagnostic d)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(d.PnpDeviceId)) parts.Add($"ID: {d.PnpDeviceId}");
        if (!string.IsNullOrWhiteSpace(d.HardwareId)) parts.Add($"HW: {d.HardwareId}");
        if (!string.IsNullOrWhiteSpace(d.InfName)) parts.Add($"INF: {d.InfName}");
        var provider = string.IsNullOrWhiteSpace(d.Provider) ? d.Manufacturer : d.Provider;
        if (!string.IsNullOrWhiteSpace(provider)) parts.Add($"Prov: {provider}");
        return parts.Count == 0 ? string.Empty : "\n" + string.Join(" · ", parts);
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e) => RenderInventory();

    private void RenderInventory()
    {
        var query = SearchBox?.Text?.Trim() ?? string.Empty;
        var filtered = string.IsNullOrEmpty(query)
            ? _drivers
            : _drivers.Where(d =>
                d.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                d.DeviceClass.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                d.Manufacturer.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        DriversList.ItemsSource = filtered.Select(d => new DriverRow(
            string.IsNullOrWhiteSpace(d.DeviceClass) ? d.Name : $"{d.Name} [{d.DeviceClass}]",
            $"{d.Manufacturer} · v{d.Version} · {DriverConflicts.FormatDriverDate(d.Date)} · {DriverConflicts.SignedLabel(d.IsSigned)}" +
            (d.IsPresent ? string.Empty : " · Oculto/no presente") +
            (d.ProblemCode != 0 ? $" · {DriverConflicts.DescribeProblem(d.ProblemCode)}" : string.Empty) +
            DriverIdsLine(d))).ToList();
        InventoryCountText.Text = _drivers.Count == 0
            ? string.Empty
            : $"{filtered.Count} de {_drivers.Count} en lista.";
    }
}
