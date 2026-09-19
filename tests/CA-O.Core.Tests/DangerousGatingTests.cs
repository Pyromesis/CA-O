using System;
using System.Linq;
using CAO.Core.Catalog;
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
        var batch = CatalogProjections.BatchDefault.Select(o => o.Definition.Id).ToList();
        foreach (var id in new[] { "disable-vbs", "windows-component-store-resetbase", "disable-dynamic-tick" })
        {
            if (!batch.Contains(id, StringComparer.OrdinalIgnoreCase)) continue;
            var def = OptimizationCatalog.All
                .First(o => o.Definition.Id.Equals(id, StringComparison.OrdinalIgnoreCase)).Definition;
            var blocked = def.Flags.HasFlag(CAO.Shared.OptimizationFlags.ExpertOnly)
                || def.Flags.HasFlag(CAO.Shared.OptimizationFlags.SecurityTradeoff)
                || def.SecurityImpact == CAO.Shared.SecurityImpact.ReducedProtection;
            Assert.True(blocked, id + " en batch debe estar bloqueado por policy (ExpertOnly/SecurityTradeoff).");
        }
    }
}
