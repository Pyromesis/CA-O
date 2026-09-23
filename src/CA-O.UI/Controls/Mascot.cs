using System;
using CAO.UI.ViewModels;

namespace CAO.UI.Controls;

/// <summary>
/// Helper para cambiar el mood global de la mascota (<see cref="UiState.MascotMood"/>).
/// Cada <see cref="CaoCat"/> suscrito al cambio de mood actualiza su ciclo solo.
/// Nunca lanza: la mascota no puede romper la página.
/// </summary>
public static class Mascot
{
    public static void Set(string mood)
    {
        try { AppHost.Resolve<UiState>().MascotMood = Normalize(mood); }
        catch { }
    }

    /// <summary>
    /// Celebra y vuelve a Idle a los 4 s (solo si nadie cambió el mood entretanto).
    /// </summary>
    public static void CelebrateThenIdle(Microsoft.UI.Dispatching.DispatcherQueue queue)
    {
        try
        {
            Set("Celebrate");
            var timer = queue.CreateTimer();
            timer.Interval = TimeSpan.FromSeconds(4);
            timer.IsRepeating = false;
            timer.Tick += (_, __) =>
            {
                try
                {
                    var state = AppHost.Resolve<UiState>();
                    if (state.MascotMood == "Celebrate")
                        state.MascotMood = "Idle";
                }
                catch { }
            };
            timer.Start();
        }
        catch { }
    }

    private static string Normalize(string mood) => mood switch
    {
        "Idle" or "Working" or "Celebrate" or "Warn" or "Sleep" => mood,
        _ => "Idle",
    };
}
