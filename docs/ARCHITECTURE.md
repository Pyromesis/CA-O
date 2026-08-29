# Arquitectura CA-O 2.0

## Principio rector

> "¿Cuál es la mejor configuración para ESTE Windows, ESTE hardware, ESTE driver, ESTE juego y ESTE objetivo?"

No "¿qué tweaks tiene Internet?". Cada decisión de diseño sirve a los cuatro pilares: **DIAGNOSE → OPTIMIZE → BENCHMARK → RECOVER**.

## Procesos y confianza

```
┌──────────────────────┐   Named Pipe (ACL+nonce)   ┌─────────────────────────┐
│  CA-O.UI (WinUI 3)   │ ─────────────────────────► │ CA-O.Privileged         │
│  requireAdministrator│  Request fuertemente       │ Servicio Windows        │
│  - Diagnóstico       │  tipado + allowlist        │ (LocalSystem)           │
│  - Análisis          │ ◄───────────────────────── │ - Registry/Services     │
│  - Benchmark local   │  Respuesta JSON            │ - Power/Network/BCD     │
└──────────────────────┘                            └─────────────────────────┘
```

- La UI **siempre eleva** (`app.manifest` `requireAdministrator` — UAC en cada inicio); toda escritura cruza el pipe al servicio `SYSTEM` aun estando elevada, manteniendo el modelo de privilegio mínimo y auditado.
- El servicio valida: versión de protocolo, identidad del cliente (`GetImpersonationUserName`), GUID único + nonce (replay), esquema de `OperationParameters`, y sólo ejecuta operaciones de la lista blanca.
- No existe ningún endpoint HTTP ni ejecución de cadenas arbitrarias.

## Capas

| Proyecto | Responsabilidad | Dependencias |
|---|---|---|
| `CA-O.Shared` | DTOs, contratos IPC v2 (+Ping/GetServiceStatus), `CorrelationId`, `CaOPaths`, `ErrorCodes CAO-XXX-nnn` (`Contracts/`, `DTO/`, `IPC/`, `Enums/`, `Versioning/`) | — |
| `CA-O.Core` | Catálogo y motor transaccional (`PRECHECK→SNAPSHOT→APPLY→VERIFY→COMMIT` + `GameCompatibilityPolicy` matriz SAFE/CAUTION/BLOCKED `CAO-GAME-001`), scoring, perfiles, `AntiCheatGuard`, `HealthEngine`, `CaoHealthCheck`, `CrashRecovery` | Shared |
| `CA-O.Infrastructure` | WMI 5s timeout + `SystemAnalysisService` (WhenAll, cancelación) + `AnalysisStateStore` (atomico, 24h TTL) + `SnapshotRepository` (TX identity) + `StructuredLogger` (correlation) + `FileSnapshotStore` (SHA-256) + `JsonHistoryLogger` (hash-chain) + benchmark | Core, Shared |
| `CA-O.Privileged` | Servicio `SYSTEM` + Named Pipe `CA-O.Privileged.v1` (ACL + `Ping` health + `ReplayCache` 30s) + `OptimizationEngine` con hard block gaming | Core, Infrastructure, Shared |
| `CA-O.UI` | WinUI 3 Mica + 8 ViewModels DI (`AppHost`) + `Controls` (`MetricCard/RiskBadge/ScoreRing`) + `ErrorTranslator` + `ReducedMotion` + páginas con `VisualState` responsive | Core, Infrastructure, Shared |
| `CA-O.InstallerGui` / `CA-O.Setup` | Instalador GUI 680×620 + consola fallback, ambos `requireAdministrator`, auto-registro servicio + atajos | — |

Las versiones de paquetes se centralizan en `Directory.Packages.props`.

## Motores

| Motor | Ubicación | Notas |
|---|---|---|
| Transaccional (PRECHECK→COMPATIBILITY→SNAPSHOT→APPLY→VERIFY→BENCHMARK→COMMIT) | `Core/Rollback/OptimizationTransaction.cs` | Rollback con verificación post-reversión |
| Crash recovery | `Core/Rollback/CrashRecoveryService.cs` | Snapshot sin commit+verify = Incomplete; nunca asume éxito |
| Health | `Core/Diagnostics/HealthEngine.cs` | Sólo dimensiones medidas se puntúan; el resto Score=null |
| Recomendaciones + scoring | `Core/Scoring/` | Buckets analyze-first + score 0-100 |
| Known-issues | `Core/Compatibility/KnownIssueMatcher.cs` + store JSON en Infrastructure | DB versionada con override drop-in en `%ProgramData%\CA-O\known-issues.json` |
| Detección de juegos / anti-cheats | `Infrastructure/Gaming/` | Procesos conocidos; lectura de servicios/drivers |

