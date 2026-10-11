using System.Runtime.InteropServices;

namespace CAO.Infrastructure.Overclock;

/// <summary>Outcome of one AMD power-limit write, classified from the raw ADLX status.</summary>
/// <remarks>
/// The codes come from the official ADLX <c>ADLX_RESULT</c> enum
/// (ADLXDefines.h, verified against SDK v2.0.0.125): 0 OK, 1 ALREADY_ENABLED,
/// 2 ALREADY_INITIALIZED, 12 NOT_SUPPORTED, 15 GPU_IN_USE, 18 RESET_NEEDED.
/// </remarks>
public enum AmdTuningWriteOutcome
{
    Applied,
    DriverBlocked,
    ResetNeeded,
    GpuInUse,
    Rejected,
}

/// <summary>
/// What ADLX reports about the first AMD GPU. Every power value is percent of
/// TDP, which is exactly the catalog's unit, so there is no anchor to resolve
/// and no conversion to get wrong (unlike the NVIDIA milliwatt path).
/// </summary>
/// <param name="DriverVersion">
/// Always null: the verified read chain (System -&gt; GPUTuningServices -&gt;
/// ManualPowerTuning) exposes no driver-version getter, and CA-O does not
/// invent one. Callers that need a version treat null as "unknown".
/// </param>
public sealed record AmdTuningDevice(
    string? DriverVersion,
    int? MinPowerLimitPercent,
    int? MaxPowerLimitPercent,
    int? DefaultPowerLimitPercent);

/// <summary>Injectable AMD write surface, so the backend is testable without AMD silicon.</summary>
public interface IAmdTuningApi
{
    bool IsAvailable { get; }

    string? UnavailableReasonKey { get; }

    bool TryDescribe(out AmdTuningDevice? device);

    int? ReadPowerLimitPercent();

    /// <summary>Sets the power limit in percent. Returns the raw ADLX status; classification lives in <see cref="AmdAdlxInterop.ClassifyWrite"/>.</summary>
    int SetPowerLimitPercent(int percent);
}

/// <summary>Production AMD surface: resolves the ADLX vtable chain on every call.</summary>
public sealed class AdlxTuningApi : IAmdTuningApi
{
    public static AdlxTuningApi Instance { get; } = new();

    private AdlxTuningApi()
    {
    }

    private readonly object _gate = new();
    private AmdTuningDevice? _device;

    public bool IsAvailable => AmdAdlxInterop.TryInitialize();

    public string? UnavailableReasonKey => AmdAdlxInterop.FailureKey;

    public bool TryDescribe(out AmdTuningDevice? device)
    {
        lock (_gate)
        {
            if (_device is not null)
            {
                device = _device;
                return true;
            }

            if (!AmdAdlxInterop.TryGetPowerRange(out int min, out int max, out int? def))
            {
                device = null;
                return false;
            }

            _device = new AmdTuningDevice(null, min, max, def);
            device = _device;
            return true;
        }
    }

    public int? ReadPowerLimitPercent() =>
        AmdAdlxInterop.TryGetPowerLimit(out int percent) ? percent : null;

    public int SetPowerLimitPercent(int percent) =>
        AmdAdlxInterop.SetPowerLimit(percent);
}

/// <summary>
/// P/Invoke into AMD's ADLX (<c>amdadlx64.dll</c>, shipped by the AMD driver,
/// never redistributed).
///
/// Why this shape: ADLX exposes one flat entry point (<c>ADLXInitialize</c>,
/// __cdecl) and everything else is COM-style vtables called __stdcall. Every
/// vtable index below was read verbatim from the official GPUOpen headers
/// (SDK v2.0.0.125): IADLXSystem has NO Acquire/Release (it is a singleton);
/// every other interface starts Acquire/Release/QueryInterface; the power
/// tuning vtable is QueryInterface, GetPowerLimitRange, GetPowerLimit,
/// SetPowerLimit, ... with <c>IADLXManualPowerTuning1</c> adding
/// GetPowerLimitDefault at slot 10. Interface ids are wide strings
/// (<c>const wchar_t*</c>), not binary GUIDs.
///
/// This file was verified against headers only: the build machine has no AMD
/// GPU (RTX 4060 Ti), so the write path below has never executed on silicon.
/// <see cref="AmdAdlxBackend"/> documents that where the user can see it.
/// </summary>
internal static class AmdAdlxInterop
{
    public const string DllName = "amdadlx64.dll";
    public const string InitFunctionName = "ADLXInitialize";

