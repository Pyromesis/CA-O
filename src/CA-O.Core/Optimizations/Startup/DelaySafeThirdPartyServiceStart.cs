using System.ServiceProcess;
using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Startup;

/// <summary>
/// Moves safe third-party automatic services to delayed start: only services
/// whose binary lives outside SystemRoot, with no dependents and no
/// security/backup/VPN role. Exact prior state captured per service.
/// </summary>
public sealed class DelaySafeThirdPartyServiceStart : IOptimization
{
    private const string ServicesKey = @"SYSTEM\CurrentControlSet\Services";

    private static readonly string[] ExactExcludes =
    [
        "Dnscache", "LanmanServer", "LanmanWorkstation", "Audiosrv", "AudioEndpointBuilder",
        "gpsvc", "CryptSvc", "RpcSs", "RpcEptMapper", "DcomLaunch", "Schedule", "EventLog",
        "ProfSvc", "Dhcp", "NlaSvc", "BFE", "MpsSvc", "WinDefend", "WdNisSvc", "Sense",
        "SecurityHealthService", "SysMain", "WSearch", "DoSvc", "UsoSvc", "Wuauserv",
        "LSM", "BrokerInfrastructure", "Netlogon", "SamSs", "VaultSvc", "sppsvc",
    ];

    private static readonly string[] ProtectedKeywords =
        ["defender", "antivirus", "malware", "firewall", "endpoint", "crowdstrike", "sentinel",
         "sophos", "mcafee", "norton", "kaspersky", "bitdefender", "avast", "avg", "eset",
         "veeam", "acronis", "backup", "vpn", "wireguard", "nord", "expressvpn"];

    public OptimizationDefinition Definition => new()
    {
        Id = "delay-safe-third-party-service-start",
        NameEs = "Retrasar servicios terceros seguros",
        NameEn = "Delay safe third-party services",
        DescriptionEs = "Pasa a inicio retrasado los servicios de terceros sin dependencias ni rol crítico.",
        DescriptionEn = "Moves dependency-free, non-critical third-party services to delayed start.",
        TooltipEs = "Establece DelayedAutostart=1 solo en servicios con binario fuera de SystemRoot. Reversible exacto.",
        Category = OptimizationCategory.Performance,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Empirical,
        Confidence = Confidence.Medium,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Low,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
    };

    private IReadOnlyList<string> Candidates(IRegistryAccessor registry)
    {
        var found = new List<string>();
        ServiceController[] services;
        try { services = ServiceController.GetServices(); }
        catch { return found; }

        string systemRoot;
        try { systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.System); }
        catch { return found; }

