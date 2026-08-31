namespace CAO.Shared;

/// <summary>Single source of truth for assembly/app versioning facts.</summary>
public static class AppVersion
{
    public const string Major = "2";
    public const string Minor = "1";
    public const string Patch = "5";

    public const string Semantic = $"{Major}.{Minor}.{Patch}";

    /// <summary>Current IPC protocol version; bump on any wire-format change. Aligned with IpcProtocol.Version.</summary>
    public const int ProtocolVersion = 2;
}

/// <summary>Application profiles offered by the profile engine (spec 104).</summary>
public enum ProfileId
{
    Safe,
    Balanced,
    Gaming,
    Competitive,
    Privacy,
    Security,
    Productivity,
    PowerSaver,
    Maintenance,
    Expert,
    Custom,
}

