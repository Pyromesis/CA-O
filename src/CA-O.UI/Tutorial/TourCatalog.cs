using System;
using System.Collections.Generic;

namespace CAO.UI.Tutorial;

/// <summary>One spotlight step: a route, the x:Name of the control to highlight, the copy and the mascot mood.</summary>
public sealed record TourStep(string Route, string TargetName, string Title, string Body, string Mood);

/// <summary>A tutorial chapter rendered on the Tutorial page and linked to the tour step of the same route.</summary>
public sealed record TutorialChapter(string Route, string Title, string Summary, IReadOnlyList<string> Points);

/// <summary>
/// Single source of truth for every tour step and tutorial chapter. Pure data, no WinUI,
/// so the whole catalog is unit-testable.
/// </summary>
public static class TourCatalog
{
    public static IReadOnlyList<TourStep> Steps { get; } = Array.AsReadOnly(new[]
    {
        new TourStep(
            "dashboard",
            "AnalyzeButton",
            "Este es el Panel",
            "Aqui ves la salud del sistema y el resumen de lo que CA-O ha encontrado. Este boton lanza un analisis completo: es siempre el primer paso.",
            "Idle"),
        new TourStep(
            "analyze",
            "RunButton",
            "1. Analizar",
            "El analisis recorre el sistema y guarda los puntos de mejora. No cambia nada todavia: solo informa.",
            "Working"),
        new TourStep(
            "optimize",
            "ApplyRecommendedButton",
            "2. Optimizar",
            "CA-O propone una lista de cambios recomendados. Este boton los aplica de forma reversible, guardando un punto de restauracion antes.",
            "Working"),
        new TourStep(
            "cleanup",
            "CleanAllButton",
            "3. Limpieza",
            "Limpia archivos temporales y caches que ocupan espacio. Es rapido y tambien queda registrado en el historial.",
            "Working"),
        new TourStep(
            "restore",
            "NoteBar",
            "4. Restaurar",
            "Cada cambio aplicado deja un punto de restauración antes de tocar nada. Si algo no te gusta, vuelve al estado anterior con un clic.",
            "Celebrate"),
    });

    public static IReadOnlyList<TutorialChapter> Chapters { get; } = Array.AsReadOnly(new[]
    {
        new TutorialChapter(
            "analyze",
            "Analizar",
            "Lee el estado real del sistema y genera la lista de mejoras.",
            new[]
            {
                "Lanza un analisis completo con el boton de analisis.",
                "Cada hallazgo incluye una explicacion y un impacto estimado.",
                "El analisis no modifica nada: es siempre seguro repetirlo.",
            }),
        new TutorialChapter(
            "optimize",
            "Optimizar",
            "Aplica los cambios recomendados de forma reversible.",
            new[]
            {
                "Antes de tocar nada se crea un punto de restauracion.",
                "Puedes filtrar las recomendaciones antes de aplicarlas.",
                "Modo solo lectura disponible si prefieres revisar sin aplicar.",
            }),
        new TutorialChapter(
            "cleanup",
            "Limpieza",
            "Libera espacio en disco con archivos temporales y caches.",
            new[]
            {
                "Cada categoria se puede limpiar por separado o de golpe.",
                "El progreso se muestra en vivo y se puede cancelar.",
                "Tambien se registra en el historial.",
            }),
        new TutorialChapter(
            "restore",
            "Restaurar",
            "Vuelve el sistema a un punto de restauracion anterior.",
            new[]
            {
                "Cada aplicacion deja un punto con su fecha y su resumen.",
                "Restaurar revierte los cambios de esa operacion.",
                "Es la red de seguridad de todo el proceso.",
            }),
        new TutorialChapter(
            "drivers",
            "Drivers",
            "Detecta conflictos entre controladores y dispositivos.",
            new[]
            {
                "Escanea los dispositivos con problemas del sistema.",
                "Señala conflictos conocidos antes de que causen fallos.",
                "Indica el fabricante y la version de cada controlador.",
            }),
        new TutorialChapter(
            "history",
            "Historial",
            "Todo lo que CA-O ha hecho, con fecha y resultado.",
            new[]
            {
                "Registro completo de analisis, optimizaciones y limpiezas.",
                "Permite auditar que se aplico en cada momento.",
                "Se puede filtrar por tipo de operacion.",
            }),
        new TutorialChapter(
            "settings",
            "Ajustes",
            "Idioma, tema, modo seguro y comportamiento de la ventana.",
            new[]
            {
                "Idioma y tema (claro, oscuro o del sistema).",
                "Modo seguro: ninguna accion se aplica sin permiso.",
                "Minimizar y cerrar a la bandeja del sistema.",
                "Este recorrido guiado se puede volver a lanzar desde Tutorial.",
            }),
    });
}