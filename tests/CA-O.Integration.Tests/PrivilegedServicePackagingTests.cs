using Xunit;

namespace CAO.Integration.Tests;

/// <summary>
/// Contrato de empaquetado del servicio privilegiado. Se instala en maquinas
/// sin runtime de .NET, asi que el payload debe ser self-contained. Un publish
/// framework-dependent muere dentro del apphost ("You must install .NET to run
/// this application") antes de llamar a StartServiceCtrlDispatcher, y el SCM
/// reporta StartService ERROR 1053 con ESTADO_DE_SALIDA_DE_WIN32 1
/// (SERVICE_NEVER_STARTED) aunque el servicio este correctamente registrado.
/// </summary>
public sealed class PrivilegedServicePackagingTests
{
    [Fact]
    public void ServiceProject_IsSelfContained()
    {
        var csproj = TestUtils.ReadRepoFile("src", "CA-O.Privileged", "CA-O.Privileged.csproj");

        Assert.Contains("<SelfContained>true</SelfContained>", csproj, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("build-release.ps1")]
    [InlineData("install.ps1")]
    [InlineData("publish-privileged-service.ps1")]
    public void PublishScript_NeverShipsFrameworkDependentService(string scriptName)
    {
        var script = TestUtils.ReadRepoFile("scripts", scriptName);

        Assert.DoesNotContain("--self-contained false", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--self-contained true", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildRelease_VerifiesSelfContainedServicePayload()
    {
        var script = TestUtils.ReadRepoFile("scripts", "build-release.ps1");

        Assert.Contains("coreclr.dll", script, StringComparison.OrdinalIgnoreCase);
    }
}
