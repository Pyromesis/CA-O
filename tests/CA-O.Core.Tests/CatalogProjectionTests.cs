using CAO.Core.Catalog;
using Xunit;

namespace CAO.Core.Tests;

public sealed class CatalogProjectionTests
{
    [Fact]
    public void Projections_Partition_All_Exactly()
    {
        var all = OptimizationCatalog.All.Select(o => o.Definition.Id).ToList();
        var projected = CatalogProjections.RepairActions
            .Concat(CatalogProjections.Diagnostics)
            .Concat(CatalogProjections.Restores)
            .Concat(CatalogProjections.BatchDefault)
            .Select(o => o.Definition.Id).ToList();
        Assert.Equal(all.Count, projected.Count);
        Assert.Empty(all.Except(projected, StringComparer.OrdinalIgnoreCase));
        Assert.Empty(projected.Except(all, StringComparer.OrdinalIgnoreCase));
        Assert.Equal(projected.Count, projected.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Every_Projected_Id_Resolves_In_All()
    {
        foreach (var id in CatalogProjections.RepairIds
            .Concat(CatalogProjections.DiagnosticIds)
            .Concat(CatalogProjections.RestoreIds))
        {
            Assert.Contains(OptimizationCatalog.All, o =>
                o.Definition.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void BatchDefault_Has_No_Repairs_Diagnostics_Restores()
    {
        foreach (var o in CatalogProjections.BatchDefault)
        {
            Assert.DoesNotContain(o.Definition.Id, CatalogProjections.RepairIds);
            Assert.DoesNotContain(o.Definition.Id, CatalogProjections.DiagnosticIds);
            Assert.DoesNotContain(o.Definition.Id, CatalogProjections.RestoreIds);
        }
    }
}
