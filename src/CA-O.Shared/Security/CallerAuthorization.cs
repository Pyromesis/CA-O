namespace CAO.Shared.Security;

/// <summary>
/// Identity facts extracted from the caller's Windows token after pipe
/// impersonation (FASE 2). Authorization decisions are made from these
/// facts — never from the raw user name alone.
/// </summary>
public sealed record CallerIdentity(
    string Sid,
    string Name,
    bool IsAdministrator,
    bool IsElevated,
    int SessionId);

/// <summary>Structured authorization decision.</summary>
public sealed record AuthorizationResult(
    bool Allowed,
    string ReasonCode,
    string CallerSid,
    string CallerName,
    bool IsAdministrator,
    bool IsElevated,
    int SessionId)
{
    public static AuthorizationResult Denied(CallerIdentity caller, string reasonCode) =>
        new(false, reasonCode, caller.Sid, caller.Name, caller.IsAdministrator, caller.IsElevated, caller.SessionId);

    public static AuthorizationResult AllowedFor(CallerIdentity caller, string reasonCode) =>
        new(true, reasonCode, caller.Sid, caller.Name, caller.IsAdministrator, caller.IsElevated, caller.SessionId);
}

/// <summary>
/// Authorizes a privileged caller for an operation (FASE 2). Implemented in
/// the privileged host against real Windows tokens; unit tests use fakes.
/// </summary>
public interface IPrivilegedCallerAuthorizer
{
    /// <summary>Well-known reason codes returned in AuthorizationResult.ReasonCode.</summary>
    IReadOnlyCollection<string> KnownReasonCodes { get; }

    AuthorizationResult Authorize(CallerIdentity caller);
}
