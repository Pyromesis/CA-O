using System.Net.NetworkInformation;
using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Network;

/// <summary>
/// Desactiva Nagle + delayed ACK (TcpAckFrequency=1, TCPNoDelay=1) solo en
/// interfaces FÍSICAS (Ethernet/Wi-Fi en Up): los paquetes TCP pequeños salen
/// al instante en vez de esperar coalescencia (hasta ~200 ms de lag artificial
/// en juegos, VoIP y RDP). Las virtuales/VPN/loops se excluyen: tocarlas no
/// ayuda y puede romper túneles. Documentado por Microsoft (KB328890).
/// Reversible (borra valores). Requiere reinicio.
/// </summary>
public sealed class DisableNagleTcpAcks : IOptimization
{
    internal const string InterfacesBase = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces";

    /// <summary>Marcas de interfaces no físicas (nombre o descripción, OrdinalIgnoreCase).</summary>
    private static readonly string[] VirtualPatterns =
        ["loop", "virtual", "vpn", "pseudo", "tap", "tun", "hyper-v", "vmware", "virtualbox", "wsl"];

    /// <summary>
    /// Predicado puro: NIC física candidata (Up + tipo Ethernet/Wi-Fi + sin
    /// marcas virtuales). Tupla sintética para hacerlo hermético en tests
    /// (precedente FindWifiInterface de DisableWifiBackgroundScan).
    /// </summary>
    internal static bool IsPhysicalCandidate(
        (string Name, string Description, NetworkInterfaceType Type, OperationalStatus Status) nic)
    {
        if (nic.Status != OperationalStatus.Up)
            return false;
        if (nic.Type is not (NetworkInterfaceType.Ethernet
            or NetworkInterfaceType.FastEthernetT
            or NetworkInterfaceType.FastEthernetFx
            or NetworkInterfaceType.Wireless80211
            or NetworkInterfaceType.GigabitEthernet))
            return false;
        var name = nic.Name ?? string.Empty;
        var description = nic.Description ?? string.Empty;
        return !VirtualPatterns.Any(p =>
            name.Contains(p, StringComparison.OrdinalIgnoreCase)
            || description.Contains(p, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Normaliza GUIDs de interfaz (con o sin llaves, cualquier caja).</summary>
    internal static string NormalizeId(string id) => id.Trim().Trim('{', '}').Trim();

    private static IReadOnlyList<(string Id, string Name, string Description, NetworkInterfaceType Type, OperationalStatus Status)> LiveNics()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Select(n => (n.Id, n.Name ?? string.Empty, n.Description ?? string.Empty,
                    n.NetworkInterfaceType, n.OperationalStatus))
                .ToList();
        }
        catch { return []; }
    }

    /// <summary>
    /// Subclaves del registro que corresponden a NICs físicas (intersección
    /// por GUID normalizado). `nics: null` = enumeración BCL viva; lista
    /// explícita = determinista para tests.
    /// </summary>
    internal static IReadOnlyList<string> PhysicalCandidateIds(
        IRegistryAccessor registry,
        IEnumerable<(string Id, string Name, string Description, NetworkInterfaceType Type, OperationalStatus Status)>? nics)
    {
        var physical = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var nic in nics ?? LiveNics())
        {
            if (!IsPhysicalCandidate((nic.Name, nic.Description, nic.Type, nic.Status)))
                continue;
            physical.Add(NormalizeId(nic.Id));
        }
        return InterfaceIds(registry).Where(id => physical.Contains(NormalizeId(id))).ToList();
    }

    public OptimizationDefinition Definition => new()
    {
        Id = "disable-nagle-tcp-acks",
        NameEs = "Ping bajo: desactivar Nagle/ACK retardado",
        NameEn = "Low ping: disable Nagle/delayed ACK",
        DescriptionEs = "TCP confirma cada paquete al instante en tus tarjetas físicas (TcpAckFrequency=1, TCPNoDelay=1).",
        DescriptionEn = "TCP ACKs every packet immediately on your physical NICs (TcpAckFrequency=1, TCPNoDelay=1).",
        TooltipEs = "Quita el retardo artificial en TCP conversacional (juegos, VoIP). Genera algo más de tráfico: en conexiones muy lentas puede no convenir. Requiere reinicio. Reversible.",
        Category = OptimizationCategory.Network,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Official,
        Confidence = Confidence.Medium,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Moderate,
        Compatibility = CompatibilityStatus.Conditional,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
        Flags = OptimizationFlags.RequiresReboot,
    };

    /// <summary>IDs de interfaces presentes (subclaves). Testeable con MemoryRegistry.</summary>
    internal static IReadOnlyList<string> InterfaceIds(IRegistryAccessor registry) =>
        registry.GetSubKeyNames(RegistryHive2.LocalMachine, InterfacesBase)
            .Where(s => !string.IsNullOrWhiteSpace(s)).ToList();

