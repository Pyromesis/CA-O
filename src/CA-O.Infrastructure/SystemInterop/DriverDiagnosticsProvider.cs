using System.Management;
using CAO.Shared;

namespace CAO.Infrastructure.SystemInterop;

public sealed class DriverDiagnosticsProvider
{
    private static readonly TimeSpan WmiTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Inventario total estilo Administrador de dispositivos: SetupAPI enumera
    /// TODOS (presentes + ocultos, con y sin controlador) y WMI enriquece con
    /// versión/fecha/firma/INF. WMI solo jamás basta: omite sin-driver (28),
    /// ocultos y sin firma.
    /// </summary>
    public async Task<DriverDiagnosticsReport> MeasureAsync(CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var devices = SetupApiDeviceEnumerator.EnumerateAll(ct);
            var wmi = ReadWmi(ct);

            var drivers = new List<DriverDiagnostic>(devices.Count + wmi.Count);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var dev in devices)
            {
                ct.ThrowIfCancellationRequested();
                wmi.TryGetValue(dev.InstanceId, out var row);
                var record = Merge(dev, row);
                if (string.IsNullOrWhiteSpace(record.Name)) continue;
                seen.Add(dev.InstanceId);
                drivers.Add(record);
            }
            // Respaldo: filas WMI que SetupAPI no vio (no debería pasar, pero
            // antes eran la única fuente: no se pierde nada).
            foreach (var (id, row) in wmi)
            {
                ct.ThrowIfCancellationRequested();
                if (!seen.Add(id)) continue;
                if (string.IsNullOrWhiteSpace(row.Name)) continue;
                drivers.Add(new DriverDiagnostic(
                    row.Name, row.DeviceClass, row.Manufacturer, row.Version, row.Date,
                    row.IsSigned, string.IsNullOrWhiteSpace(row.Status) ? "OK" : row.Status,
                    row.ProblemCode, row.DeviceId, row.HardwareId, row.InfName, row.Provider, IsPresent: true));
            }

