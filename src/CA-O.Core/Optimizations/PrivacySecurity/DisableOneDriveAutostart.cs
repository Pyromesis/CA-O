using System.Diagnostics;
using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Core.Optimizations.PrivacySecurity;

/// <summary>
/// #15: disable OneDrive autostart for the current user. Expert-only.
/// The exact Run value is snapshotted and restored on revert; the app is
/// NOT uninstalled (that would be an irreversible action).
/// </summary>
public sealed class DisableOneDriveAutostart : IOptimization
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "OneDrive";

    public OptimizationDefinition Definition => new()
    {
        Id = Id,
        NameEs = "Desactivar inicio automático de OneDrive",
        NameEn = "Disable OneDrive autostart",
        DescriptionEs = "Evita que OneDrive arranque con la sesión. No desinstala la app.",
        DescriptionEn = "Stops OneDrive from launching at logon. Does NOT uninstall the app.",
        TooltipEs = "Elimina la entrada Run de OneDrive (capturada para restaurarla). Si usas OneDrive, no actives esto.",
        Category = OptimizationCategory.PrivacySecurity,
        Impact = ImpactLevel.Medium,
        Flags = OptimizationFlags.ExpertOnly,
    };

    private static string Id => "disable-onedrive-autostart";

    /// <summary>Current Run entry injected by the engine before Detect/Capture.</summary>
    public object? ObservedRunValue { get; set; }

    public OptimizationState Detect(IRegistryAccessor registry) =>
        registry.GetValue(RegistryHive2.CurrentUser, RunKeyPath, ValueName) is null
            ? OptimizationState.AppliedByCao
            : OptimizationState.NotApplied;

    public OptimizationSnapshot Capture(IRegistryAccessor registry)
    {
        var snapshot = new OptimizationSnapshot();
        var value = ObservedRunValue ?? registry.GetValue(RegistryHive2.CurrentUser, RunKeyPath, ValueName);
        snapshot.Registry.Add(new RegistrySnapshotEntry(
            RegistryHive2.CurrentUser.ToString(), RunKeyPath, ValueName, value, Existed: value is not null));
        return snapshot;
    }

    public Task<OperationResult> ApplyAsync(OptimizationContext context, CancellationToken ct = default)
    {
        try
        {
            foreach (var process in Process.GetProcessesByName("OneDrive"))
            {
                process.Kill(entireProcessTree: true);
                process.Dispose();
            }
        }
        catch
        {
            // Best effort: the Run-key removal is what matters at next logon.
        }

        context.Registry.DeleteValue(RegistryHive2.CurrentUser, RunKeyPath, ValueName);
        return Task.FromResult(OperationResult.Ok("Inicio automático de OneDrive desactivado."));
    }

    public Task<OperationResult> RevertAsync(OptimizationContext context, OptimizationSnapshot snapshot, CancellationToken ct = default)
    {
        var entry = snapshot.Registry.FirstOrDefault(e =>
            e.ValueName == ValueName && e.KeyPath == RunKeyPath);

        if (entry is not null && entry.Existed && entry.Value is not null)
        {
            context.Registry.SetValue(RegistryHive2.CurrentUser, RunKeyPath, ValueName, entry.Value, RegistryValueKind2.String);
            return Task.FromResult(OperationResult.Ok("Entrada Run de OneDrive restaurada."));
        }
        return Task.FromResult(OperationResult.Ok("No había entrada de inicio que restaurar."));
    }
}
