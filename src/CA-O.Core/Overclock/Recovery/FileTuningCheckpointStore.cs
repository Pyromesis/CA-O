using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CAO.Shared;

namespace CAO.Core.Overclock.Recovery;

/// <summary>
/// Almacén de checkpoints. La implementación de disco nunca borra evidencia: un
/// checkpoint ilegible se aparta con sufijo <c>.corrupt-&lt;timestamp&gt;</c> y se
/// registra, porque "no sé qué había escrito" es precisamente el dato que hay que
/// conservar cuando el kernel se ha comido la máquina.
/// </summary>
public interface ITuningCheckpointStore
{
    Task<IReadOnlyList<TuningCheckpoint>> ListAsync(CancellationToken cancellationToken);

    Task SaveAsync(TuningCheckpoint checkpoint, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid runId, CancellationToken cancellationToken);

    /// <summary>Aparta un checkpoint ilegible sin destruirlo. Devuelve la ruta resultante.</summary>
    Task<string?> QuarantineAsync(Guid runId, string reasonKey, CancellationToken cancellationToken);
}

public sealed class FileTuningCheckpointStore : ITuningCheckpointStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _directory;

    public FileTuningCheckpointStore(string? directory = null) =>
        _directory = directory ?? CaOPaths.OverclockRunsDirectory;

    public async Task<IReadOnlyList<TuningCheckpoint>> ListAsync(CancellationToken cancellationToken)
    {
        var result = new List<TuningCheckpoint>();
        if (!Directory.Exists(_directory))
        {
            return result;
        }

        foreach (var file in Directory.EnumerateFiles(_directory, "*.json"))
        {
            cancellationToken.ThrowIfCancellationRequested();

            TuningCheckpoint? checkpoint;
            try
            {
                var json = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
                checkpoint = JsonSerializer.Deserialize<TuningCheckpoint>(json, SerializerOptions);
            }
            catch (JsonException)
            {
                checkpoint = null;
            }
            catch (IOException)
            {
                // Fichero bloqueado o ilegible en este instante: se salta este ciclo.
                // No se cuenta como corrupto porque podría ser transitorio.
                continue;
            }

            if (checkpoint is null)
            {
                await QuarantineAsync(RunIdFrom(file), "unparseable", cancellationToken).ConfigureAwait(false);
                continue;
            }

            result.Add(checkpoint);
        }

        return result;
    }

    public async Task SaveAsync(TuningCheckpoint checkpoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);

        Directory.CreateDirectory(_directory);
        var path = PathFor(checkpoint.RunId);

        // Escritura atómica: un corte de luz a mitad del fichero deja el checkpoint
        // anterior intacto en vez de un JSON truncado que el recovery no puede leer.
        var temp = path + ".tmp";
        var json = JsonSerializer.Serialize(checkpoint, SerializerOptions);
        await File.WriteAllTextAsync(temp, json, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        File.Move(temp, path, overwrite: true);
    }

    public Task<bool> DeleteAsync(Guid runId, CancellationToken cancellationToken)
    {
        var path = PathFor(runId);
        if (!File.Exists(path))
        {
            return Task.FromResult(false);
        }

        File.Delete(path);
        return Task.FromResult(true);
    }

    public Task<string?> QuarantineAsync(Guid runId, string reasonKey, CancellationToken cancellationToken)
    {
        var path = PathFor(runId);
        if (!File.Exists(path))
        {
            return Task.FromResult<string?>(null);
        }

        var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        var target = $"{path}.corrupt-{stamp}-{reasonKey}";
        File.Move(path, target);
        return Task.FromResult<string?>(target);
    }

    private string PathFor(Guid runId) => Path.Combine(_directory, $"{runId:D}.json");

    private static Guid RunIdFrom(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        return Guid.TryParse(name, out var id) ? id : Guid.Empty;
    }
}