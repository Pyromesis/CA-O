namespace CAO.UI.Controls;

/// <summary>
/// Catálogo puro del flipbook de la mascota: moods, número de frames, duración
/// de cada frame y ruta relativa de cada imagen. Sin dependencias de UI, para
/// poder testearlo headless (igual que <see cref="CatMoodCatalog"/>).
/// Los frames son PNG generados con el motor local (Open Generative AI /
/// stable-diffusion.cpp + DreamShaper 8) y limpiados por
/// <c>scripts/generate-mascot-frames.ps1</c>.
/// </summary>
public static class MascotFlipbook
{
    /// <summary>Moods válidos, en el mismo orden que <see cref="CatMoodCatalog.ValidMoods"/>.</summary>
    public static IReadOnlyList<string> Moods { get; } =
        new[] { "Idle", "Working", "Celebrate", "Warn", "Sleep" };

    /// <summary>Conjuntos de arte: el que se usa según el tema de la app.</summary>
    public static IReadOnlyList<string> Themes { get; } = new[] { "light", "dark" };

    /// <summary>Frames por mood (flipbook de 8 fotogramas por ciclo).</summary>
    public static int FrameCount(string? mood) => Normalize(mood) switch
    {
        "Working" => 8,
        "Celebrate" => 8,
        "Warn" => 8,
        "Sleep" => 8,
        _ => 8,
    };

    /// <summary>Duración de cada fotograma, en milisegundos (frame a frame, sin interpolación).</summary>
    public static int FrameMs(string? mood) => Normalize(mood) switch
    {
        "Working" => 90,
        "Celebrate" => 80,
        "Warn" => 110,
        "Sleep" => 300,
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

    /// <summary>Carpeta relativa de un mood y tema, con separadores '/'.</summary>
    public static string FolderPath(string theme, string mood) =>
        $"Assets/mascot/frames/{NormalizeTheme(theme)}/{Normalize(mood).ToLowerInvariant()}";

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
        return $"ms-appx:///Assets/mascot/frames/{NormalizeTheme(theme)}/{Normalize(mood).ToLowerInvariant()}/f{index:00}.png";
    }

    /// <summary>Conjunto de arte para un tema; cualquier valor raro cae a "light".</summary>
    public static string NormalizeTheme(string? theme) =>
        string.Equals(theme, "dark", StringComparison.OrdinalIgnoreCase) ? "dark" : "light";
}
