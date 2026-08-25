using CAO.Core.Security;
using CAO.Shared;
using CAO.Shared.IPC;
using CAO.Shared.Security;
using Xunit;

namespace CAO.Security.Tests;

/// <summary>
/// Adversarial IPC validation + caller authorization (FASE 2/3): every
/// malformed, replayed, expired or unauthorized request must be rejected
/// before dispatch.
/// </summary>
public sealed class PrivilegedIpcSecurityTests
{
    private static IpcRequest ValidRequest(Func<IpcRequest, IpcRequest>? mutate = null)
    {
        var request = new IpcRequest(
            ProtocolVersion: IpcProtocol.Version,
            RequestId: Guid.NewGuid(),
            Nonce: Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)),
            CreatedAtUtc: DateTime.UtcNow,
            Operation: PrivilegedOperationKind.DetectOptimization,
            Payload: new OptimizationTargetPayload("disable-vbs"));
        return mutate?.Invoke(request) ?? request;
    }

    private static readonly AdministratorsOnlyAuthorizer Authorizer = new();

    [Fact]
    public void AcceptsWellFormedTypedRequest()
    {
        Assert.True(IpcRequestValidator.TryValidate(ValidRequest(), out _, out _));
    }

    [Theory]
    [InlineData("../../../../Windows/System32/config")]
    [InlineData("disable-vbs; rm -rf /")]
    [InlineData("disable-vbs\x00hidden")]
    [InlineData("DISABLE-VBS-UPPERCASE")]
    public void RejectsMaliciousOptimizationIds(string optimizationId)
    {
        var request = ValidRequest(r => r with
        {
            Payload = new OptimizationTargetPayload(optimizationId),
        });

        Assert.False(IpcRequestValidator.TryValidate(request, out _, out _));
    }

    [Fact]
    public void RejectsWrongProtocolVersion()
    {
        Assert.False(IpcRequestValidator.TryValidate(
            ValidRequest(r => r with { ProtocolVersion = IpcProtocol.Version + 1 }), out var code, out var detail));
        Assert.Equal(ErrorCodes.IpcProtocolVersionMismatch, code);
    }

    [Fact]
    public void RejectsExpiredRequest()
    {
        Assert.False(IpcRequestValidator.TryValidate(
            ValidRequest(r => r with { CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5) }),
            out var code, out var detail));
        Assert.Equal(ErrorCodes.IpcRequestExpired, code);
    }

    [Fact]
    public void RejectsPayloadSchemaMismatch()
    {
        // Detect declared but a payload type from another schema family.
        Assert.False(IpcRequestValidator.TryValidate(
            ValidRequest(r => r with { Payload = null! }), out var code, out var detail));
        Assert.Equal(ErrorCodes.IpcPayloadSchemaInvalid, code);
    }

    // ---------------- Authorization policy ----------------

    [Fact]
    public void ElevatedAdministratorIsAllowed()
    {
        var caller = new CallerIdentity("S-1-5-21-1-1001", "DESKTOP\\admin", true, true, 1);

        var result = Authorizer.Authorize(caller);

        Assert.True(result.Allowed);
        Assert.Equal(AuthorizationReasons.AdminAllowed, result.ReasonCode);
    }

    [Fact]
    public void StandardUserIsDenied()
    {
        var caller = new CallerIdentity("S-1-5-21-1-1002", "DESKTOP\\user", false, false, 2);

        var result = Authorizer.Authorize(caller);

        Assert.False(result.Allowed);
        Assert.Equal(AuthorizationReasons.SidNotConfigured, result.ReasonCode);
    }

    [Fact]
    public void AdminGroupWithoutElevationIsDenied()
    {
        // Filtered token: member of admin group but not elevated.
        var caller = new CallerIdentity("S-1-5-21-1-1003", "DESKTOP\\admin", true, false, 3);

        var result = Authorizer.Authorize(caller);

        Assert.False(result.Allowed);
    }

    [Fact]
    public void ConfiguredServiceSidIsAllowedEvenWhenNotAdmin()
    {
        var authorizer = new AdministratorsOnlyAuthorizer(["S-1-5-21-1-9999"]);
        var caller = new CallerIdentity("S-1-5-21-1-9999", "NT SERVICE\\cao-worker", false, false, 0);

        var result = authorizer.Authorize(caller);

        Assert.True(result.Allowed);
        Assert.Equal(AuthorizationReasons.ConfiguredSidAllowed, result.ReasonCode);
    }

    [Fact]
    public void UnknownOrTamperedSidIsDenied()
    {
        foreach (var sid in new[] { "", "domain\\user" })
        {
            var caller = new CallerIdentity(sid, "x", true, true, 0);
            Assert.False(Authorizer.Authorize(caller).Allowed);
        }
    }
}
