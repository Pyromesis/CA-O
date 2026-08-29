using System.Text;
using Microsoft.UI.Xaml;

namespace CAO.Uninstaller;

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
            var cmd = Environment.GetCommandLineArgs();
            bool silent = cmd.Any(a => a.Equals("/S", StringComparison.OrdinalIgnoreCase) || a.Equals("--silent", StringComparison.OrdinalIgnoreCase));
            if (silent)
            {
                // Silent mode: ejecutar desinstalación sin ventana
                UninstallService.SilentUninstall();
                Exit();
                return;
            }
            _window = new MainWindow();
            _window.Activate();
        }
        catch (Exception ex)
        {
            WriteCrashLog(ex);
            ShowFatalError(ex);
            throw;
        }
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        WriteCrashLog(e.Exception);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

    private static void ShowFatalError(Exception ex)
    {
        try
        {
            var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CA-O", "logs", "cao-uninstall-crash.log");
            var msg = $"CA-O Desinstalador no pudo iniciar.\n\n{ex.GetType().Name}: {ex.Message}\n\nLog: {logPath}";
            _ = MessageBoxW(IntPtr.Zero, msg, "CA-O — error de inicio", 0x10);
        }
        catch { }
    }

    internal static void WriteCrashLog(Exception ex)
    {
        try
        {
            var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CA-O", "logs");
            Directory.CreateDirectory(logDir);
            var path = Path.Combine(logDir, "cao-uninstall-crash.log");
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
        catch { }
    }
}
