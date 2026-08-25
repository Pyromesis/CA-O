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
            catch
            {
                // Corrupt settings must never brick the app.
                return new AppSettings();
            }
        }
    }

    public void Save(AppSettings settings)
    {
        lock (_gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            File.WriteAllText(_filePath, JsonSerializer.Serialize(settings, Options));
        }
    }
}
