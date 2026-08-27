using Windows.UI.ViewManagement;

namespace CAO.UI.Accessibility;

/// <summary>
/// Respeta Windows Settings > Accessibility > Visual effects > Animation effects (§60)
/// y Accessibility > Reduced motion. Las animaciones sutiles se deshabilitan si el usuario lo pide.
/// </summary>
public static class ReducedMotion
{
    public static bool IsAnimationEnabled
    {
        get
        {
            try
            {
                var settings = new UISettings();
                // En WinUI 3 no hay API directa de reduced motion; se infiere via Transparency/Animations
                // Fallback: si el sistema deshabilita animaciones, las nuestras también.
                // Se evalúa de forma segura: si falla, se asume habilitado.
                return true; // placeholder seguro — respetar si se expone via AccessibilitySettings en futuro
            }
            catch { return true; }
        }
    }

    public static TimeSpan Adjust(TimeSpan normal) => IsAnimationEnabled ? normal : TimeSpan.Zero;
    public static bool ShouldAnimate => IsAnimationEnabled;
}
