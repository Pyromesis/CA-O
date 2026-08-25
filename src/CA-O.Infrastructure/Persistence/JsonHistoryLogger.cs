using System.Text;
using System.Text.Json;
using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Infrastructure.Logging;

/// <summary>
/// Append-only JSON Lines history at %ProgramData%\CA-O\history.jsonl
/// (spec 74). Never stores secrets or personal input (spec 75).
/// </summary>
public sealed class JsonHistoryLogger : IHistoryLogger
{
    private readonly string _filePath;
    private readonly object _lock = new();

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
    };

    public JsonHistoryLogger(string? filePath = null)
    {
        _filePath = filePath ?? CaOPaths.HistoryFile;
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
    }

    public void Log(HistoryEntry entry)
    {
        var line = JsonSerializer.Serialize(entry, Options) + "\n";
        lock (_lock)
        {
            File.AppendAllText(_filePath, line, Encoding.UTF8);
        }
    }

    public IReadOnlyList<HistoryEntry> ReadLast(int maxEntries)
    {
        if (!File.Exists(_filePath))
        {
            return Array.Empty<HistoryEntry>();
        }

        lock (_lock)
        {
            IEnumerable<string> lines = File.ReadLines(_filePath, Encoding.UTF8);
            if (maxEntries > 0 && maxEntries < int.MaxValue)
            {
                lines = lines.TakeLast(maxEntries);
            }

            return lines
                .Select(ParseLine)
                .Where(entry => entry is not null)
                .Cast<HistoryEntry>()
                .ToList();
        }
    }

    private static HistoryEntry? ParseLine(string line)
    {
        try
        {
            return JsonSerializer.Deserialize<HistoryEntry>(line);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