            return new DriverDiagnosticsReport(drivers, DateTime.UtcNow);
        }, ct);
    }

    /// <summary>
    /// Cruce SetupAPI (identidad viva: nombre, clase, fabricante, problema,
    /// presencia) + WMI (detalle del driver: versión, fecha, firma, INF).
    /// Gana el dato vivo; WMI rellena lo que SetupAPI no trae (firma).
    /// </summary>
    internal static DriverDiagnostic Merge(SetupApiDevice dev, WmiDriverInfo? row)
    {
        var name = !string.IsNullOrWhiteSpace(dev.FriendlyName) ? dev.FriendlyName
            : !string.IsNullOrWhiteSpace(dev.Description) ? dev.Description
            : row?.Name ?? string.Empty;
        var deviceClass = !string.IsNullOrWhiteSpace(dev.DeviceClass) ? dev.DeviceClass : row?.DeviceClass ?? string.Empty;
        var manufacturer = !string.IsNullOrWhiteSpace(dev.Manufacturer) ? dev.Manufacturer : row?.Manufacturer ?? string.Empty;
        var version = row is not null && !string.IsNullOrWhiteSpace(row.Version) ? row.Version : dev.DriverVersion;
        var date = row is not null && !string.IsNullOrWhiteSpace(row.Date) ? row.Date : dev.DriverDate;
        var inf = row is not null && !string.IsNullOrWhiteSpace(row.InfName) ? row.InfName : dev.InfPath;
        var provider = row is not null && !string.IsNullOrWhiteSpace(row.Provider) ? row.Provider
            : !string.IsNullOrWhiteSpace(dev.DriverProvider) ? dev.DriverProvider : string.Empty;
        // El problema vivo (CM) manda; si SetupAPI no lo midió (-1), vale el WMI.
        var problem = dev.ProblemCode >= 0 ? dev.ProblemCode : row?.ProblemCode ?? 0;
        var status = row is not null && !string.IsNullOrWhiteSpace(row.Status) ? row.Status
            : dev.IsPresent ? "OK" : "No presente";
        var hwId = dev.HardwareIds.Length > 0 ? dev.HardwareIds[0] : row?.HardwareId ?? string.Empty;
        return new DriverDiagnostic(
            name, deviceClass, manufacturer, version, date, row?.IsSigned, status, problem,
            dev.InstanceId, hwId, inf, provider, dev.IsPresent);
    }

    private static Dictionary<string, WmiDriverInfo> ReadWmi(CancellationToken ct)
    {
        var map = new Dictionary<string, WmiDriverInfo>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var opts = new System.Management.EnumerationOptions { Timeout = WmiTimeout, BlockSize = 20, Rewindable = false };
            using var searcher = new ManagementObjectSearcher(
                new System.Management.ManagementScope(@"root\cimv2"),
                new System.Management.ObjectQuery("SELECT DeviceName, DeviceClass, Manufacturer, DriverVersion, DriverDate, IsSigned, Status, ConfigManagerErrorCode, DeviceID, HardwareID, InfName, DriverProviderName FROM Win32_PnPSignedDriver"),
                opts);
            foreach (var device in searcher.Get().Cast<ManagementObject>())
            {
                ct.ThrowIfCancellationRequested();
                var id = device["DeviceID"]?.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(id) || map.ContainsKey(id)) continue;
                map[id] = new WmiDriverInfo(
                    id,
                    device["DeviceName"]?.ToString() ?? string.Empty,
                    device["DeviceClass"]?.ToString() ?? string.Empty,
                    device["Manufacturer"]?.ToString() ?? string.Empty,
                    device["DriverVersion"]?.ToString() ?? string.Empty,
                    device["DriverDate"]?.ToString() ?? string.Empty,
                    device["IsSigned"] is bool signed ? signed : null,
                    device["Status"]?.ToString() ?? string.Empty,
                    Convert.ToInt32(device["ConfigManagerErrorCode"] ?? 0),
                    (device["HardwareID"] as string[])?.FirstOrDefault() ?? string.Empty,
                    device["InfName"]?.ToString() ?? string.Empty,
                    device["DriverProviderName"]?.ToString() ?? string.Empty);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (ManagementException)
        {
            // WMI may be unavailable or restricted; SetupAPI ya listó todo.
        }
        return map;
    }

    /// <summary>
    /// Identidad del equipo para buscar drivers originales del fabricante.
    /// Lee sistema + placa base + BIOS: los clónicos/VM mienten en sistema
    /// ("Default string") y la placa suele decir la verdad.
    /// </summary>
    public async Task<ComputerInfo> GetComputerInfoAsync(CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            string manufacturer = string.Empty, model = string.Empty, serial = string.Empty;
            string boardMaker = string.Empty, boardProduct = string.Empty, biosMaker = string.Empty;
            try
            {
                var opts = new System.Management.EnumerationOptions { Timeout = WmiTimeout, BlockSize = 20, Rewindable = false };
                var scope = new System.Management.ManagementScope(@"root\cimv2");
                using var system = new ManagementObjectSearcher(
                    scope,
                    new System.Management.ObjectQuery("SELECT Manufacturer, Model FROM Win32_ComputerSystem"),
                    opts);
                foreach (var item in system.Get().Cast<ManagementObject>())
                {
                    manufacturer = item["Manufacturer"]?.ToString() ?? string.Empty;
                    model = item["Model"]?.ToString() ?? string.Empty;
                    break;
                }
                using var board = new ManagementObjectSearcher(
                    scope,
                    new System.Management.ObjectQuery("SELECT Manufacturer, Product FROM Win32_BaseBoard"),
                    opts);
                foreach (var item in board.Get().Cast<ManagementObject>())
                {
                    boardMaker = item["Manufacturer"]?.ToString() ?? string.Empty;
                    boardProduct = item["Product"]?.ToString() ?? string.Empty;
                    break;
                }
                using var bios = new ManagementObjectSearcher(
                    scope,
                    new System.Management.ObjectQuery("SELECT Manufacturer, SerialNumber FROM Win32_BIOS"),
                    opts);
                foreach (var item in bios.Get().Cast<ManagementObject>())
                {
                    biosMaker = item["Manufacturer"]?.ToString() ?? string.Empty;
                    serial = item["SerialNumber"]?.ToString() ?? string.Empty;
                    break;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (ManagementException) { }
            ct.ThrowIfCancellationRequested();
            return new ComputerInfo(manufacturer.Trim(), model.Trim(), serial.Trim(),
                boardMaker.Trim(), boardProduct.Trim(), biosMaker.Trim());
        }, ct);
    }
}