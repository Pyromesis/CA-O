using CAO.Shared;
namespace CAO.Core.Abstractions;

/// <summary>Persistent app settings (settings.json).</summary>
public interface ISettingsStore
{
    AppSettings Load();

    void Save(AppSettings settings);
}

/// <summary>Append-only change history (%ProgramData%\CA-O\history.log).</summary>
public interface IHistoryLogger
{
    void Log(HistoryEntry entry);

    IReadOnlyList<HistoryEntry> ReadLast(int maxEntries);
}

/// <summary>Windows system restore points.</summary>
public interface IRestorePointService
{
    /// <summary>Creates a restore point; returns false with reason when unsupported/failing.</summary>
    Task<(bool Success, string ReasonEs)> CreateAsync(string description, CancellationToken ct = default);

    Task<IReadOnlyList<RestorePointInfo>> ListAsync(CancellationToken ct = default);
}

/// <summary>Live hardware/OS facts for Dashboard + gating rules.</summary>
public sealed record SystemInfoReport(
    string WindowsVersion,
    string WindowsEdition,
    int RamGb,
    string CpuName,
    bool HasSsd,
    bool IsElevated,
    bool IsLaptop)
{
    public int CpuCores { get; init; }

    public int CpuLogicalProcessors { get; init; }

    public string Architecture { get; init; } = string.Empty;

    public int WindowsBuild { get; init; }
}

public interface ISystemInfoProvider
{
    Task<SystemInfoReport> GetAsync(CancellationToken ct = default);
}
