using CAO.UI.Helpers;
using Xunit;

namespace CAO.UI.Tests;

/// <summary>El registrador black-box: cada fase del sondeo deja una miga para el siguiente crash nativo.</summary>
public sealed class CrashLogStepTests
{
    [Fact]
    public void Step_AppendsABreadcrumb_WithTheStageName()
    {
        var stage = "test-breadcrumb-" + Guid.NewGuid().ToString("N");
        var path = Path.Combine(Path.GetTempPath(), "cao-step-" + Guid.NewGuid().ToString("N") + ".log");
        CrashLog.LogPathOverride = path;
        try
        {
            CrashLog.Step(stage);
            Assert.Contains("step: " + stage, File.ReadAllText(path));
        }
        finally
        {
            CrashLog.LogPathOverride = null;
        }

        File.Delete(path);
    }

    [Fact]
    public void Step_NeverThrows()
    {
        var ex = Record.Exception(() => CrashLog.Step("test-never-throws"));
        Assert.Null(ex);
    }
}
