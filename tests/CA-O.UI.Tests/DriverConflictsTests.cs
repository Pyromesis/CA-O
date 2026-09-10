using CAO.Shared;
using CAO.UI.Helpers;
using Xunit;

namespace CAO.UI.Tests;

/// <summary>Descripciones honestas de conflictos de drivers (fase 1, solo lectura).</summary>
public sealed class DriverConflictsTests
{
    private static DriverDiagnostic Driver(int code, bool? signed = true) =>
        new("Test Device", "System", "Vendor", "1.0", "20240101000000.000000-000", signed, "OK", code);

    [Theory]
    [InlineData(0, "Correcto")]
    [InlineData(10, "Código 10")]
    [InlineData(28, "Código 28")]
    [InlineData(43, "Código 43")]
    [InlineData(52, "Código 52")]
    public void DescribeProblem_CoversKnownCodes(int code, string expectedStart)
    {
        Assert.StartsWith(expectedStart, DriverConflicts.DescribeProblem(code));
    }

    [Fact]
    public void DescribeProblem_UnknownCode_IsHonest()
    {
        Assert.Contains("777", DriverConflicts.DescribeProblem(777));
    }

    [Fact]
    public void Predicates_ClassifyCorrectly()
    {
        Assert.False(DriverConflicts.IsProblem(Driver(0)));
        Assert.True(DriverConflicts.IsProblem(Driver(10)));
        Assert.True(DriverConflicts.IsMissing(Driver(28)));
        Assert.False(DriverConflicts.IsMissing(Driver(10)));
        Assert.True(DriverConflicts.IsUnsigned(Driver(0, false)));
        Assert.False(DriverConflicts.IsUnsigned(Driver(0, true)));
        Assert.False(DriverConflicts.IsUnsigned(Driver(0, null)));
    }

    [Fact]
    public void IsPhantom_FlagsNonPresentDevices()
    {
        var present = Driver(0);
        var phantom = present with { IsPresent = false };
        Assert.False(DriverConflicts.IsPhantom(present));
        Assert.True(DriverConflicts.IsPhantom(phantom));
    }

    [Theory]
    [InlineData("20240115120000.000000-060", "15/01/2024")]
    [InlineData("", "Fecha desconocida")]
    [InlineData("no-fecha", "no-fecha")]
    public void FormatDriverDate_ParsesOrPassesThrough(string input, string expected)
    {
        Assert.Equal(expected, DriverConflicts.FormatDriverDate(input));
    }
}
