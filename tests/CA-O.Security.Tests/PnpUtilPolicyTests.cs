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
}
