using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Diagnostics;

namespace CAO.Uninstaller;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        if (!UninstallService.IsAdmin())
        {
            InfoBar.Severity = InfoBarSeverity.Warning;
            InfoBar.Message = "Este desinstalador requiere permisos de administrador. Se solicitará elevación.";
        }
    }

    internal void OnUninstallClick(object sender, RoutedEventArgs e)
    {
        _ = UninstallAsync();
    }

    internal void OnCancelClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private async Task UninstallAsync()
    {
        if (!UninstallService.IsAdmin())
        {
            await ShowErrorAsync("Permisos insuficientes", "El desinstalador debe ejecutarse como administrador. Haz clic derecho y selecciona Ejecutar como administrador.");
            return;
        }

        UninstallButton.IsEnabled = false;
        CancelButton.IsEnabled = false;
        ProgressCard.Visibility = Visibility.Visible;
        LogCard.Visibility = Visibility.Visible;
        DeleteHistoryCheck.IsEnabled = false;
        ProgressBar.Value = 0;
        ProgressStatusText.Text = "Desinstalando...";
        ProgressDetailText.Visibility = Visibility.Visible;
        Log("Iniciando desinstalación...");

        try
        {
            bool deleteHistory = DeleteHistoryCheck.IsChecked == true;
            UpdateProgress(10, "Deteniendo servicio...", "Deteniendo CAO.Privileged");
            await Task.Run(() => UninstallService.DoUninstall(deleteHistory, msg =>
            {
                Log(msg);
                // Map logs to progress
                if (msg.Contains("[1/5]")) UpdateProgress(20, "Deteniendo servicio...", null);
                else if (msg.Contains("[2/5]")) UpdateProgress(40, "Eliminando servicio...", null);
                else if (msg.Contains("[3/5]")) UpdateProgress(60, "Eliminando accesos directos...", null);
                else if (msg.Contains("[4/5]")) UpdateProgress(80, "Eliminando registro...", null);
                else if (msg.Contains("[5/5]")) UpdateProgress(90, "Eliminando archivos...", null);
            }));

            UpdateProgress(100, "Desinstalación completada", null);
            Log("CA-O eliminado correctamente.");

            var dialog = new ContentDialog
            {
                Title = "CA-O desinstalado",
                Content = new TextBlock { Text = "CA-O 2.0 se ha desinstalado correctamente.\n\nSe ha eliminado el servicio, archivos y accesos directos." + (deleteHistory ? "\nHistorial también borrado." : "\nHistorial conservado en %ProgramData%\\CA-O."), TextWrapping = TextWrapping.Wrap },
                PrimaryButtonText = "Cerrar",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = Content.XamlRoot
            };
            await dialog.ShowAsync();
            Close();
            // Si nos ejecutamos desde dentro del directorio, el batch hará el borrado final tras cerrar
            try { Process.GetCurrentProcess().Kill(); } catch { }
        }
        catch (Exception ex)
        {
            Log($"ERROR: {ex.Message}");
            await ShowErrorAsync("Error al desinstalar", $"Error desinstalando CA-O:\n{ex.Message}\n\nLog: {Path.Combine(Path.GetTempPath(), "CA-O-Uninstall.log")}");
            UninstallButton.IsEnabled = true;
            CancelButton.IsEnabled = true;
        }
    }

    internal void UpdateProgress(int value, string status, string? detail)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ProgressBar.Value = value;
            ProgressStatusText.Text = status;
            if (detail != null)
            {
                ProgressDetailText.Text = detail;
                ProgressDetailText.Visibility = Visibility.Visible;
            }
        });
    }

    internal void Log(string msg)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {msg}";
        DispatcherQueue.TryEnqueue(() =>
        {
            LogTextBox.Text += line + "\n";
            var sv = GetScrollViewer(LogTextBox);
            sv?.ScrollToVerticalOffset(double.MaxValue);
        });
        try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "CA-O-Uninstall.log"), line + "\n"); } catch { }
    }

    private ScrollViewer? GetScrollViewer(DependencyObject element)
    {
        if (element is ScrollViewer sv) return sv;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
        {
            var child = VisualTreeHelper.GetChild(element, i);
            var result = GetScrollViewer(child);
            if (result != null) return result;
        }
        return null;
    }

    private async Task ShowErrorAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            CloseButtonText = "Aceptar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot
        };
        await dialog.ShowAsync();
    }
}
