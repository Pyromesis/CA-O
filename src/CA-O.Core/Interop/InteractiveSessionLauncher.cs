using System.Runtime.InteropServices;

namespace CAO.Core.Interop;

/// <summary>
/// Relaunches a GUI process inside the active interactive session.
/// The privileged service runs as SYSTEM in session 0: starting
/// explorer.exe there would leave the user's desktop dead (and a
/// session-0 copy could even fake a "running" check). This helper
/// steals the logged-on user's token (WTSQueryUserToken) and starts
/// the process on winsta0\default via CreateProcessAsUser.
/// </summary>
internal static class InteractiveSessionLauncher
{
    private const uint InvalidSessionId = 0xFFFFFFFF;
    private const int SW_SHOW = 5;
    private const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;
    private const uint CREATE_NO_WINDOW = 0x08000000;

    public static bool TryLaunch(string exePath, string? arguments, bool hidden, out string error)
    {
        error = string.Empty;
        var sessionId = WTSGetActiveConsoleSessionId();
        if (sessionId == InvalidSessionId)
        {
            error = "Sin sesión interactiva activa (equipo bloqueado o sin usuario).";
            return false;
        }
        if (!WTSQueryUserToken(sessionId, out var userToken) || userToken == IntPtr.Zero)
        {
            error = $"No se pudo obtener el token de la sesión {sessionId} (Win32 {Marshal.GetLastWin32Error()}).";
            return false;
        }
        try
        {
            if (!DuplicateTokenEx(userToken, 0x02000000 /* MAXIMUM_ALLOWED */, IntPtr.Zero,
                    2 /* SecurityImpersonation */, 1 /* TokenPrimary */, out var primaryToken) || primaryToken == IntPtr.Zero)
            {
                error = $"DuplicateTokenEx falló (Win32 {Marshal.GetLastWin32Error()}).";
                return false;
            }
            try
            {
                CreateEnvironmentBlock(out var env, primaryToken, false);
                try
                {
                    var startup = new STARTUPINFO();
                    startup.cb = Marshal.SizeOf<STARTUPINFO>();
                    startup.lpDesktop = "winsta0\\default";
                    startup.dwFlags = 0x00000001 /* STARTF_USESHOWWINDOW */;
                    startup.wShowWindow = (short)(hidden ? 0 /* SW_HIDE */ : SW_SHOW);
                    var commandLine = arguments is null ? $"\"{exePath}\"" : $"\"{exePath}\" {arguments}";
                    var ok = CreateProcessAsUser(
                        primaryToken, exePath, commandLine,
                        IntPtr.Zero, IntPtr.Zero, false,
                        CREATE_UNICODE_ENVIRONMENT | (hidden ? CREATE_NO_WINDOW : 0),
                        env, null, ref startup, out var procInfo);
                    if (!ok)
                    {
                        error = $"CreateProcessAsUser falló (Win32 {Marshal.GetLastWin32Error()}).";
                        return false;
                    }
                    CloseHandle(procInfo.hProcess);
                    CloseHandle(procInfo.hThread);
                    return true;
                }
                finally
                {
                    if (env != IntPtr.Zero) DestroyEnvironmentBlock(env);
                }
            }
            finally
            {
                CloseHandle(primaryToken);
            }
        }
        finally
        {
            CloseHandle(userToken);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern uint WTSGetActiveConsoleSessionId();

    [DllImport("wtsapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WTSQueryUserToken(uint sessionId, out IntPtr token);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DuplicateTokenEx(
        IntPtr hExistingToken, uint dwDesiredAccess, IntPtr lpTokenAttributes,
        int impersonationLevel, int tokenType, out IntPtr phNewToken);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcessAsUser(
        IntPtr hToken, string? lpApplicationName, string lpCommandLine,
        IntPtr lpProcessAttributes, IntPtr lpThreadAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool bInheritHandles, uint dwCreationFlags,
        IntPtr lpEnvironment, string? lpCurrentDirectory,
        ref STARTUPINFO lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("userenv.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateEnvironmentBlock(out IntPtr lpEnvironment, IntPtr hToken,
        [MarshalAs(UnmanagedType.Bool)] bool bInherit);

    [DllImport("userenv.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyEnvironmentBlock(IntPtr lpEnvironment);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFO
    {
        public int cb;
        public string? lpReserved;
        public string? lpDesktop;
        public string? lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }
}
