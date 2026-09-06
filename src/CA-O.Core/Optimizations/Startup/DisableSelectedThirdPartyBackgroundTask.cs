using System.Text;
using CAO.Core.Abstractions;
using CAO.Shared;
using CAO.Shared.Security;

namespace CAO.Core.Optimizations.Startup;

/// <summary>
/// Disables enabled non-Microsoft scheduled tasks (excluding updaters and
/// security tooling) via schtasks. Exact disabled list captured for rollback.
/// </summary>
public sealed class DisableSelectedThirdPartyBackgroundTask : IOptimization
{
    private static readonly string[] ExcludedKeywords =
        ["update", "defender", "security", "antivirus", "firewall", "backup", "office"];

    /// <summary>
    /// Índice de tareas desactivadas por esta optimización (solo nombres;
    /// la mutación real vive en el Programador de tareas, esto solo la lista
    /// para poder revertirla y se limpia al revertir).
    /// </summary>
    private const string MarkerKey = @"Software\CA-O\DisabledTasks";

    public OptimizationDefinition Definition => new()
    {
        Id = "disable-selected-third-party-background-task",
        NameEs = "Desactivar tareas segundo plano terceros",
        NameEn = "Disable third-party background tasks",
        DescriptionEs = "Desactiva tareas programadas de terceros habilitadas. Excluye Microsoft, Update y Defender.",
        DescriptionEn = "Disables enabled third-party scheduled tasks. Excludes Microsoft, Update and Defender.",
        TooltipEs = "Ejecuta schtasks /Change /TN ... /DISABLE solo en tareas no Microsoft. Reversible con /ENABLE.",
        Category = OptimizationCategory.Performance,
        ExpectedImpact = PerformanceImpact.Small,
        Evidence = EvidenceLevel.Empirical,
        Confidence = Confidence.Medium,
        AntiCheatImpact = AntiCheatImpact.None,
        Risk = RiskLevel.Moderate,
        Compatibility = CompatibilityStatus.Compatible,
        SecurityImpact = SecurityImpact.None,
        Impact = ImpactLevel.Low,
    };

    public OptimizationState Detect(IRegistryAccessor registry) =>
        registry.GetValueNames(RegistryHive2.CurrentUser, MarkerKey).Count > 0
            ? OptimizationState.AppliedByCao
            : OptimizationState.NotApplied;

    public OptimizationSnapshot Capture(IRegistryAccessor registry)
    {
        var snapshot = new OptimizationSnapshot();
        foreach (var name in registry.GetValueNames(RegistryHive2.CurrentUser, MarkerKey))
        {
            snapshot.RawNotes.Add("task=" + name);
        }
        return snapshot;
    }

    private static IReadOnlyList<string> EnabledThirdPartyTasks(string csv)
    {
        var rows = ParseCsv(csv);
        if (rows.Count < 2) return [];
        var header = rows[0];
        var nameIdx = IndexOf(header, "TaskName");
        var stateIdx = IndexOf(header, "Scheduled Task State");
        var statusIdx = IndexOf(header, "Status");
        if (nameIdx < 0) return [];

        var found = new List<string>();
        foreach (var row in rows.Skip(1))
        {
            if (nameIdx >= row.Count) continue;
            var name = row[nameIdx].Trim();
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (name.StartsWith(@"\Microsoft", StringComparison.OrdinalIgnoreCase)) continue;
            if (ExcludedKeywords.Any(k => name.Contains(k, StringComparison.OrdinalIgnoreCase))) continue;
            var enabled = stateIdx >= 0 && stateIdx < row.Count
                ? row[stateIdx].Trim().Equals("Enabled", StringComparison.OrdinalIgnoreCase)
                : statusIdx >= 0 && statusIdx < row.Count &&
                  (row[statusIdx].Trim().Equals("Ready", StringComparison.OrdinalIgnoreCase) ||
                   row[statusIdx].Trim().Equals("Running", StringComparison.OrdinalIgnoreCase));
            if (enabled) found.Add(name);
        }
        return found;
    }

    private static int IndexOf(IReadOnlyList<string> header, string column)
    {
        for (var i = 0; i < header.Count; i++)
        {
            if (header[i].Trim().Trim('"').Equals(column, StringComparison.OrdinalIgnoreCase)) return i;
        }
        return -1;
    }

