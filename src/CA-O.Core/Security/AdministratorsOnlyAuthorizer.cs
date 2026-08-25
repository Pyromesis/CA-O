using CAO.Shared.Security;

namespace CAO.Core.Security;

/// <summary>Reason codes emitted by CA-O authorization policy.</summary>
public static class AuthorizationReasons
{
    public const string AdminAllowed = "CAO-SEC-001";
    public const string ConfiguredSidAllowed = "CAO-SEC-002";
    public const string StandardUserDenied = "CAO-SEC-003";
    public const string UnknownCallerDenied = "CAO-SEC-004";
    public const string SidNotConfigured = "CAO-SEC-005";
}

/// <summary>
/// Default authorization policy (FASE 2): only members of the local
/// Administrators group, plus any explicitly configured SID (allow-list for
/// service accounts), may invoke privileged operations. Standard users are
/// denied regardless of operation. The decision is made from the token
/// facts — group membership and elevation state — never from names.
/// </summary>
public sealed class AdministratorsOnlyAuthorizer : IPrivilegedCallerAuthorizer
{
    private readonly IReadOnlySet<string> _extraAuthorizedSids;

    public AdministratorsOnlyAuthorizer(IEnumerable<string>? extraAuthorizedSids = null)
    {
        _extraAuthorizedSids = new HashSet<string>(extraAuthorizedSids ?? [], StringComparer.OrdinalIgnoreCase);
        KnownReasonCodes =
        [
            AuthorizationReasons.AdminAllowed,
            AuthorizationReasons.ConfiguredSidAllowed,
            AuthorizationReasons.StandardUserDenied,
            AuthorizationReasons.UnknownCallerDenied,
            AuthorizationReasons.SidNotConfigured,
        ];
    }

    public IReadOnlyCollection<string> KnownReasonCodes { get; }

    public AuthorizationResult Authorize(CallerIdentity caller)
    {
        if (caller is null ||
            string.IsNullOrWhiteSpace(caller.Sid) || caller.Sid.Contains('\\') || caller.Sid.Contains('/'))
        {
            var unknown = caller ?? new CallerIdentity("S-0-0", "?", false, false, -1);
            return AuthorizationResult.Denied(unknown, AuthorizationReasons.UnknownCallerDenied);
        }

        if (caller.IsAdministrator && caller.IsElevated)
        {
            return AuthorizationResult.AllowedFor(caller, AuthorizationReasons.AdminAllowed);
        }

        // A token carrying the admin group but not elevated is still a
        // filtered token: treat as standard until elevated (RunAs).
        if (!caller.IsAdministrator && _extraAuthorizedSids.Contains(caller.Sid))
        {
            return AuthorizationResult.AllowedFor(caller, AuthorizationReasons.ConfiguredSidAllowed);
        }

        return AuthorizationResult.Denied(
            caller,
            caller.IsAdministrator
                ? AuthorizationReasons.StandardUserDenied
                : AuthorizationReasons.SidNotConfigured);
    }
}
