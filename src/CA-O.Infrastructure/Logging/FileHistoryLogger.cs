using System.Text;
using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Infrastructure.Logging;

/// <summary>Append-only history log at %ProgramData%\CA-O\history.log (#5).</summary>
public sealed class FileHistoryLogger : IHistoryLogger
{
    private readonly string _filePath;

    public FileHistoryLogger(string? filePath = null)
    {
        _filePath = filePath
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CA-O", "history.log");
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
    }

    public void Log(HistoryEntry entry)
    {
        var line = new StringBuilder()
            .Append(entry.TimestampUtc.ToString("yyyy-MM-ddTHH:mm:ssZ")).Append('\t')
            .Append(entry.User).Append('\t')
            .Append(entry.OptimizationId).Append('\t')
            .Append(entry.Action).Append('\t')
            .Append(entry.Success ? "OK" : "FAIL").Append('\t')
            .Append(Escape(entry.PreviousState)).Append('\t')
            .Append(Escape(entry.NewState)).Append('\t')
            .Append(Escape(entry.Error))
            .AppendLine()
            .ToString();

        lock (_lock)
        {
            File.AppendAllText(_filePath, line);
        }
    }

    public IReadOnlyList<HistoryEntry> ReadLast(int maxEntries)
    {
        if (!File.Exists(_filePath)) return Array.Empty<HistoryEntry>();

        lock (_lock)
        {
            var lines = File.ReadAllLines(_filePath);
            return lines
                .TakeLast(Math.Max(0, maxEntries))
                .Select(ParseLine)
                .Where(e => e is not null)
                .Cast<HistoryEntry>()
                .ToList();
        }
    }

    private static HistoryEntry? ParseLine(string line)
    {
        var parts = line.Split('\t');
        if (parts.Length < 5) return null;
        if (!DateTime.TryParse(parts[0], out var ts)) return null;
        return new HistoryEntry
        {
            TimestampUtc = ts.ToUniversalTime(),
            User = parts[1],
            OptimizationId = parts[2],
            Action = parts[3],
            Success = parts[4] == "OK",
            PreviousState = Unescape(parts.ElementAtOrDefault(5)),
            NewState = Unescape(parts.ElementAtOrDefault(6)),
            Error = Unescape(parts.ElementAtOrDefault(7)),
        };
    }

    private static string? Escape(string? value) =>
        value is null ? null : value.Replace("\t", "\\t").Replace("\r", "\\r").Replace("\n", "\\n");

    private static string? Unescape(string? value) =>
        value is null or "" ? null : value.Replace("\\t", "\t").Replace("\\r", "\r").Replace("\\n", "\n");

    private readonly object _lock = new();
}
