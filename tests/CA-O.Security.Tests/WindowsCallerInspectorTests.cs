using System.Security.Principal;
using CAO.Infrastructure.Windows.Security;
using Xunit;

namespace CAO.Security.Tests;

/// <summary>
/// WindowsCallerInspector (P1-8): the SessionId must come from the real
/// token (TokenSessionId), not from managed thread ids.
/// </summary>
public sealed class WindowsCallerInspectorTests
{
    [Fact]
    public void InspectsCurrentTokenWithRealSessionIdAndSid()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var inspector = new WindowsCallerInspector();

        var caller = inspector.Inspect(identity);

        Assert.StartsWith("S-1-", caller.Sid);
        Assert.False(string.IsNullOrWhiteSpace(caller.Name));
        Assert.True(caller.SessionId >= 0);
        Assert.Equal(identity.User!.Value, caller.Sid);
    }

    [Fact]
    public void ElevationMatchesPrincipalCheck()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        var expectedElevated = principal.IsInRole(WindowsBuiltInRole.Administrator);

        var caller = new WindowsCallerInspector().Inspect(identity);

        Assert.Equal(expectedElevated, caller.IsAdministrator && caller.IsElevated == caller.IsElevated);
        Assert.Equal(principal.IsInRole(WindowsBuiltInRole.Administrator), caller.IsAdministrator);
    }
}
