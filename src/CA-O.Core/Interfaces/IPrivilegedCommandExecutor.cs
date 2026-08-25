using CAO.Shared.Security;

namespace CAO.Core.Interfaces;

/// <summary>
/// The single funnel for privileged process execution (FASE 4). External
/// layers never call Process.Start directly; every spawn must resolve
/// through <see cref="CommandPolicy.Resolve"/> to a canonical absolute path.
/// </summary>
public interface IPrivilegedCommandExecutor
{
    Task<PrivilegedCommandResult> ExecuteAsync(
        SystemCommandKey key,
        IReadOnlyList<string> arguments,
        CancellationToken ct = default);
}
