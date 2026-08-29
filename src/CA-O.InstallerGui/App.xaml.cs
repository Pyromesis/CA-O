using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace CAO.InstallerGui;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
        UnhandledException += OnUnhandledException;
        // Single instance deshabilitado para garantizar que el GUI siempre abra (si o si)
        // Si hay otra instancia, la nueva simplemente abre su propia ventana.
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
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

    internal static void WriteCrashLog(Exception ex)
    {
        try
        {
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CA-O", "logs");
            Directory.CreateDirectory(logDir);
            var path = Path.Combine(logDir, "cao-installer-crash.log");
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
            // Intentar extraer HRESULT y XAML line info si existe
            try { sb.AppendLine("HResult: 0x" + ex.HResult.ToString("X")); } catch { }
            File.AppendAllText(path, sb.ToString(), Encoding.UTF8);
        }
        catch
        {
            try
            {
                var fallback = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "CA-O", "logs", "cao-installer-crash.log");
                Directory.CreateDirectory(Path.GetDirectoryName(fallback)!);
                var text = DateTime.UtcNow.ToString("o") + " " + ex.GetType().Name + ": " + ex.Message + (ex.InnerException != null ? " Inner: " + ex.InnerException.Message : "") + Environment.NewLine;
                File.AppendAllText(fallback, text, Encoding.UTF8);
            }
            catch { }
        }
    }
}