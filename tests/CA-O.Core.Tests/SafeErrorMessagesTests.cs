using System;

using CAO.Core.Abstractions;

using Xunit;

namespace CAO.Core.Tests;

/// <summary>
/// Contrato del mensaje que el servicio privilegiado devuelve al cliente cuando
/// una operacion lanza una excepcion.
/// CAO-BUG-2026-10-06: nueve bloques <c>catch</c> de
/// <c>PrivilegedPipeService.DispatchOperationAsync</c> contestaban
/// <c>$"Error aplicando DNS: {ex.Message}"</c> sin escribir la excepcion en el
/// log del servicio. Eso hacia dos cosas malas a la vez: entregaba al cliente no
/// privilegiado rutas absolutas, claves de registro, detalle de WMI y HRESULT
/// nativos, y perdia la unica traza del fallo. El servicio ya declara lo
/// contrario en sus comentarios ("el detalle va solo al log del servicio"), y la
/// rama general de seguridad si lo cumple.
/// </summary>
public sealed class SafeErrorMessagesTests
{
    /// <summary>
    /// El mensaje debe seguir diciendo QUE operacion fallo: sin ese dato el
    /// usuario no puede ni decidir si reintentar. Perder el codigo de error real
    /// en el transporte (F7) fue precisamente el motivo de que el usuario
    /// dijera "no se por que"; esta clase no debe repetir ese error.
    /// </summary>
    [Theory]
    [InlineData("Error aplicando DNS", "Error aplicando DNS.")]
    [InlineData("Error instalando driver", "Error instalando driver.")]
    [InlineData("Error buscando en el catalogo", "Error buscando en el catalogo.")]
    public void DispatchFailure_KeepsTheOperationLabel(string label, string expectedPrefix)
    {
        var message = SafeErrorMessages.DispatchFailure(label);

        Assert.StartsWith(expectedPrefix, message, StringComparison.Ordinal);
    }

    /// <summary>
    /// El detalle de la excepcion jamas debe llegar al cliente. Es el fallo que
    /// se esta corrigiendo, asi que se comprueba contra una excepcion realista.
    /// </summary>
    [Fact]
    public void DispatchFailure_NeverLeaksTheExceptionDetail()
    {
        var error = new UnauthorizedAccessException(
            @"Access to the path 'C:\Windows\System32\drivers\etc\hosts' is denied. (0x80070005)");

        var message = SafeErrorMessages.DispatchFailure("Error aplicando DNS", error);

        Assert.DoesNotContain("hosts", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Windows", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("0x80070005", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(error.Message, message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Sin etiqueta no hay nada que decir: el mensaje degenera en el generico,
    /// que es justo lo que ya devuelve la rama general de seguridad.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DispatchFailure_WithoutLabel_FallsBackToTheGenericMessage(string? label)
    {
        var message = SafeErrorMessages.DispatchFailure(label);

        // El sufijo se mantiene tambien en el generico: sin el, el usuario ve un
        // "Fallo interno del servicio." sin ninguna pista de donde mirar, que es
        // justo la sensacion de "no se por que" que se quiere eliminar.
        Assert.Equal("Fallo interno del servicio. Detalle en el registro del servicio.", message);
    }

    /// <summary>
    /// La etiqueta se normaliza para no dejar dos puntos suspensivos ni espacios
    /// sueltos delante del punto final.
    /// </summary>
    [Theory]
    [InlineData("Error aplicando DNS: ", "Error aplicando DNS.")]
    [InlineData("  Error aplicando DNS  ", "Error aplicando DNS.")]
    [InlineData("Error aplicando DNS...", "Error aplicando DNS.")]
    public void DispatchFailure_NormalizesTrailingPunctuation(string label, string expected)
    {
        Assert.StartsWith(
            expected,
            SafeErrorMessages.DispatchFailure(label),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// El mensaje debe senalar donde esta el detalle, para que el usuario pueda
    /// revisar el diagnostico por su cuenta.
    /// </summary>
    [Fact]
    public void DispatchFailure_PointsToTheServiceLog()
    {
        Assert.Contains("registro", SafeErrorMessages.DispatchFailure("Error aplicando DNS"),
            StringComparison.OrdinalIgnoreCase);
    }
}