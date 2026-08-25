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

    /// <summary>Startup/crash diagnostics written next to the executable.</summary>
    internal static void WriteCrashLog(Exception ex)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "cao-ui-crash.log");
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
            // Never crash because of the crash logger.
        }
    }
}
