using System.Diagnostics;
using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.Startup;

/// <summary>
/// Base para desactivar entradas reales de inicio (claves Run de HKCU/HKLM).
/// Clasifica por publicador (FileVersionInfo) y excluye antivirus, drivers,
/// componentes Microsoft y utilidades protegidas. Snapshot exacto por valor.
/// </summary>
public abstract class StartupRunKeyOptimization : IOptimization
{
    public abstract OptimizationDefinition Definition { get; }

    /// <summary>¿Debe desactivarse esta entrada (ya filtrada como no protegida)?</summary>
    protected abstract bool ShouldDisable(string name, string command, string? company);

    private static readonly (RegistryHive2 Hive, string Key)[] RunKeys =
    [
        (RegistryHive2.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run"),
        (RegistryHive2.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run"),
    ];

    private static readonly string[] ProtectedCompanies =
        ["microsoft", "norton", "mcafee", "kaspersky", "bitdefender", "eset", "avast", "avg",
         "sophos", "crowdstrike", "sentinelone", "malwarebytes", "nvidia", "amd", "intel",
         "realtek", "synaptics", "logitech"];

    private static readonly string[] ProtectedNameKeywords =
        ["defender", "securityhealth", "antivirus", "firewall", "backup", "driver", "windows "];

    protected static bool IsProtected(string name, string command, string? company)
    {
        var lowerName = name.ToLowerInvariant();
        if (ProtectedNameKeywords.Any(k => lowerName.Contains(k))) return true;
        if (!string.IsNullOrWhiteSpace(company) &&
            ProtectedCompanies.Any(c => company.Contains(c, StringComparison.OrdinalIgnoreCase))) return true;
        var exe = ExtractExePath(command);
        if (exe is null) return false;
        var lowerExe = exe.ToLowerInvariant();
        if (lowerExe.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.System).ToLowerInvariant(), StringComparison.Ordinal)) return true;
        if (lowerExe.Contains(@"\windowsapps\")) return true;
        return false;
    }

    protected static string? GetCompany(string command)
    {
        try
        {
            var exe = ExtractExePath(command);
            if (exe is null || !File.Exists(exe)) return null;
            return FileVersionInfo.GetVersionInfo(exe).CompanyName;
        }
        catch { return null; }
    }

    private static string? ExtractExePath(string command)
    {
        try
        {
            var trimmed = command.Trim();
            string candidate;
            if (trimmed.StartsWith('"'))
            {
                var end = trimmed.IndexOf('"', 1);
                if (end <= 1) return null;
                candidate = trimmed[1..end];
            }
            else
            {
                var token = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
                if (token is null) return null;
                candidate = token.Trim('"');
            }
            candidate = Environment.ExpandEnvironmentVariables(candidate);
            if (!candidate.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return null;
            return candidate;
        }
        catch { return null; }
    }

    private IReadOnlyList<(RegistryHive2 Hive, string Key, string Name, object Value, RegistryValueKind2 Kind)> Candidates(
        IRegistryAccessor registry, bool onlyDisabled)
    {
        var found = new List<(RegistryHive2, string, string, object, RegistryValueKind2)>();
        foreach (var (hive, key) in RunKeys)
        {
            foreach (var name in registry.GetValueNames(hive, key))
            {
                var raw = registry.GetValueRaw(hive, key, name, out var kind);
                if (raw is null) continue;
                var command = raw.ToString() ?? string.Empty;
                var company = GetCompany(command);
                if (IsProtected(name, command, company)) continue;
                if (ShouldDisable(name, command, company) == onlyDisabled) continue;
                found.Add((hive, key, name, raw, kind));
            }
        }
        return found;
    }

    /// <summary>Entradas que esta optimización desactivaría ahora mismo.</summary>
    protected IReadOnlyList<(RegistryHive2 Hive, string Key, string Name, object Value, RegistryValueKind2 Kind)> Pending(
        IRegistryAccessor registry) => Candidates(registry, onlyDisabled: false);

    public OptimizationState Detect(IRegistryAccessor registry) =>
        Pending(registry).Count == 0 ? OptimizationState.AppliedByCao : OptimizationState.NotApplied;

    public OptimizationSnapshot Capture(IRegistryAccessor registry)
    {
        var snapshot = new OptimizationSnapshot();
        foreach (var (hive, key) in RunKeys)
        {
            foreach (var name in registry.GetValueNames(hive, key))
            {
                var raw = registry.GetValueRaw(hive, key, name, out var kind);
                if (raw is null) continue;
                snapshot.Registry.Add(new RegistrySnapshotEntry(
                    hive.ToString(), key, name, raw, Existed: true)
                { Kind = kind });
            }
        }
        return snapshot;
    }

    public Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        var pending = Pending(context.Registry);
        foreach (var (hive, key, name, _, _) in pending)
        {
            context.Registry.DeleteValue(hive, key, name);
        }
        return Task.FromResult(pending.Count == 0
            ? OperationResult.Ok("No quedaban entradas de inicio para desactivar.")
            : OperationResult.Ok($"Entradas de inicio desactivadas: {pending.Count}."));
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
        return Task.FromResult(OperationResult.Ok("Entradas de inicio restauradas desde el snapshot."));
    }

    public Task<VerificationResult> VerifyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        var observed = Detect(context.Registry);
        return Task.FromResult(observed switch
        {
            OptimizationState.AppliedByCao =>
                VerificationResult.Passed(observed, "Inicio verificado sin entradas pendientes."),
            _ => VerificationResult.Failed(observed, "Aún quedan entradas de inicio sin desactivar."),
        });
    }

    public Task<OptimizationPreview> PreviewAsync(IRegistryAccessor registry, CancellationToken ct = default)
    {
        var lines = Pending(registry).Select(p => new PreviewLine
        {
            Kind = "Registry",
            Target = $@"{p.Hive}\{p.Key}\{p.Name}",
            Before = p.Value?.ToString() ?? string.Empty,
            After = "(eliminado)",
        }).ToList();

        if (lines.Count == 0)
        {
            lines.Add(new PreviewLine
            {
                Kind = "Registry",
                Target = @"HKCU/HKLM\...\CurrentVersion\Run",
                Before = "sin entradas pendientes",
                After = "sin cambios",
            });
        }

        var definition = Definition;
        return Task.FromResult(new OptimizationPreview
        {
            OptimizationId = definition.Id,
            Lines = lines,
            Risk = definition.Risk,
            SecurityImpact = definition.SecurityImpact,
            Flags = definition.Flags,
        });
    }
}
