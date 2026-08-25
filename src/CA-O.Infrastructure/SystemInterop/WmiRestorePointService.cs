using System.Management;
using CAO.Core.Abstractions;
using CAO.Shared;

namespace CAO.Infrastructure.SystemInterop;

/// <summary>System restore via the SystemRestore WMI class.</summary>
public sealed class WmiRestorePointService : IRestorePointService
{
    public async Task<(bool Success, string ReasonEs)> CreateAsync(string description, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                var scope = new ManagementScope(@"root\default");
                scope.Connect();

                using var sysRestoreClass = new ManagementClass(scope, new ManagementPath("SystemRestore"), null);
                var inParams = sysRestoreClass.GetMethodParameters("CreateRestorePoint");
                inParams["Description"] = description;
                inParams["RestorePointType"] = 12; // MODIFY_SETTINGS
                inParams["EventType"] = 100;       // BEGIN_SYSTEM_CHANGE

                var outParams = sysRestoreClass.InvokeMethod("CreateRestorePoint", inParams, null);
                return outParams?["ReturnValue"] is null || Convert.ToUInt32(outParams["ReturnValue"]) == 0
                    ? (true, "Punto de restauración creado.")
                    : (false, "El sistema devolvió un error al crear el punto (código " + outParams["ReturnValue"] + ").");
            }
            catch (Exception ex)
            {
                return (false, "No se pudo crear el punto de restauración: " + ex.Message.Trim());
            }
        }, ct);
    }

    public async Task<IReadOnlyList<RestorePointInfo>> ListAsync(CancellationToken ct = default)
    {
        return await Task.Run<IReadOnlyList<RestorePointInfo>>(() =>
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(@"root\default", "SELECT * FROM SystemRestore");
                return searcher.Get().Cast<ManagementObject>()
                    .Select(o => new RestorePointInfo
                    {
                        CreationTime = ToDateTime(o["CreationTime"]),
                        Description = o["Description"]?.ToString() ?? string.Empty,
                        SequenceNumber = Convert.ToInt32(o["SequenceNumber"] ?? 0),
                    })
                    .OrderByDescending(r => r.CreationTime)
                    .ToList();
            }
            catch
            {
                return Array.Empty<RestorePointInfo>();
            }
        }, ct);
    }

    private static DateTime ToDateTime(object? dmtf) =>
        ManagementDateTimeConverter.ToDateTime(dmtf?.ToString() ?? string.Empty);
}
