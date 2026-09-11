using CAO.Core.Security;
using CAO.Shared;
using CAO.Shared.IPC;
using CAO.Shared.Security;
using Xunit;

namespace CAO.Security.Tests;

/// <summary>
/// Fase drivers 2: pnputil solo con formas fijas. El Instance ID real lleva
/// '&amp;' y '\' legítimos (vetados por SafeArg), así que estas claves usan su
/// validador estricto propio; todo lo demás (espacios, comillas, shell) es null.
/// </summary>
public sealed class PnpUtilPolicyTests
{
    private const string RealId = @"HDAUDIO\FUNC_01&VEN_10EC&DEV_0283&SUBSYS_10EC0000&REV_1000\4&1234ABCD&0&0001";

    private static string System32(string tool) =>
        Path.Combine(Environment.ExpandEnvironmentVariables(@"%SystemRoot%\System32"), tool);

    [Theory]
    [InlineData(SystemCommandKey.PnPUtilScanDevices, new[] { "/scan-devices" })]
    [InlineData(SystemCommandKey.PnPUtilEnumProblemDevices, new[] { "/enum-devices", "/problem" })]
    [InlineData(SystemCommandKey.PnPUtilEnableDevice, new[] { "/enable-device", RealId })]
    [InlineData(SystemCommandKey.PnPUtilRemoveDevice, new[] { "/remove-device", RealId })]
    [InlineData(SystemCommandKey.PnPUtilAddDriver, new[] { "/add-driver", @"C:\Users\Ana\Downloads\audio\oem.inf", "/install" })]
    [InlineData(SystemCommandKey.PnPUtilAddDriver, new[] { "/add-driver", @"C:\Drivers (2024)\hda&audio\drv.inf", "/install" })]
    public void PnpUtilKeysResolveToCanonicalExecutable(SystemCommandKey key, string[] arguments)
    {
        Assert.Equal(System32("pnputil.exe"), CommandPolicy.Resolve(key, arguments));
    }

    [Theory]
    [InlineData(SystemCommandKey.PnPUtilAddDriver, new[] { "/add-driver", @"C:\x\drv.exe", "/install" })]
    [InlineData(SystemCommandKey.PnPUtilAddDriver, new[] { "/add-driver", @"C:\x\..\drv.inf", "/install" })]
    [InlineData(SystemCommandKey.PnPUtilAddDriver, new[] { "/add-driver", @"C:\x\drv.inf:ads", "/install" })]
    [InlineData(SystemCommandKey.PnPUtilAddDriver, new[] { "/add-driver", "relativo\\drv.inf", "/install" })]
    [InlineData(SystemCommandKey.PnPUtilAddDriver, new[] { "/add-driver", @"C:\x\drv.inf" })]
    [InlineData(SystemCommandKey.PnPUtilAddDriver, new[] { "/add-driver", @"C:\x\drv.inf", "/install", "extra" })]
    public void PnpUtilAddDriverDeviationResolvesToNull(SystemCommandKey key, string[] arguments)
    {
        Assert.Null(CommandPolicy.Resolve(key, arguments));
    }

    [Theory]
    [InlineData(SystemCommandKey.PnPUtilEnableDevice, new[] { "/enable-device", "x;calc.exe" })]
    [InlineData(SystemCommandKey.PnPUtilEnableDevice, new[] { "/enable-device", "x|calc" })]
    [InlineData(SystemCommandKey.PnPUtilEnableDevice, new[] { "/enable-device", "x\"y" })]
    [InlineData(SystemCommandKey.PnPUtilEnableDevice, new[] { "/enable-device", "x y" })]
    [InlineData(SystemCommandKey.PnPUtilEnableDevice, new[] { "/enable-device", "..\\x" })]
    [InlineData(SystemCommandKey.PnPUtilEnableDevice, new[] { "/enable-device", "x%y" })]
    [InlineData(SystemCommandKey.PnPUtilRemoveDevice, new[] { "/remove-device", "x|whoami" })]
    [InlineData(SystemCommandKey.PnPUtilRemoveDevice, new[] { "/remove-device", "x\ny" })]
    [InlineData(SystemCommandKey.PnPUtilEnableDevice, new[] { "/disable-device", RealId })]
    [InlineData(SystemCommandKey.PnPUtilScanDevices, new[] { "/scan-devices", "extra" })]
    public void PnpUtilInjectionOrShapeDeviationResolvesToNull(SystemCommandKey key, string[] arguments)
    {
        Assert.Null(CommandPolicy.Resolve(key, arguments));
    }

