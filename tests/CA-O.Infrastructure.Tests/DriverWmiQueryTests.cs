using System.Management;
using CAO.Infrastructure.SystemInterop;
using Xunit;

namespace CAO.Infrastructure.Tests;

/// <summary>
/// CAO-BUG-2026-10-17: la consulta de drivers firmados era inválida y el
/// inventario se quedaba sin firma, versión ni proveedor. Estas pruebas
/// ejecutan la consulta real contra WMI: si vuelve a pedir una propiedad que
/// la clase no tiene, falla aquí y no en el equipo del usuario.
/// </summary>
public sealed class DriverWmiQueryTests
{
    [Fact]
    public void QueryOnlyAsksForPropertiesOfWin32PnPSignedDriver()
    {
        Assert.DoesNotContain("ConfigManagerErrorCode", DriverDiagnosticsProvider.SignedDriverQuery, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RealQueryReturnsSignedDriversOnThisMachine()
    {
        if (!OperatingSystem.IsWindows()) return;

        // Suelo del entorno: filas que ve una consulta directa a la clase. En
        // runners/VM sin WMI (bloqueado o más lento que el timeout de 5 s del
        // proveedor) no hay verdad contra la que comparar: sin filas, la
        // prueba no aplica y no es una regresión de la app.
        int? groundTruth = CountSignedDriversViaWmi();
        if (groundTruth is null or 0) return;

        var rows = DriverDiagnosticsProvider.ReadWmi(CancellationToken.None);
        Assert.NotEmpty(rows);
        Assert.Contains(rows.Values, r => r.IsSigned == true);
        Assert.Contains(rows.Values, r => !string.IsNullOrWhiteSpace(r.Version));
    }

    private static int? CountSignedDriversViaWmi()
    {
        try
        {
            var opts = new System.Management.EnumerationOptions { Timeout = TimeSpan.FromSeconds(5) };
            using var searcher = new ManagementObjectSearcher(
                new ManagementScope(@"root\cimv2"),
                new ObjectQuery("SELECT DeviceID FROM Win32_PnPSignedDriver"),
                opts);
            return searcher.Get().Count;
        }
        catch (ManagementException)
        {
            return null;
        }
    }
}
