using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CAO.Core.Abstractions;
using CAO.Core.Rollback;

namespace CAO.Infrastructure.Persistence;

/// <summary>
/// Transaction-scoped snapshot store v3 (P0-3): snapshots/{txid}/ containing
/// snapshot.json (typed state), manifest.json and integrity.json (SHA-256 of
/// snapshot.json). Immutable directories, atomic per-file writes, integrity
/// verified on load. Legacy flat {optId}.json files remain readable/deletable.
/// </summary>
public sealed class FileSnapshotStore : ISnapshotStore
{
    private const int SchemaVersion = 3;

    private readonly string _root;
    private readonly object _sync = new();

    public FileSnapshotStore(string? rootDirectory = null)
    {
        _root = rootDirectory
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "CA-O", "snapshots");
        Directory.CreateDirectory(_root);
    }

    // ---- DTOs ----

    public sealed class StateDto
    {
        public int SchemaVersion { get; set; } = FileSnapshotStore.SchemaVersion;

        public DateTime TimestampUtc { get; set; }

        public List<StateEntryDto> Entries { get; set; } = [];

        public List<string> ServiceStartTypes { get; set; } = [];

        public List<string> RawNotes { get; set; } = [];
    }

    public sealed class StateEntryDto
    {
        public string Hive { get; set; } = string.Empty;

        public string KeyPath { get; set; } = string.Empty;

        public string ValueName { get; set; } = string.Empty;

        public bool Existed { get; set; }

        /// <summary>REG_* wire name of the exact captured kind.</summary>
        public string KindWire { get; set; } = "REG_NONE";

        /// <summary>"b64" for binary, "json" for multi-string, null otherwise.</summary>
        public string? EncodingName { get; set; }

        public string? ValueString { get; set; }
    }

    internal sealed record ManifestDto(
        Guid TxId,
        string OptId,
        string DefVersion,
        int Schema,
        string AppVer,
        int Build,
        DateTime Utc,
        string? BySid);

    internal sealed record IntegrityDto(string Algorithm, string StateSha256);

    // ---- API ----

    public void Save(TransactionSnapshotRecord record)
    {
        lock (_sync)
        {
            var dir = Path.Combine(_root, record.Manifest.TransactionId.ToString("D"));
            if (Directory.Exists(dir))
            {
                throw new InvalidOperationException($"Snapshot inmutable: {record.Manifest.TransactionId} ya existe.");
            }
            Directory.CreateDirectory(dir);

            var stateJson = JsonSerializer.Serialize(ToStateDto(record.State), JsonOpts);
            var sha = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(stateJson)));

            try
            {
                WriteAtomic(Path.Combine(dir, "snapshot.json"), stateJson);
                WriteAtomic(Path.Combine(dir, "manifest.json"), JsonSerializer.Serialize(
                    new ManifestDto(record.Manifest.TransactionId, record.Manifest.OptimizationId,
                        record.Manifest.DefinitionVersion, record.Manifest.SchemaVersion,
                        record.Manifest.AppVersion, record.Manifest.WindowsBuild,
                        record.Manifest.TimestampUtc, record.Manifest.RequestedBySid), JsonOpts));
                WriteAtomic(Path.Combine(dir, "integrity.json"), JsonSerializer.Serialize(
                    new IntegrityDto("SHA256", sha), JsonOpts));

                // Verify without re-entering lock (direct read)
                if (!TryLoadCore(record.Manifest.TransactionId, out var reloaded) ||
                    reloaded is null ||
                    !SnapshotStateEquals(record.State, reloaded.State))
                {
                    throw new InvalidOperationException("Snapshot no superó la verificación post-escritura.");
                }
            }
            catch
            {
                try { Directory.Delete(dir, recursive: true); } catch { /* best effort */ }
                throw;
            }
        }
    }

    private bool TryLoadCore(Guid transactionId, out TransactionSnapshotRecord? record)
    {
        record = null;
        var dir = Path.Combine(_root, transactionId.ToString("D"));
        var snapPath = Path.Combine(dir, "snapshot.json");
        var manifestPath = Path.Combine(dir, "manifest.json");
        var integrityPath = Path.Combine(dir, "integrity.json");
        if (!File.Exists(snapPath) || !File.Exists(manifestPath) || !File.Exists(integrityPath)) return false;
        try
        {
            var manifest = JsonSerializer.Deserialize<ManifestDto>(File.ReadAllText(manifestPath), JsonOpts);
            var integrity = JsonSerializer.Deserialize<IntegrityDto>(File.ReadAllText(integrityPath), JsonOpts);
            if (manifest is null || integrity is null || !string.Equals(integrity.Algorithm, "SHA256", StringComparison.Ordinal)) return false;
            var stateJson = File.ReadAllText(snapPath);
            var expectedSha = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(stateJson)));
            if (!string.Equals(expectedSha, integrity.StateSha256, StringComparison.Ordinal)) return false;
            var stateDto = JsonSerializer.Deserialize<StateDto>(stateJson, JsonOpts);
            if (stateDto is null) return false;
            record = new TransactionSnapshotRecord
            {
                Manifest = new TransactionSnapshotManifest
                {
                    TransactionId = manifest.TxId,
                    OptimizationId = manifest.OptId,
                    DefinitionVersion = manifest.DefVersion,
                    SchemaVersion = manifest.Schema,
                    AppVersion = manifest.AppVer,
                    WindowsBuild = manifest.Build,
                    TimestampUtc = manifest.Utc,
                    RequestedBySid = manifest.BySid,
                    StateSha256 = integrity.StateSha256,
                },
                State = FromStateDto(stateDto),
            };
            return true;
        }
        catch (JsonException) { return false; }
    }

    public bool TryLoad(Guid transactionId, out TransactionSnapshotRecord? record)
    {
        lock (_sync) return TryLoadCore(transactionId, out record);
    }

    public bool TryLoadLatestForOptimization(string optimizationId, out TransactionSnapshotRecord? record)
    {
        lock (_sync)
        {
            // Direct scan without re-entering ListAll lock
            record = null;
            if (!Directory.Exists(_root)) return false;
            TransactionSnapshotRecord? best = null;
            foreach (var dir in Directory.GetDirectories(_root))
            {
                if (Guid.TryParse(Path.GetFileName(dir), out var txid) && TryLoadCore(txid, out var cand) && cand is not null
                    && cand.Manifest.OptimizationId.Equals(optimizationId, StringComparison.OrdinalIgnoreCase))
                {
                    if (best == null || cand.Manifest.TimestampUtc > best.Manifest.TimestampUtc) best = cand;
                }
            }
            record = best;
            return record is not null;
        }
    }

    public void Delete(Guid transactionId)
    {
        lock (_sync)
        {
            var dir = Path.Combine(_root, transactionId.ToString("D"));
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    public IReadOnlyList<TransactionSnapshotRecord> ListAll()
    {
        lock (_sync)
        {
            var result = new List<TransactionSnapshotRecord>();
            if (!Directory.Exists(_root)) return result;
            foreach (var dir in Directory.GetDirectories(_root))
            {
                if (Guid.TryParse(Path.GetFileName(dir), out var txid) && TryLoadCore(txid, out var record) && record is not null)
                    result.Add(record);
            }
            return result;
        }
    }

    // ---- legacy v2 ----

    public bool TryLoadLegacy(string optimizationId, out OptimizationSnapshot? snapshot)
    {
        snapshot = null;
        var file = Path.Combine(_root, SafeId(optimizationId) + ".json");
        if (!File.Exists(file)) return false;

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            var root = doc.RootElement;
            DateTime legacyTimestamp = DateTime.UtcNow;
            if (root.TryGetProperty("TimestampUtc", out var ts) &&
                ts.ValueKind == JsonValueKind.String &&
                DateTime.TryParse(ts.GetString(), out var parsed))
            {
                legacyTimestamp = parsed.ToUniversalTime();
            }
            var legacy = new OptimizationSnapshot { TimestampUtc = legacyTimestamp };

            if (root.TryGetProperty("Registry", out var registryArray) &&
                registryArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in registryArray.EnumerateArray())
                {
                    string GetStr(string name) =>
                        element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String
                            ? prop.GetString() ?? string.Empty : string.Empty;
                    bool GetBool(string name) =>
                        element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.True;

                    legacy.Registry.Add(new RegistrySnapshotEntry(
                        GetStr("Hive"), GetStr("KeyPath"), GetStr("ValueName"),
                        null, GetBool("Existed")));
                }
            }

            snapshot = legacy;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public void DeleteLegacy(string optimizationId)
    {
        var file = Path.Combine(_root, SafeId(optimizationId) + ".json");
        if (File.Exists(file)) File.Delete(file);
    }

    public IReadOnlyList<string> ListLegacyIds()
    {
        if (!Directory.Exists(_root)) return Array.Empty<string>();
        return Directory.GetFiles(_root, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => name is not null)
            .Cast<string>()
            .ToList();
    }

    // ---- codec ----

    internal static StateDto ToStateDto(OptimizationSnapshot snapshot) => new()
    {
        TimestampUtc = snapshot.TimestampUtc,
        Entries = snapshot.Registry.Select(entry =>
        {
            var (valueString, encoding) = EncodeValue(entry.Kind, entry.Value);
            return new StateEntryDto
            {
                Hive = entry.Hive,
                KeyPath = entry.KeyPath,
                ValueName = entry.ValueName,
                Existed = entry.Existed,
                KindWire = entry.Kind.ToWire(),
                EncodingName = encoding,
                ValueString = valueString,
            };
        }).ToList(),
        ServiceStartTypes = snapshot.ServiceStartTypes,
        RawNotes = snapshot.RawNotes,
    };

    internal static OptimizationSnapshot FromStateDto(StateDto dto)
    {
        var snapshot = new OptimizationSnapshot
        {
            TimestampUtc = dto.TimestampUtc,
        };
        foreach (var entry in dto.Entries)
        {
            var kind = ParseWireSafe(entry.KindWire);
            snapshot.Registry.Add(new RegistrySnapshotEntry(
                entry.Hive, entry.KeyPath, entry.ValueName,
                DecodeValue(kind, entry.EncodingName, entry.ValueString),
                entry.Existed)
            { Kind = kind });
        }
        snapshot.ServiceStartTypes.AddRange(dto.ServiceStartTypes);
        snapshot.RawNotes.AddRange(dto.RawNotes);
        return snapshot;
    }

    private static (string? ValueString, string? EncodingName) EncodeValue(
        RegistryValueKind2 kind, object? value) => (kind, value) switch
    {
        (_, null) => (null, null),
        (RegistryValueKind2.Binary, byte[] bytes) => (Convert.ToBase64String(bytes), "b64"),
        (RegistryValueKind2.MultiString, string[] multi) =>
            (JsonSerializer.Serialize(multi), "json"),
        _ => (Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture), null),
    };

    private static object? DecodeValue(RegistryValueKind2 kind, string? encoding, string? raw)
    {
        if (raw is null) return null;
        switch (kind)
        {
            case RegistryValueKind2.Binary when encoding == "b64":
                return Convert.FromBase64String(raw);
            case RegistryValueKind2.MultiString when encoding == "json":
                return JsonSerializer.Deserialize<string[]>(raw);
            case RegistryValueKind2.DWord:
                return int.TryParse(raw, System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out var i) ? i : raw;
            case RegistryValueKind2.QWord:
                return long.TryParse(raw, System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out var l) ? l : raw;
            default:
                return raw;
        }
    }

    private static RegistryValueKind2 ParseWireSafe(string wire) =>
        RegistryKind2WireExtensions.TryParseWire(wire, out var kind) ? kind : RegistryValueKind2.None;

    private static bool SnapshotStateEquals(OptimizationSnapshot expected, OptimizationSnapshot actual)
    {
        if (expected.Registry.Count != actual.Registry.Count) return false;
        for (var index = 0; index < expected.Registry.Count; index++)
        {
            var e = expected.Registry[index];
            var a = actual.Registry[index];
            if (!string.Equals(e.Hive, a.Hive, StringComparison.Ordinal) ||
                !string.Equals(e.KeyPath, a.KeyPath, StringComparison.Ordinal) ||
                !string.Equals(e.ValueName, a.ValueName, StringComparison.Ordinal) ||
                e.Existed != a.Existed ||
                e.Kind != a.Kind)
            {
                return false;
            }

            var eBytes = e.Value as byte[];
            var aBytes = a.Value as byte[];
            if ((eBytes, aBytes) is ({ Length: > 0 }, { Length: > 0 }))
            {
                if (!eBytes.AsSpan().SequenceEqual(aBytes)) return false;
            }
            else if (!Equals(e.Value, a.Value))
            {
                return false;
            }
        }
        return true;
    }

    private static void WriteAtomic(string path, string content)
    {
        var temp = path + ".tmp";
        using (var stream = File.Create(temp))
        using (var writer = new StreamWriter(stream, Encoding.UTF8))
        {
            writer.Write(content);
            writer.Flush();
            stream.Flush(flushToDisk: true);
        }
        File.Move(temp, path, overwrite: true);
    }

    private static string SafeId(string id) =>
        string.Concat(id.Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '_'));

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };
}
