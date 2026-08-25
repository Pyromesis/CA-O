using CAO.Core.Abstractions;

namespace CAO.Core.Services;

public sealed record StartupAppEntry(
    string Name,
    string Command,
    bool Enabled,
    bool IsUserScope);

/// <summary>
/// #20: startup apps management. Entries are never deleted — "disable"
/// moves them into a backup Run key so they can be re-enabled safely.
/// </summary>
public sealed class StartupAppsService
{
    public const string BackupKeySuffix = "-CAO-Disabled";

    private readonly IRegistryAccessor _registry;

    public StartupAppsService(IRegistryAccessor registry) => _registry = registry;

    private static (string runKey, string backupKey, bool userScope)[] Locations => new[]
    {
        (@"Software\Microsoft\Windows\CurrentVersion\Run",
         @"Software\Microsoft\Windows\CurrentVersion\Run" + BackupKeySuffix, true),
        (@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
         @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run" + BackupKeySuffix, false),
    };

    public IReadOnlyList<StartupAppEntry> List()
    {
        var entries = new List<StartupAppEntry>();
        foreach (var (runKey, backupKey, userScope) in Locations)
        {
            foreach (var name in _registry.GetValueNames(RegistryHive2.CurrentUser, runKey).Concat(
                userScope ? Array.Empty<string>() : _registry.GetValueNames(RegistryHive2.LocalMachine, runKey)))
            {
                var hive = userScope ? RegistryHive2.CurrentUser : RegistryHive2.LocalMachine;
                var command = _registry.GetValue(hive, runKey, name)?.ToString() ?? string.Empty;
                entries.Add(new StartupAppEntry(name, command, Enabled: true, IsUserScope: userScope));
            }
            foreach (var name in _registry.GetValueNames(RegistryHive2.CurrentUser, backupKey).Concat(
                userScope ? Array.Empty<string>() : _registry.GetValueNames(RegistryHive2.LocalMachine, backupKey)))
            {
                var hive = userScope ? RegistryHive2.CurrentUser : RegistryHive2.LocalMachine;
                var command = _registry.GetValue(hive, backupKey, name)?.ToString() ?? string.Empty;
                entries.Add(new StartupAppEntry(name, command, Enabled: false, IsUserScope: userScope));
            }
        }
        return entries.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Moves the entry to the backup key. Returns false when missing.</summary>
    public bool Disable(string name, bool userScope)
    {
        var (runKey, backupKey, _) = Resolve(userScope);
        var hive = userScope ? RegistryHive2.CurrentUser : RegistryHive2.LocalMachine;
        var value = _registry.GetValue(hive, runKey, name);
        if (value is null) return false;
        _registry.SetValue(hive, backupKey, name, value, RegistryValueKind2.String);
        _registry.DeleteValue(hive, runKey, name);
        return true;
    }

    public bool Enable(string name, bool userScope)
    {
        var (runKey, backupKey, _) = Resolve(userScope);
        var hive = userScope ? RegistryHive2.CurrentUser : RegistryHive2.LocalMachine;
        var value = _registry.GetValue(hive, backupKey, name);
        if (value is null) return false;
        _registry.SetValue(hive, runKey, name, value, RegistryValueKind2.String);
        _registry.DeleteValue(hive, backupKey, name);
        return true;
    }

    private static (string, string, bool) Resolve(bool userScope) =>
        Locations.First(l => l.userScope == userScope);
}