Desviaciones documentadas respecto al árbol literal del prompt: `Core/Services` conserva accesores reutilizados por el servicio; `Infrastructure/SystemInterop` agrupa proveedores WMI en lugar de dividirlos por carpeta Windows/*; ETW queda como limitación documentada (DPC/ISR por contador, atribución por driver futura vía trazas).

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

El health score **nunca depende** de la cantidad de tweaks aplicados: cada dimensión se puntúa únicamente con mediciones reales y, sin datos, queda explícitamente como no medida (Score=null).

1. `AntiCheatGuard.Evaluate` — motivos específicos: `blocked-anticheat` > `blocked-by-default` > caution.
2. `Compatibility.Rules.EvaluatePreconditions` — contexto real (build, térmico, batería, SSD).
3. `RecommendationEngine.Classify` — exactamente un bucket por optimización:
   Recommended / Optional / Experimental / SecuritySensitive / NotApplicable.
4. `OptimizationScoreCalculator` — 0..100 combinando beneficio esperado, evidencia, riesgo, seguridad, compatibilidad y reversibilidad. Los cambios `DiagnosticOnly` no se puntúan.

## Perfiles (spec 104)

Safe · Balanced · Gaming · Competitive · Privacy · Security · Maintenance · Expert · Custom. Cada perfil consulta el `SystemContext`; ninguno es una lista fija. Expert es el único que ve experimental/security-sensitive y aun así exige confirmación + snapshot.

## Persistencia (atomica + versionada + tolerante a corrupción)

| Ruta | Contenido | Garantía |
|---|---|---|
| `%ProgramData%\CA-O\analysis-state.json` | `AnalysisStateStore` Schema v2: `TimestampUtc, AppVersion, WindowsBuild, Context, Recommendations, Health, AnalysisState (Completed/WithWarnings/Failed), Warnings, Duration, CorrelationId` | `tmp→flush→Move(overwrite)`, cuarentena `.corrupt.timestamp` |
| `%ProgramData%\CA-O\history.jsonl` | `JsonHistoryLogger` hash-chain SHA-256: `seq, prevHash, hash, entry` — `ReadLast` tolera líneas corruptas, `VerifyIntegrity` reporta | `VerifyIntegrity` warnings, nunca crash |
| `%ProgramData%\CA-O\snapshots/{txid}/` | `snapshot.json + manifest.json + integrity.json (SHA256)` — TX identity, `FindLatestForOptimization` | `SnapshotRepository` (UI nunca `Directory.GetDirectories`) |
| `%ProgramData%\CA-O\benchmarks\baseline.json` | `SystemBenchmarkResult` + `BenchmarkRunHeader` | Mediana de trials, suelo 3% |
| `%AppData%\CA-O\settings.json` | Preferencias UI + `UiState.LastAnalysisUtc` restaurado al iniciar | 3 fases startup sin bloquear UI |

Nunca se registran secretos, credenciales ni contenido de entrada del usuario (spec 75, 116).

## Decisiones técnicas documentadas

- **PowerShell como fallback only**: las optimizaciones usan APIs nativas (Registry API, Service Control Manager vía wrappers, `powercfg`); cualquier comando externo debe existir en `ElevatedCommandCatalog` (contrato probado en `ElevatedCommandCatalogTests`).
- **DPC/ISR**: muestreo por contadores de rendimiento (% DPC Time / % Interrupt Time). La atribución por driver exige trazas ETW kernel; está documentada como limitación en lugar de fingir precisión.
- **Térmicas**: zonas ACPI vía WMI cuando existen; umbrales heurísticos declarados como tales hasta integrar APIs de fabricante.
- **i18n**: diccionario tipado es-ES/en-US (`Localizer`); migración a `.resw` planificada sin cambiar llamadas (`Localizer.Get(key)`).
- **HAGS/VBS/MPO/etc.**: nunca tratados como ganancias universales; HAGS es WorkloadDependent+Conditional+RequiresReboot, VBS es Critical+SecurityTradeoff+ExpertOnly.

## Pruebas (245 passed, 0 failed — Release)

| Suite | Cubre | Count |
|---|---|---|
| `CA-O.Core.Tests` | Contratos catálogo, `AnalysisStateStore` (save/load/corrupt), `GameCompatibility` (VBS bloqueado), transacciones, scoring, health | 134 |
| `CA-O.Security.Tests` | IPC validator (`Ping`/`Apply` cross-check), `IpcPingTests`, inyección | 33 |
| `CA-O.Infrastructure.Tests` | `HistoryRobustness` (malformed), `SnapshotRepository` (TX identity), `SystemContextCache` dual-TTL | 17 |
| `CA-O.Integration.Tests` | `E2EFlowsTests` 10 flujos (Abrir→Analizar→Persistir→Vanguard→Restore→Benchmark) + `TransactionJournalRecovery` | 46 |
| `CA-O.Benchmark.Tests` | `SystemBenchmarkRunner` (suelo 3%, mediana) | 7 |
| `CA-O.UI.Tests` | `ViewModelTests` (Analyze/Dashboard con `SystemAnalysisService` + `Correlation`) | 8 |
| **Total** | **Gates 1-5 `verify.ps1` + `build-release` con `gui-installer`** | **245** |
