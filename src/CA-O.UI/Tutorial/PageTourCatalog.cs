using System;
using System.Collections.Generic;

namespace CAO.UI.Tutorial;

/// <summary>
/// Tour por pagina: una lista de pasos por cada ruta de <c>RouteTable.Order</c> salvo
/// "tutorial", que es el hub y queda excluida. Mismo tipo <c>TourStep</c> y mismo patron
/// de literales que <c>TourCatalog</c>: datos puros, sin WinUI, totalmente testeables.
/// Cada <c>TargetName</c> existe como <c>x:Name</c> en el XAML de su pagina.
/// </summary>
public static class PageTourCatalog
{
    private static IReadOnlyList<TourStep> List(params TourStep[] steps) =>
        Array.AsReadOnly(steps);

    public static IReadOnlyDictionary<string, IReadOnlyList<TourStep>> ByRoute { get; } =
        new Dictionary<string, IReadOnlyList<TourStep>>(StringComparer.Ordinal)
        {
            ["dashboard"] = List(
                new TourStep(
                    "dashboard",
                    "AnalyzeButton",
                    "Empieza aqui: Analizar",
                    "Este boton lanza el analisis completo del sistema. Es siempre el primer paso: CA-O mide antes de recomendar nada.",
                    "Working"),
                new TourStep(
                    "dashboard",
                    "OptimizeCtaButton",
                    "Ver recomendadas",
                    "Te lleva a Optimizar con las recomendaciones del ultimo analisis. Solo se actua sobre lo ya medido.",
                    "Idle"),
                new TourStep(
                    "dashboard",
                    "ScoreValueText",
                    "Indice global",
                    "Nota de 0 a 100 calculada desde las dimensiones medidas. Sin un analisis previo no hay nota.",
                    "Idle"),
                new TourStep(
                    "dashboard",
                    "CpuNameText",
                    "Tu procesador",
                    "Modelo detectado y su carga actual. Si hay throttling termico, CA-O avisa antes de proponer cambios.",
                    "Idle"),
                new TourStep(
                    "dashboard",
                    "GpuNameText",
                    "Tus graficos",
                    "GPU detectada y sus detalles. El ajuste fino de GPU vive en Overclock, con herramientas del fabricante.",
                    "Idle"),
                new TourStep(
                    "dashboard",
                    "RamUsedText",
                    "Memoria en uso",
                    "Cuanta RAM ocupas y cuanta queda libre. Liberar memoria temporal se hace desde Limpieza.",
                    "Idle"),
                new TourStep(
                    "dashboard",
                    "DiskFreeText",
                    "Disco del sistema",
                    "Espacio libre y total del disco del sistema. Con poco espacio fallan las actualizaciones y los snapshots.",
                    "Idle"),
                new TourStep(
                    "dashboard",
                    "SecBootText",
                    "Postura de seguridad",
                    "Secure Boot, VBS, HVCI y servicio de un vistazo. Con anti-cheat activo, algunos cambios quedan bloqueados.",
                    "Celebrate")),
            ["analyze"] = List(
                new TourStep(
                    "analyze",
                    "RunButton",
                    "El boton principal",
                    "Ejecuta el analisis completo: CPU, GPU, memoria, disco, red, seguridad, drivers, termica e input. No modifica nada.",
                    "Working"),
                new TourStep(
                    "analyze",
                    "StatusText",
                    "Estado del diagnostico",
                    "Aqui se anuncia el progreso y el resultado de cada medicion, en lenguaje claro y sin tecnicismos.",
                    "Idle"),
                new TourStep(
                    "analyze",
                    "NoteBar",
                    "Diagnosticos primero",
                    "La regla de oro de CA-O: cada area se mide antes de recomendar cualquier cambio. Nada aqui toca el sistema.",
                    "Idle"),
                new TourStep(
                    "analyze",
                    "HowItWorksCard",
                    "Como funciona",
                    "Mide, revisa y actua: tres pasos guiados que te llevan del analisis a Optimizar o a Limpieza.",
                    "Idle"),
                new TourStep(
                    "analyze",
                    "NetworkText",
                    "Red",
                    "Latencia, jitter y DNS medidos de verdad. Si un DNS es lento, aqui mismo puedes aplicar el mejor detectado.",
                    "Idle"),
                new TourStep(
                    "analyze",
                    "SecurityText",
                    "Seguridad",
                    "Secure Boot, VBS y anti-cheat detectado. Lo que reduzca seguridad queda marcado antes de aplicarse.",
                    "Idle"),
                new TourStep(
                    "analyze",
                    "GamingBlockedText",
                    "Gaming y anti-cheat",
                    "Si hay anti-cheat, las optimizaciones que chocan con el quedan con candado en Optimizar para no romper tus juegos.",
                    "Idle"),
                new TourStep(
                    "analyze",
                    "FindingsList",
                    "Hallazgos",
                    "Cada hallazgo sale de una medicion real, con su severidad. Son la materia prima de las recomendaciones.",
                    "Celebrate")),
            ["optimize"] = List(
                new TourStep(
                    "optimize",
                    "ApplyRecommendedButton",
                    "Aplicar recomendados",
                    "Aplica de golpe los cambios recomendados. Antes de tocar nada se crea un punto de restauracion.",
                    "Working"),
                new TourStep(
                    "optimize",
                    "StatusText",
                    "Estado de la operacion",
                    "Cada aplicacion informa aqui su resultado verificado. Nada se da por aplicado sin verificacion exacta.",
                    "Idle"),
                new TourStep(
                    "optimize",
                    "FilterAllButton",
                    "Filtro: todas",
                    "Muestra la lista completa de optimizaciones detectadas, con su contador entre parentesis.",
                    "Idle"),
                new TourStep(
                    "optimize",
                    "FilterRecommendedButton",
                    "Filtro: recomendadas",
                    "Solo lo seguro y medido. Es el unico grupo que se aplica sin Modo Expert.",
                    "Idle"),
                new TourStep(
                    "optimize",
                    "FilterOptionalButton",
                    "Filtro: opcionales",
                    "Mejoras validas pero no esenciales. Requieren Modo Expert para aplicarse.",
                    "Idle"),
                new TourStep(
                    "optimize",
                    "FilterExperimentalButton",
                    "Filtro: experimentales",
                    "Cambios que pueden ser inestables. Solo visibles en Modo Expert y siempre con confirmacion.",
                    "Idle"),
                new TourStep(
                    "optimize",
                    "RecommendationsList",
                    "La lista",
                    "Cada tarjeta muestra diff real, riesgo, seguridad, compatibilidad y reversibilidad. Lo ya aplicado figura como Activo.",
                    "Celebrate")),
            ["cleanup"] = List(
                new TourStep(
                    "cleanup",
                    "CleanAllButton",
                    "Ejecutar todo",
                    "Lanza seguidas las limpiezas de temporales de esta tarjeta. Es rapido y queda registrado en el historial.",
                    "Working"),
                new TourStep(
                    "cleanup",
                    "CardTemp",
                    "Archivos temporales",
                    "Temporales de Windows y cache de Delivery Optimization. Solo borra lo que no esta en uso.",
                    "Idle"),
                new TourStep(
                    "cleanup",
                    "CardDisk",
                    "Disco",
                    "Analisis del espacio del disco del sistema: que ocupa y cuanto se puede liberar con seguridad.",
                    "Idle"),
                new TourStep(
                    "cleanup",
                    "CardWinSxS",
                    "Mantenimiento",
                    "Limpieza de componentes de Windows (WinSxS) con las herramientas oficiales del sistema.",
                    "Idle"),
                new TourStep(
                    "cleanup",
                    "CardTimer",
                    "Timer de precision",
                    "Resolucion del temporizador del sistema. Afecta a la latencia en juegos; se revierte desde aqui mismo.",
                    "Idle"),
                new TourStep(
                    "cleanup",
                    "CardSense",
                    "Sensor de almacenamiento",
                    "Configuracion del Sensor de almacenamiento de Windows para mantener el disco limpio de forma automatica.",
                    "Idle"),
                new TourStep(
                    "cleanup",
                    "CardDeep",
                    "Limpieza profunda",
                    "Categorias adicionales que liberan mas espacio. Revisa cada una: alguna pide confirmacion.",
                    "Idle"),
                new TourStep(
                    "cleanup",
                    "CleanupStatusText",
                    "Resultado",
                    "Cada limpieza informa aqui archivos y KB liberados. Sin esta linea, no se da por hecha.",
                    "Celebrate")),
            ["network"] = List(
                new TourStep(
                    "network",
                    "ConnectionCard",
                    "Tu conexion",
                    "Cable o Wi-Fi y el adaptador activo. Por Wi-Fi, un cable suele arreglar mas picos de ping que cualquier ajuste.",
                    "Idle"),
                new TourStep(
                    "network",
                    "MeasureRegionsButton",
                    "Ping a regiones",
                    "Mide ping, jitter y perdida reales contra las regiones de nube donde se alojan muchos juegos. No cambia nada.",
                    "Working"),
                new TourStep(
                    "network",
                    "RegionsList",
                    "La mejor region",
                    "Ordenadas de menor a mayor ping. Elige en el juego la mejor; selecciona una fila para vigilarla en vivo.",
                    "Idle"),
                new TourStep(
                    "network",
                    "LiveToggleButton",
                    "Monitor en vivo",
                    "Una medicion por segundo mientras juegas: ping, jitter, perdida y picos. Acepta la IP del servidor del juego.",
                    "Working"),
                new TourStep(
                    "network",
                    "LiveChartCanvas",
                    "Grafica de ping",
                    "El ultimo minuto de mediciones. Una perdida se dibuja arriba del todo para que no pase desapercibida.",
                    "Idle"),
                new TourStep(
                    "network",
                    "TraceButton",
                    "Donde nace el lag",
                    "Traceroute salto a salto con diagnostico: tu Wi-Fi/router, tu proveedor o la distancia.",
                    "Idle"),
                new TourStep(
                    "network",
                    "BoostApplyButton",
                    "Optimizar la red",
                    "Prioridad QoS para juegos, sin limite con audio, TCP inmediato y tarjeta sin ahorro. Todo reversible.",
                    "Working"),
                new TourStep(
                    "network",
                    "HonestyCard",
                    "Lo que no se promete",
                    "Las rutas alternativas por servidores propios necesitan una red de servidores que CA-O no tiene. Aqui solo lo medible.",
                    "Celebrate")),
            ["solucionar"] = List(
                new TourStep(
                    "solucionar",
                    "PageContent",
                    "Soluciones que actuan",
                    "Esta pagina no diagnostica: ejecuta soluciones directas para fallos comunes, cada una con efecto real y verificacion.",
                    "Idle"),
                new TourStep(
                    "solucionar",
                    "SolucionarCat",
                    "Tu ayudante",
                    "La mascota te acompana mientras se aplica cada solucion y celebra cuando el problema queda resuelto.",
                    "Idle"),
                new TourStep(
                    "solucionar",
                    "AudioStatusText",
                    "Audio",
                    "Reinicio de servicios de audio, acceso al microfono y Bluetooth. Aqui se confirma cada resultado.",
                    "Working"),
                new TourStep(
                    "solucionar",
                    "VideoStatusText",
                    "Video",
                    "Compositor de escritorio y cache de iconos. Soluciones habituales cuando la imagen falla o va a saltos.",
                    "Idle"),
                new TourStep(
                    "solucionar",
                    "NetworkStatusText",
                    "Red",
                    "Vaciar cache DNS y reiniciar el cliente DNS. Primer paso cuando internet falla solo en este equipo.",
                    "Idle"),
                new TourStep(
                    "solucionar",
                    "PrintStatusText",
                    "Impresion",
                    "Reinicio de la cola de impresion. Lo tipico cuando los documentos se atascan sin error claro.",
                    "Idle"),
                new TourStep(
                    "solucionar",
                    "SystemStatusText",
                    "Sistema",
                    "Explorador de Windows y otros arreglos generales, con fallback local si el servicio no responde.",
                    "Celebrate")),
            ["drivers"] = List(
                new TourStep(
                    "drivers",
                    "ScanButton",
                    "Escanear drivers",
                    "Lee el inventario total de drivers instalados con SetupAPI y WMI. Tarda segundos y no cambia nada.",
                    "Working"),
                new TourStep(
                    "drivers",
                    "ScanStatusText",
                    "Estado del escaneo",
                    "Te dice cuando pulsar Escanear y que se encontro. Sin escaneo no hay diagnostico.",
                    "Idle"),
                new TourStep(
                    "drivers",
                    "TotalText",
                    "Total",
                    "Cuantos drivers hay instalados en el equipo. La cifra base de todo el inventario.",
                    "Idle"),
                new TourStep(
                    "drivers",
                    "ProblemsText",
                    "Con problemas",
                    "Dispositivos con codigo de problema de Windows. Son los primeros candidatos a corregir.",
                    "Idle"),
                new TourStep(
                    "drivers",
                    "ConflictsText",
                    "Conflictos",
                    "Resumen de conflictos conocidos antes de que causen fallos, con fabricante y version de cada controlador.",
                    "Idle"),
                new TourStep(
                    "drivers",
                    "DriversList",
                    "El inventario",
                    "La lista completa filtrable por nombre, clase o fabricante. Nada se toca solo por mirarla.",
                    "Idle"),
                new TourStep(
                    "drivers",
                    "SearchBox",
                    "Filtrar",
                    "Escribe para localizar un driver concreto sin recorrer la lista entera.",
                    "Idle"),
                new TourStep(
                    "drivers",
                    "OemText",
                    "Enlace del fabricante",
                    "CA-O detecta el fabricante y te da su enlace oficial. Los drivers se descargan del vendor, nunca de sitios raros.",
                    "Celebrate")),
            ["benchmark"] = List(
                new TourStep(
                    "benchmark",
                    "BaselineButton",
                    "Medir linea base",
                    "Paso 1: mide CPU, memoria y disco antes del cambio. Sin linea base no hay comparacion honesta.",
                    "Working"),
                new TourStep(
                    "benchmark",
                    "AfterButton",
                    "Medir tras cambio",
                    "Paso 3: repite las mismas cargas despues de aplicar el cambio. Cargas identicas, o no vale.",
                    "Idle"),
                new TourStep(
                    "benchmark",
                    "BenchStatusText",
                    "Medicion A/B",
                    "El estado de la comparacion en curso: que se esta midiendo y en que paso del flujo vas.",
                    "Idle"),
                new TourStep(
                    "benchmark",
                    "DnsButton",
                    "Medir DNS",
                    "Benchmark real de DNS: latencia medida, no impuesta. El mas rapido se puede aplicar desde aqui.",
                    "Idle"),
                new TourStep(
                    "benchmark",
                    "DnsResultText",
                    "Resultado DNS",
                    "Tiempos por proveedor con el ganador marcado. Sin numeros no hay recomendacion.",
                    "Idle"),
                new TourStep(
                    "benchmark",
                    "FluencyButton",
                    "Fluidez gaming",
                    "Mide 10 segundos de fluidez real: FPS medio, 1% low y P99. Lo que sientes al jugar, en cifras.",
                    "Idle"),
                new TourStep(
                    "benchmark",
                    "BootText",
                    "Arranque",
                    "Cuanto tardo el ultimo arranque del sistema. Referencia util tras optimizar el inicio.",
                    "Idle"),
                new TourStep(
                    "benchmark",
                    "ComparisonText",
                    "Veredicto",
                    "Comparacion A/B con suelo de ruido del 3%: si la diferencia no es medible, se dice. Sin humo.",
                    "Celebrate")),
            ["overclock"] = List(
                new TourStep(
                    "overclock",
                    "HeadlineText",
                    "Overclock guiado",
                    "Potencia y relojes de la GPU, turbo de la CPU y estado de la RAM, con APIs oficiales del fabricante. Nada de valores inventados ni escrituras a ciegas.",
                    "Idle"),
                new TourStep(
                    "overclock",
                    "ProfileBox",
                    "El perfil",
                    "Conservador (de fábrica), Equilibrado o Rendimiento. Aplica valores fijos sin prueba de estabilidad.",
                    "Idle"),
                new TourStep(
                    "overclock",
                    "ApplyProfileButton",
                    "Aplicar perfil",
                    "Aplica los valores del perfil elegido. Antes se crea un punto de restauración de Windows.",
                    "Working"),
                new TourStep(
                    "overclock",
                    "AutoTuneButton",
                    "Overclock automático",
                    "Sube potencia y turbo, y prueba los relojes de la GPU paso a paso. Deja un juego o benchmark abierto: sin carga, un paso no cuenta como estable.",
                    "Working"),
                new TourStep(
                    "overclock",
                    "RestoreAppliedButton",
                    "Restaurar valores",
                    "Deshace el último perfil o el último overclock automático de un clic.",
                    "Idle"),
                new TourStep(
                    "overclock",
                    "ReadOnlyNotice",
                    "Cómo funciona",
                    "Los tres pasos: medir, aplicar y volver a medir. Cada control de abajo dice si se puede ajustar y, si no, por qué.",
                    "Idle"),
                new TourStep(
                    "overclock",
                    "RefreshButton",
                    "Detectar",
                    "Vuelve a sondear el hardware mediante las APIs del fabricante. Usalo tras actualizar drivers.",
                    "Idle"),
                new TourStep(
                    "overclock",
                    "BaselineButton",
                    "Capturar linea base",
                    "Mide el sistema antes del perfil. El efecto del overclock no es aditivo: se mide del perfil completo.",
                    "Idle"),
                new TourStep(
                    "overclock",
                    "CandidateButton",
                    "Medir con el perfil",
                    "Repite la medicion con el perfil aplicado y publica el delta total con su techo. Asi sabes si gano algo.",
                    "Celebrate")),
            ["restore"] = List(
                new TourStep(
                    "restore",
                    "NoteBar",
                    "Reversibilidad garantizada",
                    "CA-O crea snapshots propios antes de cada cambio. Si algo no te gusta, vuelves atras con un clic.",
                    "Idle"),
                new TourStep(
                    "restore",
                    "RestoreCat",
                    "Red de seguridad",
                    "Cada transaccion guarda su estado exacto antes de mutar. Esta pagina es la red de seguridad de todo el proceso.",
                    "Idle"),
                new TourStep(
                    "restore",
                    "OpenHistoryButton",
                    "Ver historial",
                    "Salta al historial completo de esa operacion: quien, cuando y con que resultado.",
                    "Idle"),
                new TourStep(
                    "restore",
                    "RecoveryHintText",
                    "Recuperaciones",
                    "Si un cierre inesperado dejo operaciones sin cerrar, aqui se anuncian para que decidas que hacer.",
                    "Idle"),
                new TourStep(
                    "restore",
                    "SnapshotsList",
                    "Tus snapshots",
                    "Uno por transaccion, con fecha y resumen. Restaurar revierte los cambios de esa operacion y lo verifica.",
                    "Working"),
                new TourStep(
                    "restore",
                    "PendingRecoveryList",
                    "Pendientes",
                    "Operaciones a medio cerrar por un reinicio: se revierten o se confirman desde aqui, nunca solas.",
                    "Celebrate")),
            ["history"] = List(
                new TourStep(
                    "history",
                    "HistoryCat",
                    "Todo queda registrado",
                    "Analisis, optimizaciones y limpiezas con fecha y resultado. Auditable de principio a fin.",
                    "Idle"),
                new TourStep(
                    "history",
                    "FilterBox",
                    "Filtrar",
                    "Todo, optimizacion, rollback, errores o seguridad. Localiza lo que buscas sin leer el archivo entero.",
                    "Idle"),
                new TourStep(
                    "history",
                    "SearchBox",
                    "Buscar",
                    "Busca por TransactionId u OptimizationId. Cada operacion tiene su identificador unico.",
                    "Idle"),
                new TourStep(
                    "history",
                    "HistoryList",
                    "La timeline",
                    "Cada fila muestra TransactionId, operacion, verificacion y su eslabon hash. La cadena se valida sola.",
                    "Working"),
                new TourStep(
                    "history",
                    "WarningBar",
                    "Alerta de corrupcion",
                    "Si alguna entrada no cuadra con la cadena hash, se avisa aqui de forma explicita. Sin silencios.",
                    "Idle"),
                new TourStep(
                    "history",
                    "SourceNote",
                    "La fuente",
                    "Donde vive el archivo real del historial en tu disco, para que puedas auditarlo por tu cuenta.",
                    "Idle"),
                new TourStep(
                    "history",
                    "EmptyHistoryCard",
                    "Sin historial aun",
                    "Si aun no aplicaste nada, veras este aviso. El primer cambio estrena la auditoria.",
                    "Celebrate")),
            ["settings"] = List(
                new TourStep(
                    "settings",
                    "SettingsCat",
                    "Ajustes por categorias",
                    "Sin listas infinitas: apariencia, politica, bandeja, servicio y actualizaciones, cada cosa en su tarjeta.",
                    "Idle"),
                new TourStep(
                    "settings",
                    "ThemeBox",
                    "Tema",
                    "Claro, oscuro o el del sistema. Se aplica al instante en toda la app.",
                    "Idle"),
                new TourStep(
                    "settings",
                    "LanguageBox",
                    "Idioma",
                    "Espanol o ingles como ciudadanos de primera. Toda la interfaz pasa por el Localizer.",
                    "Idle"),
                new TourStep(
                    "settings",
                    "ExpertSwitch",
                    "Modo Expert",
                    "Expone cambios opcionales, experimentales y sensibles a seguridad. Nunca aplica nada sin confirmacion ni snapshot.",
                    "Working"),
                new TourStep(
                    "settings",
                    "MinimizeToTraySwitch",
                    "Minimizar a la bandeja",
                    "Al minimizar, la ventana se oculta y queda el icono para que el monitoreo no se interrumpa.",
                    "Idle"),
                new TourStep(
                    "settings",
                    "CloseToTraySwitch",
                    "Cerrar a la bandeja",
                    "La X oculta en vez de salir. La salida real esta en el menu del icono de la bandeja.",
                    "Idle"),
                new TourStep(
                    "settings",
                    "ServiceCheckButton",
                    "Servicio privilegiado",
                    "Comprueba el servicio SYSTEM que aplica los cambios. La UI nunca tiene privilegios: todo viaja por el pipe seguro.",
                    "Idle"),
                new TourStep(
                    "settings",
                    "UpdateCheckButton",
                    "Actualizaciones",
                    "Comprueba GitHub Releases. Solo avisa: nunca descarga ni instala solo.",
                    "Celebrate")),
        };

    /// <summary>
    /// Pasos del tour de una pagina. Devuelve lista vacia para "tutorial" (el hub,
    /// excluido por diseno) y para rutas desconocidas.
    /// </summary>
    public static IReadOnlyList<TourStep> ForRoute(string? route) =>
        route is not null && ByRoute.TryGetValue(route, out var steps)
            ? steps
            : Array.AsReadOnly(Array.Empty<TourStep>());
}
