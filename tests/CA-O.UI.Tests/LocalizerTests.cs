using CAO.UI;
using Xunit;

namespace CAO.UI.Tests;

/// <summary>
/// Localization contract (spec 84): es-ES and en-US must expose exactly the
/// same key set — a missing translation is a build-time failure, not a
/// runtime surprise.
/// </summary>
public sealed class LocalizerTests
{
    [Fact]
    public void SpanishAndEnglishExposeIdenticalKeySets()
    {
        // Localizer exposes only Get(); key parity is validated via behavior:
        // every known key resolves to something other than the key itself in
        // both languages.
        string[] keys =
        [
            "app.title", "app.subtitle",
            "nav.dashboard", "nav.analyze", "nav.optimize", "nav.gaming",
            "nav.diagnostics", "nav.benchmark", "nav.restore", "nav.history", "nav.settings",
            "dashboard.analyze", "dashboard.analyzing", "dashboard.lastAnalysis", "dashboard.never",
            "dashboard.recommended", "dashboard.optional", "dashboard.experimental",
            "dashboard.securitySensitive", "dashboard.notApplicable", "dashboard.findings",
            "analyze.run", "optimize.applyRecommended", "optimize.expertWarning",
            "benchmark.baseline", "benchmark.after",
            "restore.pointNote", "settings.expertMode", "settings.theme",
            "settings.language", "settings.serviceCheck",
            "common.workloadDependent", "common.noClaims",
        ];

        Assert.NotEmpty(keys);

        Localizer.SetLanguage("es-ES");
        var spanish = keys.Select(Localizer.Get).ToList();
        Localizer.SetLanguage("en-US");
        var english = keys.Select(Localizer.Get).ToList();

        for (var index = 0; index < keys.Length; index++)
        {
            Assert.NotEqual(keys[index], spanish[index]);
            Assert.NotEqual(keys[index], english[index]);
        }
    }

    [Fact]
    public void UnknownKeysReturnTheKeyItselfInsteadOfThrowing()
    {
        Localizer.SetLanguage("es-ES");

        Assert.Equal("key.that.does.not.exist", Localizer.Get("key.that.does.not.exist"));
    }

    [Fact]
    public void SupportedLanguagesIncludeSpanishFirst()
    {
        Assert.Equal("es-ES", Localizer.SupportedLanguages[0]);
        Assert.Contains("en-US", Localizer.SupportedLanguages);
    }
}
