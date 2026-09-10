using CAO.Infrastructure.SystemInterop;
using Xunit;

namespace CAO.Infrastructure.Tests;

/// <summary>
/// Inventario total de drivers vía SetupAPI (paridad con Administrador de
/// dispositivos), con WMI como enriquecimiento. Los tests vivos corren en
/// Windows (dev + CI windows-latest) con aserciones tolerantes al equipo.
/// </summary>
public sealed class SetupApiInventoryTests
{
    [Fact]
    public void EnumerateAll_ListsPresentAndHiddenDevices()
    {
        var devices = SetupApiDeviceEnumerator.EnumerateAll();

        Assert.True(devices.Count > 10, $"Se esperaban decenas de dispositivos, hay {devices.Count}.");
        Assert.True(devices.Any(d => d.IsPresent), "Debe haber dispositivos presentes.");
        Assert.All(devices, d => Assert.False(string.IsNullOrWhiteSpace(d.InstanceId)));
        Assert.All(devices, d => Assert.False(
            string.IsNullOrWhiteSpace(d.Description) && string.IsNullOrWhiteSpace(d.FriendlyName)));
        Assert.All(devices, d => Assert.True(d.ProblemCode >= -1));
        Assert.Equal(devices.Count, devices.Select(d => d.InstanceId).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public async Task MeasureAsync_ReturnsNamedUniqueInventory()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var report = await new DriverDiagnosticsProvider().MeasureAsync(cts.Token);

        Assert.True(report.Drivers.Count > 10, $"Inventario pobre: {report.Drivers.Count}.");
        Assert.All(report.Drivers, d => Assert.False(string.IsNullOrWhiteSpace(d.Name)));
        Assert.All(report.Drivers, d => Assert.False(string.IsNullOrWhiteSpace(d.PnpDeviceId)));
        Assert.Equal(
            report.Drivers.Count,
            report.Drivers.Select(d => d.PnpDeviceId).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Theory]
    [InlineData("6-21-2006", "20060621")]
    [InlineData("06/21/2006", "20060621")]
    [InlineData("20240115120000.000000-060", "20240115120000.000000-060")]
    [InlineData("no-fecha", "no-fecha")]
    [InlineData("", "")]
    public void NormalizeDriverDate_ConvertsRegistryStyle(string input, string expectedStart)
    {
        Assert.StartsWith(expectedStart, SetupApiDeviceEnumerator.NormalizeDriverDate(input));
    }

    [Fact]
    public void SplitMultiString_SplitsAndTrims()
    {
        var parts = SetupApiDeviceEnumerator.SplitMultiString("PCI\\VEN_8086\0  \0USB\\VID_1234\0\0");
        Assert.Equal(new[] { "PCI\\VEN_8086", "USB\\VID_1234" }, parts);
    }

    [Fact]
    public void Merge_PrefersLiveDataAndWmiEnrichment()
    {
        var dev = new SetupApiDevice(
            "PCI\\VEN_8086&DEV_1234\\1", "Device Desc", "Friendly Name", "Intel",
            "System", "{guid}", new[] { "PCI\\VEN_8086" }, Array.Empty<string>(),
            "PCI", "{guid}\\0001", "9.9.9", "6-21-2006", "Intel", "oem1.inf", 0, true);
        var wmi = new WmiDriverInfo(
            "PCI\\VEN_8086&DEV_1234\\1", "WMI Name", "WMI Class", "WMI Mfg",
            "10.0.0", "20240101000000.000000-000", true, "OK", 10,
            "PCI\\VEN_8086", "oem9.inf", "WMI Prov");

        var merged = DriverDiagnosticsProvider.Merge(dev, wmi);

        Assert.Equal("Friendly Name", merged.Name); // vivo manda en identidad
        Assert.Equal("System", merged.DeviceClass);
        Assert.Equal("10.0.0", merged.Version); // WMI enriquece versión
        Assert.Equal("20240101000000.000000-000", merged.Date);
        Assert.True(merged.IsSigned);
        Assert.Equal(0, merged.ProblemCode); // CM vivo manda sobre WMI
        Assert.Equal("oem9.inf", merged.InfName);
        Assert.True(merged.IsPresent);
    }

    [Fact]
    public void Merge_UnknownProblemFallsBackToWmi_AndMarksAbsent()
    {
        var dev = new SetupApiDevice(
            "ROOT\\UNKNOWN\\0000", "Ghost", "", "", "", "", Array.Empty<string>(), Array.Empty<string>(),
            "ROOT", "", "", "", "", "", -1, false);
        var wmi = new WmiDriverInfo(
            "ROOT\\UNKNOWN\\0000", "Ghost", "", "", "", "", null, "", 28, "", "", "");

        var merged = DriverDiagnosticsProvider.Merge(dev, wmi);

        Assert.Equal(28, merged.ProblemCode);
        Assert.False(merged.IsPresent);
        Assert.Equal("Ghost", merged.Name);
    }

    [Fact]
    public void Merge_AbsentWithoutWmi_IsHonest()
    {
        var dev = new SetupApiDevice(
            "SWD\\X\\1", "Phantom", "", "", "", "", Array.Empty<string>(), Array.Empty<string>(),
            "SWD", "", "", "", "", "", -1, false);

        var merged = DriverDiagnosticsProvider.Merge(dev, null);

        Assert.Equal("No presente", merged.Status);
        Assert.Equal(0, merged.ProblemCode);
        Assert.Null(merged.IsSigned);
    }
}
