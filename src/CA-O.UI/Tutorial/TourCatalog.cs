using System;
using System.Collections.Generic;

namespace CAO.UI.Tutorial;

/// <summary>One spotlight step: a route, the x:Name of the control to highlight, the copy and the mascot mood.</summary>
public sealed record TourStep(string Route, string TargetName, string Title, string Body, string Mood);

/// <summary>A tutorial chapter rendered on the Tutorial page and linked to the tour step of the same route.</summary>
public sealed record TutorialChapter(string Route, string Title, string Summary, IReadOnlyList<string> Points, string Glyph = "");

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
            "Aquí ves la salud del sistema y el resumen de lo que CA-O ha encontrado. Este botón lanza un análisis completo: es siempre el primer paso.",
            "Idle"),
        new TourStep(
            "analyze",
            "RunButton",
            "1. Analizar",
            "El análisis recorre el sistema y guarda los puntos de mejora. No cambia nada todavía: solo informa.",
            "Working"),
        new TourStep(
            "optimize",
            "ApplyRecommendedButton",
            "2. Optimizar",
            "CA-O propone una lista de cambios recomendados. Este botón los aplica de forma reversible, guardando un punto de restauración antes.",
            "Working"),
        new TourStep(
            "cleanup",
            "CleanAllButton",
            "3. Limpieza",
            "Limpia archivos temporales y cachés que ocupan espacio. Es rápido y también queda registrado en el historial.",
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
        Chapter("dashboard", "Panel", "",
            "El punto de partida: salud del equipo, avisos y accesos rápidos.",
            "El índice global resume lo medido en el último análisis.",
            "Los avisos de arriba indican reinicios pendientes o modo solo lectura.",
            "Cada módulo de abajo te lleva directo a su página."),
        Chapter("analyze", "Analizar", "",
            "Lee el estado real del sistema y genera la lista de mejoras.",
            "Lanza un análisis completo con el botón principal.",
            "Cada hallazgo incluye una explicación y su severidad.",
            "El análisis no modifica nada: es siempre seguro repetirlo."),
        Chapter("optimize", "Optimizar", "",
            "Aplica los cambios recomendados de forma reversible.",
            "Antes de cada cambio se guarda un snapshot exacto del estado.",
            "Filtra por recomendadas, opcionales o activas antes de aplicar.",
            "Cada tarjeta muestra riesgo, evidencia y si requiere reiniciar."),
        Chapter("cleanup", "Limpieza", "",
            "Libera espacio con temporales, cachés y mantenimiento oficial de Windows.",
            "Cada categoría se puede limpiar por separado o con Ejecutar todo.",
            "Cada limpieza informa archivos y espacio liberado.",
            "Aquí también vive el timer resolution para juegos."),
        Chapter("network", "Red gaming", "",
            "Mide tu conexión a los servidores de juego y prioriza su tráfico.",
            "Ping, jitter y pérdida reales por región de juego.",
            "Monitor en vivo y análisis de ruta que dice dónde nace el lag.",
            "Ajustes de red para juegos aplicables y reversibles."),
        Chapter("solucionar", "Solucionar", "",
            "Arreglos directos para fallos comunes de audio, vídeo, red y sistema.",
            "Cada botón ejecuta una solución real, no un consejo.",
            "Las que interrumpen algo (pantalla, red) piden confirmación.",
            "El resultado aparece debajo de cada grupo."),
        Chapter("drivers", "Drivers", "",
            "Inventario de controladores, conflictos y dispositivos fantasma.",
            "Escanear lee todos los dispositivos, presentes y ocultos.",
            "Los dispositivos con error se pueden corregir o actualizar.",
            "Los fantasmas (hardware ya desconectado) se pueden limpiar."),
        Chapter("benchmark", "Benchmark", "",
            "Mide antes y después de un cambio para saber si mejoró de verdad.",
            "Primero la línea base, luego el cambio, luego medir otra vez.",
            "Por debajo del 3 por ciento de diferencia se considera ruido.",
            "También compara DNS y tiempos de arranque."),
        Chapter("overclock", "Overclock", "",
            "Ajuste de CPU y GPU con las herramientas oficiales del fabricante.",
            "Los perfiles aplican valores fijos; el automático prueba paso a paso.",
            "Antes de escribir nada se crea un punto de restauración.",
            "Restaurar valores deshace el último cambio."),
        Chapter("restore", "Restaurar", "",
            "Deshace cualquier cambio de CA-O desde su snapshot.",
            "Cada operación reversible deja un snapshot con fecha.",
            "Restaurar devuelve el estado exacto anterior y lo verifica.",
            "Las limpiezas no se pueden deshacer: los archivos ya no existen."),
        Chapter("history", "Historial", "",
            "Todo lo que CA-O ha hecho, con fecha y resultado.",
            "Registro de análisis, optimizaciones, limpiezas y reversiones.",
            "Filtra por tipo o busca por identificador.",
            "Cada entrada indica si la verificación pasó o falló."),
        Chapter("settings", "Ajustes", "",
            "Idioma, tema, Modo Expert, bandeja y servicio privilegiado.",
            "Idioma y tema (claro, oscuro o del sistema).",
            "Modo Expert muestra los cambios de mayor riesgo, siempre con confirmación.",
            "Desde aquí se instala o se comprueba el servicio."),
    });

    private static TutorialChapter Chapter(string route, string title, string glyph, string summary, params string[] points) =>
        new(route, title, summary, Array.AsReadOnly(points), glyph);
}
