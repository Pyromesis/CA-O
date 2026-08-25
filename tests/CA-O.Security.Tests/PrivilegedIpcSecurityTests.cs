using System.Text;
using CAO.Shared;
using Xunit;

namespace CAO.Security.Tests;

/// <summary>
/// Adversarial tests for the privileged IPC surface (spec 6-7, 93): schema
/// validation must reject every malformed or malicious request shape.
/// </summary>
public sealed class PrivilegedIpcSecurityTests
{
    private static PrivilegedOperationRequest ValidRequest(
        Func<PrivilegedOperationRequest, PrivilegedOperationRequest>? mutate = null)
    {
        var request = new PrivilegedOperationRequest
        {
            RequestId = Guid.NewGuid(),
            Nonce = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)),
            Operation = PrivilegedOperation.DetectOptimization,
            Parameters = new OperationParameters { OptimizationId = "disable-vbs" },
        };
        return mutate?.Invoke(request) ?? request;
    }

    [Fact]
    public void AcceptsWellFormedTypedRequest()
    {
        Assert.True(PrivilegedOperationValidator.TryValidate(ValidRequest(), out _));
    }

    [Theory]
    [InlineData("../../../../Windows/System32/config")]
    [InlineData("disable-vbs; rm -rf /")]
    [InlineData("disable-vbs' OR '1'='1")]
    [InlineData("disable-vbs\x00hidden")]
    [InlineData("../..")]
    [InlineData("DISABLE-VBS-UPPERCASE")]
    public void RejectsMaliciousOptimizationIds(string optimizationId)
    {
        var request = ValidRequest(r => r with
        {
            Parameters = new OperationParameters { OptimizationId = optimizationId },
        });

        Assert.False(PrivilegedOperationValidator.TryValidate(request, out _));
    }

    [Fact]
    public void RejectsWrongProtocolVersion()
    {
        var request = ValidRequest(r => r with { ProtocolVersion = AppVersion.ProtocolVersion + 1 });

        Assert.False(PrivilegedOperationValidator.TryValidate(request, out _));
    }

    [Fact]
    public void RejectsEmptyRequestId()
    {
        var request = ValidRequest(r => r with { RequestId = Guid.Empty });

        Assert.False(PrivilegedOperationValidator.TryValidate(request, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("nonce\x01with-control")]
    [InlineData("a\nb")]
    public void RejectsControlOrEmptyNonces(string nonce)
    {
        var request = ValidRequest(r => r with { Nonce = nonce });

        Assert.False(PrivilegedOperationValidator.TryValidate(request, out _));
    }

    [Fact]
    public void RejectsUndefinedOperations()
    {
        var request = ValidRequest(r => r with { Operation = (PrivilegedOperation)999 });

        Assert.False(PrivilegedOperationValidator.TryValidate(request, out _));
    }

    [Fact]
    public void OperationsRequiringOptimizationIdRejectMissingValue()
    {
        foreach (var operation in new[]
                 {
                     PrivilegedOperation.ApplyOptimization,
                     PrivilegedOperation.RevertOptimization,
                     PrivilegedOperation.CaptureSnapshot,
                     PrivilegedOperation.VerifyOptimization,
                     PrivilegedOperation.DetectOptimization,
                 })
        {
            var request = ValidRequest(r => r with
            {
                Operation = operation,
                Parameters = new OperationParameters(),
            });

            Assert.False(PrivilegedOperationValidator.TryValidate(request, out _),
                $"{operation} debe exigir OptimizationId.");
        }
    }

    [Fact]
    public void RejectsInvalidDriveLetters()
    {
        var request = ValidRequest(r => r with
        {
            Parameters = new OperationParameters { DriveLetter = '1' },
        });

        Assert.False(PrivilegedOperationValidator.TryValidate(request, out _));
    }

    [Fact]
    public void RejectsServiceNamesWithPathSeparators()
    {
        var baseRequest = ValidRequest();
        var serviceName = Encoding.UTF8.GetString([0x57, 0x69, 0x6E]) + "\\..\\..";
        var request = baseRequest with
        {
            Parameters = new OperationParameters { ServiceName = serviceName },
        };

        Assert.False(PrivilegedOperationValidator.TryValidate(request, out _));
    }
}
