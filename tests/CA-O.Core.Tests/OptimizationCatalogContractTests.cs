using CAO.Core.Catalog;
using CAO.Shared;
using Xunit;

namespace CAO.Core.Tests;

/// <summary>
/// Optimization contract tests (spec 92): every catalog entry must be fully
/// documented, classified, reversible (or explicitly maintenance) and
/// security-annotated. A catalog entry that cannot answer these questions
/// must not ship.
/// </summary>
public sealed class OptimizationCatalogContractTests
{
    public static TheoryData<string> AllIds() =>
        new(OptimizationCatalog.All.Select(optimization => optimization.Definition.Id));

    [Fact]
    public void CatalogIsNotEmpty()
    {
        Assert.NotEmpty(OptimizationCatalog.All);
    }

    [Fact]
    public void IdsAreUnique()
    {
        var ids = OptimizationCatalog.All.Select(o => o.Definition.Id).ToList();
        Assert.Equal(19, ids.Count); // producción verificada (49 STUBs retirados)
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void LegacyCatalogKeeps68ForTraceability()
    {
        Assert.Equal(68, OptimizationCatalog.AllLegacy.Count);
    }

    [Theory]
    [MemberData(nameof(AllIds))]
    public void EveryOptimizationHasCompleteMetadata(string id)
    {
        var definition = Resolve(id);

        Assert.False(string.IsNullOrWhiteSpace(definition.NameEs), $"{id}: falta NameEs");
        Assert.False(string.IsNullOrWhiteSpace(definition.NameEn), $"{id}: falta NameEn");
        Assert.False(string.IsNullOrWhiteSpace(definition.DescriptionEs), $"{id}: falta DescriptionEs");
        Assert.False(string.IsNullOrWhiteSpace(definition.DescriptionEn), $"{id}: falta DescriptionEn");
        Assert.True(Enum.IsDefined(definition.Category), $"{id}: categoría inválida");
        Assert.NotEqual(EvidenceLevel.Unknown, definition.Evidence);
        Assert.NotEqual(Confidence.Unknown, definition.Confidence);
        Assert.NotEqual(AntiCheatImpact.Unknown, definition.AntiCheatImpact);
        Assert.True(Enum.IsDefined(definition.Risk), $"{id}: riesgo inválido");
        Assert.True(Enum.IsDefined(definition.SecurityImpact), $"{id}: impacto de seguridad no clasificado");
        Assert.True(Enum.IsDefined(definition.Compatibility) && definition.Compatibility != CompatibilityStatus.Unknown,
            $"{id}: compatibilidad sin clasificar");
    }

    [Theory]
    [MemberData(nameof(AllIds))]
    public void ReversibleOptimizationsMustSupportSnapshotRollback(string id)
    {
        var optimization = OptimizationCatalog.All.First(o => o.Definition.Id == id);

        if (!optimization.Definition.Reversible &&
            !optimization.Definition.Flags.HasFlag(OptimizationFlags.NotReversible))
        {
            Assert.Fail($"{id}: irreversible pero sin la marca NotReversible que lo documente.");
        }
    }

    [Theory]
    [MemberData(nameof(AllIds))]
    public void SecurityReducingChangesMustBeFlaggedExpertOnly(string id)
    {
        var definition = Resolve(id);
        if (definition.SecurityImpact == SecurityImpact.ReducedProtection)
        {
            Assert.True(definition.Flags.HasFlag(OptimizationFlags.SecurityTradeoff),
                $"{id}: reduce seguridad y debe llevar SecurityTradeoff.");
            Assert.Equal(SecurityImpact.ReducedProtection, definition.SecurityImpact);
        }
    }

    [Theory]
    [MemberData(nameof(AllIds))]
    public void NoUnsupportedClaimsInDescriptions(string id)
    {
        var definition = Resolve(id);
        foreach (var text in new[] { definition.DescriptionEs, definition.DescriptionEn, definition.TooltipEs })
        {
            Assert.DoesNotContain("FPS", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("%", text, StringComparison.Ordinal); // no "+20%" style promises
            Assert.DoesNotContain("guaranteed", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("garantizado", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static OptimizationDefinition Resolve(string id) =>
        OptimizationCatalog.All.First(o => o.Definition.Id == id).Definition;
}
