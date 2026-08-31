using System.Runtime.Versioning;
using System.Text;

namespace CAO.Shared.Constants;

/// <summary>
/// Single source of truth for all build-time and runtime constants.
/// All projects must reference this file - no hardcoded versions, paths, or names anywhere else.
/// </summary>
[SupportedOSPlatform("windows")]
public static class BuildConstants
{
    // Version - SINGLE SOURCE OF TRUTH
    public const string ProductVersion = "2.1.5";
    public const string ProductVersionMajor = "2";
    public const string ProductVersionMinor = "1";
    public const string ProductVersionPatch = "5";

    // Product Identity
    public const string ProductName = "CA-O";
    public const string ProductTitle = "CA-O Windows Optimizer";
    public const string ProductDescription = "Windows 11 Performance, Diagnostics & Optimization Platform";
    public const string Publisher = "CA-O Team";
    public const string ProductUrl = "https://github.com/Pyromesis/CA-O";

    // Service Identity - SINGLE CANONICAL NAME
    public const string ServiceName = "CAO.Privileged";
    public const string ServiceDisplayName = "CA-O Privileged Service";
    public const string ServiceDescription = "CA-O privileged operations host (SYSTEM) - Named Pipe IPC with ACL";

    // Runtime & Framework
    public const string TargetFramework = "net10.0-windows10.0.19041.0";
    public const string RuntimeIdentifier = "win-x64";
    public const string Configuration = "Release";

    // Directory Structure
    public const string InstallDirectoryName = "CA-O";
    public const string ProgramDataDirectoryName = "CA-O";
    public const string UiSubdirectory = "ui";
    public const string ServiceSubdirectory = "service";
    public const string UninstallSubdirectory = "uninstall";
    public const string SetupSubdirectory = "setup";
    public const string GuiInstallerSubdirectory = "gui-installer";

    // Executable Names
    public const string UiExecutable = "CA-O.UI.exe";
    public const string ServiceExecutable = "CA-O.Privileged.exe";
    public const string UninstallerExecutable = "CA-O.Uninstaller.exe";
    public const string GuiInstallerExecutable = "CA-O.InstallerGui.exe";
    public const string SetupExecutable = "CA-O.Setup.exe";

    // Package Names
    public const string FullPackageName = "CA-O-{0}-win-x64.zip";
    public const string GuiInstallerPackageName = "CA-O-Setup-GUI-x64.zip";
    public const string GuiInstallerExeName = "CA-O-Setup-GUI-x64.exe";
    public const string SetupPackageName = "CA-O.Setup.exe";

    // Manifest Files
    public const string Sha256ManifestName = "SHA256SUMS.txt";
    public const string SbomFileName = "bom.json";

    // Named Pipe
    public const string PipeName = "CA-O.Privileged.v1";

    // Registry Paths
    public const string UninstallRegistryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\CA-O";
    public const string UninstallRegistryPathWow64 = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\CA-O";

    // Shortcut Names
    public const string StartMenuShortcutName = "CA-O.lnk";
    public const string DesktopShortcutName = "CA-O.lnk";
    public const string StartMenuFolderName = "CA-O";

    // Data Directories (under ProgramData\CA-O)
    public const string TransactionsDirectory = "transactions";
    public const string SnapshotsDirectory = "snapshots";
    public const string HistoryFile = "history.jsonl";
    public const string AnalysisStateFile = "analysis-state.json";
    public const string BenchmarksDirectory = "benchmarks";
    public const string SettingsFile = "settings.json";
    public const string KnownIssuesFile = "known-issues.json";

    // IPC Protocol
    public const int IpcProtocolVersion = 2;
    public const int MaxRequestBytes = 64 * 1024;
    public const int MaxResponseBytes = 256 * 1024;
    public static readonly TimeSpan IpcMaxAge = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan IpcRequestTimeout = TimeSpan.FromSeconds(15);

    // Service Recovery
    public const string ServiceRecoveryActions = "restart/5000/restart/10000/reboot/60000";
    public const int ServiceRecoveryResetSeconds = 86400;

    // Build Artifact Paths (relative to repository root)
    public const string ArtifactsRoot = "artifacts";
    public const string ReleaseArtifactsDir = "artifacts/release";
    public const string SbomDir = "artifacts/sbom";

    // Analysis State
    public const int AnalysisStateSchemaVersion = 2;
    public static readonly TimeSpan AnalysisTtl = TimeSpan.FromHours(24);

    // Benchmark
    public const double BenchmarkNoiseFloorPercent = 3.0;
    public const int BenchmarkDefaultTrials = 5;

    // Helper Methods
    public static string GetInstallDirectory() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), InstallDirectoryName);

    public static string GetProgramDataRoot() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), ProgramDataDirectoryName);

    public static string GetUiInstallPath() =>
        Path.Combine(GetInstallDirectory(), UiSubdirectory);

    public static string GetServiceInstallPath() =>
        Path.Combine(GetInstallDirectory(), ServiceSubdirectory);

    public static string GetUninstallInstallPath() =>
        Path.Combine(GetInstallDirectory(), UninstallSubdirectory);

    public static string GetGuiInstallerInstallPath() =>
        Path.Combine(GetInstallDirectory(), GuiInstallerSubdirectory);

    public static string GetSetupInstallPath() =>
        Path.Combine(GetInstallDirectory(), SetupSubdirectory);

    public static string GetUiExecutablePath() =>
        Path.Combine(GetUiInstallPath(), UiExecutable);

    public static string GetServiceExecutablePath() =>
        Path.Combine(GetServiceInstallPath(), ServiceExecutable);

    public static string GetUninstallerExecutablePath() =>
        Path.Combine(GetUninstallInstallPath(), UninstallerExecutable);

    public static string GetProgramDataTransactionsPath() =>
        Path.Combine(GetProgramDataRoot(), TransactionsDirectory);

    public static string GetProgramDataSnapshotsPath() =>
        Path.Combine(GetProgramDataRoot(), SnapshotsDirectory);

    public static string GetProgramDataHistoryPath() =>
        Path.Combine(GetProgramDataRoot(), HistoryFile);

    public static string GetProgramDataAnalysisStatePath() =>
        Path.Combine(GetProgramDataRoot(), AnalysisStateFile);

    public static string GetProgramDataBenchmarksPath() =>
        Path.Combine(GetProgramDataRoot(), BenchmarksDirectory);

    public static string GetProgramDataSettingsPath() =>
        Path.Combine(GetProgramDataRoot(), SettingsFile);

    public static string GetProgramDataKnownIssuesPath() =>
        Path.Combine(GetProgramDataRoot(), KnownIssuesFile);

    // Cached format for package name
    private static readonly CompositeFormat FullPackageFormat = CompositeFormat.Parse(FullPackageName);

    public static string GetFullPackageName() =>
        string.Format(null, FullPackageFormat, ProductVersion);

    public static string GetGuiInstallerPackageName() =>
        GuiInstallerPackageName;

    public static string GetSha256ManifestPath(string artifactRoot) =>
        Path.Combine(artifactRoot, Sha256ManifestName);

    public static string GetSbomPath() =>
        Path.Combine(SbomDir, SbomFileName);
}
