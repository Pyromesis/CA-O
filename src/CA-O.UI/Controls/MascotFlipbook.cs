namespace CAO.UI.Controls;

/// <summary>
/// Catálogo puro del flipbook de la mascota: moods, número de frames, duración
/// de cada frame y ruta relativa de cada imagen. Sin dependencias de UI, para
/// poder testearlo headless (igual que <see cref="CatMoodCatalog"/>).
/// Los frames son PNG de 1024 generados con el motor local (Open Generative AI /
/// stable-diffusion.cpp + Z-Image-Turbo); los sets derivados (idle, look, lick,
/// lie, roll, happy) salen de <c>artifacts/mascot-new/derive-*.py</c>.
/// </summary>
public static class MascotFlipbook
{
    /// <summary>Moods válidos, en el mismo orden que <see cref="CatMoodCatalog.ValidMoods"/>.</summary>
    public static IReadOnlyList<string> Moods { get; } =
        new[] { "Idle", "Working", "Celebrate", "Warn", "Sleep", "Groom" };

    /// <summary>Conjuntos de arte: el que se usa según el tema de la app.</summary>
    public static IReadOnlyList<string> Themes { get; } = new[] { "light", "dark" };

    /// <summary>
    /// Carpeta de arte de cada mood. El nombre público del mood se mantiene para
    /// no tocar las páginas; cada uno mapea a su set derivado.
    /// </summary>
    public static string FolderName(string? mood) => Normalize(mood) switch
    {
        "Working" => "roll",
        "Celebrate" => "happy",
        "Warn" => "look",
        "Sleep" => "lie",
        "Groom" => "lick",
        _ => "idle",
    };

    /// <summary>Frames por mood (cada set derivado con su propio conteo).</summary>
    public static int FrameCount(string? mood) => Normalize(mood) switch
    {
        "Working" => 16,
        "Celebrate" => 12,
        "Warn" => 5,
        "Sleep" => 30,
        "Groom" => 10,
        _ => 30,
    };

    /// <summary>Duración de cada fotograma, en milisegundos (frame a frame, sin interpolación).</summary>
    public static int FrameMs(string? mood) => Normalize(mood) switch
    {
        "Working" => 110,
        "Celebrate" => 100,
        "Warn" => 200,
        "Sleep" => 300,
        "Groom" => 240,
        _ => 130,
    };

    /// <summary>Normaliza un mood desconocido o vacío a <c>Idle</c>.</summary>
    public static string Normalize(string? mood)
    {
        if (string.IsNullOrWhiteSpace(mood)) return "Idle";
        foreach (var known in Moods)
        {
            if (string.Equals(known, mood, StringComparison.OrdinalIgnoreCase)) return known;
        }
        return "Idle";
    }

    /// <summary>Duración del ciclo completo del mood, en milisegundos.</summary>
    public static int CycleMs(string? mood) => FrameCount(mood) * FrameMs(mood);

    /// <summary>
    /// Moods de ocasión: se reproducen unos ciclos y vuelven solos a Idle.
    /// Sleep persiste (el gato dormido se queda dormido hasta que el mood
    /// global cambie) y Groom ya vuelve solo al mood global.
    /// </summary>
    public static bool ReturnsToIdle(string? mood) => Normalize(mood) switch
    {
        "Working" or "Celebrate" or "Warn" => true,
        _ => false,
    };

    /// <summary>Ciclos completos que reproduce un mood de ocasión antes de volver a Idle.</summary>
    public static int OneShotCycles(string? mood) => ReturnsToIdle(mood) ? 2 : 0;

    /// <summary>Carpeta relativa de un mood y tema, con separadores '/'.</summary>
    public static string FolderPath(string theme, string mood) =>
        $"Assets/mascot/frames/{NormalizeTheme(theme)}/{FolderName(mood)}";

    /// <summary>Ruta relativa del frame (base = carpeta de la app).</summary>
    public static string RelativePath(string theme, string mood, int frame)
    {
        var count = FrameCount(mood);
        var index = ((frame % count) + count) % count;
        return $"{FolderPath(theme, mood)}/f{index:00}.png";
    }

    /// <summary>
    /// URI <c>ms-appx</c> del frame: vía de carga principal en WinUI (los PNG van
    /// como Content a <c>Assets/mascot/frames</c>). En apps unpackaged resuelve a la
    /// carpeta del ejecutable; CaoCat usa la ruta de archivo solo como fallback.
    /// </summary>
    public static string AppxUri(string theme, string mood, int frame)
    {
        var count = FrameCount(mood);
        var index = ((frame % count) + count) % count;
        return $"ms-appx:///Assets/mascot/frames/{NormalizeTheme(theme)}/{FolderName(mood)}/f{index:00}.png";
    }

    /// <summary>Conjunto de arte para un tema; cualquier valor raro cae a "light".</summary>
    public static string NormalizeTheme(string? theme) =>
        string.Equals(theme, "dark", StringComparison.OrdinalIgnoreCase) ? "dark" : "light";
}
