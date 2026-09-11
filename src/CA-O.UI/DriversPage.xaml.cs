using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CAO.Core.Optimization;
using CAO.Infrastructure.SystemInterop;
using CAO.Shared;
using CAO.UI.Helpers;

namespace CAO.UI.Pages;

/// <summary>Pestaña Controladores: inventario total SetupAPI + WMI, conflictos y fixes. Solo lectura salvo fix/inf.</summary>
public sealed partial class DriversPage : Page
{
    private sealed record DriverRow(string Title, string Detail);

    private sealed record DriverUpdateRow(string UpdateId, string Title, string Detail);

    private sealed record ExportDriverResult(string Path, int Files, string Message);

    private static readonly JsonSerializerOptions CaseInsensitiveJson = new() { PropertyNameCaseInsensitive = true };

    private IReadOnlyList<DriverDiagnostic> _drivers = Array.Empty<DriverDiagnostic>();
    private IReadOnlyList<DriverDiagnostic> _phantoms = Array.Empty<DriverDiagnostic>();
    private List<DriverUpdateInfo> _wuUpdates = new();
    private readonly HashSet<string> _wuSelected = new(StringComparer.OrdinalIgnoreCase);
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
            RenderPhantoms();
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

    private sealed record ConflictRow(string InstanceId, string Title, string Detail, string FixLabel, string FixAction, bool CanFix, string HwId, string Name);

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
            UpdateAllButton.IsEnabled = false;
            UpdateAllButton.Content = "Actualizar todo";
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
                CanFix: !string.IsNullOrWhiteSpace(d.PnpDeviceId),
                d.HardwareId, d.Name);
        }).ToList();
        var updatable = problems.Count(d => !string.IsNullOrWhiteSpace(d.HardwareId));
        UpdateAllButton.IsEnabled = updatable > 0;
        UpdateAllButton.Content = updatable > 0 ? $"Actualizar todo ({updatable})" : "Actualizar todo";
    }

    /// <summary>
    /// Copia el HWID y muestra el enlace oficial del catálogo (página
    /// confiable de Microsoft). No abre navegador (la UI no lanza procesos).
    /// </summary>
    private void OnCopyHwidClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ConflictRow row } || string.IsNullOrWhiteSpace(row.HwId)) return;
        try
        {
            var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
            package.SetText(row.HwId);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
            var url = "https://www.catalog.update.microsoft.com/Search.aspx?q=" + Uri.EscapeDataString(row.HwId);
            FixStatusText.Visibility = Visibility.Visible;
            FixStatusText.Text = $"HWID copiado: {row.HwId}\nBúscalo en la página oficial: {url}";
        }
        catch (Exception ex) { App.WriteCrashLog(ex); }
    }

    /// <summary>
    /// Modo automático: por cada dispositivo con problema y HWID, busca la
    /// oferta oficial más nueva, la descarga y la instala. Una confirmación,
    /// un punto de restauración, resumen final. Lo que no se puede resolver
    /// solo se reporta con su motivo (sin oferta, misma versión, sin INF…).
    /// </summary>
    private async void OnUpdateAllClick(object sender, RoutedEventArgs e)
    {
        var candidates = _drivers
            .Where(d => DriverConflicts.IsProblem(d) && !string.IsNullOrWhiteSpace(d.HardwareId))
            .ToList();
        if (candidates.Count == 0) return;
        var preview = string.Join("\n", candidates.Take(8).Select(d => $"• {d.Name}"));
        var confirm = new ContentDialog
        {
            Title = $"Actualizar {candidates.Count} drivers automáticamente",
            Content = new TextBlock
            {
                Text = $"Se buscará el driver oficial más nuevo por dispositivo, se descargará del catálogo de Microsoft y se instalará:\n{preview}" +
                    (candidates.Count > 8 ? $"\n…y {candidates.Count - 8} más." : string.Empty) +
                    "\n\nSe crea un punto de restauración antes (si el sistema lo permite). Puede tardar y pedir reinicio. ¿Continuar?",
                TextWrapping = TextWrapping.Wrap,
            },
            PrimaryButtonText = "Actualizar todo",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot,
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

        UpdateAllButton.IsEnabled = false;
        UpdateAllStatusText.Visibility = Visibility.Visible;
        var pipe = AppHost.Resolve<PrivilegedPipeClient>();
        using var master = new CancellationTokenSource(TimeSpan.FromMinutes(45));

        try
        {
            UpdateAllStatusText.Text = "Creando punto de restauración…";
            string rpNote;
            try
            {
                using var rpCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
                var (created, reason) = await new WmiRestorePointService()
                    .CreateAsync("CA-O: actualización automática de drivers", rpCts.Token);
                rpNote = created ? "Punto de restauración creado. " : $"Sin punto ({reason}). ";
            }
            catch { rpNote = "Sin punto de restauración. "; }

            int done = 0, skipped = 0, failed = 0;
            var failures = new List<string>();
            for (int i = 0; i < candidates.Count; i++)
            {
                master.Token.ThrowIfCancellationRequested();
                var d = candidates[i];
                UpdateAllStatusText.Text = $"{rpNote}{i + 1}/{candidates.Count}: {d.Name}… (buscando)";
                CatalogSearchResult? search = await SearchCatalogQuietAsync(pipe, d, master.Token);
                if (search is null || search.Offers.Count == 0) { skipped++; failures.Add($"{d.Name}: sin oferta oficial"); continue; }
                var best = search.Offers[0];
                if (!CatalogDriverProtocol.ShouldTryOffer(d.Version, best.Version)) { skipped++; continue; }
                UpdateAllStatusText.Text = $"{i + 1}/{candidates.Count}: {d.Name}… (descargando {best.Version})";
                CatalogDownloadResult? pkg = await DownloadCatalogQuietAsync(pipe, best.UpdateId, d.HardwareId, master.Token);
                if (pkg is null || pkg.InfPaths.Count == 0) { skipped++; failures.Add($"{d.Name}: sin INF utilizable"); continue; }
                var inf = CatalogDriverProtocol.PickBestInfMatch(pkg.InfPaths, d.HardwareId) ?? pkg.InfPaths[0];
                UpdateAllStatusText.Text = $"{i + 1}/{candidates.Count}: {d.Name}… (instalando)";
                try
                {
                    using var installCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
                    var installed = await pipe.InstallDriverAsync(inf, d.PnpDeviceId, installCts.Token);
                    if (installed is { Accepted: true }) done++;
                    else { failed++; failures.Add($"{d.Name}: {installed?.SafeMessage ?? "rechazado"}"); }
                }
                catch (Exception ex) { failed++; failures.Add($"{d.Name}: {ex.Message}"); }
            }
            var summary = $"Hecho: {done} actualizados, {skipped} omitidos, {failed} fallidos.";
            if (failures.Count > 0)
                summary += "\n" + string.Join("\n", failures.Take(10)) + (failures.Count > 10 ? $"\n…y {failures.Count - 10} más." : string.Empty);
            UpdateAllStatusText.Text = summary + " Re-escaneando…";
        }
        catch (OperationCanceledException)
        {
            UpdateAllStatusText.Text = "Actualización cancelada o tiempo agotado. Re-escaneando…";
        }
        catch (Exception ex)
        {
            UpdateAllStatusText.Text = $"Error ({ex.Message}). Re-escaneando…";
            App.WriteCrashLog(ex);
        }
        OnScanClick(ScanButton, new RoutedEventArgs());
    }

    private static async Task<CatalogSearchResult?> SearchCatalogQuietAsync(
        PrivilegedPipeClient pipe, DriverDiagnostic d, CancellationToken ct)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromMinutes(5));
            var response = await pipe.SearchCatalogDriversAsync(d.HardwareId, d.Name, cts.Token);
            if (response is not { Accepted: true } || string.IsNullOrWhiteSpace(response.DetailJson)) return null;
            return JsonSerializer.Deserialize<CatalogSearchResult>(response.DetailJson, CaseInsensitiveJson);
        }
        catch { return null; }
    }

    private static async Task<CatalogDownloadResult?> DownloadCatalogQuietAsync(
        PrivilegedPipeClient pipe, string updateId, string hardwareId, CancellationToken ct)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromMinutes(30));
            var response = await pipe.DownloadCatalogDriverAsync(updateId, hardwareId, cts.Token);
            if (response is not { Accepted: true } || string.IsNullOrWhiteSpace(response.DetailJson)) return null;
            return JsonSerializer.Deserialize<CatalogDownloadResult>(response.DetailJson, CaseInsensitiveJson);
        }
        catch { return null; }
    }

    /// <summary>
    /// Fantasmas: no presentes (restos de hardware desconectado). Se listan
    /// aparte y se limpian en bloque con confirmación; el servicio solo toca
    /// lo que no esté arrancado. Los datos sobreviven al cambio de pestaña
    /// (página cacheada) hasta el próximo escaneo.
    /// </summary>
    private void RenderPhantoms()
    {
        _phantoms = _drivers.Where(d => !d.IsPresent && !string.IsNullOrWhiteSpace(d.PnpDeviceId)).ToList();
        if (_phantoms.Count == 0)
        {
            PhantomsText.Text = _drivers.Count == 0
                ? "Aparecen al escanear."
                : "✓ Sin fantasmas: no hay restos de hardware desconectado.";
            PhantomsList.Visibility = Visibility.Collapsed;
            PhantomsList.ItemsSource = null;
            CleanPhantomsButton.IsEnabled = false;
            CleanPhantomsButton.Content = "Limpiar fantasmas";
            return;
        }
        PhantomsText.Text = $"{_phantoms.Count} restos de hardware desconectado (audio, USB, monitores…). Suelen causar conflictos: se pueden desinstalar sin riesgo, el driver queda en la tienda.";
        PhantomsList.ItemsSource = _phantoms
            .OrderBy(d => d.Name)
            .Select(d => new DriverRow(
                string.IsNullOrWhiteSpace(d.DeviceClass) ? d.Name : $"{d.Name} [{d.DeviceClass}]",
                $"ID: {d.PnpDeviceId}" +
                (string.IsNullOrWhiteSpace(d.HardwareId) ? string.Empty : $" · HW: {d.HardwareId}")))
            .ToList();
        PhantomsList.Visibility = Visibility.Visible;
        CleanPhantomsButton.IsEnabled = true;
        CleanPhantomsButton.Content = $"Limpiar {_phantoms.Count} fantasmas";
    }

    private async void OnCleanPhantomsClick(object sender, RoutedEventArgs e)
    {
        if (_phantoms.Count == 0) return;
        var sample = string.Join("\n", _phantoms.Take(6).Select(d => $"• {d.Name}"));
        var confirm = new ContentDialog
        {
            Title = $"Limpiar {_phantoms.Count} fantasmas",
            Content = new TextBlock
            {
                Text = $"Se desinstalarán estos restos (el driver NO se borra):\n{sample}" +
                    (_phantoms.Count > 6 ? $"\n…y {_phantoms.Count - 6} más." : string.Empty) +
                    "\n\nSolo se toca lo no presente y no arrancado; lo en uso se omite. ¿Continuar?",
                TextWrapping = TextWrapping.Wrap,
            },
            PrimaryButtonText = "Limpiar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot,
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

        PhantomStatusText.Visibility = Visibility.Visible;
        PhantomStatusText.Text = $"Limpiando {_phantoms.Count} fantasmas…";
        CleanPhantomsButton.IsEnabled = false;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
            var pipe = AppHost.Resolve<PrivilegedPipeClient>();
            var ids = _phantoms.Select(d => d.PnpDeviceId).ToList();
            var response = await pipe.RemovePhantomDevicesAsync(ids, cts.Token);
            PhantomStatusText.Text = response is { Accepted: true }
                ? $"✓ {response.DetailJson ?? "Limpieza completada."}"
                : $"Rechazado [{response?.ErrorCode}]: {response?.SafeMessage ?? "sin respuesta"}";
        }
        catch (Exception ex)
        {
            PhantomStatusText.Text = $"Servicio no disponible ({ex.Message})";
            App.WriteCrashLog(ex);
        }
        OnScanClick(ScanButton, new RoutedEventArgs());
    }

    private async void OnSearchDriverUpdatesClick(object sender, RoutedEventArgs e)
    {
        SearchUpdatesButton.IsEnabled = false;
        UpdatesRing.IsActive = true;
        UpdatesRing.Visibility = Visibility.Visible;
        UpdatesStatusText.Text = "Buscando en Windows Update (en línea, puede tardar minutos)…";
        DriverUpdatesList.Visibility = Visibility.Collapsed;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
            var pipe = AppHost.Resolve<PrivilegedPipeClient>();
            var response = await pipe.SearchDriverUpdatesAsync(cts.Token);
            if (response is not { Accepted: true } || string.IsNullOrWhiteSpace(response.DetailJson))
            {
                UpdatesStatusText.Text = $"Sin resultados [{response?.ErrorCode}]: {response?.SafeMessage ?? "sin respuesta"}";
                return;
            }
            try
            {
                _wuUpdates = JsonSerializer.Deserialize<List<DriverUpdateInfo>>(response.DetailJson, CaseInsensitiveJson) ?? new();
            }
            catch
            {
                _wuUpdates = new();
            }
            _wuSelected.Clear();
            if (_wuUpdates.Count == 0)
            {
                UpdatesStatusText.Text = "Windows Update no ofrece drivers para este equipo. Todo al día.";
                return;
            }
            DriverUpdatesList.ItemsSource = _wuUpdates.Select(u => new DriverUpdateRow(
                u.UpdateId,
                string.IsNullOrWhiteSpace(u.Title) ? u.UpdateId : u.Title,
                $"{u.Kb} · {FormatBytes(u.SizeBytes)}".Trim(' ', '·') +
                (u.RebootRequired ? " · Requiere reinicio" : string.Empty))).ToList();
            DriverUpdatesList.Visibility = Visibility.Visible;
            UpdatesStatusText.Text = $"{_wuUpdates.Count} actualizaciones disponibles. Marca las que quieras instalar.";
            UpdateInstallButton();
        }
        catch (Exception ex)
        {
            UpdatesStatusText.Text = $"Servicio no disponible ({ex.Message})";
            App.WriteCrashLog(ex);
        }
        finally
        {
            UpdatesRing.IsActive = false;
            UpdatesRing.Visibility = Visibility.Collapsed;
            SearchUpdatesButton.IsEnabled = true;
        }
    }

    private void OnDriverUpdateToggled(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox { Tag: string id }) return;
        if (!_wuUpdates.Any(u => u.UpdateId.Equals(id, StringComparison.OrdinalIgnoreCase))) return;
        if (!_wuSelected.Add(id)) _wuSelected.Remove(id);
        UpdateInstallButton();
    }

    private void UpdateInstallButton()
    {
        InstallUpdatesButton.IsEnabled = _wuSelected.Count > 0;
        InstallUpdatesButton.Content = _wuSelected.Count == 0
            ? "Descargar e instalar"
            : $"Descargar e instalar ({_wuSelected.Count})";
    }

    private async void OnInstallDriverUpdatesClick(object sender, RoutedEventArgs e)
    {
        if (_wuSelected.Count == 0) return;
        var picked = _wuUpdates.Where(u => _wuSelected.Contains(u.UpdateId)).ToList();
        var totalMb = picked.Sum(u => (double)u.SizeBytes) / 1024 / 1024;
        var reboot = picked.Any(u => u.RebootRequired);
        var confirm = new ContentDialog
        {
            Title = $"Instalar {picked.Count} drivers",
            Content = new TextBlock
            {
                Text = $"Descarga oficial de Windows Update ({totalMb:0.#} MB)." +
                    (reboot ? " Alguno REQUIERE REINICIO." : string.Empty) +
                    " Se crea un punto de restauración antes (si el sistema lo permite). ¿Continuar?",
                TextWrapping = TextWrapping.Wrap,
            },
            PrimaryButtonText = "Instalar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot,
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

        DriverUpdateResultText.Visibility = Visibility.Visible;
        DriverUpdateResultText.Text = $"Descargando e instalando {picked.Count} drivers (puede tardar muchos minutos)…";
        InstallUpdatesButton.IsEnabled = false;
        SearchUpdatesButton.IsEnabled = false;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(30));
            var pipe = AppHost.Resolve<PrivilegedPipeClient>();
            var response = await pipe.InstallDriverUpdatesAsync(picked.Select(u => u.UpdateId).ToList(), cts.Token);
            DriverUpdateResultText.Text = response is { Accepted: true }
                ? $"✓ {response.DetailJson ?? response.SafeMessage ?? "Instalación completada."}"
                : $"Rechazado [{response?.ErrorCode}]: {response?.SafeMessage ?? "sin respuesta"}";
            _wuSelected.Clear();
            UpdateInstallButton();
        }
        catch (Exception ex)
        {
            DriverUpdateResultText.Text = $"Servicio no disponible ({ex.Message})";
            App.WriteCrashLog(ex);
        }
        finally
        {
            SearchUpdatesButton.IsEnabled = true;
        }
        OnScanClick(ScanButton, new RoutedEventArgs());
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
        _ => $"{bytes / 1024.0 / 1024:0.#} MB",
    };

    /// <summary>
    /// Respalda los archivos reales del driver (INF+SYS+DLL+CAT) a
    /// %ProgramData%\CA-O\DriverBackup y abre la carpeta. Solo lectura del
    /// sistema: ideal antes de tocar nada o para reinstalar "bien hecho".
    /// </summary>
    private async void OnBackupDriverClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ConflictRow row } || !row.CanFix) return;
        var confirm = new ContentDialog
        {
            Title = $"Respaldar: {row.Title}",
            Content = new TextBlock
            {
                Text = "Se exportarán los archivos reales del driver (INF, SYS, DLL) a la carpeta de respaldo de CA-O. No cambia nada del sistema. ¿Continuar?",
                TextWrapping = TextWrapping.Wrap,
            },
            PrimaryButtonText = "Respaldar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot,
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

        FixStatusText.Visibility = Visibility.Visible;
        FixStatusText.Text = $"Respaldando {row.Title}…";
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(15));
            var pipe = AppHost.Resolve<PrivilegedPipeClient>();
            var response = await pipe.ExportDriverAsync(row.InstanceId, cts.Token);
            if (response is not { Accepted: true } || string.IsNullOrWhiteSpace(response.DetailJson))
            {
                FixStatusText.Text = $"{row.Title}: rechazado [{response?.ErrorCode}]: {response?.SafeMessage ?? "sin respuesta"}";
                return;
            }
            ExportDriverResult? backup = null;
            try { backup = JsonSerializer.Deserialize<ExportDriverResult>(response.DetailJson, CaseInsensitiveJson); } catch { }
            if (backup is null || string.IsNullOrWhiteSpace(backup.Path) || backup.Files <= 0)
            {
                FixStatusText.Text = $"{row.Title}: respuesta inesperada del servicio.";
                return;
            }
            FixStatusText.Text = $"✓ {row.Title}: {backup.Files} archivos en {backup.Path}. Abriendo carpeta…";
            OpenBackupFolder(backup.Path);
        }
        catch (Exception ex)
        {
            FixStatusText.Text = $"{row.Title}: servicio no disponible ({ex.Message})";
            App.WriteCrashLog(ex);
        }
    }

    /// <summary>Abre la carpeta solo si está bajo la raíz de respaldo (nunca rutas ajenas).</summary>
    private static void OpenBackupFolder(string path)
    {
        try
        {
            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CA-O", "DriverBackup")
                + Path.DirectorySeparatorChar;
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !Directory.Exists(path)) return;
            System.Diagnostics.Process.Start("explorer.exe", $"\"{path}\"");
        }
        catch (Exception ex) { App.WriteCrashLog(ex); }
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
        var choice = new ContentDialog
        {
            Title = $"Driver para {row.Title}",
            Content = new TextBlock
            {
                Text = "Descarga el paquete oficial del Catálogo de Microsoft (firmado: INF+SYS+DLL) o elige un .inf que ya tengas del fabricante.",
                TextWrapping = TextWrapping.Wrap,
            },
            PrimaryButtonText = "Descargar oficial",
            SecondaryButtonText = "Elegir archivo…",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot,
        };
        var picked = await choice.ShowAsync();
        if (picked == ContentDialogResult.Primary)
        {
            await DownloadOfficialFlowAsync(row);
        }
        else if (picked == ContentDialogResult.Secondary)
        {
            var path = await PickInfAsync();
            if (!string.IsNullOrWhiteSpace(path)) await InstallInfAsync(path, row.InstanceId, row.Title);
        }
    }

    private sealed record CatalogOfferRow(string UpdateId, string Title, string Detail);

    private sealed record CatalogInfRow(string FullPath, string FileName);

    /// <summary>
    /// Flujo Driver-Booster honesto: busca el HWID en el catálogo de
    /// Microsoft, descarga el .cab firmado, extrae y ofrece los .inf para
    /// instalar con el flujo verificado (pnputil + comprobación).
    /// </summary>
    private async Task DownloadOfficialFlowAsync(ConflictRow row)
    {
        if (string.IsNullOrWhiteSpace(row.HwId))
        {
            InfStatusText.Visibility = Visibility.Visible;
            InfStatusText.Text = "Este dispositivo no expone ID de hardware: usa Elegir archivo… o el enlace del fabricante.";
            return;
        }
        InfStatusText.Visibility = Visibility.Visible;
        InfStatusText.Text = $"Buscando '{row.HwId}' en el catálogo de Microsoft…";
        CatalogSearchResult? search;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            var pipe = AppHost.Resolve<PrivilegedPipeClient>();
            var response = await pipe.SearchCatalogDriversAsync(row.HwId, row.Name, cts.Token);
            if (response is not { Accepted: true } || string.IsNullOrWhiteSpace(response.DetailJson))
            {
                InfStatusText.Text = $"Catálogo sin respuesta [{response?.ErrorCode}]: {response?.SafeMessage ?? "sin respuesta"}";
                return;
            }
            try { search = JsonSerializer.Deserialize<CatalogSearchResult>(response.DetailJson, CaseInsensitiveJson); }
            catch { search = null; }
            if (search is null || search.Offers.Count == 0)
            {
                InfStatusText.Text = search?.Message ?? "El catálogo no ofrece drivers para este hardware. Usa Elegir archivo… o el enlace del fabricante.";
                return;
            }
        }
        catch (Exception ex)
        {
            InfStatusText.Text = $"Servicio no disponible ({ex.Message})";
            App.WriteCrashLog(ex);
            return;
        }

        var offersList = new ListView
        {
            SelectionMode = ListViewSelectionMode.Single,
            MaxHeight = 300,
            ItemsSource = search.Offers.Select(o => new CatalogOfferRow(
                o.UpdateId, o.Title,
                string.IsNullOrWhiteSpace(o.Version) ? "versión desconocida" : $"Versión {o.Version}")).ToList(),
        };
        offersList.ItemTemplate = (DataTemplate)Application.Current.Resources["CatalogOfferTemplate"];
        var offerDialog = new ContentDialog
        {
            Title = $"{search.Offers.Count} oficiales encontrados",
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = "Elige el paquete a descargar (firmado por Microsoft):", TextWrapping = TextWrapping.Wrap },
                    offersList,
                },
            },
            PrimaryButtonText = "Descargar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot,
        };
        if (await offerDialog.ShowAsync() != ContentDialogResult.Primary) return;
        if (offersList.SelectedItem is not CatalogOfferRow offer)
        {
            InfStatusText.Text = "Elige una oferta primero.";
            return;
        }

        InfStatusText.Text = $"Descargando '{offer.Title}' (puede tardar minutos)…";
        CatalogDownloadResult? pkg;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(30));
            var pipe = AppHost.Resolve<PrivilegedPipeClient>();
            var response = await pipe.DownloadCatalogDriverAsync(offer.UpdateId, row.HwId, cts.Token);
            if (response is not { Accepted: true } || string.IsNullOrWhiteSpace(response.DetailJson))
            {
                InfStatusText.Text = $"Descarga fallida [{response?.ErrorCode}]: {response?.SafeMessage ?? "sin respuesta"}";
                return;
            }
            try { pkg = JsonSerializer.Deserialize<CatalogDownloadResult>(response.DetailJson, CaseInsensitiveJson); }
            catch { pkg = null; }
            if (pkg is null || pkg.InfPaths.Count == 0)
            {
                InfStatusText.Text = "Descarga sin .inf utilizables.";
                return;
            }
        }
        catch (Exception ex)
        {
            InfStatusText.Text = $"Servicio no disponible ({ex.Message})";
            App.WriteCrashLog(ex);
            return;
        }

        string infPath;
        var autoPick = pkg.InfPaths.Count > 1
            ? CatalogDriverProtocol.PickBestInfMatch(pkg.InfPaths, row.HwId)
            : null;
        if (pkg.InfPaths.Count == 1)
        {
            infPath = pkg.InfPaths[0];
        }
        else if (autoPick is not null)
        {
            InfStatusText.Text = $"INF elegido automáticamente por HWID: {Path.GetFileName(autoPick)}.";
            infPath = autoPick;
        }
        else
        {
            var infList = new ListView
            {
                SelectionMode = ListViewSelectionMode.Single,
                MaxHeight = 300,
                ItemsSource = pkg.InfPaths.Select(p => new CatalogInfRow(p, Path.GetFileName(p))).ToList(),
            };
            infList.ItemTemplate = (DataTemplate)Application.Current.Resources["CatalogInfTemplate"];
            var infDialog = new ContentDialog
            {
                Title = "Elige el .inf a instalar",
                Content = infList,
                PrimaryButtonText = "Instalar este",
                CloseButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = Content.XamlRoot,
            };
            if (await infDialog.ShowAsync() != ContentDialogResult.Primary) return;
            if (infList.SelectedItem is not CatalogInfRow infRow) return;
            infPath = infRow.FullPath;
        }
        await InstallInfAsync(infPath, row.InstanceId, row.Title);
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
