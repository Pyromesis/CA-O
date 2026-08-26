using System.Runtime.InteropServices;
using System.Security.Principal;
using CAO.Shared.Security;

namespace CAO.Infrastructure.Windows.Security;

/// <summary>
/// Extracts real Windows-token facts (FASE 2/P1-8): SID, name, session id
/// (TokenSessionId), elevation (TokenElevation) and admin group membership.
/// No derived placeholders: every value comes from the actual token handle.
/// </summary>
public sealed class WindowsCallerInspector
{
    private enum TOKEN_INFORMATION_CLASS
    {
        TokenSessionId = 12,
        TokenElevation = 20,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TOKEN_ELEVATION
    {
        public uint TokenIsElevated;
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetTokenInformation(
        IntPtr tokenHandle,
        TOKEN_INFORMATION_CLASS tokenInformationClass,
        out uint tokenInformation,
        uint tokenInformationLength,
        out uint returnLength);

    public CallerIdentity Inspect(WindowsIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);

        var sid = identity.User?.Value ?? "S-0-0";
        var name = identity.Name ?? string.Empty;
        var isElevated = GetUintTokenInfo(identity.Token, TOKEN_INFORMATION_CLASS.TokenElevation) != 0;
        var sessionId = checked((int)GetUintTokenInfo(identity.Token, TOKEN_INFORMATION_CLASS.TokenSessionId));
        var isAdmin = new WindowsPrincipal(identity)
            .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);

        return new CallerIdentity(sid, name, isAdmin, isElevated, sessionId);
    }

    private static uint GetUintTokenInfo(IntPtr token, TOKEN_INFORMATION_CLASS infoClass)
    {
        if (GetTokenInformation(token, infoClass, out var value, sizeof(uint), out _))
        {
            return value;
        }
        return 0; // unavailable information reads as false/zero, never throws
    }
}
