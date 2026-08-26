using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CAO.Core.Rollback;
using CAO.Shared;

namespace CAO.Infrastructure.Persistence;

/// <summary>
/// File-backed transaction journal (FASE 5/12):
/// %ProgramData%\CA-O\transactions\{txid}.jsonl — one event per line,
/// append-only, flushed to disk. A missing terminal event means INCOMPLETE.
/// </summary>
public sealed class FileTransactionJournal : ITransactionJournal
{
    private readonly string _directory;
    private readonly object _lock = new();

    private sealed record Line([property: JsonPropertyName("e")] TransactionEvent Event);

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

    public FileTransactionJournal(string? directory = null)
    {
        _directory = directory ?? Path.Combine(CaOPaths.ProgramDataRoot, "transactions");
        Directory.CreateDirectory(_directory);
    }

    public void Append(TransactionEvent evt)
    {
        var file = PathFor(evt.TransactionId);
        var line = JsonSerializer.Serialize(new Line(evt), Options) + "\n";
        lock (_lock)
        {
            using var stream = new FileStream(file, FileMode.Append, FileAccess.Write, FileShare.Read);
            using var writer = new StreamWriter(stream, Encoding.UTF8);
            writer.Write(line);
            writer.Flush();
            stream.Flush(flushToDisk: true);
        }
    }

    public IReadOnlyList<(Guid TransactionId, IReadOnlyList<TransactionEvent> Events)> LoadAll()
    {
        var result = new List<(Guid, IReadOnlyList<TransactionEvent>)>();
        if (!Directory.Exists(_directory))
        {
            return result;
        }

        lock (_lock)
        {
            foreach (var file in Directory.GetFiles(_directory, "*.jsonl"))
            {
                if (!Guid.TryParse(Path.GetFileNameWithoutExtension(file), out var txid))
                {
                    continue; // foreign/corrupt file name: skip, never crash
                }

                var events = new List<TransactionEvent>();
                foreach (var line in File.ReadLines(file, Encoding.UTF8))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        var wrapped = JsonSerializer.Deserialize<Line>(line, Options);
                        if (wrapped?.Event is not null)
                        {
                            events.Add(wrapped.Event);
                        }
                    }
                    catch (JsonException)
                    {
                        // Corrupt line inside a journal: keep the rest.
                    }
                }

                if (events.Count > 0)
                {
                    result.Add((txid, events));
                }
            }
        }

        return result;
    }

    public string DebugDirectory() => _directory;

    private string PathFor(Guid txid) => Path.Combine(_directory, txid.ToString("D") + ".jsonl");

}
