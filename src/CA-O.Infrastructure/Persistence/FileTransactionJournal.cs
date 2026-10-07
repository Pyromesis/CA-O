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

    /// <summary>
    /// Tiempo que se conservan las transacciones ya cerradas antes de podarlas.
    /// CAO-BUG-2026-10-06: el journal era un log de solo-anexado sin politica de
    /// retencion. Medido en esta maquina: 102 ficheros, 68 KB, y releerlos
    /// enteros costaba 75 ms en cada llamada. Esa lectura ocurre en cada apply
    /// (guarda de recuperacion), en cada revert y en cada "Analizar" del
    /// Dashboard, en el hilo de la UI. Los ficheros cerrados no los lee nadie:
    /// la unica consulta del journal es <c>Incomplete()</c>, que los descarta.
    /// Se conserva un mes para no perder diagnostico reciente. Es interno para
    /// que las pruebas puedan verificar la poda.
    /// </summary>
    internal static TimeSpan Retention = TimeSpan.FromDays(30);

    /// <summary>Veces que se ha releido el journal desde disco. Solo para pruebas.</summary>
    internal static int ParsePasses { get; private set; }

    private static string? _parsedFor;
    private static (int Files, DateTime Newest) _parsedStamp;
    private static IReadOnlyList<(Guid, IReadOnlyList<TransactionEvent>)>? _parsed;

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

        // Solo se poda cuando la transaccion queda cerrada: las abiertas son
        // justamente lo que CrashRecoveryService necesita para decidir.
        if (TransactionEvent.IsTerminal(evt.Phase))
        {
            PruneClosed();
        }
    }

    /// <summary>
    /// Borra las transacciones ya cerradas cuyo fichero supere la retencion.
    /// Nunca borra un fichero cuyo ultimo evento no se pueda leer con certeza:
    /// un estado desconocido no es un estado prescindible.
    /// </summary>
    private void PruneClosed()
    {
        var cutoff = DateTime.UtcNow - Retention;
        string[] files;
        try
        {
            files = Directory.GetFiles(_directory, "*.jsonl");
        }
        catch (IOException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        foreach (var file in files)
        {
            try
            {
                if (File.GetLastWriteTimeUtc(file) > cutoff)
                {
                    continue;
                }

                // Solo lo que CA-O creo: un nombre que no sea un Guid es ajeno.
                if (!Guid.TryParse(Path.GetFileNameWithoutExtension(file), out _))
                {
                    continue;
                }

                var last = ReadLastEvent(file);
                if (last is null || !TransactionEvent.IsTerminal(last.Phase))
                {
                    continue;
                }

                File.Delete(file);
            }
            catch (IOException)
            {
                // Bloqueado o ya borrado: se deja para la proxima pasada.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>Ultimo evento del journal, o null si el fichero no es legible.</summary>
    private static TransactionEvent? ReadLastEvent(string file)
    {
        TransactionEvent? last = null;
        try
        {
            foreach (var line in File.ReadLines(file, Encoding.UTF8))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    last = JsonSerializer.Deserialize<Line>(line, Options)?.Event;
                }
                catch (JsonException)
                {
                    // Linea corrupta: no se puede saber como termina la
                    // transaccion, asi que no se borra el fichero.
                    return null;
                }
            }
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }

        return last;
    }

    public IReadOnlyList<(Guid TransactionId, IReadOnlyList<TransactionEvent> Events)> LoadAll()
    {
        // CAO-BUG-2026-10-06: el journal entero se releia en cada consulta.
        // Comparar su huella (numero de ficheros + fecha del mas reciente) es
        // mucho mas barato que parsearlo, y esa comparacion detecta cualquier
        // anexo o poda, de modo que no hace falta sincronizacion entre
        // procesos: si el directorio no ha cambiado, el resultado tampoco.
        lock (_lock)
        {
            var stamp = Fingerprint();
            if (_parsed is not null && _parsedFor == _directory && _parsedStamp == stamp)
            {
                return _parsed;
            }

            ParsePasses++;
            _parsed = ReadAll();
            _parsedFor = _directory;
            _parsedStamp = stamp;
            return _parsed;
        }
    }

    /// <summary>
    /// Huella barata del directorio. Cualquier anexo cambia la fecha del
    /// fichero y cualquier poda cambia el numero de ficheros.
    /// </summary>
    private (int Files, DateTime Newest) Fingerprint()
    {
        try
        {
            var files = Directory.GetFiles(_directory, "*.jsonl");
            var newest = DateTime.MinValue;
            foreach (var file in files)
            {
                var stamp = File.GetLastWriteTimeUtc(file);
                if (stamp > newest) newest = stamp;
            }

            return (files.Length, newest);
        }
        catch (IOException)
        {
            return (int.MinValue, DateTime.MinValue);
        }
        catch (UnauthorizedAccessException)
        {
            return (int.MinValue, DateTime.MinValue);
        }
    }

    private IReadOnlyList<(Guid TransactionId, IReadOnlyList<TransactionEvent> Events)> ReadAll()
    {
        var result = new List<(Guid, IReadOnlyList<TransactionEvent>)>();
        if (!Directory.Exists(_directory))
        {
            return result;
        }

        lock (_lock)
        {
            string[] files;
            try
            {
                files = Directory.GetFiles(_directory, "*.jsonl");
            }
            catch (IOException)
            {
                return result; // directorio ilegible: recuperación parcial vacía, sin crash
            }
            catch (UnauthorizedAccessException)
            {
                return result;
            }

            foreach (var file in files)
            {
                if (!Guid.TryParse(Path.GetFileNameWithoutExtension(file), out var txid))
                {
                    continue; // foreign/corrupt file name: skip, never crash
                }

                var events = new List<TransactionEvent>();
                IEnumerable<string> lines;
                try
                {
                    lines = File.ReadLines(file, Encoding.UTF8);
                    // Materializar dentro del try: la enumeración diferida
                    // lanzaría fuera si el fichero se bloquea a mitad.
                    lines = lines.ToList();
                }
                catch (IOException)
                {
                    continue; // journal bloqueado o borrado a mitad: se salta, no tumba la recuperación
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }

                foreach (var line in lines)
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