    /// <summary>ADLX_MAKE_FULL_VER(2, 0, 0, 125): ((u64)major&lt;&lt;48)|(minor&lt;&lt;32)|(release&lt;&lt;16)|build.</summary>
    public const ulong SdkVersion = ((ulong)2 << 48) | 125u;

    public const int AdlxOk = 0;
    public const int AdlxAlreadyEnabled = 1;
    public const int AdlxAlreadyInitialized = 2;
    public const int AdlxFail = 3;
    public const int AdlxNotSupported = 12;
    public const int AdlxGpuInUse = 15;
    public const int AdlxResetNeeded = 18;

    // IADLXSystemVtbl slots (ISystem.h): no Acquire/Release, singleton.
    private const int SystemGetGpus = 1;
    private const int SystemGetGpuTuningServices = 8;

    // IADLXGPUListVtbl slots (ISystem.h): Acquire/Release/QueryInterface first.
    private const int GpuListAtGpu = 11;

    // IADLXGPUTuningServicesVtbl slots (IGPUTuning.h).
    private const int TuningIsSupportedManualPower = 11;
    private const int TuningGetManualPower = 17;

    // IADLXManualPowerTuningVtbl slots (IGPUManualPowerTuning.h).
    private const int PowerGetRange = 3;
    private const int PowerGet = 4;
    private const int PowerSet = 5;

    // IADLXManualPowerTuning1Vtbl slot (IGPUManualPowerTuning.h).
    private const int Power1GetDefault = 10;

    // Every non-singleton interface starts Acquire/Release/QueryInterface.
    private const int IfaceRelease = 1;
    private const int IfaceQuery = 2;

    public const string IidManualPowerTuning = "IADLXManualPowerTuning";
    public const string IidManualPowerTuning1 = "IADLXManualPowerTuning1";

    private static readonly object Gate = new();
    private static bool _attempted;
    private static bool _initialized;
    private static IntPtr _system;
    private static string? _failureKey;

    public static string? FailureKey
    {
        get
        {
            lock (Gate)
            {
                return _failureKey;
            }
        }
    }

    /// <summary>Loads ADLX and initializes it. Never throws: a missing AMD stack is a supported configuration.</summary>
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
                IntPtr library = LoadLibrary(DllName);
                if (library == IntPtr.Zero)
                {
                    _failureKey = "overclock-adlx-missing";
                    return false;
                }

                IntPtr entry = GetProcAddressAnsi(library, InitFunctionName);
                if (entry == IntPtr.Zero)
                {
                    _failureKey = "overclock-adlx-entrypoint-missing";
                    return false;
                }

                var initialize = Marshal.GetDelegateForFunctionPointer<AdlxInitializeDelegate>(entry);
                int status = initialize(SdkVersion, out IntPtr system);
                if (!AdlxSucceeded(status) || system == IntPtr.Zero)
                {
                    _failureKey = "overclock-adlx-init-failed";
                    return false;
                }

