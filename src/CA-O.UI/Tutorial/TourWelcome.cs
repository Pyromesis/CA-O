using System.Threading.Tasks;
using CAO.UI.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CAO.UI.Tutorial;

/// <summary>
/// Pantalla de bienvenida del primer arranque. Enrutada por UiDialogs para no
/// colisionar nunca con otro ContentDialog (bug CAO-BUG-2026-10-06).
/// </summary>
public static class TourWelcome
{
    public const string StartButtonText = "Empezar recorrido";
    public const string LaterButtonText = "Ahora no";

    public static async Task<bool?> ShowAsync(XamlRoot? xamlRoot)
    {
        if (xamlRoot is null) return null;

        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = "Bienvenido a CA-O",
            Content = "Este recorrido guiado de 5 pasos te lleva desde el análisis hasta la restauración. Puedes saltarlo cuando quieras y volver a lanzarlo desde Tutorial.",
            PrimaryButtonText = StartButtonText,
            CloseButtonText = LaterButtonText,
            DefaultButton = ContentDialogButton.Primary,
        };

        var presented = false;
        dialog.Opened += (_, _) => presented = true;
        var result = await UiDialogs.ShowAsync(dialog);
        if (result == ContentDialogResult.Primary) return true;
        return presented ? false : null;
    }
}
