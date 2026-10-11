using System.Runtime.InteropServices;

namespace CAO.Infrastructure.Overclock;

/// <summary>
/// Minimal READ-ONLY NVAPI surface.
///
/// Only the entry points CA-O actually reads are declared here. Setters
/// (clock offsets, power limit, fan curve, VBIOS) are deliberately absent until
/// phase F3 proves the write path on real silicon; a P/Invoke that is declared
/// but never exercised is a liability.
///
/// nvapi64.dll ships with every NVIDIA driver, so it is invoked when present
/// and never redistributed (design 6.18). Nothing here touches the kernel, a
/// game directory, or a process other than this one.
/// </summary>
internal static class NvInterop
{
    /// <summary>Ships with every NVIDIA driver; invoked when present, never redistributed.</summary>
    public const string DllName = "nvapi64.dll";

    /// <summary>NVAPI_STATUS_SUCCESS.</summary>
    private const int NvapiOk = 0;

    /// <summary>GPU_CLOCK_CURRENT.</summary>
    public const int ClockCurrent = 0;

    /// <summary>GPU_CLOCK_BOOST.</summary>
    public const int ClockBoost = 1;

    /// <summary>GPU_CLOCK_MAX.</summary>
    public const int ClockMax = 2;

    /// <summary>GPU_CLOCK_MEMORY.</summary>
    public const int ClockMemory = 8;

    /// <summary>NV_GPU_POWER_MGMT_LIMIT_DEFAULT_POWER_LIMIT.</summary>
    private const int PowerLimitDefault = 0;

    private static readonly object Gate = new();
    private static bool _attempted;
    private static bool _initialized;
    private static string? _failureKey;

    /// <summary>True once NVAPI answered NVAPI_STATUS_SUCCESS.</summary>
    public static bool IsInitialized => _initialized;

    /// <summary>
    /// Localization key explaining why NVAPI is unusable, or null when it works.
    /// CA-O reports this reason instead of silently hiding the GPU section.
    /// </summary>
    public static string? FailureKey
    {
        get { lock (Gate) { return _failureKey; } }
    }

    /// <summary>
    /// Loads and initializes NVAPI exactly once per process. Never throws:
    /// a machine without an NVIDIA driver is a supported configuration, not an
    /// error (design 6.12).
    /// </summary>
    public static bool TryInitialize()
    {
        lock (Gate)
        {
            if (_attempted)
            {
                return _initialized;
            }

            _attempted = true;

            try
            {
                _initialized = Nvapi_Initialize() == NvapiOk;
                _failureKey = _initialized ? null : "overclock-nvapi-init-failed";
            }
            catch (DllNotFoundException)
            {
                _failureKey = "overclock-nvapi-missing";
            }
            catch (EntryPointNotFoundException)
            {
                _failureKey = "overclock-nvapi-entrypoint-missing";
            }
            catch (BadImageFormatException)
            {
                _failureKey = "overclock-nvapi-arch-mismatch";
            }

            return _initialized;
        }
    }

