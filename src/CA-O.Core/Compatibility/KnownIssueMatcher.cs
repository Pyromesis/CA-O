using CAO.Shared;

namespace CAO.Core.Compatibility;

/// <summary>A known issue that matches the current machine context (spec 55).</summary>
public sealed record KnownIssueFinding(KnownIssue Issue, string MatchReasonEs);

/// <summary>
/// Matches the versioned known-issues database against the measured system
/// context. Matching is conservative: only explicit, non-sample entries are
/// surfaced, and every finding explains WHY it matched.
/// </summary>
public static class KnownIssueMatcher
{
    public static IReadOnlyList<KnownIssueFinding> Match(
        IReadOnlyList<KnownIssue> database,
        SystemContext context,
        InterruptPressureReport? interrupts = null)
    {
        var findings = new List<KnownIssueFinding>();

        foreach (var issue in database)
        {
            if (issue.Status == KnownIssueStatus.Sample)
            {
                continue;
            }

            if (issue.MinWindowsBuild is not null && context.WindowsBuild < issue.MinWindowsBuild ||
                issue.MaxWindowsBuild is not null && context.WindowsBuild > issue.MaxWindowsBuild)
            {
                continue;
            }

            if (issue.GpuVendorOrDriverContains is not null &&
                !ContainsAny(context.GpuName + " " + context.GpuDriverVersion, issue.GpuVendorOrDriverContains))
            {
                continue;
            }

            if (issue.GameNameContains is not null &&
                !context.GamesDetected.Any(game => Contains(game, issue.GameNameContains)))
            {
                continue;
            }

            string reason;
            if (issue.ComponentKind is not null)
            {
                // Component-scoped issues require observed interrupt pressure.
                if (interrupts is null || interrupts.TotalMaxDpcPercent < 10)
                {
                    continue;
                }
                reason = $"Presión DPC medida ({interrupts.TotalMaxDpcPercent:0.#}%) en componente '{issue.ComponentKind}'.";
            }
            else
            {
                reason = "Coincidencia de versión de Windows/driver.";
            }

            findings.Add(new KnownIssueFinding(issue, reason));
        }

        return findings;
    }

    private static bool ContainsAny(string haystack, string needle) =>
        haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);

    private static bool Contains(string haystack, string needle) =>
        haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
}
