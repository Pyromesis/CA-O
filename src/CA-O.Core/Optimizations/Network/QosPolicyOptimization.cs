using CAO.Core.Abstractions;
using CAO.Core.Optimizations;
using CAO.Shared;

namespace CAO.Core.Optimizations.Network;

/// <summary>
/// Policy-based QoS de Windows (el mecanismo que despliega una GPO): una
/// política por ejecutable que marca su tráfico con un valor DSCP, sin
/// límite de ancho de banda, más "Do not use NLA" para que se aplique fuera
/// de un dominio. Varias optimizaciones de CA-O comparten esa última clave:
/// al revertir una, la clave solo vuelve a su valor anterior si ninguna otra
/// política de CA-O sigue activa (si no, revertir el QoS de voz apagaría el
/// de juegos). Revert borra los valores: una política sin "Version" se ignora.
/// </summary>
public abstract class QosPolicyOptimization : RegistryOptimizationBase
{
    internal const string PoliciesKey = @"SOFTWARE\Policies\Microsoft\Windows\QoS";
    internal const string NlaKey = @"SYSTEM\CurrentControlSet\Services\Tcpip\QoS";
    internal const string NlaValue = "Do not use NLA";
    internal const string DscpExpeditedForwarding = "46";

    private IReadOnlyList<ValueTarget>? _targets;

    protected abstract string PolicyNamePrefix { get; }

    protected abstract IReadOnlyList<string> Executables { get; }

    protected virtual string Dscp => DscpExpeditedForwarding;

    protected override IReadOnlyList<ValueTarget> Targets => _targets ??= BuildTargets();

    internal static string PolicyKeyFor(string prefix, string exe) =>
        PoliciesKey + @"\" + prefix + Path.GetFileNameWithoutExtension(exe);

    /// <summary>Todas las políticas que CA-O puede crear (para saber si otra sigue activa).</summary>
    internal static IReadOnlyList<(string Prefix, IReadOnlyList<string> Executables)> AllCaoPolicies =>
    [
        (PrioritizeGameTrafficQos.PolicyPrefix, PrioritizeGameTrafficQos.GameExecutables),
        (PrioritizeVoiceChatQos.PolicyPrefix, PrioritizeVoiceChatQos.VoiceExecutables),
    ];

    private List<ValueTarget> BuildTargets()
    {
        var targets = new List<ValueTarget>
        {
            new(RegistryHive2.LocalMachine, NlaKey, NlaValue, "1", RegistryValueKind2.String),
        };
        foreach (var exe in Executables)
        {
            var key = PolicyKeyFor(PolicyNamePrefix, exe);
            void Add(string name, string value) =>
                targets.Add(new(RegistryHive2.LocalMachine, key, name, value, RegistryValueKind2.String));
            Add("Version", "1.0");
            Add("Application Name", exe);
            Add("Protocol", "*");
            Add("Local Port", "*");
            Add("Local IP", "*");
            Add("Local IP Prefix Length", "*");
            Add("Remote Port", "*");
            Add("Remote IP", "*");
            Add("Remote IP Prefix Length", "*");
            Add("DSCP Value", Dscp);
            Add("Throttle Rate", "-1");
        }
        return targets;
    }

    private bool AnotherCaoPolicyIsActive(IRegistryAccessor registry) =>
        AllCaoPolicies
            .Where(p => p.Prefix != PolicyNamePrefix)
            .SelectMany(p => p.Executables.Select(exe => PolicyKeyFor(p.Prefix, exe)))
            .Any(key => registry.GetValue(RegistryHive2.LocalMachine, key, "Version") is not null);

    private const string NlaOwnedByCaoNote = "qos-nla-owned-by-cao";

    /// <summary>
    /// Si al capturar "Do not use NLA" ya lo había puesto otra política de CA-O,
    /// se anota: al revertir la última política activa se borra en vez de
    /// restaurar un valor que tampoco era del usuario.
    /// </summary>
    public override OptimizationSnapshot Capture(IRegistryAccessor registry)
    {
        var snapshot = base.Capture(registry);
        if (AnotherCaoPolicyIsActive(registry)) snapshot.RawNotes.Add(NlaOwnedByCaoNote);
        return snapshot;
    }

    public override async Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default)
    {
        var keepNla = AnotherCaoPolicyIsActive(context.Registry);
        var filtered = new OptimizationSnapshot { TimestampUtc = snapshot.TimestampUtc };
        foreach (var entry in snapshot.Registry)
        {
            var isNla = entry.KeyPath.Equals(NlaKey, StringComparison.OrdinalIgnoreCase)
                && entry.ValueName.Equals(NlaValue, StringComparison.OrdinalIgnoreCase);
            if (isNla && keepNla) continue;
            if (isNla && snapshot.RawNotes.Contains(NlaOwnedByCaoNote))
            {
                filtered.Registry.Add(entry with { Existed = false, Value = null });
                continue;
            }
            filtered.Registry.Add(entry);
        }
        filtered.RawNotes.AddRange(snapshot.RawNotes);
        filtered.ServiceStartTypes.AddRange(snapshot.ServiceStartTypes);
        var result = await base.RevertAsync(context, filtered, ct).ConfigureAwait(false);
        return keepNla && result.Success
            ? OperationResult.Ok("Políticas restauradas; 'Do not use NLA' se mantiene porque otra prioridad QoS de CA-O sigue activa.")
            : result;
    }
}
