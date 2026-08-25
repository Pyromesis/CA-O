using CAO.Core.Abstractions;
using CAO.Infrastructure.Logging;
using CAO.Shared;
using Xunit;

namespace CAO.Infrastructure.Tests;

/// <summary>
/// Audit log integrity (FASE 13): hash chain over history.jsonl detects
/// tampering, reordering, deletion and corruption — never silently ignored.
/// </summary>
public sealed class HistoryHashChainTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(
        Path.GetTempPath(), "cao-chain-" + Guid.NewGuid().ToString("N"));

    private string LogPath => Path.Combine(_tempDirectory, "history.jsonl");

    [Fact]
    public void ChainValidatesWhenUntampered()
    {
        var logger = new JsonHistoryLogger(LogPath);
        logger.Log(Entry("a"));
        logger.Log(Entry("b"));
        logger.Log(Entry("c"));

        Assert.Empty(logger.VerifyIntegrity());
        Assert.Equal(3, logger.ReadLast(int.MaxValue).Count);
    }

    [Fact]
    public void TamperedLineIsDetected()
    {
        var logger = new JsonHistoryLogger(LogPath);
        logger.Log(Entry("one"));
        logger.Log(Entry("two"));
        TamperLine(2, static line => line.Replace("\"Success\":true", "\"Success\":false"));

        Assert.Single(logger.VerifyIntegrity());
    }

    [Fact]
    public void DeletedMiddleLineIsDetected()
    {
        var logger = new JsonHistoryLogger(LogPath);
        logger.Log(Entry("1"));
        logger.Log(Entry("2"));
        logger.Log(Entry("3"));
        DeleteLine(2);

        var warnings = logger.VerifyIntegrity();

        Assert.Contains(warnings, warning => warning.ReasonEs.Contains("Secuencia rota"));
        Assert.Contains(warnings, warning => warning.ReasonEs.Contains("previo"));
    }

    [Fact]
    public void CorruptJsonLineIsReportedButDoesNotBreakReading()
    {
        var logger = new JsonHistoryLogger(LogPath);
        logger.Log(Entry("valid-1"));
        File.AppendAllText(LogPath, "{this is not json\n");
        logger.Log(Entry("valid-2"));

        var warnings = logger.VerifyIntegrity();
        var entries = logger.ReadLast(int.MaxValue);

        Assert.Contains(warnings, warning => warning.ReasonEs.Contains("inválida"));
        Assert.Equal(2, entries.Count);
    }

    private static HistoryEntry Entry(string id) => new()
    {
        TimestampUtc = DateTime.UtcNow,
        AppVersion = AppVersion.Semantic,
        WindowsBuild = 26200,
        OptimizationId = id,
        Operation = "apply",
        Success = true,
    };

    private void TamperLine(int lineNumber, Func<string, string> mutate)
    {
        var lines = File.ReadAllLines(LogPath).ToList();
        lines[lineNumber - 1] = mutate(lines[lineNumber - 1]);
        File.WriteAllLines(LogPath, lines);
    }

    private void DeleteLine(int lineNumber)
    {
        var lines = File.ReadAllLines(LogPath).ToList();
        lines.RemoveAt(lineNumber - 1);
        File.WriteAllLines(LogPath, lines);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}
