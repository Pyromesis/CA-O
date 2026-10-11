using System.Runtime.InteropServices;
using System.Text;

namespace CAO.Infrastructure.Overclock;

/// <summary>Clock domain a write targets. Values match <c>nvmlClockType_t</c>.</summary>
public enum NvidiaClockDomain
{
    /// <summary>NVML_CLOCK_GRAPHICS / SM. The core clock offset.</summary>
    Graphics = 0,

    /// <summary>NVML_CLOCK_MEM. The memory clock offset.</summary>
    Memory = 2,
}

/// <summary>
/// How the driver answered a write, classified from codes NVIDIA publishes in
/// its own header (nvml.h, API version 8). Nothing here is guessed: an
/// unclassified code is reported as <see cref="Rejected"/> carrying the raw
/// number instead of being dressed up as a friendlier lie (design 6.12).
/// </summary>
public enum NvidiaTuningWriteOutcome
{
    /// <summary>The driver accepted the write.</summary>
    Applied,

    /// <summary>The driver or the GPU does not implement this setter at all.</summary>
    DriverBlocked,

    /// <summary>The driver refused because the caller is not elevated.</summary>
    NoPermission,

    /// <summary>The driver refused for a reason CA-O does not classify.</summary>
    Rejected,
}

/// <summary>
/// What the driver can do on this machine, read once and cached. Every field
/// is nullable: a value the driver did not report is absent, never zero.
/// </summary>
public sealed record NvidiaTuningDevice(
    string? DriverVersion,
    int? MinPowerLimitMilliwatts,
    int? MaxPowerLimitMilliwatts,
    int? DefaultPowerLimitMilliwatts,
    int? EnforcedPowerLimitMilliwatts,
    int? CoreOffsetMinMegahertz,
    int? CoreOffsetMaxMegahertz,
    int? MemoryOffsetMinMegahertz,
    int? MemoryOffsetMaxMegahertz);

/// <summary>
/// The NVIDIA WRITE surface, behind an interface so the backend can be tested
/// without a GPU (design 6.15). <see cref="NvmlTuningApi"/> is the real one.
///
/// Reads deliberately stay on NVAPI in <see cref="NvInterop"/>: that path is
/// already proven and green, and splitting reads across two DLLs for no reason
/// is how interop layers rot. Only the setters - which have no public NVAPI
/// equivalent - come from NVML.
/// </summary>
public interface INvidiaTuningApi
{
    /// <summary>True when NVML answered <c>NVML_SUCCESS</c> on init.</summary>
    bool IsAvailable { get; }

    /// <summary>Localization key explaining an unavailable write path, or null.</summary>
    string? UnavailableReasonKey { get; }

    /// <summary>The cached description of this device. False, and a null device, when it cannot be read.</summary>
    bool TryDescribe(out NvidiaTuningDevice? device);

    /// <summary>Current power limit in milliwatts, or null when not readable.</summary>
    int? ReadPowerLimitMilliwatts();

    /// <summary>Current core clock offset in MHz, or null when not readable.</summary>
    int? ReadCoreOffsetMegahertz();

    /// <summary>Current memory clock offset in MHz, or null when not readable.</summary>
    int? ReadMemoryOffsetMegahertz();

    /// <summary>Writes the power limit in milliwatts. Returns the raw NVML status.</summary>
    int SetPowerLimitMilliwatts(int milliwatts);

    /// <summary>Writes the core clock offset in MHz. Returns the raw NVML status.</summary>
    int SetCoreOffsetMegahertz(int megahertz);

    /// <summary>Writes the memory clock offset in MHz. Returns the raw NVML status.</summary>
    int SetMemoryOffsetMegahertz(int megahertz);
}

/// <summary>
/// The real NVML-backed implementation of <see cref="INvidiaTuningApi"/>.
///
/// Anti-cheat posture is identical to the read path: a P/Invoke into the
/// vendor's own signed DLL, shipped by the driver, invoked when present and
/// never redistributed (design 6.18). No kernel component, no overlay, no
/// injection, no game directory.
/// </summary>
public sealed class NvmlTuningApi : INvidiaTuningApi
{
    private readonly object _gate = new();
    private NvidiaTuningDevice? _device;
    private bool _described;

    /// <summary>The single instance the backend uses by default.</summary>
    public static NvmlTuningApi Instance { get; } = new();

    /// <inheritdoc />
    public bool IsAvailable => NvmlInterop.TryInitialize();

