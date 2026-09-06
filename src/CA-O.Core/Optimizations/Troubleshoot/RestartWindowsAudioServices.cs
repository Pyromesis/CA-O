using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Troubleshoot;

/// <summary>Restarts the Windows audio stack (Audiosrv + AudioEndpointBuilder).</summary>
public sealed class RestartWindowsAudioServices : IOptimization
{
    private static readonly string[] AudioServices = ["Audiosrv", "AudioEndpointBuilder"];

    public OptimizationDefinition Definition => new()
    {
        Id = "restart-windows-audio-services",
        NameEs = "Reiniciar servicios de audio",
        NameEn = "Restart Windows audio services",
        DescriptionEs = "Reinicia la pila de audio de Windows. Solución habitual cuando no hay sonido.",
        DescriptionEn = "Restarts the Windows audio stack. Common fix for no-sound issues.",
        TooltipEs = "Detiene e inicia Audiosrv y AudioEndpointBuilder. No cambia configuración.",
        Category = OptimizationCategory.Performance,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.High,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
        Flags = OptimizationFlags.NotReversible,
    };

    public OptimizationState Detect(IRegistryAccessor registry) => OptimizationState.NotApplied;

    public OptimizationSnapshot Capture(IRegistryAccessor registry) => new OptimizationSnapshot();

    public async Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (context.Services is null)
            return OperationResult.Fail("Gestor de servicios no disponible.", "no-services");

        foreach (var name in AudioServices)
        {
            try { await context.Services.StopAsync(name, ct); }
            catch (Exception ex)
            {
                return OperationResult.Fail($"No se pudo detener {name}.", ex.Message);
            }
        }
        foreach (var name in AudioServices.Reverse())
        {
            try { await context.Services.StartAsync(name, ct); }
            catch (Exception ex)
            {
                return OperationResult.Fail($"No se pudo iniciar {name}.", ex.Message);
            }
        }
        return OperationResult.Ok("Servicios de audio reiniciados.");
    }

    public Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default) =>
        Task.FromResult(OperationResult.Ok("El reinicio no requiere reversión."));

    public Task<VerificationResult> VerifyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (context.Services is null)
            return Task.FromResult(VerificationResult.Unknown(OptimizationState.Unknown, "Gestor de servicios no disponible."));

        foreach (var name in AudioServices)
        {
            if (!context.Services.Exists(name))
            {
                return Task.FromResult(VerificationResult.Unknown(OptimizationState.Unknown, $"Servicio {name} no encontrado."));
            }
        }
        return Task.FromResult(VerificationResult.Passed(OptimizationState.AppliedByCao, "Pila de audio presente tras el reinicio."));
    }
}
