using System;
using System.Linq;
using CAO.Core.Catalog;
using CAO.Core.Engine;
using CAO.Shared;
using Xunit;

namespace CAO.Core.Tests;

public sealed class DangerousGatingTests
{
    [Theory]
    [InlineData("disable-vbs")]
    [InlineData("windows-component-store-resetbase")]
    [InlineData("reset-network-stack-repair")]
    public void Dangerous_Requires_Restore_Point(string id)
    {
        var def = OptimizationCatalog.All
            .First(o => o.Definition.Id.Equals(id, StringComparison.OrdinalIgnoreCase)).Definition;
        Assert.True(def.RequiresRestorePoint, id + " debe exigir restore-point (FASE 12).");
    }

    [Theory]
    [InlineData("disable-vbs")]
    [InlineData("windows-component-store-resetbase")]
    [InlineData("disable-dynamic-tick")]
    public void Dangerous_Is_ExpertOnly(string id)
    {
        var def = OptimizationCatalog.All
            .First(o => o.Definition.Id.Equals(id, StringComparison.OrdinalIgnoreCase)).Definition;
        Assert.True(def.Flags.HasFlag(CAO.Shared.OptimizationFlags.ExpertOnly), id + " solo Expertos.");
    }

    [Fact]
    public void Dangerous_Never_Recommended_In_Batch()
    {
        // Camino real: la policy del RecommendationEngine sobre BatchDefault, no
        // una reimplementation inline de ExpertOnly/SecurityTradeoff.
        var recommendations = RecommendationEngine.BuildAll(
            CatalogProjections.BatchDefault, new MemoryRegistry(), SystemContextFactory.Default());
        var byId = recommendations.ToDictionary(r => r.OptimizationId, StringComparer.OrdinalIgnoreCase);

        foreach (var id in new[] { "disable-vbs", "windows-component-store-resetbase", "disable-dynamic-tick" })
        {
            Assert.True(byId.ContainsKey(id), id + " debe existir en BatchDefault para evaluar la policy real.");
            Assert.NotEqual(RecommendationBucket.Recommended, byId[id].Bucket);
        }
    }
}
