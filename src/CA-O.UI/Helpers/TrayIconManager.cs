using System.Globalization;
using System.Runtime.InteropServices;

namespace CAO.UI.Helpers;

/// <summary>
/// Icono de la bandeja del sistema con Win32 puro (Shell_NotifyIconW).
/// Sin WinForms a proposito: UseWindowsForms es incompatible con WinUI
/// (error MC6000 de WinFX.targets). Ventana message-only propia para los
/// callbacks; el menu se reconstruye en cada apertura con el estado actual.
/// </summary>
internal sealed class TrayIconManager : IDisposable
{
    public event EventHandler? OpenRequested;
    public event EventHandler? ExitRequested;
    public event EventHandler<bool>? MinimizeToTrayChanged;
    public event EventHandler<bool>? CloseToTrayChanged;

    private const uint WmApp = 0x8000;
    private const uint CallbackMsg = WmApp + 1;
    private const uint WmRButtonUp = 0x0205;
    private const uint WmLButtonDblClk = 0x0203;

    private const uint NifMessage = 0x01;
    private const uint NifIcon = 0x02;
    private const uint NifTip = 0x04;
    private const uint NimAdd = 0x00;
    private const uint NimModify = 0x01;
    private const uint NimDelete = 0x02;

    private const uint MfString = 0x00;
    private const uint MfSeparator = 0x0800;
    private const uint MfChecked = 0x0008;
    private const uint TpmRightButton = 0x0002;
    private const uint TpmReturnCmd = 0x0100;

    private const uint CmdOpen = 1001;
    private const uint CmdToggleMinimize = 1002;
    private const uint CmdToggleClose = 1003;
    private const uint CmdExit = 1004;

    private const string ClassName = "CAO_TrayIconHost";

    private static WndProcDelegate? _wndProc;
    private static ushort _classAtom;
    private static TrayIconManager? _active;

    private nint _hwnd;
    private nint _icon;
    private bool _ownsIcon = true;
    private string _tooltip = "CA-O";
    private bool _minimizeToTray = true;
    private bool _closeToTray = true;
    private bool _visible;
    private bool _disposed;

    public void EnsureCreated(string iconPath, string tooltip)
    {
        if (_hwnd != nint.Zero) return;
        _tooltip = string.IsNullOrWhiteSpace(tooltip) ? "CA-O" : tooltip;
        EnsureWindowClass();
        var hInstance = GetModuleHandleW(null);
        _hwnd = CreateWindowExW(0, ClassName, string.Empty, 0, 0, 0, 0, 0,
            new nint(-3), nint.Zero, hInstance, nint.Zero);
        if (_hwnd == nint.Zero) return;
        _active = this;
        _icon = ResolveIcon(iconPath);
        if (_icon == nint.Zero)
        {
            LogTrayFailure("NIM_ADD omitido: sin handle de icono para '" + iconPath + "'.");
            return;
        }
        var data = BuildData(NifMessage | NifTip | NifIcon);
        _visible = Shell_NotifyIconW(NimAdd, ref data);
    }

    public void SyncMenu(bool minimizeToTray, bool closeToTray)
    {
        _minimizeToTray = minimizeToTray;
        _closeToTray = closeToTray;
    }

    public void Show()
    {
        if (_disposed || _hwnd == nint.Zero || _visible) return;
        if (_icon == nint.Zero) return;
        var data = BuildData(NifMessage | NifTip | NifIcon);
        _visible = Shell_NotifyIconW(NimAdd, ref data);
    }

