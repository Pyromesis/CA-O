using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Infrastructure.Logging;

/// <summary>Integrity problems detected while reading the audit log.</summary>
public sealed record HistoryIntegrityWarning(int LineNumber, string ReasonEs);

/// <summary>
/// Append-only JSON Lines audit log with a HASH CHAIN (FASE 13): every line
/// carries sequence number, previous-hash and its own SHA-256 over
/// prevHash+payload, so tampering/corruption/truncation is detectable.
/// Never stores secrets or personal input.
/// </summary>
public sealed class JsonHistoryLogger : IHistoryLogger
{
    /// <summary>Hash of the empty chain.</summary>
    public const string GenesisHash = "0000000000000000000000000000000000000000000000000000000000000000";

    private readonly string _filePath;
    private readonly object _lock = new();

    internal sealed record ChainedEntry(
        [property: JsonPropertyName("seq")] int Seq,
        [property: JsonPropertyName("prev")] string PrevHash,
        [property: JsonPropertyName("hash")] string Hash,
        [property: JsonPropertyName("entry")] HistoryEntry Entry);

    public JsonHistoryLogger(string? filePath = null)
    {
        _filePath = filePath ?? CaOPaths.HistoryFile;
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
    }

    public void Log(HistoryEntry entry)
    {
        lock (_lock)
        {
            var (seq, prevHash) = ReadTailState();
            var hash = ComputeHash(seq + 1, prevHash, entry);
            var line = JsonSerializer.Serialize(
                new ChainedEntry(seq + 1, prevHash, hash, entry), ChainOptions);

            using var stream = new FileStream(_filePath, FileMode.Append,
                FileAccess.Write, FileShare.Read);
            using var writer = new StreamWriter(stream, Encoding.UTF8);
            writer.WriteLine(line);
            writer.Flush();
            stream.Flush(flushToDisk: true);
        }
    }

    public IReadOnlyList<HistoryEntry> ReadLast(int maxEntries)
    {
        lock (_lock)
        {
            return ReadChain(out _)
                .Select(chained => chained.Entry)
                .TakeLast(Math.Max(0, maxEntries))
                .ToList();
        }
    }

    /// <summary>
    /// Validates sequence continuity and hashes over the whole file.
    /// Corruption is NEVER silent: every break produces a warning.
    /// </summary>
    public IReadOnlyList<HistoryIntegrityWarning> VerifyIntegrity()
    {
        lock (_lock)
        {
            ReadChain(out var warnings);
            return warnings;
        }
    }

    private List<(int Seq, string Hash, HistoryEntry Entry)> ReadChain(out List<HistoryIntegrityWarning> warnings)
    {
        warnings = new List<HistoryIntegrityWarning>();
        var result = new List<(int, string, HistoryEntry)>();
        if (!File.Exists(_filePath))
        {
            return result;
        }

        var lineNumber = 0;
        var expectedSeq = 0;
        var expectedPrevHash = GenesisHash;

        foreach (var line in File.ReadLines(_filePath, Encoding.UTF8))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            ChainedEntry? chained;
            try
            {
                chained = JsonSerializer.Deserialize<ChainedEntry>(line, ChainOptions);
            }
            catch (JsonException)
            {
                warnings.Add(new HistoryIntegrityWarning(lineNumber, "Línea inválida (JSON corrupto)."));
                continue;
            }

            if (chained is null || chained.Entry is null)
            {
                warnings.Add(new HistoryIntegrityWarning(lineNumber, "Línea sin entrada válida."));
                continue;
            }

            expectedSeq++;
            if (chained.Seq != expectedSeq)
            {
                warnings.Add(new HistoryIntegrityWarning(lineNumber,
                    $"Secuencia rota: esperado {expectedSeq}, encontrado {chained.Seq}."));
            }

            if (!string.Equals(chained.PrevHash, expectedPrevHash, StringComparison.Ordinal))
            {
                warnings.Add(new HistoryIntegrityWarning(lineNumber,
                    "Hash previo no coincide: posible eliminación o reordenación."));
            }

            var recomputed = ComputeHash(chained.Seq, chained.PrevHash, chained.Entry);
            if (!string.Equals(recomputed, chained.Hash, StringComparison.Ordinal))
            {
                warnings.Add(new HistoryIntegrityWarning(lineNumber,
                    "Hash de entrada inválido: contenido alterado."));
            }

            expectedPrevHash = chained.Hash;
            result.Add((chained.Seq, chained.Hash, chained.Entry));
        }

        return result;
    }

    private (int Seq, string PrevHash) ReadTailState()
    {
        var last = ReadChain(out _).LastOrDefault();
        return last == default ? (0, GenesisHash) : (last.Seq, last.Hash);
    }

    internal static string ComputeHash(int seq, string prevHash, HistoryEntry entry)
    {
        var payload = JsonSerializer.Serialize(entry, PayloadOptions);
        var material = $"{seq}|{prevHash}|{payload}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }

    private static readonly JsonSerializerOptions ChainOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly JsonSerializerOptions PayloadOptions = new()
    {
        WriteIndented = false,
    };
}
