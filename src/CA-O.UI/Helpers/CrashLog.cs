using System.Text;

namespace CAO.UI.Helpers;

/// <summary>
/// The crash log writer, deliberately not a member of <c>App</c>.
///
/// It used to be, and that cost more than it looked: touching a static member
/// of a <c>Windows.UI.Xaml.Application</c> pulls the WinUI runtime into
/// whichever process touches it, including a test host, where it has no window
/// to attach to and a second call from another thread has been enough to take
/// the whole run down with an access violation. Nothing here needs a window, so
/// nothing here should be able to drag one in.
/// </summary>
internal static class CrashLog
{
    /// <summary>
    /// Appends an exception and its inner chain. Never throws: a logger that
    /// can crash the caller is a second bug wearing the first one's clothes.
    /// </summary>
    /// <summary>Test seam: when set, the log goes here instead of LocalAppData. Production never sets it.</summary>
    internal static string? LogPathOverride { get; set; }

    private static string ResolvePath()
    {
        var path = LogPathOverride ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CA-O", "logs", "cao-ui-crash.log");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return path;
    }

    public static void Write(Exception ex)
    {
        try
        {
            var path = ResolvePath();
            RotateIfNeeded(path);

            var sb = new StringBuilder()
                .AppendLine("---- " + DateTime.UtcNow.ToString("o") + " ----")
                .AppendLine(ex.GetType().FullName)
                .AppendLine(ex.Message)
                .AppendLine(ex.StackTrace);

            var inner = ex.InnerException;
            var depth = 0;
            while (inner is not null && depth < 10)
            {
                depth++;
                sb.AppendLine("--- Inner ---");
                sb.AppendLine(inner.GetType().FullName);
                sb.AppendLine(inner.Message);
                sb.AppendLine(inner.StackTrace);
                inner = inner.InnerException;
            }

            try
            {
                sb.AppendLine("HResult: 0x" + ex.HResult.ToString("X"));
            }
            catch
            {
                // A COM exception can refuse to hand over its code. Not worth a log entry.
            }

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
                var text = DateTime.UtcNow.ToString("o") + " " + ex.GetType().Name + ": " + ex.Message
                    + (ex.InnerException is not null ? " Inner: " + ex.InnerException.Message : string.Empty)
                    + Environment.NewLine;
                File.AppendAllText(fallback, text, Encoding.UTF8);
            }
            catch
            {
                // Last resort: nothing left to write to and nothing left to say.
            }
        }
    }

    /// <summary>
    /// Appends a one-line breadcrumb. The Overclock refresh writes one per
    /// phase so a native crash that leaves no managed exception behind still
    /// leaves the exact step it died in. Same contract as Write: never throws.
    /// </summary>
    public static void Step(string stage)
    {
        try
        {
            var path = ResolvePath();
            RotateIfNeeded(path);
            File.AppendAllText(
                path,
                DateTime.UtcNow.ToString("o") + " step: " + stage + Environment.NewLine,
                Encoding.UTF8);
        }
        catch
        {
            // Diagnostics must never break the page they observe.
        }
    }

    private static void RotateIfNeeded(string path)
    {
        var info = new FileInfo(path);
        const long maxBytes = 2 * 1024 * 1024;
        if (!info.Exists || info.Length < maxBytes)
        {
            return;
        }

        var previous = path + ".1";
        if (File.Exists(previous))
        {
            File.Delete(previous);
        }

        File.Move(path, previous);
    }
}