    [Fact]
    public void InstanceIdValidatorAcceptsRealShape()
    {
        Assert.True(CommandPolicy.IsValidPnpInstanceId(RealId));
        Assert.True(CommandPolicy.IsValidPnpInstanceId(@"USB\VID_046D&PID_C52B\5&1A2B3C4D&0&1"));
    }

    [Theory]
    [InlineData(@"HID\{00001812-0000-1000-8000-00805F9B34FB}_DEV_VID&0002044D_PID&000065AB\8&2D0E7D8C&0&0000")]
    [InlineData(@"STORAGE\VOLUME\{8f3b2c1a-0000-0000-0000-100000000000}\0000000000100000")]
    [InlineData(@"SWD\WPDBUSENUM\{8f3b2c1a-0000-0000-0000-100000000000}#0000000000100000")]
    public void InstanceIdValidatorAcceptsGuidBraces(string id)
    {
        Assert.True(CommandPolicy.IsValidPnpInstanceId(id));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("tiene espacios")]
    [InlineData("con\"comilla")]
    public void InstanceIdValidatorRejectsUnsafe(string? id)
    {
        Assert.False(CommandPolicy.IsValidPnpInstanceId(id!));
        Assert.False(CommandPolicy.IsValidPnpInstanceId(new string('A', 300)));
    }

    [Theory]
    [InlineData("rescan")]
    [InlineData("enable")]
    [InlineData("reinstall")]
    public void FixDriverActionsAcceptKnown(string action)
    {
        Assert.True(FixDriverActions.IsValid(action));
    }

    [Theory]
    [InlineData("delete")]
    [InlineData("RESCAN")]
    [InlineData("")]
    [InlineData(null)]
    public void FixDriverActionsRejectUnknown(string? action)
    {
        Assert.False(FixDriverActions.IsValid(action));
    }