    private static List<List<string>> ParseCsv(string csv)
    {
        var rows = new List<List<string>>();
        foreach (var rawLine in csv.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = new List<string>();
            var current = new StringBuilder();
            var inQuotes = false;
            foreach (var ch in rawLine)
            {
                if (ch == '"') { inQuotes = !inQuotes; continue; }
                if (ch == ',' && !inQuotes) { fields.Add(current.ToString()); current.Clear(); continue; }
                current.Append(ch);
            }
            fields.Add(current.ToString());
            rows.Add(fields);
        }
        return rows;
    }

    public async Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (context.Executor is null)
            return OperationResult.Fail("Ejecutor no disponible.", "CAO-SEC-010");

        var query = await context.Executor.ExecuteAsync(
            SystemCommandKey.SchTasksQuery, ["/Query", "/FO", "CSV", "/V"], ct);
        if (!query.Success)
            return OperationResult.Fail("No se pudieron enumerar las tareas programadas.", query.StdErr);

        var targets = EnabledThirdPartyTasks(query.StdOut);
        if (targets.Count == 0)
            return OperationResult.Ok("No quedaban tareas de terceros habilitadas.");

        var disabled = new List<string>();
        foreach (var task in targets)
        {
            var result = await context.Executor.ExecuteAsync(
                SystemCommandKey.SchTasksDisable, ["/Change", "/TN", task, "/DISABLE"], ct);
            if (result.Success) disabled.Add(task);
        }

        foreach (var task in disabled)
        {
            context.Registry.SetValue(
                RegistryHive2.CurrentUser, MarkerKey, task, 1, RegistryValueKind2.DWord);
        }

        return disabled.Count > 0
            ? OperationResult.Ok($"Tareas de terceros desactivadas: {disabled.Count}.")
            : OperationResult.Fail("No se pudo desactivar ninguna tarea.", "apply-failed");
    }

    public async Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default)
    {
        if (context.Executor is null)
            return OperationResult.Fail("Ejecutor no disponible.", "CAO-SEC-010");

        var names = snapshot.RawNotes
            .Where(n => n.StartsWith("task=", StringComparison.Ordinal))
            .Select(n => n["task=".Length..])
            .ToList();
        if (names.Count == 0)
        {
            names = context.Registry
                .GetValueNames(RegistryHive2.CurrentUser, MarkerKey)
                .ToList();
        }
        if (names.Count == 0)
            return OperationResult.Ok("Sin tareas registradas para revertir.");

        foreach (var task in names)
        {
            await context.Executor.ExecuteAsync(
                SystemCommandKey.SchTasksEnable, ["/Change", "/TN", task, "/ENABLE"], ct);
            context.Registry.DeleteValue(RegistryHive2.CurrentUser, MarkerKey, task);
        }
        return OperationResult.Ok($"Tareas reactivadas: {names.Count}.");
    }

    public async Task<VerificationResult> VerifyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        if (context.Executor is null)
            return VerificationResult.Unknown(OptimizationState.Unknown, "Ejecutor no disponible.");

        var query = await context.Executor.ExecuteAsync(
            SystemCommandKey.SchTasksQuery, ["/Query", "/FO", "CSV", "/V"], ct);
        if (!query.Success)
            return VerificationResult.Unknown(OptimizationState.Unknown, "No se pudo verificar: " + query.StdErr);

        return EnabledThirdPartyTasks(query.StdOut).Count == 0
            ? VerificationResult.Passed(OptimizationState.AppliedByCao, "Sin tareas de terceros habilitadas.")
            : VerificationResult.Failed(OptimizationState.NotApplied, "Aún quedan tareas de terceros habilitadas.");
    }

    public Task<PreconditionResult> CheckPreconditionsAsync(SystemContext context, CancellationToken ct = default) =>
        Task.FromResult(PreconditionResult.Ok("Sin precondiciones específicas."));

    public Task<OptimizationPreview> PreviewAsync(IRegistryAccessor registry, CancellationToken ct = default) =>
        Task.FromResult(new OptimizationPreview
        {
            OptimizationId = Definition.Id,
            Lines =
            [
                new PreviewLine
                {
                    Kind = "TaskScheduler",
                    Target = "schtasks /Change /TN <terceros> /DISABLE",
                    Before = "tareas de terceros habilitadas",
                    After = "deshabilitadas (revertible con /ENABLE)",
                },
            ],
            Risk = Definition.Risk,
            SecurityImpact = Definition.SecurityImpact,
            Flags = Definition.Flags,
        });
}
