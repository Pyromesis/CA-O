using CAO.Infrastructure.Logging;
using CAO.Shared;
using Xunit;

namespace CAO.Infrastructure.Tests;

public sealed class HistoryRobustnessTests
{
    [Fact]
    public void ReadLast_WithMalformedLines_SkipsAndReturnsValid()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cao-hist-{Guid.NewGuid():N}.jsonl");
        File.WriteAllText(path, "{ not json\n");
        File.AppendAllText(path, "{\"TimestampUtc\":\"2026-08-26T10:00:00Z\",\"OptimizationId\":\"x\",\"Operation\":\"apply\",\"Success\":true}\n");
        File.AppendAllText(path, "   \n");
        File.AppendAllText(path, "{ \"bad\": }\n");
        var logger = new JsonHistoryLogger(path);
        var last = logger.ReadLast(10);
        // Debe tolerar corruptas y devolver al menos la válida migrada como legacy
        Assert.True(last.Count >= 1);
        var warnings = logger.VerifyIntegrity();
        Assert.True(warnings.Count >= 2);
    }

    [Fact]
    public void ReadLast_EmptyFile_ReturnsEmpty()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cao-empty-{Guid.NewGuid():N}.jsonl");
        File.WriteAllText(path, "");
        var logger = new JsonHistoryLogger(path);
        Assert.Empty(logger.ReadLast(10));
        Assert.Empty(logger.VerifyIntegrity());
    }
}
