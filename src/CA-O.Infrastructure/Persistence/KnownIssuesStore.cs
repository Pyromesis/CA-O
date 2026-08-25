using System.Text.Json;
using CAO.Shared;

namespace CAO.Infrastructure.Persistence;

/// <summary>
/// Known-issues database (spec 55). Ships with seed entries compiled-in and
/// supports drop-in updates via
/// %ProgramData%\CA-O\known-issues.json (merged on top of seeds) so the DB
/// can be refreshed without shipping a new app version.
/// </summary>
public sealed class KnownIssuesStore
{
    private readonly IReadOnlyList<KnownIssue> _entries;
    private readonly string _overridePath;

    public KnownIssuesStore(string? overridePath = null)
    {
        _overridePath = overridePath ?? Path.Combine(CaOPaths.ProgramDataRoot, "known-issues.json");
        _entries = Load().ToList();
    }

    public IReadOnlyList<KnownIssue> All => _entries;

    private IEnumerable<KnownIssue> Load()
    {
        foreach (var entry in Seeds)
        {
            yield return entry;
        }

        foreach (var entry in LoadOverrides())
        {
            yield return entry;
        }
    }

    private IEnumerable<KnownIssue> LoadOverrides()
    {
        List<KnownIssue>? overrides = null;
        try
        {
            if (File.Exists(_overridePath))
            {
                overrides = JsonSerializer.Deserialize<List<KnownIssue>>(
                    File.ReadAllText(_overridePath), JsonOptions);
            }
        }
        catch (JsonException)
        {
            // A corrupt override file must never break diagnostics.
            overrides = null;
        }

        if (overrides is null)
        {
            return Array.Empty<KnownIssue>();
        }

        return overrides;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Seed entries are deliberately conservative: only issues with public,
    /// verifiable vendor documentation qualify. Empty today — entries arrive
    /// through the update channel rather than being invented at code time.
    /// </summary>
    private static readonly IReadOnlyList<KnownIssue> Seeds = new List<KnownIssue>();
}
