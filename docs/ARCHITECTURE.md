# Arquitectura CA-O 2.0

## Principio rector

> "¿Cuál es la mejor configuración para ESTE Windows, ESTE hardware, ESTE driver, ESTE juego y ESTE objetivo?"

No "¿qué tweaks tiene Internet?". Cada decisión de diseño sirve a los cuatro pilares: **DIAGNOSE → OPTIMIZE → BENCHMARK → RECOVER**.

## Procesos y confianza

```
┌──────────────────────┐   Named Pipe (ACL+nonce)   ┌─────────────────────────┐
│  CA-O.UI (WinUI 3)   │ ─────────────────────────► │ CA-O.Privileged         │
│  Sin privilegios     │  Request fuertemente       │ Servicio Windows        │
│  - Diagnóstico       │  tipado + allowlist        │ (LocalSystem)           │
│  - Análisis          │ ◄───────────────────────── │ - Registry/Services     │
│  - Benchmark local   │  Respuesta JSON            │ - Power/Network/BCD     │
└──────────────────────┘                            └─────────────────────────┘
```

- La UI jamás eleva: toda lectura es accesible sin admin; toda escritura cruza el pipe.
- El servicio valida: versión de protocolo, identidad del cliente (`GetImpersonationUserName`), GUID único + nonce (replay), esquema de `OperationParameters`, y sólo ejecuta operaciones de la lista blanca.
- No existe ningún endpoint HTTP ni ejecución de cadenas arbitrarias.

## Capas

| Proyecto | Responsabilidad | Dependencias |
|---|---|---|
| `CA-O.Shared` | DTOs, contratos de ciclo de vida, enums de clasificación, constantes de rutas/IPC, versionado | — |
| `CA-O.Core` | Catálogo de optimizaciones, motor transaccional, motor de recomendaciones, perfiles, scoring, guardas anti-cheat, reglas de compatibilidad | Shared |
| `CA-O.Infrastructure` | Implementaciones Windows: WMI/CIM, contadores de rendimiento, sondas DNS/bufferbloat, DPC sampler, stores de snapshots/historial, benchmark in-process | Core, Shared |
| `CA-O.Privileged` | Host de servicio + servidor Named Pipes + auditoría | Core, Infrastructure, Shared |
| `CA-O.UI` | Shell WinUI 3, páginas, estado compartido, cliente IPC | Core, Infrastructure, Shared |

## Ciclo de vida de una optimización (spec transaccional)

```
PRECHECK  CheckPreconditionsAsync(SystemContext)
            ├─ build soportado, SSD requerido, térmico, batería, anti-cheat
SNAPSHOT  Capture() → persistido ANTES de mutar (crash-safe, spec 122)
APPLY     ApplyAsync(context)
VERIFY    VerifyAsync() → Detect en vivo; PendingReboot aceptado
COMMIT    historial JSONL con applyResult/verification/rollbackAvailable
ROLLBACK  RollbackAsync(snapshot) automático si APPLY o VERIFY fallan
```

El lote multi-optimización aplica secuencialmente; ante un fallo se detiene y revierte lo ya aplicado cuyo riesgo sea Safe/Low (spec 124).

## Clasificación y recomendación

1. `AntiCheatGuard.Evaluate` — motivos específicos: `blocked-anticheat` > `blocked-by-default` > caution.
2. `Compatibility.Rules.EvaluatePreconditions` — contexto real (build, térmico, batería, SSD).
3. `RecommendationEngine.Classify` — exactamente un bucket por optimización:
   Recommended / Optional / Experimental / SecuritySensitive / NotApplicable.
4. `OptimizationScoreCalculator` — 0..100 combinando beneficio esperado, evidencia, riesgo, seguridad, compatibilidad y reversibilidad. Los cambios `DiagnosticOnly` no se puntúan.

## Perfiles (spec 104)

Safe · Balanced · Gaming · Competitive · Privacy · Security · Maintenance · Expert · Custom. Cada perfil consulta el `SystemContext`; ninguno es una lista fija. Expert es el único que ve experimental/security-sensitive y aun así exige confirmación + snapshot.

## Persistencia

| Ruta | Contenido |
|---|---|
| `%ProgramData%\CA-O\history.jsonl` | Una entrada JSON por operación (spec 74): timestampUtc, appVersion, windowsBuild, optimizationId, operation, precondition, snapshotId, applyResult, verification, rollbackAvailable |
| `%ProgramData%\CA-O\snapshots\*.json` | Estado capturado por optimización (valores presentes *y ausencias* para restaurar con DELETE) |
| `%ProgramData%\CA-O\benchmarks\baseline.json` | Línea base del benchmark de sistema |
| `%AppData%\CA-O\settings.json` | Preferencias UI |

Nunca se registran secretos, credenciales ni contenido de entrada del usuario (spec 75, 116).

## Decisiones técnicas documentadas

- **PowerShell como fallback only**: las optimizaciones usan APIs nativas (Registry API, Service Control Manager vía wrappers, `powercfg`); cualquier comando externo debe existir en `ElevatedCommandCatalog` (contrato probado en `ElevatedCommandCatalogTests`).
- **DPC/ISR**: muestreo por contadores de rendimiento (% DPC Time / % Interrupt Time). La atribución por driver exige trazas ETW kernel; está documentada como limitación en lugar de fingir precisión.
- **Térmicas**: zonas ACPI vía WMI cuando existen; umbrales heurísticos declarados como tales hasta integrar APIs de fabricante.
- **i18n**: diccionario tipado es-ES/en-US (`Localizer`); migración a `.resw` planificada sin cambiar llamadas (`Localizer.Get(key)`).
- **HAGS/VBS/MPO/etc.**: nunca tratados como ganancias universales; HAGS es WorkloadDependent+Conditional+RequiresReboot, VBS es Critical+SecurityTradeoff+ExpertOnly.

## Pruebas

| Suite | Cubre |
|---|---|
| `CA-O.Core.Tests` (109) | Contratos de catálogo (spec 92), transacciones (122–124), perfiles (14/59/95), buckets de recomendación, scoring, health honesto |
| `CA-O.Security.Tests` (27) | Validador IPC adversarial, catálogo de comandos contra inyección |
| `CA-O.Infrastructure.Tests` (8) | history.jsonl round-trip tolerante, snapshot store (preserva ausencias, sanitiza ids), paquetes DNS y percentiles |
