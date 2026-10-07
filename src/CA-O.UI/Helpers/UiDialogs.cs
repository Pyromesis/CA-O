using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CAO.UI.Helpers;

/// <summary>
/// Show de un <see cref="ContentDialog"/> con exclusion mutua y sin excepciones.
/// </summary>
public static class UiDialogs
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    /// <summary>
    /// Muestra <paramref name="dialog"/> si no hay otro dialogo abierto.
    /// Devuelve <see cref="ContentDialogResult.None"/> cuando no se pudo mostrar.
    /// </summary>
    public static async Task<ContentDialogResult> ShowAsync(ContentDialog dialog)
    {
        if (dialog is null)
            return ContentDialogResult.None;

        // CAO-BUG-2026-10-06: ShowAsync lanza si ya hay un ContentDialog abierto
        // (la capa superpuesta de cada Page es local y no cubre el
        // NavigationView, de modo que el usuario puede abrir "Limpiar todo" en
        // Limpieza mientras el lote de recomendados sigue en curso en
        // Optimizar). La excepcion escapaba de handlers async void sin try y
        // cerraba la aplicacion. Ahora se cede el turno: el segundo dialogo se
        // descarta y devuelve None, que en un dialogo de confirmacion es
        // "cancelado" y en uno informativo es omitir un mensaje.
        var acquired = false;
        try
        {
            acquired = await Gate.WaitAsync(0);
        }
        catch (ObjectDisposedException)
        {
            return ContentDialogResult.None;
        }

        if (!acquired)
            return ContentDialogResult.None;

        try
        {
            // Un XamlRoot nulo o ya desconectado (la pagina se descargo de la
            // navegacion mientras esperaba) tambien lanza en ShowAsync.
            if (dialog.XamlRoot is null)
                return ContentDialogResult.None;

            return await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            App.WriteCrashLog(ex);
            return ContentDialogResult.None;
        }
        finally
        {
            Gate.Release();
        }
    }
}