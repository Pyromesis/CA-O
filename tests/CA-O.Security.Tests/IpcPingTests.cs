using CAO.Core.Security;
using CAO.Shared;
using CAO.Shared.IPC;
using Xunit;

namespace CAO.Security.Tests;

public sealed class IpcPingTests
{
    [Fact]
    public void Ping_Payload_Validates()
    {
        var req = new IpcRequest(IpcProtocol.Version, Guid.NewGuid(), "nonce123", DateTime.UtcNow, PrivilegedOperationKind.Ping, new PingPayload());
        Assert.True(IpcRequestValidator.TryValidate(req, out var code, out _));
    }

    [Fact]
    public void Ping_WithOptimizationPayload_Fails()
    {
        var req = new IpcRequest(IpcProtocol.Version, Guid.NewGuid(), "nonce123", DateTime.UtcNow, PrivilegedOperationKind.Ping, new ApplyOptimizationPayload("disable-transparency"));
        Assert.False(IpcRequestValidator.TryValidate(req, out _, out _));
    }

    [Fact]
    public void Apply_WithPingPayload_Fails()
    {
        var req = new IpcRequest(IpcProtocol.Version, Guid.NewGuid(), "nonce123", DateTime.UtcNow, PrivilegedOperationKind.ApplyOptimization, new PingPayload());
        Assert.False(IpcRequestValidator.TryValidate(req, out _, out _));
    }
}