    private static IpcRequest FixRequest(string instanceId, string action, object? payloadOverride = null)
    {
        object payload = payloadOverride ?? new DriverFixPayload(instanceId, action);
        return new IpcRequest(
            ProtocolVersion: IpcProtocol.Version,
            RequestId: Guid.NewGuid(),
            Nonce: Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)),
            CreatedAtUtc: DateTime.UtcNow,
            Operation: PrivilegedOperationKind.FixDriver,
            Payload: (ITypedPayload)payload);
    }

    [Theory]
    [InlineData("rescan")]
    [InlineData("enable")]
    [InlineData("reinstall")]
    public void FixDriverAcceptsValidRequest(string action)
    {
        Assert.True(IpcRequestValidator.TryValidate(FixRequest(RealId, action), out _, out _));
    }

    [Fact]
    public void FixDriverRejectsBadAction()
    {
        // Mismo contrato que SetDns inválido: código genérico de request malformado.
        Assert.False(IpcRequestValidator.TryValidate(FixRequest(RealId, "format"), out var code, out _));
        Assert.Equal(ErrorCodes.IpcMalformedRequest, code);
    }

    [Fact]
    public void FixDriverRejectsBadInstanceId()
    {
        Assert.False(IpcRequestValidator.TryValidate(FixRequest("x;rm", "rescan"), out _, out _));
    }

    [Fact]
    public void FixDriverRejectsWrongPayloadType()
    {
        Assert.False(IpcRequestValidator.TryValidate(
            FixRequest(RealId, "rescan", new PingPayload()), out _, out _));
    }

    private static IpcRequest InstallRequest(string inf, string instanceId) => new(
        ProtocolVersion: IpcProtocol.Version,
        RequestId: Guid.NewGuid(),
        Nonce: Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)),
        CreatedAtUtc: DateTime.UtcNow,
        Operation: PrivilegedOperationKind.InstallDriver,
        Payload: new InstallDriverPayload(inf, instanceId));

    [Fact]
    public void InstallDriverAcceptsValidRequest()
    {
        Assert.True(IpcRequestValidator.TryValidate(
            InstallRequest(@"C:\Users\Ana\Downloads\drv.inf", RealId), out _, out _));
        Assert.True(IpcRequestValidator.TryValidate(
            InstallRequest(@"C:\Users\Ana\Downloads\drv.inf", string.Empty), out _, out _));
    }

    [Theory]
    [InlineData(@"C:\x\drv.exe", RealId)]
    [InlineData(@"C:\x\drv.inf", "x;rm")]
    [InlineData("relativo.inf", "")]
    public void InstallDriverRejectsInvalidRequest(string inf, string instanceId)
    {
        Assert.False(IpcRequestValidator.TryValidate(InstallRequest(inf, instanceId), out _, out _));
    }

    [Theory]
    [InlineData(SystemCommandKey.PnPUtilEnumDevice, new[] { "/enum-devices", "/instanceid", RealId })]
    public void PnpUtilEnumDeviceResolvesToCanonicalExecutable(SystemCommandKey key, string[] arguments)
    {
        Assert.Equal(System32("pnputil.exe"), CommandPolicy.Resolve(key, arguments));
    }

    [Theory]
    [InlineData(SystemCommandKey.PnPUtilEnumDevice, new[] { "/enum-devices", "/instanceid", "x|whoami" })]
    [InlineData(SystemCommandKey.PnPUtilEnumDevice, new[] { "/enum-devices", RealId })]
    [InlineData(SystemCommandKey.PnPUtilEnumDevice, new[] { "/enum-devices", "/instanceid", RealId, "extra" })]
    [InlineData(SystemCommandKey.PnPUtilEnumDevice, new[] { "/enum-devices", "/problem", RealId })]
    public void PnpUtilEnumDeviceDeviationResolvesToNull(SystemCommandKey key, string[] arguments)
    {
        Assert.Null(CommandPolicy.Resolve(key, arguments));
    }

    private static IpcRequest PhantomRequest(IReadOnlyList<string>? ids, object? payloadOverride = null)
    {
        object payload = payloadOverride ?? new RemovePhantomDevicesPayload(ids!);
        return new IpcRequest(
            ProtocolVersion: IpcProtocol.Version,
            RequestId: Guid.NewGuid(),
            Nonce: Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)),
            CreatedAtUtc: DateTime.UtcNow,
            Operation: PrivilegedOperationKind.RemovePhantomDevices,
            Payload: (ITypedPayload)payload);
    }

    [Fact]
    public void RemovePhantomsAcceptsValidRequest()
    {
        Assert.True(IpcRequestValidator.TryValidate(PhantomRequest(new[] { RealId }), out _, out _));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(201)]
    public void RemovePhantomsRejectsBadCount(int count)
    {
        var ids = Enumerable.Repeat(RealId, count).ToList();
        Assert.False(IpcRequestValidator.TryValidate(PhantomRequest(ids), out _, out _));
    }

    [Fact]
    public void RemovePhantomsRejectsBadIdAndWrongPayload()
    {
        Assert.False(IpcRequestValidator.TryValidate(PhantomRequest(new[] { "x;rm" }), out _, out _));
        Assert.False(IpcRequestValidator.TryValidate(PhantomRequest(new[] { RealId }, new PingPayload()), out _, out _));
    }

    private static IpcRequest SearchDriversRequest(object? payloadOverride = null) => new(
        ProtocolVersion: IpcProtocol.Version,
        RequestId: Guid.NewGuid(),
        Nonce: Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)),
        CreatedAtUtc: DateTime.UtcNow,
        Operation: PrivilegedOperationKind.SearchDriverUpdates,
        Payload: (ITypedPayload)(payloadOverride ?? new SearchDriverUpdatesPayload()));

    [Fact]
    public void SearchDriversAcceptsValidRequest()
    {
        Assert.True(IpcRequestValidator.TryValidate(SearchDriversRequest(), out _, out _));
        Assert.False(IpcRequestValidator.TryValidate(SearchDriversRequest(new PingPayload()), out _, out _));
    }

    private static IpcRequest InstallUpdatesRequest(IReadOnlyList<string>? ids, object? payloadOverride = null) => new(
        ProtocolVersion: IpcProtocol.Version,
        RequestId: Guid.NewGuid(),
        Nonce: Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)),
        CreatedAtUtc: DateTime.UtcNow,
        Operation: PrivilegedOperationKind.InstallDriverUpdates,
        Payload: (ITypedPayload)(payloadOverride ?? new InstallDriverUpdatesPayload(ids!)));

    [Fact]
    public void InstallUpdatesAcceptsGuidIds()
    {
        var ids = new[] { "11111111-2222-3333-4444-555555555555", "AAAAAAAA-BBBB-CCCC-DDDD-EEEEEEEEEEEE" };
        Assert.True(IpcRequestValidator.TryValidate(InstallUpdatesRequest(ids), out _, out _));
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("")]
    [InlineData("x;calc")]
    public void InstallUpdatesRejectsBadIds(string id)
    {
        Assert.False(IpcRequestValidator.TryValidate(InstallUpdatesRequest(new[] { id }), out _, out _));
    }

    [Fact]
    public void InstallUpdatesRejectsBadCountAndWrongPayload()
    {
        Assert.False(IpcRequestValidator.TryValidate(InstallUpdatesRequest(Array.Empty<string>()), out _, out _));
        Assert.False(IpcRequestValidator.TryValidate(
            InstallUpdatesRequest(Enumerable.Repeat("11111111-2222-3333-4444-555555555555", 51).ToList()), out _, out _));
        Assert.False(IpcRequestValidator.TryValidate(
            InstallUpdatesRequest(new[] { "11111111-2222-3333-4444-555555555555" }, new PingPayload()), out _, out _));
    }

    [Theory]
    [InlineData("11111111-2222-3333-4444-555555555555", true)]
    [InlineData("{11111111-2222-3333-4444-555555555555}", true)]
    [InlineData("not-a-guid", false)]
    [InlineData("", false)]
    [InlineData("x;rm -rf", false)]
    public void UpdateIdValidatorIsStrict(string id, bool expected)
    {
        Assert.Equal(expected, CommandPolicy.IsValidWindowsUpdateId(id));
    }

    private static string BackupDest(string leaf) =>
        Path.Combine(CommandPolicy.ExportDriverBackupRoot(), leaf);

    [Theory]
    [InlineData(SystemCommandKey.PnPUtilExportDriver)]
    public void PnpUtilExportDriverResolvesToCanonicalExecutable(SystemCommandKey key)
    {
        var args = new[] { "/export-driver", RealId, BackupDest("HDAUDIO_FUNC_01") };
        Assert.Equal(System32("pnputil.exe"), CommandPolicy.Resolve(key, args));
    }

    [Theory]
    [InlineData("/export-driver", "x|whoami", "HDAUDIO_FUNC_01")]
    [InlineData("/export-driver", RealId, @"C:\Windows\Temp\x")]
    [InlineData("/export-driver", RealId, "relativo\\x")]
    [InlineData("/export-driver", RealId, "con espacios")]
    [InlineData("/add-driver", RealId, "HDAUDIO_FUNC_01")]
    public void PnpUtilExportDriverDeviationResolvesToNull(string verb, string id, string leaf)
    {
        var dest = leaf.Contains('\\') || leaf.Contains(' ') || leaf.Contains(':')
            ? leaf
            : BackupDest(leaf);
        Assert.Null(CommandPolicy.Resolve(
            SystemCommandKey.PnPUtilExportDriver, new[] { verb, id, dest }));
    }

    [Fact]
    public void ExportDestValidatorIsStrict()
    {
        Assert.True(CommandPolicy.IsExportDriverDest(BackupDest("USB_VID_046D")));
        Assert.False(CommandPolicy.IsExportDriverDest(BackupDest("..\\evil")));
        Assert.False(CommandPolicy.IsExportDriverDest(BackupDest("")));
        Assert.False(CommandPolicy.IsExportDriverDest(@"C:\Windows\Temp\x"));
        Assert.False(CommandPolicy.IsExportDriverDest("relativo\\x"));
    }

    private static IpcRequest ExportRequest(string instanceId, object? payloadOverride = null) => new(
        ProtocolVersion: IpcProtocol.Version,
        RequestId: Guid.NewGuid(),
        Nonce: Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)),
        CreatedAtUtc: DateTime.UtcNow,
        Operation: PrivilegedOperationKind.ExportDriver,
        Payload: (ITypedPayload)(payloadOverride ?? new ExportDriverPayload(instanceId)));

    [Fact]
    public void ExportDriverAcceptsValidRequest()
    {
        Assert.True(IpcRequestValidator.TryValidate(ExportRequest(RealId), out _, out _));
        Assert.True(IpcRequestValidator.TryValidate(
            ExportRequest(@"HID\{00001812-0000-1000-8000-00805F9B34FB}_DEV_VID&0002044D_PID&000065AB\8&2D0E7D8C&0&0000"), out _, out _));
    }

    [Fact]
    public void ExportDriverRejectsBadIdAndWrongPayload()
    {
        Assert.False(IpcRequestValidator.TryValidate(ExportRequest("x;rm"), out _, out _));
        Assert.False(IpcRequestValidator.TryValidate(ExportRequest(RealId, new PingPayload()), out _, out _));
    }

    [Theory]
    [InlineData(@"PCI\VEN_8086&DEV_AE50&SUBSYS_60071E50&REV_31\3&11583659&0&20")]
    [InlineData(@"HID\VID_3151&PID_4026&MI_01&COL02\7&2D0E7D8C&0&0001")]
    public void CatalogHardwareIdAcceptsRealShapes(string id)
    {
        Assert.True(CommandPolicy.IsValidCatalogHardwareId(id));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("tiene espacios")]
    [InlineData("x;calc")]
    [InlineData("con\"comilla")]
    public void CatalogHardwareIdRejectsUnsafe(string? id)
    {
        Assert.False(CommandPolicy.IsValidCatalogHardwareId(id));
        Assert.False(CommandPolicy.IsValidCatalogHardwareId(new string('A', 300)));
    }

    [Theory]
    [InlineData("https://download.windowsupdate.com/d/msdownload/update/driver/drvs/2024/10/x.cab", true)]
    [InlineData("https://catalog.s.download.windowsupdate.com/d/x.cab", true)]
    [InlineData("https://dl.delivery.mp.microsoft.com/filestreaming/x.cab", true)]
    [InlineData("https://download.microsoft.com/download/x.cab", true)]
    [InlineData("http://download.windowsupdate.com/x.cab", false)]
    [InlineData("https://evil.com/x.cab", false)]
    [InlineData("https://download.windowsupdate.evil.com/x.cab", false)]
    [InlineData("not-a-url", false)]
    public void CatalogHostAllowlistIsStrict(string url, bool expected)
    {
        Assert.Equal(expected, CommandPolicy.IsAllowedCatalogHost(url));
    }

    [Fact]
    public void ExpandCabResolvesToCanonicalExecutable()
    {
        var root = CommandPolicy.CatalogDriverDownloadRoot();
        var cab = Path.Combine(root, "11111111-2222-3333-4444-555555555555", "pkg.cab");
        var dest = Path.Combine(root, "11111111-2222-3333-4444-555555555555", "extracted");
        Assert.Equal(
            Path.Combine(Environment.ExpandEnvironmentVariables(@"%SystemRoot%\System32"), "expand.exe"),
            CommandPolicy.Resolve(SystemCommandKey.ExpandCab, new[] { cab, "-F:*", dest }));
    }

    [Theory]
    [InlineData("/export-driver")]
    [InlineData("-F:x")]
    public void ExpandCabDeviationResolvesToNull(string verbOrFlag)
    {
        var root = CommandPolicy.CatalogDriverDownloadRoot();
        var args = verbOrFlag.StartsWith("/export")
            ? new[] { verbOrFlag, Path.Combine(root, "x", "pkg.cab"), "-F:*" }
            : new[] { Path.Combine(root, "x", "pkg.cab"), verbOrFlag, Path.Combine(root, "x", "extracted") };
        Assert.Null(CommandPolicy.Resolve(SystemCommandKey.ExpandCab, args));
    }

    [Fact]
    public void ExpandCabRejectsOutsideRoot()
    {
        Assert.Null(CommandPolicy.Resolve(SystemCommandKey.ExpandCab,
            new[] { @"C:\Temp\x.cab", "-F:*", @"C:\Temp\out" }));
    }

    private static IpcRequest CatalogSearchRequest(string hwid, string name = "", object? payloadOverride = null) => new(
        ProtocolVersion: IpcProtocol.Version,
        RequestId: Guid.NewGuid(),
        Nonce: Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)),
        CreatedAtUtc: DateTime.UtcNow,
        Operation: PrivilegedOperationKind.SearchCatalogDrivers,
        Payload: (ITypedPayload)(payloadOverride ?? new SearchCatalogDriversPayload(hwid, name)));

    [Fact]
    public void CatalogSearchAcceptsValidRequest()
    {
        Assert.True(IpcRequestValidator.TryValidate(
            CatalogSearchRequest(@"PCI\VEN_8086&DEV_AE50&SUBSYS_60071E50&REV_31\3&11583659&0&20", "Intel Smart Sound"), out _, out _));
    }

    [Theory]
    [InlineData("x;rm")]
    [InlineData("")]
    public void CatalogSearchRejectsBadHardwareId(string hwid)
    {
        Assert.False(IpcRequestValidator.TryValidate(CatalogSearchRequest(hwid), out _, out _));
    }

    [Fact]
    public void CatalogSearchRejectsWrongPayloadAndBadName()
    {
        Assert.False(IpcRequestValidator.TryValidate(
            CatalogSearchRequest(@"PCI\VEN_8086&DEV_AE50", "x", new PingPayload()), out _, out _));
        Assert.False(IpcRequestValidator.TryValidate(
            CatalogSearchRequest(@"PCI\VEN_8086&DEV_AE50", new string('A', 121)), out _, out _));
    }

    private static IpcRequest CatalogDownloadRequest(string updateId, string hwid = "", object? payloadOverride = null) => new(
        ProtocolVersion: IpcProtocol.Version,
        RequestId: Guid.NewGuid(),
        Nonce: Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)),
        CreatedAtUtc: DateTime.UtcNow,
        Operation: PrivilegedOperationKind.DownloadCatalogDriver,
        Payload: (ITypedPayload)(payloadOverride ?? new DownloadCatalogDriverPayload(updateId, hwid)));

    [Fact]
    public void CatalogDownloadAcceptsValidRequest()
    {
        Assert.True(IpcRequestValidator.TryValidate(
            CatalogDownloadRequest("30fe5516-a254-49c5-aaad-5643e00d0da6", @"PCI\VEN_8086&DEV_AE50"), out _, out _));
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("")]
    public void CatalogDownloadRejectsBadUpdateId(string updateId)
    {
        Assert.False(IpcRequestValidator.TryValidate(CatalogDownloadRequest(updateId), out _, out _));
    }

    [Fact]
    public void CatalogDownloadRejectsBadHardwareIdAndWrongPayload()
    {
        Assert.False(IpcRequestValidator.TryValidate(
            CatalogDownloadRequest("30fe5516-a254-49c5-aaad-5643e00d0da6", "x;rm"), out _, out _));
        Assert.False(IpcRequestValidator.TryValidate(
            CatalogDownloadRequest("30fe5516-a254-49c5-aaad-5643e00d0da6", "", new PingPayload()), out _, out _));
    }
}
