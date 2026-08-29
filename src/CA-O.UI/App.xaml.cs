using System.Text;
using Microsoft.UI.Xaml;
using Windows.Storage;

namespace CAO.UI;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
        UnhandledException += OnUnhandledException;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            AppHost.Initialize();
        // Phase 1 startup: load persisted analysis + history + recovery without blocking UI (§7, §68)
        try
        {
            var store = AppHost.Resolve<CAO.Infrastructure.Persistence.AnalysisStateStore>();
            var persisted = store.LoadLatestAnalysis();
            if (persisted?.Context != null)
            {
                var uiState = AppHost.Resolve<CAO.UI.ViewModels.UiState>();
                uiState.Context = persisted.Context;
                uiState.Recommendations = persisted.Recommendations ?? Array.Empty<CAO.Shared.Recommendation>();
                uiState.LastAnalysisUtc = persisted.TimestampUtc;
            }
        }
        catch (Exception ex) { WriteCrashLog(ex); /* degraded: no previous analysis, don't block startup */ }
        _window = new MainWindow();
        _window.Activate();
        }
        catch (Exception ex)
        {
            WriteCrashLog(ex);
            throw;
        }
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        WriteCrashLog(e.Exception);
    }

    /// <summary>Startup/crash diagnostics written to %LocalAppData%\CA-O\logs (Fase 26). Never under Program Files.</summary>
    internal static void WriteCrashLog(Exception ex)
    {
        try
        {
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CA-O", "logs");
            Directory.CreateDirectory(logDir);
            var path = Path.Combine(logDir, "cao-ui-crash.log");
            var sb = new StringBuilder()
                .AppendLine("---- " + DateTime.UtcNow.ToString("o") + " ----")
                .AppendLine(ex.GetType().FullName)
                .AppendLine(ex.Message)
                .AppendLine(ex.StackTrace);
            var inner = ex.InnerException;
            while (inner != null)
            {
                sb.AppendLine("--- Inner ---");
                sb.AppendLine(inner.GetType().FullName);
                sb.AppendLine(inner.Message);
                sb.AppendLine(inner.StackTrace);
                inner = inner.InnerException;
            }
            try { sb.AppendLine("HResult: 0x" + ex.HResult.ToString("X")); } catch { }
            File.AppendAllText(path, sb.ToString(), Encoding.UTF8);
        }
        catch
        {
            try
            {
                var fallback = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "CA-O", "logs", "cao-ui-crash.log");
                Directory.CreateDirectory(Path.GetDirectoryName(fallback)!);
                var text = DateTime.UtcNow.ToString("o") + " " + ex.GetType().Name + ": " + ex.Message + (ex.InnerException != null ? " Inner: " + ex.InnerException.Message : "") + Environment.NewLine;
                File.AppendAllText(fallback, text, Encoding.UTF8);
            }
            catch { }
        }
    }
}