    /// <inheritdoc />
    public string? UnavailableReasonKey => NvmlInterop.FailureKey;

    /// <inheritdoc />
    public bool TryDescribe(out NvidiaTuningDevice? device)
    {
        lock (_gate)
        {
            if (_described && _device is not null)
            {
                device = _device;
                return true;
            }

            _described = true;
            _device = ReadDevice();
            device = _device;
            return _device is not null;
        }
    }

    /// <inheritdoc />
    public int? ReadPowerLimitMilliwatts() =>
        NvmlInterop.TryInitialize()
        && NvmlInterop.TryResolveHandle(out uint handle)
        && NvmlInterop.TryGetPowerLimit(handle, out uint milliwatts)
            ? checked((int)milliwatts)
            : null;

    /// <inheritdoc />
    public int? ReadCoreOffsetMegahertz() => ReadOffset(NvidiaClockDomain.Graphics);

    /// <inheritdoc />
    public int? ReadMemoryOffsetMegahertz() => ReadOffset(NvidiaClockDomain.Memory);

    /// <inheritdoc />
    public int SetPowerLimitMilliwatts(int milliwatts) =>
        Guarded(handle => NvmlInterop.SetPowerLimit(handle, checked((uint)milliwatts)));

    /// <inheritdoc />
    public int SetCoreOffsetMegahertz(int megahertz) =>
        Guarded(handle => NvmlInterop.SetClockOffset(handle, NvidiaClockDomain.Graphics, megahertz));

    /// <inheritdoc />
    public int SetMemoryOffsetMegahertz(int megahertz) =>
        Guarded(handle => NvmlInterop.SetClockOffset(handle, NvidiaClockDomain.Memory, megahertz));

    private static int? ReadOffset(NvidiaClockDomain domain)
    {
        if (!NvmlInterop.TryInitialize() || !NvmlInterop.TryResolveHandle(out uint handle))
        {
            return null;
        }

        return NvmlInterop.TryGetClockOffset(handle, domain, out int offset, out _, out _)
            ? offset
            : null;
    }

    /// <summary>
    /// Runs one setter. A missing export is reported as FUNCTION_NOT_FOUND
    /// rather than thrown: the DLL loaded, but the installed driver predates
    /// this setter, which is demonstrable rather than a guess.
    /// </summary>
    private static int Guarded(Func<uint, int> call)
    {
        if (!NvmlInterop.TryInitialize() || !NvmlInterop.TryResolveHandle(out uint handle))
        {
            return NvmlInterop.ErrorFunctionNotFound;
        }

        try
        {
            return call(handle);
        }
        catch (DllNotFoundException)
        {
            return NvmlInterop.ErrorFunctionNotFound;
        }
        catch (EntryPointNotFoundException)
        {
            return NvmlInterop.ErrorFunctionNotFound;
        }
        catch (BadImageFormatException)
        {
            return NvmlInterop.ErrorFunctionNotFound;
        }
    }

    private static NvidiaTuningDevice? ReadDevice()
    {
        if (!NvmlInterop.TryInitialize() || !NvmlInterop.TryResolveHandle(out uint handle))
        {
            return null;
        }

        string? driver = NvmlInterop.TryGetSystemDriverVersion(out string version) ? version : null;

        int? min = null;
        int? max = null;
        if (NvmlInterop.TryGetPowerLimitConstraints(handle, out uint lowest, out uint highest))
        {
            min = checked((int)lowest);
            max = checked((int)highest);
        }

        int? factory = NvmlInterop.TryGetDefaultPowerLimit(handle, out uint d) ? checked((int)d) : null;
        int? enforced = NvmlInterop.TryGetEnforcedPowerLimit(handle, out uint e) ? checked((int)e) : null;

        return new NvidiaTuningDevice(
            driver,
            min,
            max,
            factory,
            enforced,
            OffsetBound(handle, NvidiaClockDomain.Graphics, lowest: true),
            OffsetBound(handle, NvidiaClockDomain.Graphics, lowest: false),
            OffsetBound(handle, NvidiaClockDomain.Memory, lowest: true),
            OffsetBound(handle, NvidiaClockDomain.Memory, lowest: false));
    }

    private static int? OffsetBound(uint handle, NvidiaClockDomain domain, bool lowest) =>
        NvmlInterop.TryGetClockOffset(handle, domain, out _, out int min, out int max)
            ? checked((int)(lowest ? min : max))
            : null;
}

