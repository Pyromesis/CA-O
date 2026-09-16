using System.Runtime.Versioning;

namespace CAO.Shared;

/// <summary>
/// Versión visible de la app (UI, servicio, snapshots, health).
/// Deriva SIEMPRE de BuildConstants.ProductVersion: estaba hardcodeada
/// ("2.1.29") y la UI mostraba versión vieja aunque el build estuviera al día.
/// </summary>
[SupportedOSPlatform("windows")]
public static class AppVersion
{
    public static string Semantic => Constants.BuildConstants.ProductVersion;

    public static string Major => VersionPart(0, "2");
    public static string Minor => VersionPart(1, "1");
    public static string Patch => VersionPart(2, "0");

    private static string VersionPart(int index, string fallback)
    {
        var parts = Constants.BuildConstants.ProductVersion.Split('.');
        return parts.Length > index ? parts[index] : fallback;
    }

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

