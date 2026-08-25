using System.Text.Json;
using CAO.Core.Abstractions;

namespace CAO.Infrastructure.Persistence;

/// <summary>File-based snapshot store: %ProgramData%\CA-O\snapshots\{id}.json.</summary>
public sealed class FileSnapshotStore : ISnapshotStore
{
    private sealed record EntryDto(string Hive, string KeyPath, string ValueName, bool Existed, string? ValueString, string Kind);
    private sealed record SnapshotDto(DateTime TimestampUtc, List<EntryDto> Registry, List<string> ServiceStartTypes, List<string> RawNotes);

    private readonly string _directory;

    public FileSnapshotStore(string? directory = null)
    {
        _directory = directory
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CA-O", "snapshots");
        Directory.CreateDirectory(_directory);
    }

    public void Save(string optimizationId, OptimizationSnapshot snapshot)
    {
        var dto = new SnapshotDto(
            snapshot.TimestampUtc,
            snapshot.Registry.Select(e => new EntryDto(
                e.Hive, e.KeyPath, e.ValueName, e.Existed,
                e.Value?.ToString(), KindToString(e.Value))).ToList(),
            snapshot.ServiceStartTypes,
            snapshot.RawNotes);

        var json = JsonSerializer.Serialize(dto, JsonOptions);
        File.WriteAllText(Path.Combine(_directory, SafeId(optimizationId) + ".json"), json);
    }

    public IEnumerable<string> ListIds() =>
        Directory.Exists(_directory)
            ? Directory.GetFiles(_directory, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(name => name is not null)
                .Cast<string>()
            : Enumerable.Empty<string>();

    public bool TryLoad(string optimizationId, out OptimizationSnapshot snapshot)
    {
        snapshot = new OptimizationSnapshot();
        var file = Path.Combine(_directory, SafeId(optimizationId) + ".json");
        if (!File.Exists(file)) return false;

        try
        {
            var dto = JsonSerializer.Deserialize<SnapshotDto>(File.ReadAllText(file), JsonOptions);
            if (dto is null) return false;

            foreach (var entry in dto.Registry)
            {
                object? value = entry.Kind switch
                {
                    "Int32" => int.TryParse(entry.ValueString, out var i) ? i : null,
                    "Int64" => long.TryParse(entry.ValueString, out var l) ? l : null,
                    _ => entry.ValueString,
                };
                snapshot.Registry.Add(new RegistrySnapshotEntry(entry.Hive, entry.KeyPath, entry.ValueName, value, entry.Existed));
            }
            foreach (var s in dto.ServiceStartTypes) snapshot.ServiceStartTypes.Add(s);
            foreach (var s in dto.RawNotes) snapshot.RawNotes.Add(s);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Delete(string optimizationId)
    {
        var file = Path.Combine(_directory, SafeId(optimizationId) + ".json");
        if (File.Exists(file)) File.Delete(file);
    }

    private static string KindToString(object? value) => value switch
    {
        int => "Int32",
        long or uint or uint => "Int64",
        _ => "String",
    };

    private static string SafeId(string id) =>
        string.Concat(id.Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '_'));

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
}