/// <summary>
/// Raw NVML P/Invoke. Declared per function because no public NVAPI equivalent
/// exists for any of them: NVIDIA's open-source <c>nvapi.h</c> ships the lite
/// profile, which contains zero overclocking entry points, so the setters are
/// only reachable through NVML.
///
/// In x64 there is no <c>nvml64.dll</c>; <c>nvml.dll</c> is the real loader
/// and its export list was verified on this machine before any of this was
/// written.
/// </summary>
internal static class NvmlInterop
{
    /// <summary>Ships with every NVIDIA driver; invoked when present, never redistributed.</summary>
    public const string DllName = "nvml.dll";

    /// <summary>nvmlReturn_t.NVML_SUCCESS.</summary>
    public const int Success = 0;

    /// <summary>nvmlReturn_t.NVML_ERROR_NOT_SUPPORTED.</summary>
    public const int ErrorNotSupported = 3;

    /// <summary>nvmlReturn_t.NVML_ERROR_NO_PERMISSION.</summary>
    public const int ErrorNoPermission = 4;

    /// <summary>nvmlReturn_t.NVML_ERROR_FUNCTION_NOT_FOUND: "Local version of NVML doesn't implement this function."</summary>
    public const int ErrorFunctionNotFound = 13;

    /// <summary>sizeof(nvmlClockOffset_v1_t) | (1 &lt;&lt; 24). Only the first four fields are sent by the setter.</summary>
    private const uint ClockOffsetVersionV1 = 16 | (1u << 24);

    /// <summary>sizeof(nvmlClockOffset_v2_t) | (2 &lt;&lt; 24). The getter needs the bounds the v1 struct does not carry.</summary>
    private const uint ClockOffsetVersionV2 = 24 | (2u << 24);

    /// <summary>Performance state that owns the boost clock.</summary>
    private const uint BoostPstate = 0;

    private static readonly object Gate = new();
    private static bool _attempted;
    private static bool _initialized;
    private static bool _handleResolved;
    private static uint _handle;
    private static string? _failureKey;

    /// <summary>Localization key explaining why NVML is unusable, or null when it works.</summary>
    public static string? FailureKey
    {
        get { lock (Gate) { return _failureKey; } }
    }

