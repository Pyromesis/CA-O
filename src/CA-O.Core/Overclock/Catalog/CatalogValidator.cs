namespace CAO.Core.Overclock.OverclockCatalog;

/// <summary>Problem found while validating the catalog.</summary>
public sealed record CatalogViolation(KnobId? Knob, string Rule, string MessageKey);

/// <summary>Result of validating a knob set: valid, or the reasons it is not.</summary>
public sealed record CatalogValidation(IReadOnlyList<CatalogViolation> Violations)
{
    public bool IsValid => Violations.Count == 0;

    public static CatalogValidation Valid { get; } = new(Array.Empty<CatalogViolation>());
}

/// <summary>
/// Validates the declarative knob set before any of it reaches the hardware:
/// unique ids, coherent ranges, no missing dependency targets, no cycles, and
/// no <c>Unknown</c> evidence in the default profile (the same rule the 93-item
/// optimization catalog already follows).
/// </summary>
public static class CatalogValidator
{
    public static CatalogValidation Validate(IReadOnlyList<OverclockKnob> knobs)
    {
        var violations = new List<CatalogViolation>();

        var duplicates = knobs
            .GroupBy(k => k.Id.Value, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        foreach (var duplicate in duplicates)
        {
            violations.Add(new CatalogViolation(new KnobId(duplicate), "duplicate-id",
                "catalog_knob_duplicate"));
        }

        foreach (var knob in knobs)
        {
            ValidateRange(knob, violations);
            ValidateEvidence(knob, violations);
            ValidateDependenciesExist(knob, knobs, violations);
        }

        foreach (var cycle in FindCycles(knobs))
        {
            violations.Add(new CatalogViolation(cycle, "circular-dependency", "catalog_knob_circular"));
        }

        return new CatalogValidation(violations);
    }

    private static void ValidateRange(OverclockKnob knob, ICollection<CatalogViolation> violations)
    {
        if (knob.Kind == KnobKind.Scalar)
        {
            if (knob.Range.Min > knob.Range.Max)
            {
                violations.Add(new CatalogViolation(knob.Id, "inverted-range", "catalog_knob_range_inverted"));
            }

            if (knob.Step <= 0)
            {
                violations.Add(new CatalogViolation(knob.Id, "non-positive-step", "catalog_knob_step_invalid"));
            }

            return;
        }

        if (knob.Range.Allowed is not { Count: > 0 })
        {
            violations.Add(new CatalogViolation(knob.Id, "empty-discrete-range", "catalog_knob_range_empty"));
        }
    }

    private static void ValidateEvidence(OverclockKnob knob, ICollection<CatalogViolation> violations)
    {
        if (knob.Evidence == Shared.EvidenceLevel.Unknown)
        {
            violations.Add(new CatalogViolation(knob.Id, "unknown-evidence", "catalog_knob_evidence_unknown"));
        }
    }

    private static void ValidateDependenciesExist(
        OverclockKnob knob,
        IReadOnlyList<OverclockKnob> knobs,
        ICollection<CatalogViolation> violations)
    {
        var known = knobs.Select(k => k.Id.Value).ToHashSet(StringComparer.Ordinal);

        foreach (var dependency in knob.DependsOn.Where(d => !known.Contains(d.Other.Value)))
        {
            violations.Add(new CatalogViolation(knob.Id, "dangling-dependency", "catalog_knob_dependency_missing"));
        }

        if (knob.DependsOn.Any(d => d.Other == knob.Id))
        {
            violations.Add(new CatalogViolation(knob.Id, "self-dependency", "catalog_knob_dependency_self"));
        }
    }

    /// <summary>
    /// Finds every knob that participates in a Requires/PairsWith cycle. A cycle
    /// would make the catalog unsatisfiable, so it is a catalog error rather
    /// than a runtime surprise.
    /// </summary>
    private static IEnumerable<KnobId> FindCycles(IReadOnlyList<OverclockKnob> knobs)
    {
        // GroupBy antes de construir el diccionario: un id duplicado ya se ha reportado
        // como violación, pero el recorrido de ciclos no puede reventar con la excepcion
        // de clave duplicada de ToDictionary. El validador tiene que devolver TODAS las
        // violaciones, no morir en la primera.
        var edges = knobs
            .GroupBy(k => k.Id.Value, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group
                    .SelectMany(k => k.DependsOn)
                    .Where(d => d.Kind is DependencyKind.Requires or DependencyKind.PairsWith)
                    .Select(d => d.Other.Value)
                    .Where(known => group.Key != known)
                    .ToHashSet(StringComparer.Ordinal),
                StringComparer.Ordinal);

        var state = new Dictionary<string, int>(StringComparer.Ordinal);
        var inCycle = new HashSet<string>(StringComparer.Ordinal);

        foreach (var id in edges.Keys)
        {
            Visit(id, edges, state, inCycle);
        }

        return inCycle.Select(v => new KnobId(v)).OrderBy(v => v.Value, StringComparer.Ordinal);
    }

    private static void Visit(
        string node,
        Dictionary<string, HashSet<string>> edges,
        Dictionary<string, int> state,
        HashSet<string> inCycle)
    {
        if (state.TryGetValue(node, out var mark))
        {
            if (mark == 1)
            {
                inCycle.Add(node);
            }

            return;
        }

        state[node] = 1;

        if (edges.TryGetValue(node, out var targets))
        {
            foreach (var next in targets.OrderBy(t => t, StringComparer.Ordinal))
            {
                Visit(next, edges, state, inCycle);
            }
        }

        state[node] = 2;
    }
}