                _system = system;
                _initialized = true;
                _failureKey = null;
                return true;
            }
            catch (DllNotFoundException)
            {
                _failureKey = "overclock-adlx-missing";
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                _failureKey = "overclock-adlx-entrypoint-missing";
                return false;
            }
            catch (BadImageFormatException)
            {
                _failureKey = "overclock-adlx-arch-mismatch";
                return false;
            }
        }
    }

    public static bool TryGetPowerRange(out int min, out int max, out int? def)
    {
        min = 0;
        max = 0;
        def = null;

        if (!TryAcquireChain(out IntPtr tuning, out IntPtr list, out IntPtr gpu, out IntPtr raw, out IntPtr manual))
        {
            return false;
        }

        try
        {
            if (!CallRange(manual, PowerGetRange, out AdlxIntRange range))
            {
                return false;
            }

            min = range.MinValue;
            max = range.MaxValue;

            if (TryQuery(manual, IidManualPowerTuning1, out IntPtr manual1) && manual1 != IntPtr.Zero)
            {
                try
                {
                    if (CallGetInt(manual1, Power1GetDefault, out int d))
                    {
                        def = d;
                    }
                }
                finally
                {
                    Release(manual1);
                }
            }

            return true;
        }
        finally
        {
            ReleaseChain(tuning, list, gpu, raw, manual);
        }
    }

    public static bool TryGetPowerLimit(out int percent)
    {
        percent = 0;

        if (!TryAcquireChain(out IntPtr tuning, out IntPtr list, out IntPtr gpu, out IntPtr raw, out IntPtr manual))
        {
            return false;
        }

        try
        {
            return CallGetInt(manual, PowerGet, out percent);
        }
        finally
        {
            ReleaseChain(tuning, list, gpu, raw, manual);
        }
    }

    /// <summary>Sets the power limit in percent. Returns the raw ADLX status; <see cref="AdlxFail"/> when no chain could be acquired.</summary>
    public static int SetPowerLimit(int percent)
    {
        if (!TryAcquireChain(out IntPtr tuning, out IntPtr list, out IntPtr gpu, out IntPtr raw, out IntPtr manual))
        {
            return AdlxFail;
        }

        try
        {
            IntPtr fn = VFunc(manual, PowerSet);
            if (fn == IntPtr.Zero)
            {
                return AdlxFail;
            }

            var set = Marshal.GetDelegateForFunctionPointer<AdlxSetIntDelegate>(fn);
            return set(manual, percent);
        }
        finally
        {
            ReleaseChain(tuning, list, gpu, raw, manual);
        }
    }

    /// <summary>Honest classification from verified codes only. Anything else is a raw number, never a guess.</summary>
    public static AmdTuningWriteOutcome ClassifyWrite(int status) => status switch
    {
        AdlxOk or AdlxAlreadyEnabled or AdlxAlreadyInitialized => AmdTuningWriteOutcome.Applied,
        AdlxNotSupported => AmdTuningWriteOutcome.DriverBlocked,
        AdlxResetNeeded => AmdTuningWriteOutcome.ResetNeeded,
        AdlxGpuInUse => AmdTuningWriteOutcome.GpuInUse,
        _ => AmdTuningWriteOutcome.Rejected,
    };

    private static bool AdlxSucceeded(int status) =>
        status == AdlxOk || status == AdlxAlreadyEnabled || status == AdlxAlreadyInitialized;

    /// <summary>
    /// Walks System -&gt; GPUTuningServices -&gt; GPU list -&gt; first GPU -&gt;
    /// ManualPowerTuning. All-or-nothing: on any failure everything acquired so
    /// far is released and every out is zeroed.
    /// </summary>
    private static bool TryAcquireChain(out IntPtr tuning, out IntPtr list, out IntPtr gpu, out IntPtr raw, out IntPtr manual)
    {
        tuning = IntPtr.Zero;
        list = IntPtr.Zero;
        gpu = IntPtr.Zero;
        raw = IntPtr.Zero;
        manual = IntPtr.Zero;

        if (!TryInitialize() || _system == IntPtr.Zero)
        {
            return false;
        }

        if (!CallGetService(_system, SystemGetGpuTuningServices, out tuning) || tuning == IntPtr.Zero)
        {
            ReleaseChain(tuning, list, gpu, raw, manual);
            return false;
        }

        if (!CallGetService(_system, SystemGetGpus, out list) || list == IntPtr.Zero)
        {
            ReleaseChain(tuning, list, gpu, raw, manual);
            return false;
        }

        if (!CallListAt(list, out gpu) || gpu == IntPtr.Zero)
        {
            ReleaseChain(tuning, list, gpu, raw, manual);
            return false;
        }

        if (!CallIsSupported(tuning, gpu) || !CallGetManualPower(tuning, gpu, out raw) || raw == IntPtr.Zero)
        {
            ReleaseChain(tuning, list, gpu, raw, manual);
            return false;
        }

        if (!TryQuery(raw, IidManualPowerTuning, out manual) || manual == IntPtr.Zero)
        {
            ReleaseChain(tuning, list, gpu, raw, manual);
            return false;
        }

        return true;
    }

    private static void ReleaseChain(IntPtr tuning, IntPtr list, IntPtr gpu, IntPtr raw, IntPtr manual)
    {
        Release(manual);
        Release(raw);
        Release(gpu);
        Release(list);
        Release(tuning);
    }

    private static void Release(IntPtr iface)
    {
        if (iface == IntPtr.Zero)
        {
            return;
        }

        IntPtr fn = VFunc(iface, IfaceRelease);
        if (fn == IntPtr.Zero)
        {
            return;
        }

        Marshal.GetDelegateForFunctionPointer<AdlxReleaseDelegate>(fn)(iface);
    }

    private static bool TryQuery(IntPtr iface, string iid, out IntPtr result)
    {
        result = IntPtr.Zero;

        IntPtr fn = VFunc(iface, IfaceQuery);
        if (fn == IntPtr.Zero)
        {
            return false;
        }

        var query = Marshal.GetDelegateForFunctionPointer<AdlxQueryDelegate>(fn);
        return AdlxSucceeded(query(iface, iid, out result)) && result != IntPtr.Zero;
    }

    private static bool CallGetService(IntPtr iface, int index, out IntPtr service)
    {
        service = IntPtr.Zero;

        IntPtr fn = VFunc(iface, index);
        if (fn == IntPtr.Zero)
        {
            return false;
        }

        var call = Marshal.GetDelegateForFunctionPointer<AdlxGetServiceDelegate>(fn);
        return AdlxSucceeded(call(iface, out service)) && service != IntPtr.Zero;
    }

    private static bool CallListAt(IntPtr list, out IntPtr gpu)
    {
        gpu = IntPtr.Zero;

        IntPtr fn = VFunc(list, GpuListAtGpu);
        if (fn == IntPtr.Zero)
        {
            return false;
        }

        var call = Marshal.GetDelegateForFunctionPointer<AdlxListAtDelegate>(fn);
        return AdlxSucceeded(call(list, 0u, out gpu)) && gpu != IntPtr.Zero;
    }

    private static bool CallIsSupported(IntPtr tuning, IntPtr gpu)
    {
        IntPtr fn = VFunc(tuning, TuningIsSupportedManualPower);
        if (fn == IntPtr.Zero)
        {
            return false;
        }

        var call = Marshal.GetDelegateForFunctionPointer<AdlxIsSupportedDelegate>(fn);
        return AdlxSucceeded(call(tuning, gpu, out byte supported)) && supported != 0;
    }

    private static bool CallGetManualPower(IntPtr tuning, IntPtr gpu, out IntPtr raw)
    {
        raw = IntPtr.Zero;

        IntPtr fn = VFunc(tuning, TuningGetManualPower);
        if (fn == IntPtr.Zero)
        {
            return false;
        }

        var call = Marshal.GetDelegateForFunctionPointer<AdlxGetManualPowerDelegate>(fn);
        return AdlxSucceeded(call(tuning, gpu, out raw)) && raw != IntPtr.Zero;
    }

    private static bool CallRange(IntPtr manual, int index, out AdlxIntRange range)
    {
        range = default;

        IntPtr fn = VFunc(manual, index);
        if (fn == IntPtr.Zero)
        {
            return false;
        }

        var call = Marshal.GetDelegateForFunctionPointer<AdlxRangeDelegate>(fn);
        return AdlxSucceeded(call(manual, out range));
    }

    private static bool CallGetInt(IntPtr manual, int index, out int value)
    {
        value = 0;

        IntPtr fn = VFunc(manual, index);
        if (fn == IntPtr.Zero)
        {
            return false;
        }

        var call = Marshal.GetDelegateForFunctionPointer<AdlxGetIntDelegate>(fn);
        return AdlxSucceeded(call(manual, out value));
    }

    private static IntPtr VFunc(IntPtr iface, int index) =>
        Marshal.ReadIntPtr(Marshal.ReadIntPtr(iface), index * IntPtr.Size);

    [StructLayout(LayoutKind.Sequential)]
    internal struct AdlxIntRange
    {
        public int MinValue;
        public int MaxValue;
        public int Step;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int AdlxInitializeDelegate(ulong version, out IntPtr system);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int AdlxGetServiceDelegate(IntPtr self, out IntPtr service);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int AdlxListAtDelegate(IntPtr self, uint location, out IntPtr gpu);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int AdlxIsSupportedDelegate(IntPtr self, IntPtr gpu, out byte supported);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int AdlxGetManualPowerDelegate(IntPtr self, IntPtr gpu, out IntPtr manual);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int AdlxQueryDelegate(IntPtr self, [MarshalAs(UnmanagedType.LPWStr)] string iid, out IntPtr result);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int AdlxRangeDelegate(IntPtr self, out AdlxIntRange range);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int AdlxGetIntDelegate(IntPtr self, out int value);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int AdlxSetIntDelegate(IntPtr self, int value);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int AdlxReleaseDelegate(IntPtr self);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadLibrary([MarshalAs(UnmanagedType.LPWStr)] string lpFileName);

    private static IntPtr GetProcAddressAnsi(IntPtr library, string procName)
    {
        IntPtr namePtr = Marshal.StringToHGlobalAnsi(procName);
        try
        {
            return GetProcAddress(library, namePtr);
        }
        finally
        {
            Marshal.FreeHGlobal(namePtr);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, IntPtr procName);
}
