using CAO.Shared;
using Xunit;

namespace CAO.Core.Tests;

/// <summary>
/// Plan 2026-09-29 §1.7: un pipe inalcanzable tras una limpieza (servicio
/// detenido) es "unavailable", nunca "rejected"; y el AutoCheck reintenta
/// unavailable a los 60 s aunque ya hubiera un intento previo.
/// </summary>
public sealed class ServiceStatusMapperTests
{
    [Fact]
    public void Accepted_MapsToConnected()
    {
        Assert.Equal("connected", ServiceStatusMapper.FromPing(true, null));
    }

    [Theory]
    [InlineData(ErrorCodes.IpcPipeNotFound)]
    [InlineData(ErrorCodes.IpcTimeout)]
    public void PipeUnreachable_MapsToUnavailable_NeverRejected(string errorCode)
    {
        Assert.Equal("unavailable", ServiceStatusMapper.FromPing(false, errorCode));
    }

    [Theory]
    [InlineData("CAO-SEC-001")]
    [InlineData("validation-failed")]
    [InlineData(null)]
    public void OtherFailures_MapToRejected(string? errorCode)
    {
        Assert.Equal("rejected", ServiceStatusMapper.FromPing(false, errorCode));
    }

    [Fact]
    public void Unavailable_ThrottleIs60Seconds_RestIs5Minutes()
    {
        Assert.Equal(TimeSpan.FromMinutes(1), ServiceStatusMapper.AutoCheckThrottle("unavailable"));
        Assert.Equal(TimeSpan.FromMinutes(5), ServiceStatusMapper.AutoCheckThrottle("unknown"));
        Assert.Equal(TimeSpan.FromMinutes(5), ServiceStatusMapper.AutoCheckThrottle("connected"));
        Assert.Equal(TimeSpan.FromMinutes(5), ServiceStatusMapper.AutoCheckThrottle("rejected"));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("unknown", true)]
    [InlineData("unavailable", true)]
    [InlineData("connected", false)]
    [InlineData("rejected", false)]
    public void NeedsVerification_MatchesAutoCheckGate(string? status, bool expected)
    {
        Assert.Equal(expected, ServiceStatusMapper.NeedsVerification(status));
    }
}