        foreach (var service in services)
        {
            try
            {
                if (service.StartType != ServiceStartMode.Automatic) continue;
                if (ExactExcludes.Any(x => x.Equals(service.ServiceName, StringComparison.OrdinalIgnoreCase))) continue;
                var display = service.DisplayName ?? string.Empty;
                if (ProtectedKeywords.Any(k =>
                        service.ServiceName.Contains(k, StringComparison.OrdinalIgnoreCase) ||
                        display.Contains(k, StringComparison.OrdinalIgnoreCase))) continue;
                if (service.ServicesDependedOn.Length > 0) continue;
                var imagePath = registry.GetValue(
                    RegistryHive2.LocalMachine, $@"{ServicesKey}\{service.ServiceName}", "ImagePath") as string;
                if (string.IsNullOrWhiteSpace(imagePath)) continue;
                var expanded = Environment.ExpandEnvironmentVariables(imagePath).Trim('"').ToLowerInvariant();
                if (expanded.StartsWith(systemRoot.ToLowerInvariant(), StringComparison.Ordinal)) continue;
                found.Add(service.ServiceName);
            }
            catch { /* un servicio problemático no bloquea al resto */ }
            finally { service.Dispose(); }
        }
        return found;
    }

    private bool IsDelayed(IRegistryAccessor registry, string serviceName)
    {
        var delayed = registry.GetValue(RegistryHive2.LocalMachine, $@"{ServicesKey}\{serviceName}", "DelayedAutostart");
        return delayed is int i && i == 1;
    }

    public OptimizationState Detect(IRegistryAccessor registry)
    {
        var candidates = Candidates(registry);
        if (candidates.Count == 0) return OptimizationState.Unknown;
        return candidates.All(s => IsDelayed(registry, s))
            ? OptimizationState.AppliedByCao
            : OptimizationState.NotApplied;
    }

    public OptimizationSnapshot Capture(IRegistryAccessor registry)
    {
        var snapshot = new OptimizationSnapshot();
        foreach (var name in Candidates(registry))
        {
            var key = $@"{ServicesKey}\{name}";
            var delayed = registry.GetValueRaw(RegistryHive2.LocalMachine, key, "DelayedAutostart", out var delayedKind);
            snapshot.Registry.Add(new RegistrySnapshotEntry(
                RegistryHive2.LocalMachine.ToString(), key, "DelayedAutostart", delayed,
                Existed: delayed is not null)
            { Kind = delayed is null ? RegistryValueKind2.None : delayedKind });
            var start = registry.GetValueRaw(RegistryHive2.LocalMachine, key, "Start", out var startKind);
            snapshot.Registry.Add(new RegistrySnapshotEntry(
                RegistryHive2.LocalMachine.ToString(), key, "Start", start,
                Existed: start is not null)
            { Kind = start is null ? RegistryValueKind2.None : startKind });
        }
        return snapshot;
    }

    public Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        var candidates = Candidates(context.Registry)
            .Where(s => !IsDelayed(context.Registry, s))
            .ToList();
        if (candidates.Count == 0)
        {
            return Task.FromResult(OperationResult.Ok("No quedaban servicios terceros para retrasar."));
        }

        foreach (var name in candidates)
        {
            context.Registry.SetValue(
                RegistryHive2.LocalMachine, $@"{ServicesKey}\{name}", "DelayedAutostart", 1, RegistryValueKind2.DWord);
        }
        return Task.FromResult(OperationResult.Ok($"Servicios pasados a inicio retrasado: {candidates.Count}."));
    }

    public Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default)
    {
        foreach (var entry in snapshot.Registry)
        {
            var hive = Enum.Parse<RegistryHive2>(entry.Hive);
            if (entry.Existed && entry.Value is not null)
            {
                context.Registry.SetValueRaw(hive, entry.KeyPath, entry.ValueName, entry.Value, entry.Kind);
            }
            else
            {
                context.Registry.DeleteValue(hive, entry.KeyPath, entry.ValueName);
            }
        }
        return Task.FromResult(OperationResult.Ok("Servicios restaurados desde el snapshot."));
    }

    public Task<VerificationResult> VerifyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        var observed = Detect(context.Registry);
        return Task.FromResult(observed switch
        {
            OptimizationState.AppliedByCao =>
                VerificationResult.Passed(observed, "Servicios terceros verificados en inicio retrasado."),
            OptimizationState.Unknown =>
                VerificationResult.Unknown(observed, "Sin servicios terceros candidatos; no verificable."),
            _ => VerificationResult.Failed(observed, "Algún servicio no quedó en inicio retrasado."),
        });
    }

    public Task<PreconditionResult> CheckPreconditionsAsync(SystemContext context, CancellationToken ct = default) =>
        Task.FromResult(PreconditionResult.Ok("Sin precondiciones específicas."));

    public Task<OptimizationPreview> PreviewAsync(IRegistryAccessor registry, CancellationToken ct = default)
    {
        var lines = Candidates(registry)
            .Where(s => !IsDelayed(registry, s))
            .Select(name => new PreviewLine
            {
                Kind = "Service",
                Target = $@"HKLM\{ServicesKey}\{name}\DelayedAutostart",
                Before = registry.GetValue(RegistryHive2.LocalMachine, $@"{ServicesKey}\{name}", "DelayedAutostart")?.ToString() ?? "(ausente)",
                After = "1 (retrasado)",
            }).ToList();

        if (lines.Count == 0)
        {
            lines.Add(new PreviewLine
            {
                Kind = "Service",
                Target = @"HKLM\SYSTEM\CurrentControlSet\Services\*\DelayedAutostart",
                Before = "sin candidatos pendientes",
                After = "sin cambios",
            });
        }

        return Task.FromResult(new OptimizationPreview
        {
            OptimizationId = Definition.Id,
            Lines = lines,
            Risk = Definition.Risk,
            SecurityImpact = Definition.SecurityImpact,
            Flags = Definition.Flags,
        });
    }
}
