using CAO.Core.Security;
using CAO.Infrastructure.SystemInterop;
using CAO.Shared.IPC;
using CAO.Shared.Security;
using Xunit;

namespace CAO.Infrastructure.Tests;

/// <summary>
/// Regresión del rechazo CAO-IPC-002 al limpiar fantasmas: los IDs reales
/// con {GUID} (HID Bluetooth, volúmenes, WPD) deben pasar el validador.
/// Parte sintética (determinista) + barrido vivo de la máquina.
/// </summary>
public sealed class PhantomBatchValidationTests
{
    [Theory]
    [InlineData(@"HID\{00001812-0000-1000-8000-00805F9B34FB}_DEV_VID&0002044D_PID&000065AB\8&2D0E7D8C&0&0000")]
    [InlineData(@"STORAGE\VOLUME\{8f3b2c1a-0000-0000-0000-100000000000}\0000000000100000")]
    [InlineData(@"SWD\WPDBUSENUM\{8f3b2c1a-0000-0000-0000-100000000000}#0000000000100000")]
    [InlineData(@"USB\VID_05AC&PID_12A8&MI_00\6&2A7B76EC&0&0000")]
    [InlineData(@"HID\VID_3151&PID_4026&MI_01&COL02\7&2A412A00&D0&0001")]
    public void RealWorldIdsPassValidation(string id)
    {
        Assert.True(CommandPolicy.IsValidPnpInstanceId(id));
    }

    [Fact]
    public void LivePhantoms_AllPassRequestValidation()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var phantoms = SetupApiDeviceEnumerator.EnumerateAll(cts.Token)
            .Where(d => !d.IsPresent)
            .Select(d => d.InstanceId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(200)
            .ToList();

        Assert.All(phantoms, id => Assert.True(CommandPolicy.IsValidPnpInstanceId(id), id));

        if (phantoms.Count == 0) return; // sin fantasmas no hay lote que validar
        var request = new IpcRequest(
            ProtocolVersion: IpcProtocol.Version,
            RequestId: Guid.NewGuid(),
            Nonce: Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)),
            CreatedAtUtc: DateTime.UtcNow,
            Operation: PrivilegedOperationKind.RemovePhantomDevices,
            Payload: new RemovePhantomDevicesPayload(phantoms));
        Assert.True(IpcRequestValidator.TryValidate(request, out _, out var error), error);
    }
}
