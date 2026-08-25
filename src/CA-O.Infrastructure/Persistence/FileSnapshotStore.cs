using System.Text.Json;
using CAO.Core.Abstractions;
using Abstractions = CAO.Core.Abstractions;

namespace CAO.Infrastructure.Persistence;

/// <summary>
/// File-based snapshot store (FASE 7/8/44): %ProgramData%\CA-O\snapshots\
/// {safeId}.json. Schema v2 records the exact registry kind per entry
/// (REG_* wire names), binary as base64 and multi-strings as arrays;
/// schemaVersion enables future migrations. Legacy v1 files (no kind info)
/// still load with inferred kinds.
/// </summary>
public sealed class FileSnapshotStore : ISnapshotStore
{
    private const int SchemaVersion = 2;

    private sealed record EntryDto(
        string Hive, string KeyPath, string ValueName, bool Existed,
        string? ValueString, string Kind,
        string? KindWire = null, string? Encoding = null);

    private sealed record SnapshotDto(
        int? SchemaVersion,
        DateTime TimestampUtc, List<EntryDto> Registry,
        List<string> ServiceStartTypes, List<string> RawNotes);

    private readonly string _directory;

    public FileSnapshotStore(string? directory = null)
    {
        _directory = directory
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CA-O", "snapshots");
        Directory.CreateDirectory(_directory);
    }

    public IEnumerable<string> ListIds() =>
        Directory.Exists(_directory)
            ? Directory.GetFiles(_directory, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(name => name is not null)
                .Cast<string>()
            : Enumerable.Empty<string>();

    public void Save(string optimizationId, OptimizationSnapshot snapshot)
    {
        var dto = new SnapshotDto(
            SchemaVersion,
            snapshot.TimestampUtc,
            snapshot.Registry.Select(e =>
            {
                var (valueString, encoding) = Encode(e.Value);
                return new EntryDto(
                    e.Hive, e.KeyPath, e.ValueName, e.Existed,
                    valueString, KindToString(e.Value),
                    KindWire: e.Kind.ToWire(),
                    Encoding: encoding);
            }).ToList(),
            snapshot.ServiceStartTypes,
            snapshot.RawNotes);

        // Atomic write (FASE 7): temp -> flush -> replace.
        var final = Path.Combine(_directory, SafeId(optimizationId) + ".json");
        var temp = Path.Combine(_directory, SafeId(optimizationId) + ".tmp");
        var json = JsonSerializer.Serialize(dto, JsonOptions);
        using (var stream = File.Create(temp))
        using (var writer = new StreamWriter(stream))
        {
            writer.Write(json);
            writer.Flush();
            stream.Flush(flushToDisk: true);
        }
        File.Move(temp, final, overwrite: true);
    }

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
                var kind = entry.KindWire is not null
                    ? RegistryKind2Wire(entry.KindWire)
                    : InferLegacyKind(entry.Kind, entry.ValueString);
                snapshot.Registry.Add(new RegistrySnapshotEntry(
                    entry.Hive, entry.KeyPath, entry.ValueName,
                    Decode(entry.KindWire ?? entry.Kind, entry.Encoding, entry.ValueString),
                    entry.Existed)
                { Kind = kind });
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

    // ---- codec ----

    private static (string? ValueString, string? Encoding) Encode(object? value) => value switch
    {
        byte[] bytes => (Convert.ToBase64String(bytes), "base64"),
        string[] multi => (JsonSerializer.Serialize(multi), "json-string-array"),
        null or _ => (value?.ToString(), null),
    };

    private static object? Decode(string? kindWireOrLegacy, string? encoding, string? valueString)
    {
        if (valueString is null) return null;
        if (encoding == "base64") return Convert.FromBase64String(valueString);
        if (encoding == "json-string-array") return JsonSerializer.Deserialize<string[]>(valueString);
        if (kindWireOrLegacy == "Int32" && int.TryParse(valueString, out var i)) return i;
        if ((kindWireOrLegacy == "Int64") && long.TryParse(valueString, out var l)) return l;

        // Legacy v1 heuristic kept only for old files.
        return kindWireOrLegacy switch
        {
            "REG_DWORD" when int.TryParse(valueString, out var d) => d,
            "REG_QWORD" when long.TryParse(valueString, out var q) => q,
            _ => valueString,
        };
    }

    private static RegistryValueKind2 RegistryKind2Wire(string wire)
    {
        try { return CAO.Core.Abstractions.RegistryKind2WireExtensions.ParseWire(wire); }
        catch (FormatException) { return RegistryValueKind2.String; }
    }

    private static RegistryValueKind2 InferLegacyKind(string legacyKind, string? value) => legacyKind switch
    {
        "Int32" => RegistryValueKind2.DWord,
        "Int64" => RegistryValueKind2.QWord,
        _ => RegistryValueKind2.String,
    };

    private static string KindToString(object? value) => value switch
    {
        int => "Int32",
        long or uint => "Int64",
        _ => "String",
    };

    private static string SafeId(string id) =>
        string.Concat(id.Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '_'));

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
}
