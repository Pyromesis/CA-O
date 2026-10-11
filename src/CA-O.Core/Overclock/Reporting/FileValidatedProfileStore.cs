using System.Globalization;
using System.Text;
using System.Text.Json;
using CAO.Shared;

namespace CAO.Core.Overclock.Reporting;

/// <summary>
/// File-backed validated profiles. Same discipline as the checkpoint store: atomic
/// writes, unreadable files quarantined and never deleted, transient I/O skipped.
/// One file per knob, so saving twice keeps the latest run instead of history.
/// </summary>
public sealed class FileValidatedProfileStore : IValidatedProfileStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _directory;

    public FileValidatedProfileStore(string? directory = null) =>
        _directory = directory ?? CaOPaths.OverclockProfilesDirectory;

    public async Task<IReadOnlyList<ValidatedOverclockProfile>> ListAsync(CancellationToken cancellationToken)
    {
        var result = new List<ValidatedOverclockProfile>();
        if (!Directory.Exists(_directory))
        {
            return result;
        }

        foreach (var file in Directory.EnumerateFiles(_directory, "*.json"))
        {
            cancellationToken.ThrowIfCancellationRequested();

            ValidatedOverclockProfile? profile;
            try
            {
                var json = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
                profile = JsonSerializer.Deserialize<ValidatedOverclockProfile>(json, SerializerOptions);
            }
            catch (JsonException)
            {
                profile = null;
            }
            catch (IOException)
            {
                continue;
            }

            if (profile is null)
            {
                Quarantine(file);
                continue;
            }

            result.Add(profile);
        }

        return result;
    }

    public async Task SaveAsync(ValidatedOverclockProfile profile, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);

        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, profile.Knob.Value + ".json");

        var temp = path + ".tmp";
        var json = JsonSerializer.Serialize(profile, SerializerOptions);
        await File.WriteAllTextAsync(temp, json, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        File.Move(temp, path, overwrite: true);
    }

    private static void Quarantine(string file)
    {
        var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        File.Move(file, $"{file}.corrupt-{stamp}-unparseable");
    }
}
