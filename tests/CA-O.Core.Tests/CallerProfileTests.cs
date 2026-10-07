using CAO.Core.Abstractions;
using Xunit;

namespace CAO.Core.Tests;

/// <summary>
/// CAO-BUG-2026-10-06 (F1): el servicio privilegiado se suplanta al llamante, pero
/// <c>WindowsIdentity.RunImpersonated</c> solo suplanta el HILO y devuelve en cuanto
/// el delegado devuelve su Task, de modo que en un metodo async toda continuation
/// posterior al primer <c>await</c> corre ya como SYSTEM. Por eso el SID viaja
/// aparte en un <c>AsyncLocal</c> y estas rutas de perfil se resuelven de forma
/// explicita en lugar de depender del token del hilo.
/// </summary>
public sealed class CallerProfileTests
{
    [Fact]
    public void Folder_WithoutCaller_FallsBackToTheProcessProfile()
    {
        CallerContext.CurrentUserSid = null;

        Assert.Null(CallerProfile.ProfileDirectory());
        Assert.Equal(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            CallerProfile.Folder(Environment.SpecialFolder.LocalApplicationData));
    }

    [Fact]
    public void Folder_WithBlankCaller_FallsBackToTheProcessProfile()
    {
        // CallerContext normaliza los blancos a null: un SID vacio debe comportarse
        // como si no hubiera llamante, no como un perfil mal formado.
        CallerContext.CurrentUserSid = "   ";

        Assert.Null(CallerProfile.ProfileDirectory());
        Assert.Equal(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            CallerProfile.Folder(Environment.SpecialFolder.LocalApplicationData));
    }

    [Fact]
    public void ProfileDirectory_WithAnUnknownSid_ReturnsNullInsteadOfGuessing()
    {
        // Un SID que no existe en ProfileList no debe inventar un perfil: devolver
        // null hace que las rutas caigan al perfil del proceso, que es el fallo
        // seguro, en vez de apuntar a un directorio inexistente.
        CallerContext.CurrentUserSid = "S-1-5-21-0-0-0-4242";

        Assert.Null(CallerProfile.ProfileDirectory());
    }
}