    /// <summary>
    /// Initializes NVML exactly once per process and never throws. The library
    /// is deliberately never shut down: NVML init is reference counted and
    /// another component may already hold it (design 6.12).
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
                _initialized = NvmlInit() == Success;
                _failureKey = _initialized ? null : "overclock-nvml-init-failed";
            }
            catch (DllNotFoundException)
            {
                _failureKey = "overclock-nvml-missing";
            }
            catch (EntryPointNotFoundException)
            {
                _failureKey = "overclock-nvml-entrypoint-missing";
            }
            catch (BadImageFormatException)
            {
                _failureKey = "overclock-nvml-arch-mismatch";
            }

            return _initialized;
        }
    }

    /// <summary>
    /// The primary adapter, as an opaque handle value. CA-O reads and writes the
    /// first device only: multi-GPU systems report every card separately rather
    /// than pretending one profile fits all (same rule as the read path).
    /// </summary>
    public static bool TryResolveHandle(out uint handle)
    {
        lock (Gate)
        {
            if (_handleResolved)
            {
                handle = _handle;
                return _handle != 0;
            }

            _handleResolved = true;
            handle = 0;

            try
            {
                if (NvmlDeviceGetCount(out uint devices) != Success || devices == 0)
                {
                    _failureKey ??= "overclock-nvml-no-device";
                    return false;
                }

                if (NvmlDeviceGetHandleByIndex(0, out IntPtr device) != Success || device == IntPtr.Zero)
                {
                    _failureKey ??= "overclock-nvml-device-unavailable";
                    return false;
                }

                _handle = unchecked((uint)device.ToInt64());
                handle = _handle;
                return true;
            }
            catch (DllNotFoundException)
            {
                _failureKey ??= "overclock-nvml-missing";
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                _failureKey ??= "overclock-nvml-entrypoint-missing";
                return false;
            }
        }
    }

    /// <summary>Driver version string. NVML only exposes the system-wide one.</summary>
    public static bool TryGetSystemDriverVersion(out string version)
    {
        version = string.Empty;
        if (!TryInitialize())
        {
            return false;
        }

        IntPtr buffer = IntPtr.Zero;
        try
        {
            const int Capacity = 128;
            buffer = Marshal.AllocHGlobal(Capacity);
            for (int i = 0; i < Capacity; i++)
            {
                Marshal.WriteByte(buffer, i, 0);
            }

            if (NvmlSystemGetDriverVersion(buffer, Capacity) != Success)
            {
                return false;
            }

            var bytes = new byte[Capacity];
            Marshal.Copy(buffer, bytes, 0, Capacity);
            int length = Array.IndexOf(bytes, (byte)0);
            if (length < 0)
            {
                length = Capacity;
            }

            version = Encoding.ASCII.GetString(bytes, 0, length).Trim();
            return version.Length > 0;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
        finally
        {
            if (buffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }

    /// <summary>Power limit bounds the hardware accepts, in milliwatts.</summary>
    public static bool TryGetPowerLimitConstraints(uint handle, out uint minMilliwatts, out uint maxMilliwatts)
    {
        minMilliwatts = 0;
        maxMilliwatts = 0;
        if (!TryInitialize() || handle == 0)
        {
            return false;
        }

        try
        {
            return NvmlDeviceGetPowerManagementLimitConstraints(Ptr(handle), out minMilliwatts, out maxMilliwatts) == Success;
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

    /// <summary>Power limit the driver currently enforces, in milliwatts.</summary>
    public static bool TryGetPowerLimit(uint handle, out uint milliwatts)
    {
        milliwatts = 0;
        if (!TryInitialize() || handle == 0)
        {
            return false;
        }

        try
        {
            return NvmlDeviceGetPowerManagementLimit(Ptr(handle), out milliwatts) == Success;
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

    /// <summary>Factory power limit, in milliwatts.</summary>
    public static bool TryGetDefaultPowerLimit(uint handle, out uint milliwatts)
    {
        milliwatts = 0;
        if (!TryInitialize() || handle == 0)
        {
            return false;
        }

        try
        {
            return NvmlDeviceGetPowerManagementDefaultLimit(Ptr(handle), out milliwatts) == Success;
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

    /// <summary>Ceiling the hardware/DVFS actually enforces, in milliwatts.</summary>
    public static bool TryGetEnforcedPowerLimit(uint handle, out uint milliwatts)
    {
        milliwatts = 0;
        if (!TryInitialize() || handle == 0)
        {
            return false;
        }

        try
        {
            return NvmlDeviceGetEnforcedPowerLimit(Ptr(handle), out milliwatts) == Success;
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
    /// Reads a clock offset plus the range the driver permits for it. The getter
    /// takes the v2 struct because only that one carries the bounds.
    /// </summary>
    public static bool TryGetClockOffset(uint handle, NvidiaClockDomain domain, out int offsetMegahertz, out int minMegahertz, out int maxMegahertz)
    {
        offsetMegahertz = 0;
        minMegahertz = 0;
        maxMegahertz = 0;
        if (!TryInitialize() || handle == 0)
        {
            return false;
        }

        try
        {
            var request = new NvmlClockOffset
            {
                Version = ClockOffsetVersionV2,
                Type = (uint)domain,
                Pstate = BoostPstate,
            };

            if (NvmlDeviceGetClockOffsets(Ptr(handle), ref request) != Success)
            {
                return false;
            }

            offsetMegahertz = request.ClockOffsetMHz;
            minMegahertz = request.MinClockOffsetMHz;
            maxMegahertz = request.MaxClockOffsetMHz;
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

    /// <summary>Writes the power limit and returns the raw NVML status.</summary>
    public static int SetPowerLimit(uint handle, uint milliwatts)
    {
        if (!TryInitialize() || handle == 0)
        {
            return ErrorNotSupported;
        }

        try
        {
            return NvmlDeviceSetPowerManagementLimit(Ptr(handle), milliwatts);
        }
        catch (DllNotFoundException)
        {
            return ErrorFunctionNotFound;
        }
        catch (EntryPointNotFoundException)
        {
            return ErrorFunctionNotFound;
        }
    }

    /// <summary>
    /// Writes a clock offset and returns the raw NVML status. The setter takes
    /// the v1 struct: the driver rejects a version word that does not match the
    /// struct it is handed, answering INVALID_ARGUMENT when it does not.
    /// </summary>
    public static int SetClockOffset(uint handle, NvidiaClockDomain domain, int megahertz)
    {
        if (!TryInitialize() || handle == 0)
        {
            return ErrorNotSupported;
        }

        try
        {
            var request = new NvmlClockOffset
            {
                Version = ClockOffsetVersionV1,
                Type = (uint)domain,
                Pstate = BoostPstate,
                ClockOffsetMHz = megahertz,
            };

            return NvmlDeviceSetClockOffsets(Ptr(handle), ref request);
        }
        catch (DllNotFoundException)
        {
            return ErrorFunctionNotFound;
        }
        catch (EntryPointNotFoundException)
        {
            return ErrorFunctionNotFound;
        }
    }

    /// <summary>
    /// Maps a raw NVML status onto CA-O's outcome vocabulary using only the codes
    /// NVIDIA documents. Anything unrecognised stays
    /// <see cref="NvidiaTuningWriteOutcome.Rejected"/> so the raw number reaches
    /// the user instead of a wrong explanation.
    /// </summary>
    public static NvidiaTuningWriteOutcome ClassifyWrite(int status) => status switch
    {
        Success => NvidiaTuningWriteOutcome.Applied,
        ErrorNotSupported or ErrorFunctionNotFound => NvidiaTuningWriteOutcome.DriverBlocked,
        ErrorNoPermission => NvidiaTuningWriteOutcome.NoPermission,
        _ => NvidiaTuningWriteOutcome.Rejected,
    };

    private static IntPtr Ptr(uint handle) => new((long)handle);

    /// <summary>
    /// <c>nvmlClockOffset_t</c>. The setter fills the first four fields; the
    /// getter additionally reads the bounds from the last two. Sequential and
    /// all-primitive, so it marshals as plain memory.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct NvmlClockOffset
    {
        public uint Version;
        public uint Type;
        public uint Pstate;
        public int ClockOffsetMHz;
        public int MinClockOffsetMHz;
        public int MaxClockOffsetMHz;
    }

    [DllImport(DllName, EntryPoint = "nvmlInit_v2", CallingConvention = CallingConvention.StdCall)]
    private static extern int NvmlInit();

    [DllImport(DllName, EntryPoint = "nvmlDeviceGetCount_v2", CallingConvention = CallingConvention.StdCall)]
    private static extern int NvmlDeviceGetCount(out uint count);

    [DllImport(DllName, EntryPoint = "nvmlDeviceGetHandleByIndex_v2", CallingConvention = CallingConvention.StdCall)]
    private static extern int NvmlDeviceGetHandleByIndex(uint index, out IntPtr device);

    [DllImport(DllName, EntryPoint = "nvmlSystemGetDriverVersion", CallingConvention = CallingConvention.StdCall)]
    private static extern int NvmlSystemGetDriverVersion(IntPtr version, uint length);

    [DllImport(DllName, EntryPoint = "nvmlDeviceGetPowerManagementLimit", CallingConvention = CallingConvention.StdCall)]
    private static extern int NvmlDeviceGetPowerManagementLimit(IntPtr device, out uint milliwatts);

    [DllImport(DllName, EntryPoint = "nvmlDeviceSetPowerManagementLimit", CallingConvention = CallingConvention.StdCall)]
    private static extern int NvmlDeviceSetPowerManagementLimit(IntPtr device, uint milliwatts);

    [DllImport(DllName, EntryPoint = "nvmlDeviceGetPowerManagementLimitConstraints", CallingConvention = CallingConvention.StdCall)]
    private static extern int NvmlDeviceGetPowerManagementLimitConstraints(IntPtr device, out uint minMilliwatts, out uint maxMilliwatts);

    [DllImport(DllName, EntryPoint = "nvmlDeviceGetPowerManagementDefaultLimit", CallingConvention = CallingConvention.StdCall)]
    private static extern int NvmlDeviceGetPowerManagementDefaultLimit(IntPtr device, out uint milliwatts);

    [DllImport(DllName, EntryPoint = "nvmlDeviceGetEnforcedPowerLimit", CallingConvention = CallingConvention.StdCall)]
    private static extern int NvmlDeviceGetEnforcedPowerLimit(IntPtr device, out uint milliwatts);

    [DllImport(DllName, EntryPoint = "nvmlDeviceGetClockOffsets", CallingConvention = CallingConvention.StdCall)]
    private static extern int NvmlDeviceGetClockOffsets(IntPtr device, ref NvmlClockOffset clockOffset);

    [DllImport(DllName, EntryPoint = "nvmlDeviceSetClockOffsets", CallingConvention = CallingConvention.StdCall)]
    private static extern int NvmlDeviceSetClockOffsets(IntPtr device, ref NvmlClockOffset clockOffset);
}