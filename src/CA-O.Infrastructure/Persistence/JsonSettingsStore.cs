using System.Text.Json;
using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Infrastructure.Persistence;

/// <summary>%AppData%\CA-O\settings.json with the v2 schema.</summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
    };

    private readonly string _filePath;
    private readonly object _gate = new();

    public JsonSettingsStore(string? filePath = null)
    {
        _filePath = filePath
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CA-O", "settings.json");
    }

    public AppSettings Load()
    {
        lock (_gate)
        {
            try
            {
                if (!File.Exists(_filePath)) return new AppSettings();
                var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_filePath), Options);
                return settings ?? new AppSettings();
            }
            catch (JsonException)
            {
                // Fichero corrupto: cuarentena antes de devolver defaults para
                // no sobrescribirlo con valores de fábrica en el próximo Save.
                try
                {
                    var quarantine = _filePath + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture);
                    File.Move(_filePath, quarantine);
                }
                catch { }
                return new AppSettings();
            }
            catch (IOException)
            {
                return new AppSettings();
            }
            catch (UnauthorizedAccessException)
            {
                return new AppSettings();
            }
        }
    }

    public void Save(AppSettings settings)
    {
        lock (_gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            // Escritura atómica: tmp + flush a disco + move. Un corte a mitad
            // de escritura nunca deja settings.json truncado.
            var tmp = _filePath + ".tmp-" + Guid.NewGuid().ToString("N");
            File.WriteAllText(tmp, JsonSerializer.Serialize(settings, Options));
            try
            {
                using var fs = new FileStream(tmp, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                fs.Flush(true);
            }
            catch { }
            File.Move(tmp, _filePath, overwrite: true);
        }
    }
}
