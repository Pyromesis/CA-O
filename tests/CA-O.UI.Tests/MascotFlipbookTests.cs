using System;
using System.Collections.Generic;
using System.IO;
using CAO.UI.Controls;
using Xunit;

namespace CAO.UI.Tests;

/// <summary>
/// La mascota es un flipbook de PNG: el catálogo es puro y los assets tienen
/// que estar donde el catálogo dice, en los dos temas y con formato válido.
/// </summary>
public sealed class MascotFlipbookTests
{
    private static string AssetsRoot => AppContext.BaseDirectory;

    private static string Resolve(string relative) =>
        Path.Combine(AssetsRoot, relative.Replace('/', Path.DirectorySeparatorChar));

    [Fact]
    public void Moods_MatchMoodCatalog()
    {
        Assert.Equal(CatMoodCatalog.ValidMoods, MascotFlipbook.Moods);
    }

    [Theory]
    [InlineData(null, "Idle")]
    [InlineData("", "Idle")]
    [InlineData("   ", "Idle")]
    [InlineData("celebrate", "Celebrate")]
    [InlineData("SLEEP", "Sleep")]
    [InlineData("groom", "Groom")]
    [InlineData("Bailando", "Idle")]
    public void Normalize_ResolvesKnownMoodsAndFallsBack(string? mood, string expected)
    {
        Assert.Equal(expected, MascotFlipbook.Normalize(mood));
    }

    [Fact]
    public void FolderName_MapsMoodToDerivedSet()
    {
        Assert.Equal("idle", MascotFlipbook.FolderName("Idle"));
        Assert.Equal("roll", MascotFlipbook.FolderName("Working"));
        Assert.Equal("happy", MascotFlipbook.FolderName("Celebrate"));
        Assert.Equal("look", MascotFlipbook.FolderName("Warn"));
        Assert.Equal("lie", MascotFlipbook.FolderName("Sleep"));
        Assert.Equal("lick", MascotFlipbook.FolderName("Groom"));
        Assert.Equal("idle", MascotFlipbook.FolderName("Bailando"));
    }

    [Fact]
    public void EveryMoodHasEnoughFramesForFrameByFrameAnimation()
    {
        foreach (var mood in MascotFlipbook.Moods)
        {
            Assert.InRange(MascotFlipbook.FrameCount(mood), 5, 30);
            Assert.InRange(MascotFlipbook.FrameMs(mood), 60, 400);
        }
    }

    [Fact]
    public void SleepIsSlowerThanWorking()
    {
        Assert.True(MascotFlipbook.FrameMs("Sleep") > MascotFlipbook.FrameMs("Working"));
        Assert.True(MascotFlipbook.CycleMs("Sleep") >= 2000);
    }

    [Fact]
    public void RelativePath_IsStableAndWraps()
    {
        Assert.Equal("Assets/mascot/frames/dark/idle/f00.png", MascotFlipbook.RelativePath("dark", "Idle", 0));
        Assert.Equal("Assets/mascot/frames/light/happy/f03.png", MascotFlipbook.RelativePath("light", "Celebrate", 3));
        // el índice envuelve: sirve para loops y para el último frame de cada mood
        var count = MascotFlipbook.FrameCount("Idle");
        Assert.Equal(MascotFlipbook.RelativePath("light", "Idle", 0),
            MascotFlipbook.RelativePath("light", "Idle", count));
    }

    [Fact]
    public void AppxUri_IsStableAndWraps()
    {
        // Vía de carga principal en WinUI (ms-appx). CaoCat la usa antes del fallback a archivo.
        Assert.Equal("ms-appx:///Assets/mascot/frames/dark/look/f00.png", MascotFlipbook.AppxUri("dark", "Warn", 0));
        Assert.Equal("ms-appx:///Assets/mascot/frames/light/idle/f03.png", MascotFlipbook.AppxUri("light", "Idle", 3));
        // el índice envuelve igual que RelativePath
        var count = MascotFlipbook.FrameCount("Warn");
        Assert.Equal(MascotFlipbook.AppxUri("dark", "Warn", 0),
            MascotFlipbook.AppxUri("dark", "Warn", count));
    }

    [Fact]
    public void NormalizeTheme_OnlyDarkIsDark()
    {
        Assert.Equal("dark", MascotFlipbook.NormalizeTheme("dark"));
        Assert.Equal("dark", MascotFlipbook.NormalizeTheme("DARK"));
        Assert.Equal("light", MascotFlipbook.NormalizeTheme("light"));
        Assert.Equal("light", MascotFlipbook.NormalizeTheme(""));
        Assert.Equal("light", MascotFlipbook.NormalizeTheme(null));
    }

    [Fact]
    public void EveryFrameAssetExistsAndIsA1024SquarePng()
    {
        foreach (var theme in MascotFlipbook.Themes)
        {
            foreach (var mood in MascotFlipbook.Moods)
            {
                for (var i = 0; i < MascotFlipbook.FrameCount(mood); i++)
                {
                    var path = Resolve(MascotFlipbook.RelativePath(theme, mood, i));
                    Assert.True(File.Exists(path), $"falta el frame {path}");
                    var bytes = File.ReadAllBytes(path);
                    Assert.True(bytes.Length > 512, $"frame sospechosamente pequeño: {path}");
                    AssertPngSignature(bytes, path);
                    var (width, height) = ReadPngSize(bytes);
                    Assert.Equal(1024, width);
                    Assert.Equal(1024, height);
                }
            }
        }
    }

    private static void AssertPngSignature(byte[] bytes, string path)
    {
        byte[] signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        for (var i = 0; i < signature.Length; i++)
            Assert.True(bytes[i] == signature[i], $"PNG inválido: {path}");
    }

    private static (int Width, int Height) ReadPngSize(byte[] bytes)
    {
        // IHDR: ancho y alto big-endian a partir del byte 16
        var width = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
        var height = (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23];
        return (width, height);
    }

    [Fact]
    public void OccasionMoodsReturnToIdle_SleepAndGroomDoNot()
    {
        Assert.True(MascotFlipbook.ReturnsToIdle("Working"));
        Assert.True(MascotFlipbook.ReturnsToIdle("Celebrate"));
        Assert.True(MascotFlipbook.ReturnsToIdle("Warn"));
        Assert.False(MascotFlipbook.ReturnsToIdle("Idle"));
        Assert.False(MascotFlipbook.ReturnsToIdle("Sleep"));
        Assert.False(MascotFlipbook.ReturnsToIdle("Groom"));
        Assert.False(MascotFlipbook.ReturnsToIdle("Bailando"));
        Assert.True(MascotFlipbook.OneShotCycles("Celebrate") >= 1);
        Assert.Equal(0, MascotFlipbook.OneShotCycles("Sleep"));
    }

    [Fact]
    public void ThemesCoveredAreExactlyLightAndDark()
    {
        var expected = new List<string> { "light", "dark" };
        Assert.Equal(expected, MascotFlipbook.Themes);
    }
}
