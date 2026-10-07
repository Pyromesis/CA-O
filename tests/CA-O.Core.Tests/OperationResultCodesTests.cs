using CAO.Core.Abstractions;
using CAO.Shared;
using Xunit;

namespace CAO.Core.Tests;

public sealed class OperationResultCodesTests
{
    [Theory]
    [InlineData(null, ErrorCodes.TxnApplyFailed)]
    [InlineData("", ErrorCodes.TxnApplyFailed)]
    [InlineData("   ", ErrorCodes.TxnApplyFailed)]
    [InlineData(ErrorCodes.TxnRecoveryPending, ErrorCodes.TxnRecoveryPending)]
    [InlineData(ErrorCodes.SecReadOnlyMode, ErrorCodes.SecReadOnlyMode)]
    [InlineData("CAO-GAME-001", "CAO-GAME-001")]
    [InlineData("not-admin", "not-admin")]
    public void WireCode_PropagatesTheEngineCode_AndFallsBackToTheGenericOne(
        string? engineError, string expected)
    {
        Assert.Equal(expected, OperationResultCodes.WireCode(engineError));
    }

    [Fact]
    public void WireCode_TrimsTheEngineCode()
    {
        Assert.Equal(ErrorCodes.TxnRecoveryPending,
            OperationResultCodes.WireCode($"  {ErrorCodes.TxnRecoveryPending}\r\n"));
    }
}