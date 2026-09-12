using CAO.UI;
using Xunit;

namespace CAO.UI.Tests;

/// <summary>El crash-log no debe llenar el disco ni reventar con rutas raras.</summary>
public sealed class CrashLogRotationTests
{
    [Fact]
    public void RotateCrashLogIfNeeded_RotatesOverCapKeepsUnder()
    {
        var dir = Path.Combine(Path.GetTempPath(), "CA-O-Test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "cao-ui-crash.log");
            File.WriteAllBytes(path, new byte[600 * 1024]);
            App.RotateCrashLogIfNeeded(path);
            Assert.False(File.Exists(path));
            Assert.Equal(600 * 1024, new FileInfo(path + ".bak").Length);
            // Segunda rotación seguida no falla ni duplica basura.
            File.WriteAllBytes(path, new byte[600 * 1024]);
            App.RotateCrashLogIfNeeded(path);
            Assert.False(File.Exists(path));
            Assert.True(File.Exists(path + ".bak"));
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void RotateCrashLogIfNeeded_SmallOrMissingIsNoOp()
    {
        var dir = Path.Combine(Path.GetTempPath(), "CA-O-Test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var small = Path.Combine(dir, "small.log");
            File.WriteAllBytes(small, new byte[100]);
            App.RotateCrashLogIfNeeded(small);
            Assert.Equal(100, new FileInfo(small).Length);
            Assert.False(File.Exists(small + ".bak"));
            // Ruta inexistente: no lanza.
            App.RotateCrashLogIfNeeded(Path.Combine(dir, "no-existe.log"));
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void WriteCrashLog_NeverThrowsEvenOnGarbage()
    {
        // No debe lanzar ni siquiera con excepciones anidadas profundas.
        Exception deep = new InvalidOperationException("x");
        for (int i = 0; i < 50; i++) deep = new InvalidOperationException($"nivel {i}", deep);
        var ex = Record.Exception(() => App.WriteCrashLog(deep));
        Assert.Null(ex);
    }
}