    public void Hide()
    {
        if (_disposed || _hwnd == nint.Zero || !_visible) return;
        var data = BuildData(0);
        Shell_NotifyIconW(NimDelete, ref data);
        _visible = false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            if (_hwnd != nint.Zero)
            {
                if (_visible)
                {
                    var data = BuildData(0);
                    Shell_NotifyIconW(NimDelete, ref data);
                    _visible = false;
                }
                DestroyWindow(_hwnd);
                _hwnd = nint.Zero;
            }
            if (_icon != nint.Zero && _ownsIcon) { DestroyIcon(_icon); }
            _icon = nint.Zero;
            _ownsIcon = true;
            if (ReferenceEquals(_active, this)) _active = null;
        }
        catch { }
    }

    /// <summary>
    /// Cadena de fallbacks: small del ICO -&gt; large del ICO -&gt; LoadImage del
    /// ICO (16 y 32) -&gt; icono del propio exe -&gt; IDI_APPLICATION.
    /// Nunca devuelve cero sin haberlo intentado todo; el llamador no hace
    /// NIM_ADD sin NIF_ICON.
    /// </summary>
    private nint ResolveIcon(string iconPath)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(iconPath))
            {
                if (ExtractIconExW(iconPath, 0, out var large, out var small, 1) > 0)
                {
                    if (small != nint.Zero)
                    {
                        if (large != nint.Zero) DestroyIcon(large);
                        _ownsIcon = true;
                        return small;
                    }
                    if (large != nint.Zero)
                    {
                        _ownsIcon = true;
                        return large;
                    }
                }
                var fromFile = LoadImageW(nint.Zero, iconPath, ImageIcon, 16, 16, LoadFromFile);
                if (fromFile == nint.Zero)
                    fromFile = LoadImageW(nint.Zero, iconPath, ImageIcon, 32, 32, LoadFromFile);
                if (fromFile != nint.Zero)
                {
                    _ownsIcon = true;
                    return fromFile;
                }
            }
            var exePath = GetExePath();
            if (!string.IsNullOrEmpty(exePath))
            {
                if (ExtractIconExW(exePath, 0, out var exeLarge, out var exeSmall, 1) > 0)
                {
                    if (exeSmall != nint.Zero)
                    {
                        if (exeLarge != nint.Zero) DestroyIcon(exeLarge);
                        _ownsIcon = true;
                        return exeSmall;
                    }
                    if (exeLarge != nint.Zero)
                    {
                        _ownsIcon = true;
                        return exeLarge;
                    }
                }
            }
        }
        catch { }
        var fallback = LoadIconW(nint.Zero, IdiApplication);
        _ownsIcon = false;
        return fallback;
    }

    private static string? GetExePath()
    {
        try
        {
            var buf = new char[32767];
            var len = GetModuleFileNameW(nint.Zero, buf, (uint)buf.Length);
            if (len == 0) return null;
            return new string(buf, 0, (int)Math.Min(len, (uint)buf.Length));
        }
        catch { return null; }
    }

    private static void LogTrayFailure(string message)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CA-O", "logs");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "tray-icon.log"),
                DateTimeOffset.Now.ToString("o") + " " + message + Environment.NewLine);
        }
        catch { }
    }

    private NOTIFYICONDATAW BuildData(uint flags)
    {
        return new NOTIFYICONDATAW
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(),
            hWnd = _hwnd,
            uID = 1,
            uFlags = flags,
            uCallbackMessage = CallbackMsg,
            hIcon = _icon,
            szTip = _tooltip,
        };
    }

    private static void EnsureWindowClass()
    {
        if (_classAtom != 0) return;
        _wndProc = new WndProcDelegate(WindowProc);
        var hInstance = GetModuleHandleW(null);
        var wc = new WNDCLASSEXW
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance = hInstance,
            lpszClassName = ClassName,
        };
        _classAtom = RegisterClassExW(ref wc);
    }

    private static nint WindowProc(nint hWnd, uint msg, nint wParam, nint lParam)
    {
        try
        {
            if (msg == CallbackMsg)
                return _active?.OnTrayCallback((uint)lParam.ToInt32()) ?? DefWindowProcW(hWnd, msg, wParam, lParam);
        }
        catch { }
        return DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    private nint OnTrayCallback(uint mouseMsg)
    {
        if (mouseMsg == WmLButtonDblClk)
            OpenRequested?.Invoke(this, EventArgs.Empty);
        else if (mouseMsg == WmRButtonUp)
            ShowContextMenu();
        return nint.Zero;
    }

    private void ShowContextMenu()
    {
        bool es = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "es";
        var menu = CreatePopupMenu();
        if (menu == nint.Zero) return;
        try
        {
            AppendMenuW(menu, MfString, CmdOpen, es ? "Abrir CA-O" : "Open CA-O");
            AppendMenuW(menu, MfSeparator, 0, string.Empty);
            AppendMenuW(menu, MfString | (_minimizeToTray ? MfChecked : 0), CmdToggleMinimize,
                es ? "Minimizar a la bandeja" : "Minimize to tray");
            AppendMenuW(menu, MfString | (_closeToTray ? MfChecked : 0), CmdToggleClose,
                es ? "Cerrar a la bandeja" : "Close to tray");
            AppendMenuW(menu, MfSeparator, 0, string.Empty);
            AppendMenuW(menu, MfString, CmdExit, es ? "Salir" : "Exit");
            GetCursorPos(out var pt);
            SetForegroundWindow(_hwnd);
            uint cmd = TrackPopupMenuEx(menu, TpmRightButton | TpmReturnCmd, pt.X, pt.Y, _hwnd, nint.Zero);
            HandleMenuCommand(cmd);
        }
        finally { DestroyMenu(menu); }
    }

    private void HandleMenuCommand(uint cmd)
    {
        switch (cmd)
        {
            case CmdOpen:
                OpenRequested?.Invoke(this, EventArgs.Empty);
                break;
            case CmdToggleMinimize:
                _minimizeToTray = !_minimizeToTray;
                MinimizeToTrayChanged?.Invoke(this, _minimizeToTray);
                break;
            case CmdToggleClose:
                _closeToTray = !_closeToTray;
                CloseToTrayChanged?.Invoke(this, _closeToTray);
                break;
            case CmdExit:
                ExitRequested?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    private delegate nint WndProcDelegate(nint hWnd, uint msg, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEXW
    {
        public uint cbSize;
        public uint style;
        public nint lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public nint hInstance;
        public nint hIcon;
        public nint hCursor;
        public nint hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public nint hIconSm;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATAW
    {
        public uint cbSize;
        public nint hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public nint hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public nint hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassExW(ref WNDCLASSEXW lpwcx);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateWindowExW(uint dwExStyle, string lpClassName, string lpWindowName,
        uint dwStyle, int x, int y, int nWidth, int nHeight, nint hWndParent, nint hMenu, nint hInstance, nint lpParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint DefWindowProcW(nint hWnd, uint msg, nint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(nint hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(nint hIcon);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint CreatePopupMenu();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyMenu(nint hMenu);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool AppendMenuW(nint hMenu, uint uFlags, nuint uIDNewItem, string lpNewItem);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint TrackPopupMenuEx(nint hMenu, uint uFlags, int x, int y, nint hWnd, nint lptpm);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIconW(uint dwMessage, ref NOTIFYICONDATAW lpData);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint ExtractIconExW(string lpszFile, int nIconIndex, out nint phiconLarge, out nint phiconSmall, uint nIcons);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandleW(string? lpModuleName);

    private const uint ImageIcon = 1;
    private const uint LoadFromFile = 0x00000010;
    private static readonly nint IdiApplication = new(32512);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint LoadImageW(nint hInst, string name, uint type, int cx, int cy, uint fuLoad);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint LoadIconW(nint hInstance, nint lpIconName);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetModuleFileNameW(nint hModule, [MarshalAs(UnmanagedType.LPArray)] char[] lpFilename, uint nSize);
}
