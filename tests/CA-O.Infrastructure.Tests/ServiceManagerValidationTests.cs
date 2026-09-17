using CAO.Infrastructure.Windows.Services;
using Xunit;

namespace CAO.Infrastructure.Tests;

/// <summary>
/// ServiceManager valida el nombre antes de tocar HKLM\Services: sin esto,
/// un "..\" escapaba de la clave Services. Boot/System ya no se permiten
/// (solo Automatic/Manual/Disabled). Estos tests no tocan el registro: la
/// validación lanza antes de abrir la clave.
/// </summary>
public sealed class ServiceManagerValidationTests
{
    private readonly ServiceManager _sut = new();

    [Theory]
    [InlineData(@"WSearch\..\..\Software\Evil")]
    [InlineData(@"..\Software\Evil")]
    [InlineData("WSearch/Evil")]
    [InlineData("WSearch;whoami")]
    [InlineData("")]
    public void TraversalNamesAreRejected(string serviceName)
    {
        Assert.False(_sut.Exists(serviceName));
        Assert.Null(_sut.GetStartType(serviceName));
        Assert.Throws<System.ArgumentException>(() => _sut.SetStartType(serviceName, "Manual"));
    }

    [Theory]
    [InlineData("Boot")]
    [InlineData("System")]
    public void BootAndSystemStartTypesAreRejected(string startType)
    {
        Assert.Throws<System.ArgumentException>(() => _sut.SetStartType("WSearch", startType));
    }
}
