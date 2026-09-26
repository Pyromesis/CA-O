namespace CAO.UI.Controls;

/// <summary>Mapa puro mood → caption + clave de animación. Sin dependencias UI: testeable headless.</summary>
public static class CatMoodCatalog
{
    public static IReadOnlyList<string> ValidMoods { get; } =
        new[] { "Idle", "Working", "Celebrate", "Warn", "Sleep", "Groom" };

    public static (string Caption, string AnimationKey) Resolve(string? mood) =>
        mood switch
        {
            "Working" => ("Manos a la obra…", "Working"),
            "Celebrate" => ("¡Listo! Buen trabajo.", "Celebrate"),
            "Warn" => ("Ojo, algo necesita atención.", "Warn"),
            "Sleep" => ("Zzz… aquí estaré.", "Sleep"),
            "Groom" => ("Un momento, me acicalo…", "Groom"),
            _ => ("¿Qué optimizamos hoy?", "Idle"),
        };
}