    internal static bool HasNagleOff(IRegistryAccessor registry, string id)
    {
        var key = InterfacesBase + "\\" + id;
        return ToLong(registry.GetValue(RegistryHive2.LocalMachine, key, "TcpAckFrequency")) == 1
            && ToLong(registry.GetValue(RegistryHive2.LocalMachine, key, "TCPNoDelay")) == 1;
    }

    private static long? ToLong(object? value) => value switch
    {
        int i => i,
        uint u => u,
        long l => l,
        string s => long.TryParse(s, out var n) ? n : null,
        _ => null,
    };

    public OptimizationState Detect(IRegistryAccessor registry) =>
        Detect(registry, nics: null);

    internal OptimizationState Detect(
        IRegistryAccessor registry,
        IEnumerable<(string Id, string Name, string Description, NetworkInterfaceType Type, OperationalStatus Status)>? nics)
    {
        var ids = PhysicalCandidateIds(registry, nics);
        if (ids.Count == 0) return OptimizationState.Unknown;
        return ids.All(id => HasNagleOff(registry, id))
            ? OptimizationState.AppliedByCao
            : OptimizationState.NotApplied;
    }

    public OptimizationSnapshot Capture(IRegistryAccessor registry) =>
        Capture(registry, nics: null);

    internal OptimizationSnapshot Capture(
        IRegistryAccessor registry,
        IEnumerable<(string Id, string Name, string Description, NetworkInterfaceType Type, OperationalStatus Status)>? nics)
    {
        var snapshot = new OptimizationSnapshot();
        foreach (var id in PhysicalCandidateIds(registry, nics))
        {
            var key = InterfacesBase + "\\" + id;
            foreach (var name in new[] { "TcpAckFrequency", "TCPNoDelay" })
            {
                var existing = registry.GetValueRaw(RegistryHive2.LocalMachine, key, name, out var kind);
                snapshot.Registry.Add(new RegistrySnapshotEntry(
                    RegistryHive2.LocalMachine.ToString(), key, name, existing,
                    Existed: existing is not null)
                { Kind = existing is null ? RegistryValueKind2.None : kind });
            }
        }
        return snapshot;
    }

    public Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default) =>
        ApplyAsync(context, nics: null, ct);

    internal Task<OperationResult> ApplyAsync(
        OptimizationContext context,
        IEnumerable<(string Id, string Name, string Description, NetworkInterfaceType Type, OperationalStatus Status)>? nics,
        CancellationToken ct = default)
    {
        var ids = PhysicalCandidateIds(context.Registry, nics);
        if (ids.Count == 0)
            return Task.FromResult(OperationResult.Fail("Sin adaptadores físicos.", "no-physical-nics"));
        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();
            var key = InterfacesBase + "\\" + id;
            context.Registry.SetValue(RegistryHive2.LocalMachine, key, "TcpAckFrequency", 1, RegistryValueKind2.DWord);
            context.Registry.SetValue(RegistryHive2.LocalMachine, key, "TCPNoDelay", 1, RegistryValueKind2.DWord);
        }
        return Task.FromResult(OperationResult.Ok($"Nagle desactivado en {ids.Count} interfaz(es) física(s). Reinicia para aplicar."));
    }

    public Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default)
    {
        foreach (var entry in snapshot.Registry)
        {
            var hive = Enum.Parse<RegistryHive2>(entry.Hive);
            if (entry.Existed && entry.Value is not null)
                context.Registry.SetValueRaw(hive, entry.KeyPath, entry.ValueName, entry.Value, entry.Kind);
            else
                context.Registry.DeleteValue(hive, entry.KeyPath, entry.ValueName);
        }
        return Task.FromResult(OperationResult.Ok("Estado anterior restaurado desde el snapshot."));
    }

    public Task<OptimizationPreview> PreviewAsync(IRegistryAccessor registry, CancellationToken ct = default) =>
        PreviewAsync(registry, nics: null, ct);

    internal Task<OptimizationPreview> PreviewAsync(
        IRegistryAccessor registry,
        IEnumerable<(string Id, string Name, string Description, NetworkInterfaceType Type, OperationalStatus Status)>? nics,
        CancellationToken ct = default)
    {
        var lines = PhysicalCandidateIds(registry, nics).Select(id => new PreviewLine
        {
            Kind = "Registry",
            Target = $@"HKLM\{InterfacesBase}\{id} (TcpAckFrequency, TCPNoDelay)",
            Before = HasNagleOff(registry, id) ? "1, 1 (ya aplicado)" : "por defecto de Windows",
            After = "1, 1 (REG_DWORD)",
        }).ToList();
        if (lines.Count == 0)
        {
            lines.Add(new PreviewLine
            {
                Kind = "Registry",
                Target = @"HKLM\" + InterfacesBase,
                Before = "sin adaptadores físicos",
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
