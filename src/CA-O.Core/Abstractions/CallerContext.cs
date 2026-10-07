using System.Runtime.CompilerServices;

namespace CAO.Core.Abstractions;

/// <summary>
/// Identidad del llamante para el despacho actual, pendiente a traves de los
/// <c>await</c>.
/// </summary>
/// <remarks>
/// CAO-BUG-2026-10-06 (F1): el servicio privilegiado abria <c>HKCU</c> con
/// <c>RegOpenCurrentUser</c> y resolvia <c>%LOCALAPPDATA%</c> /
/// <c>%APPDATA%</c> del llamante gracias a
/// <c>WindowsIdentity.RunImpersonated(token, () =&gt; DispatchOperationAsync(...))</c>.
/// Esa suplantacion es DE HILO: <c>RunImpersonated</c> la retira en cuanto el
/// delegado devuelve la <c>Task</c>, no cuando la <c>Task</c> completa. En una
/// funcion <c>async</c> el delegado devuelve la <c>Task</c> en el primer
/// <c>await</c>, de modo que TODAS las continuaciones siguientes se ejecutan
/// con el token del proceso (SYSTEM) y no con el del usuario: las escrituras
/// en HKCN caian en el hive de SYSTEM y las rutas de perfil resolvian al
/// perfil de SYSTEM. Los comentarios del servicio afirmaban que ya estaba
/// arreglado; solo lo estaba para el tramo sincrono previo al primer await.
///
/// La suplantacion de hilo no puede sobrevivir a un <c>await</c> en .NET, asi
/// que la correccion no es "volver a suplantar": es dejar de depender del
/// estado ambiental del hilo. <see cref="AsyncLocal{T}"/> si se propaga a las
/// continuaciones, de modo que la identidad del llamante viaja con el flujo
/// asincrono. Un <see cref="AsyncLocal{T}"/> se propaga hacia abajo en la
/// cadena de awaits y se AISLA entre ramas concurrentes, que es justo lo que
/// hace falta aqui: el servicio admite cuatro despachos a la vez.
/// </remarks>
public static class CallerContext
{
    private static readonly AsyncLocal<string?> CurrentSid = new();

    /// <summary>
    /// SID del llamante cuyo hive y perfil deben usarse, o <c>null</c> si el
    /// proceso actual actua en su propia identidad (la UI, las pruebas, o el
    /// propio servicio).
    /// </summary>
    public static string? CurrentUserSid
    {
        get => CurrentSid.Value;
        set => CurrentSid.Value = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}