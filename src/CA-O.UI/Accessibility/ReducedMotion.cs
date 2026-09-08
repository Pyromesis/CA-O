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
                // Respeta el ajuste del sistema. Si la API no está disponible, habilitado por defecto.
                return settings.AnimationsEnabled;
            }
            catch { return true; }
        }
    }

    public static TimeSpan Adjust(TimeSpan normal) => IsAnimationEnabled ? normal : TimeSpan.Zero;
    public static bool ShouldAnimate => IsAnimationEnabled;
}
