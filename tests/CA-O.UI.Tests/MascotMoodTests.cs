using CAO.UI.Controls;
using CAO.UI.ViewModels;
using Xunit;

namespace CAO.UI.Tests;

public sealed class MascotMoodTests
{
    [Fact]
    public void UiState_MascotMood_DefaultsToIdle()
    {
        var state = new UiState();
        Assert.Equal("Idle", state.MascotMood);
    }

    [Fact]
    public void UiState_MascotMood_BlankFallsBackToIdle()
    {
        var state = new UiState { MascotMood = "   " };
        Assert.Equal("Idle", state.MascotMood);
    }

    [Theory]
    [InlineData("Idle", "¿Qué optimizamos hoy?")]
    [InlineData("Working", "Manos a la obra…")]
    [InlineData("Celebrate", "¡Listo! Buen trabajo.")]
    [InlineData("Warn", "Ojo, algo necesita atención.")]
    [InlineData("Sleep", "Zzz… aquí estaré.")]
    [InlineData("Groom", "Un momento, me acicalo…")]
    public void Catalog_Resolve_KnownMoods(string mood, string caption)
    {
        var (actualCaption, key) = CatMoodCatalog.Resolve(mood);
        Assert.Equal(caption, actualCaption);
        Assert.Equal(mood, key);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Bailando")]
    public void Catalog_Resolve_UnknownFallsBackToIdle(string? mood)
    {
        var (caption, key) = CatMoodCatalog.Resolve(mood);
        Assert.Equal("Idle", key);
        Assert.Equal("¿Qué optimizamos hoy?", caption);
    }

    [Fact]
    public void Catalog_ValidMoods_HasExactlySix()
    {
        Assert.Equal(new[] { "Idle", "Working", "Celebrate", "Warn", "Sleep", "Groom" }, CatMoodCatalog.ValidMoods);
    }
}
