namespace CAO.Core.Abstractions;

/// <summary>
/// Mensajes que el servicio privilegiado puede devolver a un cliente no
/// privilegiado cuando una operacion lanza una excepcion.
/// </summary>
public static class SafeErrorMessages
{
    /// <summary>
    /// Construye el mensaje de fallo para el cliente.
    /// CAO-BUG-2026-10-06: nueve bloques <c>catch</c> de
    /// <c>PrivilegedPipeService.DispatchOperationAsync</c> contestaban
    /// <c>$"Error aplicando DNS: {ex.Message}"</c> y no escribian la excepcion en
    /// el log del servicio. Eso entregaba al cliente rutas absolutas, claves de
    /// registro, detalle de WMI y HRESULT nativos, y ademas perdia la unica traza
    /// del fallo. El propio servicio declara lo contrario en sus comentarios
    /// ("el detalle va solo al log del servicio para no filtrar internals") y su
    /// rama general de seguridad si lo cumple.
    /// <para>
    /// El mensaje conserva la etiqueta de la operacion a proposito: sin ella el
    /// usuario no puede decidir si reintentar. Perder el motivo en el transporte
    /// fue justo el defecto F7, asi que esta clase no debe repetirlo; lo que se
    /// descarta es el detalle de la excepcion, no la informacion accionable.
    /// </para>
    /// </summary>
    /// <param name="label">Texto que identifica la operacion, sin excepcion.</param>
    /// <param name="error">
    /// Excepcion original. Se acepta para que la llamada sea explicita y para que
    /// no quede a un paso de alguien concatenarla; jamas se incorpora al mensaje.
    /// </param>
    public static string DispatchFailure(string? label, Exception? error = null)
    {
        // CAO-BUG-2026-10-06: la rama sin etiqueta no terminaba en punto, asi que
        // el mensaje final era "Fallo interno del servicio Detalle en el registro
        // del servicio.": dos frases pegadas. Se cierra igual que la rama con
        // etiqueta para que el texto sea siempre una frase y su siguiente frase.
        var head = string.IsNullOrWhiteSpace(label)
            ? "Fallo interno del servicio."
            : label.Trim().TrimEnd(' ', ':', '.', ';') + ".";

        return head + " Detalle en el registro del servicio.";
    }
}