    /// <summary>Number of NVIDIA GPUs visible to the driver.</summary>
    public static bool TryGetDeviceCount(out uint count)
    {
        count = 0;
        if (!TryInitialize())
        {
            return false;
        }

        try
        {
            int signed = 0;
            if (Nvapi_DeviceGetCount(ref signed) != NvapiOk || signed < 0)
            {
                return false;
            }

            count = (uint)signed;
            return true;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    /// <summary>Device handle for an index, used by every other query.</summary>
    public static bool TryGetDeviceHandle(int index, out uint handle)
    {
        handle = 0;
        if (!TryInitialize() || index < 0)
        {
            return false;
        }

        try
        {
            uint local = 0;
            if (Nvapi_Device_GetHandleByIndex(index, ref local) != NvapiOk)
            {
                return false;
            }

            handle = local;
            return true;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    /// <summary>
    /// Packed driver version token. Decoded with NVIDIA's published layout;
    /// when the decode is not plausible the raw token is reported instead of a
    /// fabricated "driver 0.0" (evidence discipline, design 6.4).
    /// </summary>
    public static bool TryGetDriverVersion(uint handle, out string version)
    {
        version = string.Empty;
        if (!TryInitialize())
        {
            return false;
        }

        try
        {
            int packed = 0;
            if (Nvapi_Device_GetDriverVersion(handle, ref packed) != NvapiOk)
            {
                return false;
            }

            uint raw = unchecked((uint)packed);
            uint major = (raw >> 22) & 0x3FF;
            uint minor = (raw >> 14) & 0x0FF;
            uint patch = (raw >> 6) & 0x0FF;
            uint build = raw & 0x03F;

            version = major > 0
                ? FormattableString.Invariant($"{major}.{minor}.{patch}.{build}")
                : "0x" + raw.ToString("X8", System.Globalization.CultureInfo.InvariantCulture);

            return true;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    /// <summary>One clock domain in MHz. <paramref name="clockType"/> is a GPU_CLOCK_TYPE.</summary>
    public static bool TryGetClock(uint handle, int clockType, out uint megahertz)
    {
        megahertz = 0;
        if (!TryInitialize())
        {
            return false;
        }

        try
        {
            uint local = 0;
            if (Nvapi_GPU_GetClockInfo(handle, clockType, ref local) != NvapiOk)
            {
                return false;
            }

            megahertz = local;
            return true;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    /// <summary>Fan duty cycle in percent. Not every board exposes a controllable fan.</summary>
    public static bool TryGetFanSpeed(uint handle, out uint percent)
    {
        percent = 0;
        if (!TryInitialize())
        {
            return false;
        }

        try
        {
            uint local = 0;
            if (Nvapi_GPU_GetFanSpeed(handle, ref local) != NvapiOk)
            {
                return false;
            }

            percent = local;
            return true;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    /// <summary>Default power limit in milliwatts.</summary>
    public static bool TryGetPowerLimit(uint handle, out uint milliwatts)
    {
        milliwatts = 0;
        if (!TryInitialize())
        {
            return false;
        }

        try
        {
            uint local = 0;
            if (Nvapi_GPU_GetPowerManagementLimit(handle, PowerLimitDefault, ref local) != NvapiOk)
            {
                return false;
            }

            milliwatts = local;
            return true;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    [DllImport(DllName, EntryPoint = "nvapi_Initialize", CallingConvention = CallingConvention.Cdecl)]
    private static extern int Nvapi_Initialize();

    [DllImport(DllName, EntryPoint = "nvapi_DeviceGetCount", CallingConvention = CallingConvention.Cdecl)]
    private static extern int Nvapi_DeviceGetCount(ref int count);

    [DllImport(DllName, EntryPoint = "nvapi_Device_GetHandleByIndex", CallingConvention = CallingConvention.Cdecl)]
    private static extern int Nvapi_Device_GetHandleByIndex(int index, ref uint handle);

    [DllImport(DllName, EntryPoint = "nvapi_Device_GetDriverVersion", CallingConvention = CallingConvention.Cdecl)]
    private static extern int Nvapi_Device_GetDriverVersion(uint deviceHandle, ref int driverVersion);

    [DllImport(DllName, EntryPoint = "nvapi_GPU_GetClockInfo", CallingConvention = CallingConvention.Cdecl)]
    private static extern int Nvapi_GPU_GetClockInfo(uint deviceHandle, int clockType, ref uint clock);

    [DllImport(DllName, EntryPoint = "nvapi_GPU_GetFanSpeed_v2", CallingConvention = CallingConvention.Cdecl)]
    private static extern int Nvapi_GPU_GetFanSpeed(uint deviceHandle, ref uint percentage);

    [DllImport(DllName, EntryPoint = "nvapi_GPU_GetPowerManagementLimit", CallingConvention = CallingConvention.Cdecl)]
    private static extern int Nvapi_GPU_GetPowerManagementLimit(uint deviceHandle, int limitIndex, ref uint limitMilliwatts);
}