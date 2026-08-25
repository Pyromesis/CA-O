using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Performance;

/// <summary>#6: disable WSearch (indexing). Recommended on SSDs; gated in UI.
/// Service-based (no registry targets), so it implements IOptimization directly
/// and restores the previous start type from the snapshot.</summary>
public sealed class DisableSearchIndexing : IOptimization, IServiceAwareOptimization
{
    public const string ServiceName = "WSearch";

    public OptimizationDefinition Definition => new()
    {
        Id = Id,
        NameEs = "Desactivar indexación (WSearch)",
        NameEn = "Disable search indexing (WSearch)",
        DescriptionEs = "Detiene y deshabilita el servicio de indexado de búsqueda. Recomendado solo en SSD.",
        DescriptionEn = "Stops and disables the Windows Search indexing service. SSD-only recommended.",
        TooltipEs = "En HDD suele convenir dejarlo activo. El tipo de inicio anterior se guarda en el snapshot para restaurarlo.",
        Category = OptimizationCategory.Performance,
        Impact = ImpactLevel.Medium,
        Flags = OptimizationFlags.RecommendedOnSsd,
    };

    private static string Id => "disable-search-indexing";

    /// <summary>Live start type injected by the engine before Detect/Capture.</summary>
    public string? ObservedStartType { get; set; }

    public OptimizationState Detect(IRegistryAccessor registry) =>
        string.Equals(ObservedStartType, "Disabled", StringComparison.OrdinalIgnoreCase)
            ? OptimizationState.AppliedByCao
            : ObservedStartType is null ? OptimizationState.Unknown : OptimizationState.NotApplied;

    public void SetObservedStartType(string? startType) => ObservedStartType = startType;

    public OptimizationSnapshot Capture(IRegistryAccessor registry)
    {
        var snapshot = new OptimizationSnapshot();
        snapshot.ServiceStartTypes.Add($"{ServiceName}={ObservedStartType ?? "Automatic"}");
        return snapshot;
    }

    public async Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (context.Services is null) return OperationResult.Fail("Servicios no disponibles.", "IServiceManager null");
        if (!context.Services.Exists(ServiceName)) return OperationResult.Fail("El servicio WSearch no existe en este sistema.", "missing-service");

        var current = context.Services.GetStartType(ServiceName);
        if (current == "Disabled") return OperationResult.Ok("La indexación ya estaba desactivada.");

        context.Services.SetStartType(ServiceName, "Disabled");
        await context.Services.StopAsync(ServiceName, ct);
        return OperationResult.Ok("Indexación desactivada.");
    }

    public async Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default)
    {
        if (context.Services is null) return OperationResult.Fail("Servicios no disponibles.", "IServiceManager null");
        var note = snapshot.ServiceStartTypes.FirstOrDefault(s => s.StartsWith(ServiceName + "=", StringComparison.Ordinal));
        var previous = note?[(ServiceName.Length + 1)..];
        if (string.IsNullOrWhiteSpace(previous) || previous == "Disabled")
        {
            previous = "Automatic";
        }

        context.Services.SetStartType(ServiceName, previous);
        if (previous.StartsWith("Automatic", StringComparison.Ordinal))
        {
            await context.Services.StartAsync(ServiceName, ct);
        }
        return OperationResult.Ok($"Tipo de inicio anterior ({previous}) restaurado.");
    }
}

/// <summary>Marks optimizations that need live service information to work.</summary>
public interface IServiceAwareOptimization
{
    void SetObservedStartType(string? startType);
}
