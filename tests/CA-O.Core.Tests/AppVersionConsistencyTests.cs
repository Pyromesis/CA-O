using CAO.Shared;
using CAO.Shared.Constants;
using Xunit;

namespace CAO.Core.Tests;

/// <summary>
/// La versión que muestra la UI (AppVersion) debe ser siempre la del build
/// (BuildConstants.ProductVersion). Regresión: AppVersion estuvo hardcodeada
/// a "2.1.29" y la app mostraba versión vieja tras actualizar.
/// </summary>
public sealed class AppVersionConsistencyTests
{
    [Fact]
    public void Semantic_Matches_BuildConstants_ProductVersion()
    {
        Assert.Equal(BuildConstants.ProductVersion, AppVersion.Semantic);
    }

    [Fact]
    public void Parts_Are_Consistent_With_Semantic()
    {
        Assert.Equal($"{AppVersion.Major}.{AppVersion.Minor}.{AppVersion.Patch}", AppVersion.Semantic);
    }

    [Fact]
    public void Semantic_Is_Parsable_Release_Version()
    {
        Assert.True(Version.TryParse(AppVersion.Semantic, out var v) && v.Major > 0);
    }
}
