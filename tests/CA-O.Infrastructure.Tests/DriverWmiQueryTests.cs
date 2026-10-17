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
        var rows = DriverDiagnosticsProvider.ReadWmi(CancellationToken.None);
        Assert.NotEmpty(rows);
        Assert.Contains(rows.Values, r => r.IsSigned == true);
        Assert.Contains(rows.Values, r => !string.IsNullOrWhiteSpace(r.Version));
    }
}
