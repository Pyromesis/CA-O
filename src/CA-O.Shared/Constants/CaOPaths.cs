namespace CAO.Shared;

/// <summary>Well-known file-system locations used across processes.</summary>
public static class CaOPaths
{
    public const string RootFolderName = "CA-O";

    public static string ProgramDataRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), RootFolderName);

    public static string HistoryFile => Path.Combine(ProgramDataRoot, "history.jsonl");

    public static string SnapshotsDirectory => Path.Combine(ProgramDataRoot, "snapshots");

    public static string BenchmarksDirectory => Path.Combine(ProgramDataRoot, "benchmarks");

    public static string SettingsFile =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), RootFolderName, "settings.json");

    public static string UiLogsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), RootFolderName, "logs");

    public static string UiCrashLogFile => Path.Combine(UiLogsDirectory, "cao-ui-crash.log");

    public static string ServiceLogsDirectory => Path.Combine(ProgramDataRoot, "logs");

    /// <summary>
    /// Tuning checkpoints for the Overclock section. One JSON file per run,
    /// written BEFORE any hardware state is mutated so a crash always leaves a
    /// restorable record on disk (design D4).
    /// </summary>
    public static string OverclockRunsDirectory => Path.Combine(ProgramDataRoot, "overclock", "runs");
}

/// <summary>IPC constants shared by UI client and privileged service.</summary>
public static class IpcConstants
{
    public const string PipeName = "CA-O.Privileged.v1";
}
