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

        // Single instance handling
        var mainInstance = AppInstance.FindOrRegisterForKey("CAO-Installer-GUI");
        if (!mainInstance.IsCurrent)
        {
            mainInstance.RedirectActivationToAsync(AppInstance.GetCurrent().GetActivatedEventArgs()).AsTask().Wait();
            Environment.Exit(0);
            return;
        }
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
            var text = new StringBuilder()
                .AppendLine("---- " + DateTime.UtcNow.ToString("o") + " ----")
                .AppendLine(ex.GetType().FullName)
                .AppendLine(ex.Message)
                .AppendLine(ex.StackTrace)
                .ToString();
            File.AppendAllText(path, text, Encoding.UTF8);
        }
        catch
        {
            try
            {
                var fallback = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "CA-O", "logs", "cao-installer-crash.log");
                Directory.CreateDirectory(Path.GetDirectoryName(fallback)!);
                var text = DateTime.UtcNow.ToString("o") + " " + ex.GetType().Name + ": " + ex.Message + Environment.NewLine;
                File.AppendAllText(fallback, text, Encoding.UTF8);
            }
            catch { }
        }
    }
}