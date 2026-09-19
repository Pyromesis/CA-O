using System;
using System.Linq;
using CAO.Core.Catalog;
using CAO.Core.Engine;
using CAO.Shared;
using Xunit;

namespace CAO.Core.Tests;

public sealed class BatchDefaultRecommendationTests
{
    [Fact]
    public void BatchDefault_Excludes_Repairs_Diagnostics_Restores()
    {
        var context = SystemContextFactory.Default();
        var recs = RecommendationEngine.BuildAll(CatalogProjections.BatchDefault, new MemoryRegistry(), context);
        var ids = recs.Select(r => r.OptimizationId).ToList();
        Assert.DoesNotContain(ids, id => CatalogProjections.RepairIds.Contains(id));
        Assert.DoesNotContain(ids, id => CatalogProjections.DiagnosticIds.Contains(id));
        Assert.DoesNotContain(ids, id => CatalogProjections.RestoreIds.Contains(id));
    }

    [Fact]
    public void Engine_Still_Resolves_Repair_Id_For_Solucionar_Compat()
    {
        var found = OptimizationCatalog.All.FirstOrDefault(o =>
            o.Definition.Id.Equals("flush-dns-cache", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(found);
    }
}
