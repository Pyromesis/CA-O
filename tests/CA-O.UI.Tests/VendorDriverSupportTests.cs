using CAO.UI.Helpers;
using Xunit;

namespace CAO.UI.Tests;

/// <summary>Fase drivers 3: solo dominios oficiales, serial solo local.</summary>
public sealed class VendorDriverSupportTests
{
    [Fact]
    public void DellWithSerialBuildsServiceTagLink()
    {
        var support = VendorDriverSupport.Resolve("Dell Inc.", "Inspiron 15", "ABC1234");
        Assert.Equal("Dell", support.Vendor);
        Assert.StartsWith("https://www.dell.com/support/home/", support.DriversUrl);
        Assert.Contains("ABC1234", support.DriversUrl);
    }

    [Theory]
    [InlineData("LENOVO", "https://pcsupport.lenovo.com/")]
    [InlineData("HP", "https://support.hp.com/")]
    [InlineData("ASUSTeK COMPUTER INC.", "https://www.asus.com/")]
    [InlineData("Acer", "https://www.acer.com/")]
    [InlineData("Micro-Star International", "https://www.msi.com/")]
    [InlineData("Gigabyte Technology", "https://www.gigabyte.com/")]
    [InlineData("Samsung Electronics", "https://www.samsung.com/")]
    [InlineData("Microsoft Corporation", "https://support.microsoft.com/")]
    public void KnownVendorsResolveToOfficialDomains(string maker, string expectedStart)
    {
        var support = VendorDriverSupport.Resolve(maker, "Modelo", string.Empty);
        Assert.StartsWith(expectedStart, support.DriversUrl);
    }

    [Theory]
    [InlineData("Fabricante Raro")]
    [InlineData("")]
    public void UnknownVendorFallsBackToMicrosoftCatalog(string maker)
    {
        var support = VendorDriverSupport.Resolve(maker, "X", null);
        Assert.Equal(VendorDriverSupport.MicrosoftCatalogUrl, support.DriversUrl);
    }

    [Theory]
    [InlineData("To be filled by O.E.M.", "no disponible")]
    [InlineData("", "no disponible")]
    [InlineData(null, "no disponible")]
    public void PlaceholderSerialsAreNotExposed(string? serial, string expected)
    {
        Assert.Equal(expected, VendorDriverSupport.MaskSerial(serial));
    }

    [Fact]
    public void RealSerialIsMaskedExceptLastFour()
    {
        Assert.Equal("•••1234", VendorDriverSupport.MaskSerial("ABC1234"));
    }

    [Fact]
    public void BoardFallbackWhenSystemLies()
    {
        // El caso del usuario: sistema "Default string", placa ASUS real.
        var support = VendorDriverSupport.Resolve(new CAO.Shared.ComputerInfo(
            "Default string", "Default string", "0", "ASUSTeK COMPUTER INC.", "PRIME B560M-A", "American Megatrends Inc."));
        Assert.StartsWith("https://www.asus.com/", support.DriversUrl);
        Assert.Equal("placa base", support.DetectedBy);
    }

    [Fact]
    public void BiosFallbackWhenNothingElse()
    {
        var support = VendorDriverSupport.Resolve(new CAO.Shared.ComputerInfo(
            "To be filled by O.E.M.", "", "", "", "", "Dell Inc."));
        Assert.StartsWith("https://www.dell.com/", support.DriversUrl);
        Assert.Equal("BIOS", support.DetectedBy);
    }

    [Fact]
    public void VirtualMachineIsReportedHonestly()
    {
        var support = VendorDriverSupport.Resolve(new CAO.Shared.ComputerInfo(
            "Microsoft Corporation", "Virtual Machine", "", "", "", ""));
        Assert.Equal("Máquina virtual", support.Vendor);
        Assert.Equal("virtualización", support.DetectedBy);
    }

    [Fact]
    public void TotalUnknownNeverDeadEnds()
    {
        var support = VendorDriverSupport.Resolve(new CAO.Shared.ComputerInfo("", "", "", "", "", ""));
        Assert.Equal(VendorDriverSupport.MicrosoftCatalogUrl, support.DriversUrl);
        Assert.Contains("catálogo", support.Note.ToLowerInvariant());
    }
}
