# CA-O 2.1.5 — Plataforma nativa de rendimiento, diagnóstico y optimización para Windows 11

> **Principio operativo:** diagnosticar primero → recomendar con evidencia → aplicar en transacción → verificar → revertir si falla. Sin promesas numéricas falsas, solo hechos medibles.

[![Windows 11](https://img.shields.io/badge/Windows%2011-0078D4?style=flat-square&logo=windows&logoColor=white)](https://www.microsoft.com/windows)
[![.NET 10](https://img.shields.io/badge/.NET%2010-512BD4?style=flat-square&logo=dotnet&logoColor=white)](global.json)
[![WinUI 3](https://img.shields.io/badge/WinUI%203-00B7C3?style=flat-square&logo=windows&logoColor=white)](https://microsoft.github.io/microsoft-ui-xaml/)
[![Build](https://img.shields.io/badge/build-passing-brightgreen?style=flat-square)](https://github.com/Pyromesis/CA-O/actions)
[![Tests](https://img.shields.io/badge/tests-500%20passed-brightgreen?style=flat-square)](#-pruebas)
[![Release](https://img.shields.io/badge/release-v2.1.5-blue?style=flat-square)](https://github.com/Pyromesis/CA-O/releases/tag/v2.1.5)
[![License](https://img.shields.io/badge/license-privado-lightgrey?style=flat-square)](#licencia)

**Descargas v2.1.5:** [CA-O-Setup-GUI-x64.zip (92 MB, instalador GUI)](https://github.com/Pyromesis/CA-O/releases/download/v2.1.5/CA-O-Setup-GUI-x64.zip) | [CA-O-2.1.5-win-x64.zip (394 MB, paquete completo offline)](https://github.com/Pyromesis/CA-O/releases/download/v2.1.5/CA-O-2.1.5-win-x64.zip) | [Notas de la versión](https://github.com/Pyromesis/CA-O/releases/tag/v2.1.5) | [Documentación](docs/ARCHITECTURE.md)

---

## Índice

1. [Resumen ejecutivo](#resumen-ejecutivo)
2. [Filosofía y pilares](#filosofía-y-pilares)
3. [Stack tecnológico](#stack-tecnológico)
4. [Arquitectura profunda](#arquitectura-profunda)
5. [Estructura del repositorio](#estructura-del-repositorio)
6. [Modelo de seguridad](#modelo-de-seguridad)
7. [Catálogo completo de optimizaciones (66)](#catálogo-completo-de-optimizaciones-66)
8. [Cómo funciona la app — viaje del usuario](#cómo-funciona-la-app--viaje-del-usuario)
9. [Motores internos](#motores-internos)
10. [Persistencia y rutas de datos](#persistencia-y-rutas-de-datos)
11. [Perfiles de optimización](#perfiles-de-optimización)
12. [Interfaz y páginas](#interfaz-y-páginas)
13. [Requisitos](#requisitos)
14. [Instalación](#instalación)
15. [Desarrollo](#desarrollo)
16. [Pruebas](#pruebas)
17. [Release y verificación](#release-y-verificación)
18. [Solución de problemas](#solución-de-problemas)
19. [Documentación](#documentación)
20. [Contribuir](#contribuir)
21. [Licencia](#licencia)

---

## Resumen ejecutivo

**CA-O 2.1.5** es una aplicación **100% nativa Windows** escrita en **.NET 10 + WinUI 3 (Windows App SDK 2.4)**. No es una colección de tweaks: **mide** hardware, térmicas, red, almacenamiento, drivers y postura de seguridad **antes** de recomendar. Cada cambio pertenece a un **bucket analizado-primero** (`Recommended` / `Optional` / `Experimental` / `SecuritySensitive` / `NotApplicable` / `Blocked`) y se ejecuta bajo el flujo transaccional `PRECHECK → SNAPSHOT → APPLY → VERIFY → COMMIT` con **rollback automático y verificación post-reversión**.

La UI **siempre eleva** (`app.manifest` `requireAdministrator` → UAC en cada inicio) pero **toda mutación privilegiada cruza** al servicio Windows `CAO.Privileged` (SYSTEM) vía **Named Pipe autenticado** con ACL restrictiva, validación de esquema tipado, ventana de 30 s, nonce y protección anti-replay. Sin el servicio, la app opera en **modo solo lectura** (diagnóstico + benchmark disponibles).

**En números:**
- **66 optimizaciones** verificadas y reversibles (transaccionales) + `AllLegacy` para trazabilidad
- **8 páginas** WinUI 3 con Mica, `NavigationView`, `VisualState` responsive, i18n `es-ES`/`en-US` instantáneo
- **500+ tests** en 7 suites (Core 361 · Integration 48 · Security 63 · Infra 17 · Benchmark 7 · UI 8 · App)
- **0 telemetría externa**, 0 dependencias web, 0 comandos arbitrarios

---

## Filosofía y pilares

| Pilar | Qué significa | Cómo se materializa |
|---|---|---|
| **DIAGNOSE** | No se recomienda sin medir | `SystemAnalysisService` ejecuta proveedores WMI/Registry en paralelo con timeout 5 s. Si un dato no está disponible → "no disponible", nunca inventado |
| **OPTIMIZE** | Solo lo medido genera recomendación | `RecommendationEngine` clasifica exactamente 1 bucket por optimización consultando `SystemContext` real |
| **BENCHMARK** | Sin promesas falsas | Flujo 5 pasos con mediana de trials, suelo de ruido 3 %, veredictos `Mejora medible` / `Sin mejora` / `Regresión`. No se simulan FPS |
| **RECOVER** | Todo es reversible | Snapshot **antes** de mutar, journal `transactions/{txid}.jsonl`, hash-chain `history.jsonl` + `integrity.json` SHA-256 |

**Decisión fundacional:** *¿Cuál es la mejor configuración para ESTE Windows, ESTE hardware, ESTE driver, ESTE juego y ESTE objetivo?* — no *¿qué tweaks tiene Internet?*

---

## Stack tecnológico

| Capa | Tecnología | Versión | Por qué |
|---|---|---|---|
| **Runtime** | .NET SDK | **10.0.400** (`global.json`, `rollForward: latestFeature`) | Self-contained, AOT-friendly, `LangVersion 13` |
| **UI** | WinUI 3 / Windows App SDK | **2.4.0** | Nativo, Mica, `NavigationView`, sin MSIX obligatorio (`WindowsPackageType: None`) |
| **MVVM** | CommunityToolkit.Mvvm | **8.4.0** | `ObservableObject`, `RelayCommand`, sin boilerplate |
| **DI** | Microsoft.Extensions.DependencyInjection + Hosting | **10.0.0** | `AppHost` centraliza `UiState`, `SystemAnalysisService`, `PrivilegedPipeClient` |
| **WMI** | System.Management | **10.0.0** | Lectura hardware/termal/batería |
| **Perf** | System.Diagnostics.PerformanceCounter | **10.0.0** | `% DPC Time` / `% Interrupt Time` |
| **Servicios** | System.ServiceProcess.ServiceController | **8.0.1** | `CAO.Privileged` como `BackgroundService` |
| **Build** | `Directory.Packages.props` + `Directory.Build.props` + `Version.props` (single source `2.1.5`) | Centralizado | Un lugar para bump de versión/paquetes |
| **Tests** | xUnit 2.9.2 + Microsoft.NET.Test.Sdk 17.11.1 | — | 500+ tests en Release |
| **Seguridad** | CodeQL + `dotnet audit` + Dependabot + CycloneDX SBOM | CI | Cadena de suministro auditada |

> **Self-contained:** los artefactos de release no requieren runtime instalado. El instalador es `self-contained` sin `single-file` (requisito WinUI 3).

---

## Arquitectura profunda

### Procesos y confianza

```
┌──────────────────────┐   Named Pipe (ACL+nonce+replay)   ┌─────────────────────────┐
│  CA-O.UI (WinUI 3)   │ ────────────────────────────────► │ CA-O.Privileged         │
│  requireAdministrator│  IpcRequest v2 tipado + allowlist │ Servicio Windows        │
│  - Diagnóstico       │ ◄──────────────────────────────── │ (LocalSystem)           │
│  - Benchmark local   │  IpcResponse JSON                 │ - Registry/Services     │
│  - Recomendaciones   │                                   │ - Power/Network/BCD     │
└──────────────────────┘                                   └─────────────────────────┘
         │                                                        │
         │ AnalysisStateStore / SnapshotRepository                │ OptimizationEngine
         ▼                                                        ▼
┌──────────────────────┐                              ┌──────────────────────┐
│ SystemAnalysisService│                              │   Windows system     │
│ (WhenAll + cancel)   │                              │ Registry / SCM / WMI │
└──────────────────────┘                              └──────────────────────┘
```

- **UI siempre elevada** pero **toda escritura cruza el pipe** aun elevada → modelo de privilegio mínimo auditado
- **Servicio valida:** versión protocolo (must = 2), identidad cliente (`GetImpersonationUserName` vía `RunAsClient()`), GUID único + nonce (replay), esquema `OperationParameters`, solo 7 operaciones allowlist
- **Sin HTTP**, sin ejecución de cadenas arbitrarias, sin PowerShell en el path de ejecución (solo `CommandPolicy` con rutas absolutas `%SystemRoot%\System32`)

### Capas de código

| Proyecto | Responsabilidad | Depende de |
|---|---|---|
| `CA-O.Shared` | DTOs, contratos IPC v2 (`IpcProtocol` v2, `Ping`, `GetServiceStatus`), `CaOPaths`, `ErrorCodes CAO-XXX-nnn`, `AppVersion 2.1.5`, `BuildConstants`, `Constants/IpcConstants` | — |
| `CA-O.Core` | `OptimizationCatalog` (66), `OptimizationEngine` transaccional, `RecommendationEngine` + `OptimizationScoreCalculator` (0-100), `GameCompatibilityPolicy` (matriz SAFE/CAUTION/BLOCKED `CAO-GAME-001`), `HealthEngine`, `KnownIssueMatcher`, `CrashRecoveryService`, `Rollback/*` | Shared |
| `CA-O.Infrastructure` | WMI 5 s timeout + `SystemAnalysisService` + `AnalysisStateStore` (atómico, 24 h TTL) + `SnapshotRepository`/`FileSnapshotStore` (TX identity + SHA-256) + `JsonHistoryLogger` (hash-chain) + `SystemBenchmarkRunner` + `Gaming/*` + `Networking/*` + `Storage/*` + `Security/*` + `Windows/*` | Core, Shared |
| `CA-O.Privileged` | Servicio `SYSTEM` + `PrivilegedPipeService` (Named Pipe `CA-O.Privileged.v1`, ACL + `ReplayCache` 30 s, timeout 15 s/conn) + `OptimizationEngine` hosteado + `AdministratorsOnlyAuthorizer` | Core, Infrastructure, Shared |
| `CA-O.UI` | WinUI 3 Mica + 8 ViewModels DI (`AppHost` + `UiState`) + `Controls` (`MetricCard`/`RiskBadge`/`ScoreRing`/`Diagnostic*`) + `PrivilegedPipeClient` + `ErrorTranslator` + `Helpers/LocalizationHelper` + 8 páginas con `VisualState` | Core, Infrastructure, Shared |
| `CA-O.InstallerGui` | Instalador gráfico **680×620**, Mica, progress, `requireAdministrator`, registro servicio + atajos | — |
| `CA-O.Setup` | Instalador consola fallback, `requireAdministrator` | — |
| `CA-O.Uninstaller` | Desinstalador + entrada ARP (`Programs and Features`), `UninstallService` | — |

### Ciclo de vida de una optimización (transaccional)

```
PRECHECK  → CheckPreconditionsAsync(SystemContext)  // build, SSD, térmico, batería, anti-cheat
COMPAT    → GameCompatibilityPolicy.Evaluate()       // BLOCKED CAO-GAME-001 si Vanguard/EAC
SNAPSHOT  → Capture() → persistido ANTES de mutar    // crash-safe, spec 122
APPLY     → ApplyAsync(context)                     // Registry API o CommandPolicy
VERIFY    → VerifyAsync() → Detect en vivo          // PendingReboot aceptado; Unknown → rollback
COMMIT    → history.jsonl + journal {txid}.jsonl    // applyResult/verification/rollbackAvailable
ROLLBACK  → RevertAsync(snapshot) automático si APPLY o VERIFY fallan → re-VERIFY
BENCHMARK → post-commit, fallo no invalida commit   // eventos separados
```

- Lote multi-optimización: aplica **secuencialmente**; al primer fallo **detiene y revierte** lo ya aplicado cuyo riesgo sea `Safe/Low` (spec 124)
- Cancelación: solo **antes** de `SNAPSHOT`/`APPLY`. Durante `APPLY` es **atómica** → `CancellationDeferred` tras `verify→commit`

### Startup en 3 fases (no bloquea UI)

1. `App.xaml.cs` → `AppHost` DI + `SettingsStore` + `AnalysisStateStore.Load()` (hidrata `UiState` si `fresh`)
2. `MainWindow` → `NavigationView` + `ShellNavigationService`
3. Páginas → `OnNavigatedTo` → `Render()` desde `UiState` (sin re-analizar). Análisis solo bajo demanda.

---

## Estructura del repositorio

```
CA-O.sln  (.NET 10 · LangVersion 13 · WinUI 3)
├── src/
│   ├── CA-O.Shared/                 # Contratos puros, sin IO
│   │   ├── Constants/               # AppVersion (2.1.5), BuildConstants, CaOPaths, IpcConstants
│   │   ├── IPC/                     # IpcProtocol v2, IpcRequest/Response, Payloads (7 ops)
│   │   ├── Security/                # CommandPolicy, ErrorCodes, CallerIdentity
│   │   ├── Enums/                   # RecommendationBucket, RiskLevel, EvidenceLevel, etc.
│   │   └── DTO/                     # OptimizationDefinition, SystemContext, HealthScore
│   ├── CA-O.Core/                   # Lógica de negocio, sin WMI
│   │   ├── Optimization/            # OptimizationCatalog (66), RegistryOptimizationBase, Engine
│   │   ├── Optimizations/           # 66 clases por carpeta:
│   │   │   ├── Performance/         # DisableVbs, MaximumPowerPlan, DisableVisualEffects…
│   │   │   ├── Power/               # SetBestPerformanceAc, DisablePcieLink…
│   │   │   ├── Storage/             # DisableHibernate, OptimizeSystemDrive…
│   │   │   ├── Network/             # NormalizeTcpAutoTuning, EnableRss…
│   │   │   ├── Gaming/              # EnableGameMode, DisableGameBarDvr…
│   │   │   ├── PrivacySecurity/     # DisableTelemetry, DisableCopilot…
│   │   │   ├── Startup/             # DisableHeavyStartupApps…
│   │   │   └── System/              # PendingRebootMaintenance…
│   │   ├── Scoring/                 # RecommendationEngine, OptimizationScoreCalculator
│   │   ├── Gaming/                  # GameCompatibilityPolicy (24 entradas)
│   │   ├── Diagnostics/             # HealthEngine, CaoHealthCheck
│   │   └── Rollback/                # OptimizationTransaction, Snapshot, TransactionJournal, CrashRecovery
│   ├── CA-O.Infrastructure/         # IO real (WMI, Registry, Files)
│   │   ├── Windows/
│   │   │   ├── SystemRegistry/      # RegistryAccessor (raw kind exact)
│   │   │   ├── Execution/           # SystemCommandGateway (allowlist)
│   │   │   ├── Services/            # ServiceManager (SCM)
│   │   │   ├── Security/            # WindowsCallerInspector (SID/SessionId/elevación)
│   │   │   └── Etw/                 # WprDpcCollector (DPC/ISR)
│   │   ├── SystemInterop/           # SystemContextProvider, ObservedStateProvider, Thermal/Provider
│   │   ├── Networking/              # DnsBenchmarkProvider, NetworkDiagnosticsProvider
│   │   ├── Gaming/                  # GameDetectionProvider, AntiCheatScanProvider
│   │   ├── Benchmarking/            # SystemBenchmarkRunner (mediana + suelo 3%)
│   │   ├── Persistence/             # AnalysisStateStore, FileSnapshotStore, JsonHistoryLogger
│   │   └── Services/                # SystemAnalysisService (WhenAll, 5s timeout)
│   ├── CA-O.Privileged/             # Servicio SYSTEM
│   │   ├── Program.cs               # Host.CreateDefaultBuilder().UseWindowsService()
│   │   └── PrivilegedPipeService.cs # NamedPipeServerStreamAcl + ValidateAndDispatchAsync
│   ├── CA-O.UI/                     # WinUI 3
│   │   ├── App.xaml / MainWindow.xaml
│   │   ├── Pages/                   # Dashboard, Analyze, Optimize, Gaming, Diagnostics, Benchmark, Restore, History, Settings
│   │   ├── ViewModels/              # 8 VMs (Dashboard, Analyze, Optimize, Gaming, Diagnostics, Benchmark, Restore, History, Settings) + UiState
│   │   ├── Controls/                # MetricCard, RiskBadge, ScoreRing, Diagnostic*
│   │   ├── Resources/               # DesignTokens.xaml, Localizer (es-ES/en-US)
│   │   ├── Navigation/              # ShellNavigationService, RouteTable
│   │   ├── Helpers/                 # LocalizationHelper, ErrorTranslator
│   │   └── PrivilegedPipeClient.cs  # NamedPipeClientStream + nonce + 10s timeout
│   ├── CA-O.InstallerGui/           # 680×620 GUI installer (WinUI 3, requireAdministrator)
│   ├── CA-O.Setup/                  # Consola fallback (sc.exe create/start)
│   └── CA-O.Uninstaller/            # ARP uninstaller (sc stop/delete + rmdir)
├── tests/                           # 500+ tests, Release
│   ├── CA-O.Core.Tests/             # 361: catalog, AnalysisStateStore, GameCompatibility, transacciones
│   ├── CA-O.Integration.Tests/      # 48: E2E 10 flujos + TransactionJournalRecovery
│   ├── CA-O.Security.Tests/         # 63: IpcValidator, ReplayCache, CommandPolicy
│   ├── CA-O.Infrastructure.Tests/   # 17: HistoryRobustness, SnapshotRepository
│   ├── CA-O.Benchmark.Tests/        # 7:  suelo 3%, mediana
│   ├── CA-O.UI.Tests/               # 8:  ViewModels
│   └── CA-O.Benchmark.Tests/
├── docs/                            # ARCHITECTURE.md, SECURITY.md, THREAT-MODEL.md, OPTIMIZATION-CATALOG.md, IPC_PROTOCOL.md, TRANSACTIONS.md
├── scripts/                         # build.ps1, test.ps1, verify.ps1, build-release.ps1, package.ps1, install-privileged-service.ps1, harden-data-acls.ps1, …
├── .github/workflows/ci.yml         # build + test + CodeQL + dotnet audit
├── Directory.Packages.props         # Versiones centralizadas
├── Directory.Build.props            # Propiedades MSBuild comunes
├── Version.props                    # Single source 2.1.5
├── global.json                      # SDK 10.0.400
└── CA-O.sln
```

---

## Modelo de seguridad

### Resumen STRIDE

```
CA-O.UI (WinUI 3, requireAdministrator — UAC siempre)
   │  IpcRequest { ProtocolVersion 2, RequestId GUID, Nonce 16B hex, CreatedAtUtc, Operation, TypedPayload }
   │  Validación en servicio: versión, frescura 30 s, tamaño 64 KB, esquema, anti-replay, auth
   ▼
Named Pipe  \\.\pipe\CA-O.Privileged.v1  — ACL: SYSTEM Full, Administrators R/W, Interactive R/W
   │  (conectar ≠ autorizar) → GetCallerIdentity() via RunAsClient() — SID real + nombre + SessionId + elevación
   │  IpcRequestValidator + ReplayCache + AdministratorsOnlyAuthorizer
   ▼
CA-O.Privileged (SYSTEM) — solo 7 operaciones tipadas:
   ApplyOptimization / RevertOptimization / DetectOptimization / VerifyOptimization / CaptureSnapshot / Ping / GetServiceStatus
   → Catalogo estatico CommandPolicy — UseShellExecute=false, timeout 60 s, rutas absolutas %SystemRoot%\System32
```

### Controles implementados

| Superficie | Control | Detalle |
|---|---|---|
| **Canal privilegiado v2** | Named Pipe ACL + impersonation | `NamedPipeServerStreamAcl.Create(PipeName, Byte, 1, ACL)`. `GetCallerIdentity()` vía `RunAsClient()` + `WindowsCallerInspector` (SID, nombre, `SessionId`, `IsElevated`, `IsInRole(Administrators)`). `P1-8` probado |
| **Validación request** | `IpcRequestValidator` (§10) | `ProtocolVersion==2` (CAO-IPC-001), `RequestId!=Empty`, `Nonce` 1..128 sin control chars, `CreatedAtUtc` ±30 s / +1 min futuro (CAO-IPC-003), `Operation` enum, `Payload` polimórfico exacto, `OptimizationId` regex `[a-z0-9-]{1,80}`, tamaño ≤64 KB request / ≤256 KB response |
| **Health sin OptimizationId** | `Ping` / `GetServiceStatus` (§10) | No usan optimización real como ping. Devuelven `ServiceVersion, ProtocolVersion, ProcessId, IsSystem, Status, Capabilities` |
| **Anti-replay** | `ReplayCache` | `RequestId+Nonce` single-use por vida de servicio, `MaxAge 30 s`, reloj inyectable. `CAO-IPC-004` si repetido |
| **Autorización** | `AdministratorsOnlyAuthorizer` (FASE 2 + SessionId) | Permitido: token elevado + `IsInRole(Administrators)` o SID en lista. Denegado: estándar `CAO-SEC-005`, filtrado sin elevación `CAO-SEC-003`, SID inválido `CAO-SEC-004`. Auditoría `requestedBy SID/Name → executedBy SYSTEM` |
| **Protocolo tipado v2** | `IpcProtocol.Version=2` | Envelope versionado, `RequestId` GUID, nonce alfanumérico, `CreatedAtUtc` 30 s. Errores estructurados `CAO-XXX-nnn` |
| **Gateway ejecución** | `IPrivilegedCommandExecutor → CommandPolicy.Resolve` | Rutas absolutas `%SystemRoot%` (anti PATH-hijacking), tokens exactos sin metacaracteres (anti `& | ; < > " ' % ^ \n`), sin shell, sin `PATH` lookup. Desviación → `CAO-SEC-010` |
| **Comandos permitidos** | `SystemCommandKey` (32 claves) | `powercfg`, `bcdedit`, `netsh`, `ipconfig`, `defrag`, `wpr`, `logman` con patrones de argumentos cerrados. `bcdedit` nunca por runner genérico |
| **Allowlist operaciones** | Solo 7 | `Apply/Revert/Detect/Verify/CaptureSnapshot/Ping/GetServiceStatus`. No existe "ejecutar comando" |
| **Timeout** | 15 s por conexión, 60 s por comando, 10 s UI | `PrivilegedPipeService:27` + `SystemCommandGateway:DefaultTimeout` + `PrivilegedPipeClient:CallTimeout` |
| **Cancelación segura** | FASE 6 | Solo antes de `SNAPSHOT`/`APPLY`; durante `APPLY` es atómica → `CancellationDeferred` |
| **Verificación estricta** | FASE 10 | `VerificationStatus { Passed, Failed, Unknown, NotApplicable }`. `Unknown` nunca es éxito → rollback `CAO-VERIFY-002` |
| **Gaming bloqueo real** | `GameCompatibilityPolicy` §24-26 | Matriz `SAFE/CAUTION/BLOCKED`. `disable-vbs` → `BLOCKED CAO-GAME-001` si Vanguard/EAC/BattlEye. Valida en **Core y Privileged** (no solo UI). Expert no bypassa |
| **Persistencia** | Snapshots + history | `snapshot.json` SHA-256 en `integrity.json`, dirs inmutables `{txid}/`, `SnapshotStateEquals`. History hash-chain `prevHash→hash` por línea JSONL, `GenesisHash`. Rutas `CaOPaths` bajo `%ProgramData%\CA-O` endurecidas vía `harden-data-acls.ps1` (`icacls /inheritance:r`, SYSTEM F, Admins M, Users RX) |
| **Instalador** | `requireAdministrator` | `app.manifest` + `InstallerGui` + `Setup` siempre UAC. Registra `CAO.Privileged` con `failure 86400 restart/5000/restart/10000/reboot/60000` y crea atajos Escritorio/Inicio. Log `%TEMP%\CA-O-Setup-Gui.log` |
| **Cadena de suministro** | CI + SBOM | CodeQL + `dotnet audit` en `ci.yml`, Dependabot semanal, `SHA256SUMS.txt` + SBOM CycloneDX 1.7 `bom.json` (71 paquetes) en cada release. Firma Authenticode con `CAO_SIGN_THUMBPRINT` |

> **PowerShell no existe en el path de ejecución.** Las optimizaciones usan Registry API, SCM wrappers y `powercfg`/`netsh` exactos. Cualquier comando externo debe existir en `CommandPolicy` (probado en `ElevatedCommandCatalogTests`).

Ver detalles completos en [docs/SECURITY.md](docs/SECURITY.md) y [docs/THREAT-MODEL.md](docs/THREAT-MODEL.md).

---

## Catálogo completo de optimizaciones (66)

> **Calidad sobre cantidad.** Cada entrada responde *qué cambia*, *por qué*, *con qué evidencia*, *qué riesgo/seguridad afecta*, *si es reversible* y *cómo se verifica*. Todas implementan `IOptimization` (`Definition` + `Detect` + `Capture` + `ApplyAsync` + `RevertAsync` + `PreviewAsync` + `VerifyAsync` cuando aplica).

### Tabla maestra (66) — `OptimizationCatalog.All`

| # | Id | Categoría | Impacto | Evidencia | Riesgo | Compat. | Reversible | Flags | Qué hace |
|---|---|---|---|---|---|---|---|---|---|
| 1 | `disable-background-apps` | Performance | Small | Official | Low | Compatible | ✅ | — | `HKCU\...\BackgroundAccessApplications` + `GlobalUserDisabled` |
| 2 | `disable-copilot` | PrivacySecurity | None | Official | Low | Compatible | ✅ | — | Desactiva Copilot (HKLM+HKCU) |
| 3 | `disable-cortana` | PrivacySecurity | None | Official | Low | Compatible | ✅ | — | `HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Search\AllowCortana=0` |
| 4 | `disable-game-bar-dvr` | Gaming | WorkloadDependent | Vendor | Low | Compatible | ✅ | — | DVR/GameBar off (`System\GameConfigStore\GameDVR_Enabled=0`) |
| 5 | `disable-suggestions` | PrivacySecurity | None | Official | Low | Compatible | ✅ | — | Sugerencias/contenido destacado off |
| 6 | `disable-telemetry` | PrivacySecurity | None | Official | Low | Compatible | ✅ | — | `AllowTelemetry=0`, `DoNotShowFeedbackNotifications` |
| 7 | `disable-transparency` | Performance | Tiny | Empirical | Safe | Compatible | ✅ | — | `EnableTransparency=0` (DWM) |
| 8 | `disable-visual-effects` | Performance | Tiny | Empirical | Safe | Compatible | ✅ | — | `VisualFXSetting=2` + ajustes avanzados |
| 9 | `disable-widgets` | PrivacySecurity | Tiny | Official | Low | Compatible | ✅ | — | `TaskbarDa=0` |
| 10 | `enable-game-mode` | Gaming | WorkloadDependent | Official | Low | Compatible | ✅ | — | `HKCU\Software\Microsoft\GameBar\AllowAutoGameMode=1` |
| 11 | `enable-gpu-scheduling` | Gaming | WorkloadDependent | Vendor | Moderate | Conditional | ✅ | RequiresReboot | `HwSchMode=2` (HAGS) |
| 12 | `enable-windowed-game-optimizations` | Gaming | WorkloadDependent | Official | Low | Compatible | ✅ | — | `DirectXUserGlobalSettings SwapEffectUpgradeEnable=1;` (Win11 22H2+) |
| 13 | `enable-vrr` | Gaming | WorkloadDependent | Official | Low | Compatible | ✅ | — | VRR si display compatible |
| 14 | `zero-menu-delay` | Performance | Tiny | Heuristic | Safe | Compatible | ✅ | — | `MenuShowDelay=0` |
| 15 | `disable-onedrive-autostart` | PrivacySecurity | Tiny | Official | Low | Conditional | ✅ | ExpertOnly | OneDrive autostart off |
| 16 | `disable-search-indexing` | Performance | WorkloadDependent | Empirical | Moderate | Conditional | ✅ | RecommendedOnSsd | `WSearch` delayed/manual + `PreventIndexing` |
| 17 | `maximum-power-plan` | Performance | Small | Official | Low | Compatible | ✅ | — | Activa `8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c` (High Perf), duplica Ultimate si falta |
| 18 | `disable-hibernate` | Storage | None | Official | Moderate | Compatible | ✅ | — | `powercfg /h off` (libera hiberfil.sys 4-12 GB) |
| 19 | `disable-vbs` | Performance | WorkloadDependent | Vendor | **Critical** | PotentialConflict | ✅ | ExpertOnly, SecurityTradeoff, RequiresReboot | `bcdedit /set {current} hypervisorlaunchtype off` — **BLOCKED con Vanguard/EAC** |
| 20 | `normalize-tcp-autotuning` | Network | WorkloadDependent | Official | Low | Conditional | ✅ | — | `netsh int tcp set global autotuninglevel=normal` |
| 21 | `optimize-system-drive` | Storage | None | Official | Low | Compatible | ❌ | NotReversible | `defrag C: /O` (TRIM/Optimize media-aware) |
| 22 | `set-games-high-performance-gpu` | Gaming | WorkloadDependent | Official | Low | Compatible | ✅ | — | Preferencia GPU alta para juegos detectados |
| 23 | `disable-background-game-captures` | Gaming | Tiny | Official | Low | Compatible | ✅ | — | Separa GameDVR de capturas |
| 24 | `disable-game-bar-auto-launch` | Gaming | Tiny | Official | Low | Compatible | ✅ | — | Evita auto-launch GameBar |
| 25 | `configure-gaming-power-mode-ac` | Gaming/Power | Small | Official | Low | Compatible | ✅ | — | AC → Best Performance para gaming |
| 26 | `restore-default-gpu-preference` | Gaming | None | Official | Safe | Compatible | ✅ | — | Restaura preferencia GPU por defecto |
| 27 | `enable-auto-hdr` | Gaming | None | Official | Low | Compatible | ✅ | — | Auto HDR (visual, no FPS) |
| 28 | `gaming-display-refresh-rate-audit` | Gaming | None | Official | Safe | Compatible | ✅ | DiagnosticOnly | Audita Hz del display (solo diagnóstico) |
| 29 | `set-best-performance-ac` | Power | Small | Official | Low | Compatible | ✅ | — | AC → Best Performance |
| 30 | `restore-balanced-power-dc` | Power | Small | Official | Low | Compatible | ✅ | — | DC → Balanced (batería) |
| 31 | `disable-usb-selective-suspend-ac` | Power | Tiny | Official | Low | Compatible | ✅ | — | USB selective suspend off en AC |
| 32 | `disable-pcie-link-state-power-saving-ac` | Power | Small | Official | Low | Compatible | ✅ | — | `HKLM\SYSTEM\CurrentControlSet\Services\pci\Parameters\DisableLinkStateThrottling=1` |
| 33 | `set-wireless-adapter-max-performance-ac` | Power | Tiny | Official | Low | Compatible | ✅ | — | Wi-Fi → máximo rendimiento en AC |
| 34 | `restore-power-plan-after-gaming` | Power | None | Official | Safe | Compatible | ✅ | — | Restaura plan previo tras gaming |
| 35 | `remove-unused-custom-power-plans` | Power | Tiny | Official | Low | Compatible | ✅ | — | Elimina planes personalizados huérfanos |
| 36 | `ensure-trim-enabled` | Storage | None | Official | Low | Compatible | ✅ | — | `fsutil behavior set DisableDeleteNotify 0` |
| 37 | `retrim-system-ssd` | Storage | None | Official | Low | Compatible | ✅ | — | ReTrim SSD sistema |
| 38 | `optimize-hdd-media-aware` | Storage | None | Official | Low | Conditional | ✅ | — | `defrag /O` según medio (SSD TRIM, HDD defrag) |
| 39 | `enable-storage-sense` | Storage | Tiny | Official | Low | Compatible | ✅ | — | Storage Sense on |
| 40 | `storage-sense-temp-cleanup` | Storage | Tiny | Official | Low | Compatible | ✅ | — | Política temporales Storage Sense |
| 41 | `storage-sense-recycle-bin-policy` | Storage | Tiny | Official | Low | Compatible | ✅ | — | Papelera 7-90 días |
| 42 | `cleanup-windows-temp` | Storage | Small | Official | Low | Compatible | ✅ | — | Limpia `%TEMP%` + `Windows\Temp` |
| 43 | `cleanup-delivery-optimization-cache` | Storage | Small | Official | Low | Compatible | ✅ | — | Cache Delivery Optimization |
| 44 | `windows-component-store-cleanup` | Storage | Medium | Official | Moderate | Compatible | ✅ | — | `DISM /Online /Cleanup-Image /StartComponentCleanup` |
| 45 | `windows-component-store-resetbase` | Storage | Large | Official | High | Compatible | ❌ | NotReversible | `DISM /ResetBase` (**irreversible**, libera WinSxS) |
| 46 | `disk-cleanup-system-files` | Storage | Medium | Official | Low | Compatible | ✅ | — | `cleanmgr /sagerun` system files |
| 47 | `free-low-storage-space` | Storage | Medium | Official | Low | Compatible | ✅ | — | Umbrales 10/15/20 % espacio libre |
| 48 | `restore-system-managed-pagefile` | Storage | None | Official | Low | Compatible | ✅ | — | Pagefile → System managed |
| 49 | `enable-rss` | Network | Small | Official | Low | Compatible | ✅ | — | Receive Side Scaling on |
| 50 | `restore-tcp-checksum-offload` | Network | Tiny | Official | Low | Compatible | ✅ | — | TCP checksum offload default |
| 51 | `restore-udp-checksum-offload` | Network | Tiny | Official | Low | Compatible | ✅ | — | UDP checksum offload default |
| 52 | `restore-large-send-offload` | Network | Tiny | Official | Low | Compatible | ✅ | — | LSO default |
| 53 | `configure-interrupt-moderation-for-low-latency` | Network | Small | Official | Low | Conditional | ✅ | — | Interrupt Moderation low-latency (Competitive) |
| 54 | `disable-nic-power-saving-ac` | Network | Small | Official | Low | Compatible | ✅ | — | NIC power saving off en AC |
| 55 | `restore-windows-tcp-congestion-default` | Network | Small | Official | Low | Compatible | ✅ | — | `netsh int tcp set global congestionprovider=default` |
| 56 | `flush-dns-cache` | Network | Tiny | Official | Safe | Compatible | ✅ | — | `ipconfig /flushdns` |
| 57 | `reset-network-stack-repair` | Network | Medium | Official | Moderate | Compatible | ✅ | RequiresReboot | Winsock/TCP reset |
| 58 | `delivery-optimization-bandwidth-profile` | Network | Tiny | Official | Low | Compatible | ✅ | — | DO perfil ancho de banda |
| 59 | `disable-unnecessary-startup-apps` | Startup | Small | Official | Low | Compatible | ✅ | — | Startup classification (innecesarias) |
| 60 | `disable-heavy-startup-apps` | Startup | Medium | Empirical | Moderate | Compatible | ✅ | — | Heavy startup (High impact) |
| 61 | `delay-safe-third-party-service-start` | Startup | Small | Official | Low | Compatible | ✅ | — | Servicios 3rd party → Delayed auto |
| 62 | `disable-selected-third-party-background-task` | Startup | Small | Official | Low | Compatible | ✅ | — | Tareas 3rd party background off |
| 63 | `restore-sysmain-default` | Startup | Tiny | Official | Low | Compatible | ✅ | — | SysMain (Superfetch) default |
| 64 | `restore-windows-search-default` | Startup | Tiny | Official | Low | Compatible | ✅ | — | Windows Search default |
| 65 | `create-restore-point-before-optimization-batch` | System | None | Official | Safe | Compatible | ✅ | — | `SRSetRestorePoint` antes de batch |
| 66 | `pending-reboot-maintenance` | System | None | Official | Safe | Compatible | ✅ | DiagnosticOnly | Detecta reboot pendiente |
| — | `stale-crash-dump-cleanup` | System | Tiny | Official | Low | Compatible | ✅ | — | Limpia dumps antiguos |
| — | `optimize-startup-recovery-state` | System | None | Official | Safe | Compatible | ✅ | — | Audita estado boot/recovery |

> **Nota:** `optimize-system-drive` y `windows-component-store-resetbase` son **no reversibles** (`NotReversible`) — se auditan como `VerificationStatus.NotApplicable` y no eliminan snapshot tras éxito.

### Detalle por categoría

#### Performance (7)

| Id | Clave / Registro | Verifica |
|---|---|---|
| `disable-visual-effects` | `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects\VisualFXSetting=2` | Re-lee |
| `zero-menu-delay` | `HKCU\Control Panel\Desktop\MenuShowDelay=0` | Re-lee |
| `disable-transparency` | `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\EnableTransparency=0` | Re-lee |
| `maximum-power-plan` | `powercfg /setactive 8c5e7fda...` + `powercfg /getactivescheme` verify | PowerCfg query |
| `disable-search-indexing` | `WSearch` service `DelayedAuto` + Registry | ServiceManager |
| `disable-background-apps` | `HKCU + HKLM BackgroundAccessApplications` | Registry |
| `normalize-tcp-autotuning` | `netsh int tcp set global autotuninglevel=normal` | `netsh int tcp show global` |

#### Privacy & Security (6)

Todas son `RegistryOptimizationBase` puras (reversión exacta). Ej. `disable-telemetry` → `HKLM\SOFTWARE\Policies\Microsoft\Windows\DataCollection\AllowTelemetry=0`.

#### Gaming (11)

- **GameMode** (`AllowAutoGameMode=1`), **HAGS** (`HwSchMode=2`, RequiresReboot), **Windowed Optimizations** (`SwapEffectUpgradeEnable=1;`), **VRR** (si display), **GPU high perf** por juego detectado.
- `disable-vbs` es **CRITICAL + SecurityTradeoff + ExpertOnly** y se **bloquea** con `CAO-GAME-001` si Vanguard/EAC/BattlEye presentes (validado en Core **y** Privileged).

#### Power (7)

- `set-best-performance-ac` / `restore-balanced-power-dc` → `powercfg` GUIDs oficiales
- `disable-pcie-link-state-power-saving-ac` → `HKLM\SYSTEM\CurrentControlSet\Services\pci\Parameters\DisableLinkStateThrottling=1`
- Resto: políticas de `USB selective suspend`, `Wireless max performance`, limpieza de planes huérfanos

#### Storage (14)

- `disable-hibernate` → `powercfg /h off` (libera `hiberfil.sys`)
- `ensure-trim-enabled` / `retrim-system-ssd` / `optimize-hdd-media-aware` → `fsutil` + `defrag /O`
- `Storage Sense` (enable + temp + recycle bin 7-90 d) → `HKCU\Software\Microsoft\Windows\CurrentVersion\StorageSense`
- `windows-component-store-*` → `DISM` (`ResetBase` irreversible)
- `cleanup-windows-temp` / `delivery-optimization-cache` / `disk-cleanup` → file + DO cache
- `restore-system-managed-pagefile` → `HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management`

#### Network (11)

- `enable-rss`, `restore-*-offload`, `configure-interrupt-moderation` → Registry `HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters` + `netsh`
- `normalize-tcp-autotuning` → `netsh int tcp set global autotuninglevel=normal`
- `disable-nic-power-saving-ac` → `HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e972…}\*`
- `flush-dns-cache` → `ipconfig /flushdns` (siempre Safe)
- `reset-network-stack-repair` → `netsh winsock reset` + `netsh int ip reset` (RequiresReboot)

#### Startup (6)

Clasificación por impacto (`StartupImpactClassifier`): `disable-heavy-startup-apps` (High), `disable-unnecessary-startup-apps` (innecesarias), `delay-safe-third-party-service-start` (DelayedAuto), etc. Todas vía Registry `Run`/`Services` con snapshot exacto.

#### System / Maintenance (4)

- `create-restore-point-before-optimization-batch` → `SRSetRestorePoint` (una vez por sesión)
- `pending-reboot-maintenance` → detecta `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending` + `PendingFileRenameOperations`
- `stale-crash-dump-cleanup` → `C:\Windows\Minidump` antiguos
- `optimize-startup-recovery-state` → audita `BCDEdit` + recovery

> **Evidencia honesta:** `Official` (Microsoft Docs) / `Vendor` (NVIDIA/AMD/Intel) / `Benchmark` (reproducible) / `Empirical` (controlado) / `Heuristic` (histórico). Ninguna `Unknown` en `Recommended`.

---

## Cómo funciona la app — viaje del usuario

### 1. Instalación → UAC → Servicio

```
Descarga ZIP → Descomprime → gui-installer/CA-O.InstallerGui.exe (UAC)
  → Destino C:\Program Files\CA-O\
      ui/CA-O.UI.exe (+ dlls WindowsAppSDK)
      service/CA-O.Privileged.exe
      gui-installer/ + uninstall/ + setup/
  → sc.exe create CAO.Privileged binPath= "...\service\CA-O.Privileged.exe" start= demand
  → sc.exe failure CAO.Privileged reset= 86400 actions= restart/5000/restart/10000/reboot/60000
  → sc.exe qc / qfailure verification (FASE 29)
  → Atajos Escritorio/Inicio + ARP (Programas y características)
```

### 2. Primer inicio

- `App.xaml.cs` → `AppHost` DI → `AnalysisStateStore.Load()` → si existe y `fresh` (≤7 d) hidrata `UiState` (no re-analiza)
- `MainWindow` (Mica) → `NavigationView` 8 páginas → `DashboardPage` muestra health, último análisis, conteo buckets, SecureBoot/VBS/HVCI, juegos, `ServiceStatus: connected/rejected` (InfoBar "Modo solo lectura" si no hay servicio)

### 3. Analizar

`Analizar` → `AnalyzeViewModel.RunAsync()` → `SystemAnalysisService` lanza **en paralelo** (`Task.WhenAll`):

| Provider | Qué mide | Timeout | Fallback |
|---|---|---|---|
| `SystemContextProvider` | CPU modelo/carga/freq, GPU/VRAM, RAM, disco, build Windows | 5 s | "no disponible" |
| `ThermalDiagnosticsProvider` | Zonas ACPI vía WMI | 5 s | Heurístico |
| `NetworkDiagnosticsProvider` | Latencia/jitter/pérdida (ping 1.1.1.1/8.8.8.8), bufferbloat | 5 s | — |
| `DnsBenchmarkProvider` | Multi-resolver (Cloudflare, Google, Quad9) | 5 s | — |
| `StorageDiagnosticsProvider` | Tipo disco, TRIM, espacio | 5 s | — |
| `SecurityDiagnosticsProvider` | SecureBoot, TPM, VBS, HVCI | 5 s | — |
| `DriverDiagnosticsProvider` | Drivers con problema/firma | 5 s | — |
| `InputDiagnosticsProvider` | HID | 5 s | — |
| `Gaming/AntiCheatScanProvider` | Juegos + anti-cheats (HKLM\Services) | 5 s | — |

→ `AnalysisStateStore.Save()` (atómico `tmp→flush→Move(overwrite)`, schema v2) → `UiState.Recommendations` + `HealthEngine` (solo dimensiones medidas puntúan, sin datos → `Score=null` explícito).

> **Actualización recomendada:** ~1 vez/semana o tras instalar/desinstalar juegos. ≤7 d = actualizado, >7 d = advertencia, `GameInventoryChanged` = stale inmediato. No auto-analiza al abrir.

### 4. Optimizar

`Optimizar` → tarjetas por optimización:

```
[Recommended]  [Optional]  [Experimental]  [SecuritySensitive]  [Blocked CAO-GAME-001]
 Nombre + Descripción + Bucket + Evidencia + Riesgo + Seguridad + Compatibilidad
 Current → Target (Before/After) + BenefitDetail + Flags (RequiresReboot, ExpertOnly…)
 [Detalles] [Previsualizar] [Aplicar] [Revertir]
```

- **Previsualizar:** `PreviewAsync(registry)` → diff **real** `Registry Before/After` por `ValueTarget` (kind exacto), `Risk/SecurityImpact/Reversible/RequiresReboot`
- **Aplicar:** `ContentDialog` confirmación (snapshot previo por `TransactionId` + verificación exacta + reversible) → `PrivilegedPipeClient.SendAsync(ApplyOptimization, id)` → `PrivilegedPipeService` → `OptimizationEngine.ApplyAsync()` → `OptimizationTransaction.RunAsync()` → `StatusText` + `TxText` (`Precheck/Snapshot/Apply/Verify/Commit`) → `RefreshRecommendationsAsync()` → re-render `AppliedByCao`
- **Aplicar recomendadas:** lote secuencial, **para al primer fallo** (spec 124)
- **Filtros:** `Todas (X) | Recomendadas (X) | Opcionales (X) | Experimentales (X)` dinámicos; `Experimental` exige `ExpertMode`

### 5. Revertir / Restaurar

- **Optimizar [Revertir]** → `RevertOptimization` vía pipe (mismo `TransactionId`)
- **Restaurar:** lista `snapshots/{txid}/` (fecha, `OptimizationId`, `TxId`, `EntryCount`, `WindowsBuild`, `AppVersion`, `integrity.json` SHA-256) → `RevertAsync(TransactionId)` → verificación post-reversión (`ExactMatch` → elimina snapshot; `Equivalent/Mismatch` → conserva)
- **Historial:** `history.jsonl` timeline auditable (`TxId`, operación, `applyResult`, `verification`, `rollbackAvailable`, `hash`, `prevHash`) — `ReadLast` tolera líneas corruptas, `VerifyIntegrity` warning sin crash

### 6. Benchmark honesto

`Benchmark` → 5 pasos guiados:
1. Crear línea base (`SystemBenchmarkRunner` → `baseline.json`)
2. Aplicar optimización
3. Medir después
4. Comparar (mediana, suelo ruido **3 %**)
5. Veredicto: **Mejora medible** / **Sin mejora medible** / **Regresión** — no se simulan FPS

### 7. Gaming Center

Detecta juegos (Valorant, Fortnite, Apex, CS2, OW2, LoL, R6, CoD, Destiny 2) y anti-cheats (Vanguard, EAC, BattlEye, FACEIT, Ricochet) vía `HKLM\SYSTEM\CurrentControlSet\Services`. Matriz 24 entradas `SAFE/CAUTION/BLOCKED`. Con Vanguard/EAC/BattlEye, `disable-vbs` y similares → `Blocked CAO-GAME-001` (Core **y** Privileged). Muestra contadores `bloqueadas/permitidas/en revisión`.

---

## Motores internos

| Motor | Ubicación | Qué hace |
|---|---|---|
| **Transaccional** | `Core/Rollback/OptimizationTransaction.cs` | `PRECHECK→COMPAT→SNAPSHOT→APPLY→VERIFY→COMMIT` + rollback verificado post-reversión |
| **Crash recovery** | `Core/Rollback/CrashRecoveryService.cs` | Snapshot sin `commit+verify` = `Incomplete`; nunca asume éxito. Bloquea nuevas mutaciones si `HasPendingRecovery` |
| **Health** | `Core/Diagnostics/HealthEngine.cs` | Score 0-100 por dimensión (Sistema, Térmicas, Red, Almacenamiento, Drivers, Seguridad, Gaming). Solo medidas puntúan |
| **Recomendaciones** | `Core/Scoring/RecommendationEngine` + `OptimizationScoreCalculator` | 1 bucket exacto por optimización + score 0-100 (beneficio + evidencia + riesgo + seguridad + compat + reversibilidad). `DiagnosticOnly` no puntúa |
| **Compatibilidad** | `Core/Compatibility/KnownIssueMatcher` + JSON store | DB versionada con override drop-in `%ProgramData%\CA-O\known-issues.json` |
| **Gaming** | `Core/Gaming/GameCompatibilityPolicy` + `Infrastructure/Gaming/*` | Matriz 24 entradas, lectura `Services`/`drivers` |
| **Benchmark** | `Infrastructure/Benchmarking/SystemBenchmarkRunner` | CPU/mem/disco, mediana, suelo 3 % |
| **Análisis** | `Infrastructure/Services/SystemAnalysisService` | `WhenAll` paralelo + `CancellationToken` + 5 s timeout por provider |
| **Scoring perfiles** | `Core/Scoring/ProfileEngine` | Safe/Balanced/Gaming/Competitive/Privacy/Security/Maintenance/Expert/Custom — consultan `SystemContext`, ninguno es lista fija |

---

## Persistencia y rutas de datos

| Ruta | Contenido | Garantía |
|---|---|---|
| `%ProgramData%\CA-O\analysis-state.json` | `AnalysisStateStore` Schema v2: `TimestampUtc, AppVersion, WindowsBuild, Context, Recommendations, Health, AnalysisState (Completed/WithWarnings/Failed), Warnings, Duration, CorrelationId` | Atómico `tmp→flush→Move(overwrite)`, cuarentena `.corrupt.timestamp` si schema corrupto |
| `%ProgramData%\CA-O\history.jsonl` | `JsonHistoryLogger` hash-chain SHA-256: `seq, prevHash, hash, entry` — cada entrada `TxId, OptimizationId, Operation, Success, PreviousState, Error, CallerSid, TimestampUtc` | `ReadLast` tolera líneas corruptas, `VerifyIntegrity` reporta warnings, nunca crash |
| `%ProgramData%\CA-O\snapshots/{txid}/` | `snapshot.json` + `manifest.json` (`TransactionId, OptimizationId, DefinitionVersion, SchemaVersion, AppVersion, WindowsBuild, TimestampUtc, CallerSid`) + `integrity.json` (SHA-256) — identidad **TX**, no OptimizationId | `SnapshotRepository` (UI nunca `Directory.GetDirectories`), `FindLatestForOptimization`, `SnapshotComparison.ExactMatch` para eliminar |
| `%ProgramData%\CA-O\transactions/{txid}.jsonl` | `ITransactionJournal` — fases `Started→Snapshot→Apply→Verify→Commit/RolledBack/Failed` | Fuente de verdad para `CrashRecoveryService` |
| `%ProgramData%\CA-O\benchmarks\baseline.json` | `SystemBenchmarkResult` + `BenchmarkRunHeader` | Mediana de trials |
| `%ProgramData%\CA-O\known-issues.json` | Override drop-in DB `KnownIssueMatcher` | Versionada, opcional |
| `%AppData%\CA-O\settings.json` | Preferencias UI (`Theme, Language, ExpertMode`) + `UiState.LastAnalysisUtc` | 3 fases startup sin bloquear UI |
| `%LOCALAPPDATA%\CA-O\logs\` | `cao-ui-structured.log` (JSON), `cao-ui-crash.log` | `StructuredLogger` con `CorrelationId` |
| `%TEMP%\CA-O-Setup-Gui.log` | Log instalador | — |
| `%ProgramData%\CA-O\` ACL | `icacls /inheritance:r` → SYSTEM F, Administrators M, Users RX | `scripts/harden-data-acls.ps1` |

> **Privacidad:** nunca secretos, credenciales ni contenido de entrada del usuario (spec 75, 116). Sin telemetría ni analytics. Solo sondas de latencia explícitas (ping/DNS/Cloudflare) a petición.

---

## Perfiles de optimización

| Perfil | Enfoque | Qué consulta | Expert |
|---|---|---|---|
| **Safe** | Solo `Recommended` de riesgo `Safe/Low` | `SystemContext` real | No |
| **Balanced** | `Recommended` + `Optional` seguros | Contexto | No |
| **Gaming** | Prioriza FPS/latencia, respeta anti-cheat | Juegos + anti-cheats | No |
| **Competitive** | Máximo gaming, permite `Low` power/network | Gaming + red | No |
| **Privacy** | `PrivacySecurity` + startup | Telemetry, Cortana, widgets | No |
| **Security** | Solo hardening reversible | VBS/HVCI, SecureBoot | No |
| **Maintenance** | Storage + System + reboot | Espacio, dumps, WinSxS | No |
| **Expert** | Ve `Experimental` + `SecuritySensitive` | Todo | **Sí** — exige confirmación + snapshot, **no bypassa** `BLOCKED` críticos |
| **Custom** | Usuario elige | — | — |

Cada perfil es **dinámico** — no es lista fija. Ej. `maximum-power-plan` solo aparece si `powercfg` disponible; `disable-search-indexing` solo `RecommendedOnSsd` si SSD detectado.

---

## Interfaz y páginas

| Página | Ruta | Qué muestra |
|---|---|---|
| **Panel** | `DashboardPage.xaml` | Health 0-100 por dimensión, último análisis + freshness, conteo buckets, SecureBoot/VBS/HVCI, juegos, `ServiceStatus` + `InfoBar` modo solo lectura |
| **Analizar** | `AnalyzePage.xaml` | `SystemAnalysisService` 9 providers en paralelo, `AnalysisState` + `Warnings`, botón "Ejecutar análisis completo" (10-30 s), DPC sampler 5 s, DNS `Apply` si servicio |
| **Optimizar** | `OptimizePage.xaml` | Tarjetas con bucket/evidencia/riesgo/seguridad/compat, diff Before/After, `ProgressRing` + `TxText` (`Precheck…Commit`), filtros `Todas/Recomendadas/Opcionales/Experimentales`, `Aplicar recomendadas` (batch) |
| **Gaming** | `GamingPage.xaml` | Juegos detectados + anti-cheats + matriz `SAFE/CAUTION/BLOCKED` + guidance Reflex/Anti-Lag + contadores `bloqueadas/permitidas/en revisión` |
| **Diagnóstico** | `DiagnosticsPage.xaml` | 6 dimensiones paralelas con interpretación natural ("CPU Normal", "GPU RTX 4070 12 GB driver 551.61") — "no disponible" si API ausente |
| **Benchmark** | `BenchmarkPage.xaml` | Flujo 5 pasos + `Baseline/After/Comparison/Verdict` + suelo 3 % |
| **Restaurar** | `RestorePage.xaml` | `snapshots/{txid}/` por fecha, `TxId`, conteo, build, `[Revertir]` + verificación |
| **Historial** | `HistoryPage.xaml` | `history.jsonl` timeline + hash-chain verify + filtros + `corruptedCount` warning |
| **Ajustes** | `SettingsPage.xaml` | Tema (Sistema/Claro/Oscuro), idioma (es-ES/en-US), `ExpertMode` + `InfoBar` warning, **Servicio privilegiado** con `InfoBar` explicativo + `ProgressRing` + `ServiceCheck` + `Instalar ahora` (auto-eleva, wrapper PS1, start service, verify, restart app) + `VersionsText` + `PrivilegeText` |

**Controles custom:** `MetricCard`, `RiskBadge`, `ScoreRing`, `Diagnostic*Card`, `DesignTokens.xaml` (Mica, `CaoCardStyle`, `CaoAccentButtonStyle`).

**i18n:** `Localizer` diccionario tipado `es-ES`/`en-US` (`settings.serviceExplanationTitle` etc.) → `ApplyTexts()` + `LocalizationHelper.LocalizeTree()`. Migración a `.resw` planificada sin cambiar `Localizer.Get(key)`.

---

## Requisitos

- **SO:** Windows 10 1809 (build 17763) o superior, **x64**. Diseñado y probado para **Windows 11 22H2+** (22621+)
- **Runtime:** .NET SDK **10.0.400** (`global.json`, `rollForward: latestFeature`). Artefactos release son **self-contained** (no requieren runtime)
- **Dependencias:** Windows App SDK **2.4.0**, `Microsoft.WindowsAppRuntime` incluido en builds self-contained. Sin MSIX ni registro de paquete
- **Privilegios:** instalación y ejecución requieren **cuenta de administrador con UAC habilitado**. Servicio `CAO.Privileged` corre como `LocalSystem`
- **Hardware:** 4 GB RAM, 500 MB disco (+ 394 MB ZIP release), resolución 1280×720 mínima

---

## Instalación

### Opción A — Instalador GUI (recomendado, offline)

1. Descarga [CA-O-2.1.5-win-x64.zip (394 MB)](https://github.com/Pyromesis/CA-O/releases/download/v2.1.5/CA-O-2.1.5-win-x64.zip)
2. Descomprime (mantén `ui/`, `service/`, `gui-installer/`, `uninstall/`, `setup/`)
3. Entra en `gui-installer/` → clic derecho **Ejecutar como administrador** en `CA-O.InstallerGui.exe` → UAC Sí
4. Elige destino (`C:\Program Files\CA-O`), atajos Escritorio/Inicio, progress → registra `CAO.Privileged` (`demand`, `failure 86400`) + ARP
5. **Abrir CA-O** → `C:\Program Files\CA-O\ui\CA-O.UI.exe`

> Log instalador: `%TEMP%\CA-O-Setup-Gui.log`

### Opción B — Instalador GUI online (92 MB)

1. Descarga [CA-O-Setup-GUI-x64.zip (92 MB)](https://github.com/Pyromesis/CA-O/releases/download/v2.1.5/CA-O-Setup-GUI-x64.zip)
2. Descomprime → `CA-O.InstallerGui.exe` como admin
3. Si no hay payload local (`ui/`+`service/` adyacentes), descarga `CA-O-2.1.5-win-x64.zip` desde GitHub Releases (requiere internet) y continúa

> El exe suelto de 295 KB fuera del ZIP **no inicia** (faltan DLLs WindowsAppSDK)

### Opción C — Portable ZIP (sin instalador)

Descomprime y ejecuta `ui/CA-O.UI.exe` como admin. Sin servicio → **modo solo lectura** (diagnósticos + benchmark sí, optimizar requiere servicio).

### Instalación manual del servicio

```powershell
# PowerShell como administrador
powershell -ExecutionPolicy Bypass -File scripts/install-privileged-service.ps1
sc.exe start CAO.Privileged
sc.exe query CAO.Privileged   # STATE: 4 RUNNING
sc.exe qc CAO.Privileged      # START_TYPE: DEMAND_START
sc.exe qfailure CAO.Privileged # 86400 restart/5000/...
```

### Desde la app (Ajustes)

`Ajustes → Servicio privilegiado` → **Instalar ahora** → ventana PowerShell elevada (wrapper `cao-install-{guid}.ps1`) → `Install → Start → Verify → Restart app` automático. El botón se oculta si `connected`; si `rejected` muestra "Haz clic en 'Instalar ahora'".

### Desinstalación

- `C:\Program Files\CA-O\uninstall\CA-O.Uninstaller.exe` (elevado) — o Panel de control → Programas y características → CA-O
- Detiene + elimina `CAO.Privileged`, borra atajos/archivos/ARP con auto-borrado seguro
- Manual avanzado: `scripts/uninstall.ps1` como admin

---

## Desarrollo

Requiere **.NET SDK 10.0.400** + **Windows 10 SDK 10.0.19041** + **VS 2022 17.8+** (o `dotnet` CLI). Paquetes centralizados en `Directory.Packages.props`.

```powershell
# Restaurar y compilar (Debug)
powershell -ExecutionPolicy Bypass -File scripts/build.ps1
# o manual
dotnet restore CA-O.sln
dotnet build CA-O.sln -c Debug

# Ejecutar UI (solicitará UAC)
dotnet run --project src/CA-O.UI --configuration Debug

# 5 gates (build Release + contratos + persistencia + E2E)
powershell -ExecutionPolicy Bypass -File scripts/verify.ps1

# Release self-contained + SBOM
powershell -ExecutionPolicy Bypass -File scripts/build-release.ps1
# → artifacts/release/ (ui/, service/, gui-installer/, uninstall/, setup/) + SHA256SUMS.txt

# Empaquetar
powershell -ExecutionPolicy Bypass -File scripts/package.ps1
# → artifacts/CA-O-2.1.5-win-x64.zip + .sha256

# Endurecer ACLs de datos
powershell -ExecutionPolicy Bypass -File scripts/harden-data-acls.ps1
```

**Firma Authenticode:** define `CAO_SIGN_THUMBPRINT` (thumbprint cert en `CurrentUser\My`). `scripts/sign.ps1` firma `ui/*.exe` + `service/*.exe` con timestamp. Sin cert → warning, artefactos sin firma.

**Versionado:** single source `Version.props` (`Version`/`AssemblyVersion`/`FileVersion`/`PackageVersion`/`InformationalVersion`). Bump ahí → propaga a todos los `csproj` + `AppVersion.Semantic`.

---

## Pruebas

**500+ pruebas en 7 suites, todas en Release:**

| Suite | Cubre | Cant. |
|---|---|---|
| `CA-O.Core.Tests` | Contratos catálogo, `AnalysisStateStore` (save/load/corrupt/schema), `GameCompatibility` (VBS bloqueado), `OptimizationTransaction` (snapshot/apply/verify/rollback), `RecommendationEngine`, `KnownIssueMatcher`, `HealthEngine`, 66 definiciones `IOptimization` | **361** |
| `CA-O.Security.Tests` | `IpcRequestValidator` (version/nonce/freshness/size/schema), `ReplayCache` (30 s, single-use), `CommandPolicy` (allowlist, PATH-hijacking, injection), `WindowsCallerInspector` (SID/SessionId/elevación), `PrivilegedIpcSecurityTests` (oversized/malformed/flood) | **63** |
| `CA-O.Integration.Tests` | `E2EFlowsTests` 10 flujos (Abrir→Analizar→Persistir→Vanguard→Restore→Benchmark→History→Cancel→Recovery), `TransactionJournalRecovery` (Incomplete→RollbackRequired), `ArchitectureDependencyTests` | **48** |
| `CA-O.Infrastructure.Tests` | `HistoryRobustness` (líneas malformadas), `SnapshotRepository` (TX identity), `SystemContextCache` dual-TTL, `DnsBenchmark` | **17** |
| `CA-O.Benchmark.Tests` | `SystemBenchmarkRunner` (suelo 3 %, mediana, trials) | **7** |
| `CA-O.UI.Tests` | `ViewModelTests` (Analyze/Dashboard con `SystemAnalysisService` + `CorrelationId`), `LocalizerTests` | **8** |
| `CA-O.App.Tests` | Smoke `AppHost` | — |
| **Total** | **Gates 1-5 `verify.ps1` + `build-release` con `gui-installer`** | **~504** |

```powershell
powershell -ExecutionPolicy Bypass -File scripts/test.ps1
# o
dotnet test CA-O.sln -c Release
# filtrar
dotnet test --filter "FullyQualifiedName~GameCompatibility"
```

> Nota: `ArchitectureDependencyTests` (2 tests) validan `Process.Start` solo en `SystemCommandGateway` y `UI` sin `NamedPipeServerStream` — fueron pre-existentes y se mantienen como guard de capas.

---

## Release y verificación

- **Versión actual:** **2.1.5** — [Releases](https://github.com/Pyromesis/CA-O/releases/tag/v2.1.5)
- **Artefactos:**
  - `CA-O-Setup-GUI-x64.zip` (92 MB, GUI online)
  - `CA-O-2.1.5-win-x64.zip` (343 MB, paquete completo offline con `ui/`, `service/`, `gui-installer/`, `setup/`, `uninstall/`)
- **Manifests:** `artifacts/release/SHA256SUMS.txt` + `artifacts/sbom/bom.json` (CycloneDX 1.7, 71 paquetes) cuando `CycloneDX` instalado (`dotnet tool install --global CycloneDX`)
- **Empaquetado:** `scripts/package.ps1` genera ZIP versionado + hash SHA-256. `scripts/build-release.ps1` publica `ui` y `service` como self-contained y `gui-installer` como self-contained **sin single-file** (requisito WinUI 3)

```powershell
Get-FileHash artifacts/CA-O-2.1.5-win-x64.zip -Algorithm SHA256
Get-Content artifacts/release/SHA256SUMS.txt
cat artifacts/sbom/bom.json | ConvertFrom-Json | select -ExpandProperty components | measure
```

**CI (`.github/workflows/ci.yml`):** build Debug+Release → `dotnet test` → `CodeQL` → `dotnet audit` (0 vuln) → Dependabot semanal (revisión disciplinada, no auto-merge ciego).

---

## Solución de problemas

| Síntoma | Causa | Solución |
|---|---|---|
| **App no abre / no muestra ventana** | Falta WindowsAppSDK o ejecución sin admin; `XAML parsing failed` | Verifica `%LOCALAPPDATA%\CA-O\logs\cao-ui-crash.log` y `cao-installer-crash.log`. Usa ZIP completo descomprimido, **Ejecutar como administrador**. Exe suelto 295 KB fuera de carpeta no inicia. `taskkill /F /IM CA-O.UI.exe` (elevado) si cuelgue |
| **Error 404 al instalar con GUI** | URL obsoleta (v2.0.4 y anteriores `v2.0.1/CA-O-2.0.0-…`) | Corregido v2.1.5 con fallback a `latest`. Usa `CA-O-2.1.5-win-x64.zip` offline o `CA-O-Setup-GUI-x64.zip` con internet |
| **Servicio no disponible / `CAO-IPC-004`** | `CAO.Privileged` no instalado o detenido | `sc.exe query CAO.Privileged` → `STATE: 1 STOPPED` → `powershell -ExecutionPolicy Bypass -File scripts/install-privileged-service.ps1` (admin) → `sc.exe start CAO.Privileged`. O `Ajustes → Instalar ahora` |
| **Access is denied al `sc.exe start`** | Falta elevación | Ejecuta `cmd`/`PowerShell` como admin. `CA-O.UI` ya pide UAC; el servicio requiere `LocalSystem` |
| **`Build Release falla CA1806`** | `MessageBoxW` HRESULT ignorado | Corregido v2.1.4 con `_ = MessageBoxW(...)` |
| **Análisis siempre "no disponible"** | WMI bloqueado / provider timeout | Revisa `cao-ui-structured.log` (`%LOCALAPPDATA%\CA-O\logs\`) — providers con 5 s timeout marcan `Warning`, no crash |
| **Optimización `BLOCKED CAO-GAME-001`** | Vanguard/EAC/BattlEye detectado | Intencional — `disable-vbs` y similares bloqueadas aunque `ExpertMode` on. Desinstala anti-cheat o usa perfil `Balanced` |
| **Snapshot no revierte** | `NotReversible` (defrag/DISM ResetBase) | Audidado como `NotApplicable` — no se elimina snapshot, pero no hay reversión exacta |
| **History con `corruptedCount`** | `history.jsonl` con líneas corruptas | `ReadLast` las salta, `VerifyIntegrity` muestra `InfoBar` warning sin crash. Borra `%ProgramData%\CA-O\history.jsonl` si persiste |
| **Logs** | — | `%TEMP%\CA-O-Setup-Gui.log` (instalador), `%LOCALAPPDATA%\CA-O\logs\cao-ui-structured.log` (JSON), `cao-ui-crash.log` |

---

## Documentación

| Doc | Qué cubre |
|---|---|
| [Arquitectura](docs/ARCHITECTURE.md) | Procesos, capas, motores, ciclo transaccional, persistencia |
| [Seguridad](docs/SECURITY.md) | Controles IPC, ejecución, autorización, cadena de suministro |
| [Modelo de amenazas](docs/THREAT-MODEL.md) | STRIDE, trust boundaries, residual risks |
| [Catálogo de optimizaciones](docs/OPTIMIZATION-CATALOG.md) | 19 prod + 49 históricas retiradas, evidencia/riesgo/flags |
| [Protocolo IPC](docs/IPC_PROTOCOL.md) | Envelope v2, payloads por operación, cadena validación |
| [Transacciones](docs/TRANSACTIONS.md) | Fases P0-11, estados journal, rollback verificado, recuperación |
| [Contribuir](CONTRIBUTING.md) | Flujo `feature/*`, tests, `verify.ps1`, PR checklist |
| [Changelog](CHANGELOG.md) | v2.0.0 reconstrucción nativa |

---

## Contribuir

Consulta [CONTRIBUTING.md](CONTRIBUTING.md).

**Flujo:**
1. Rama `feature/*` desde `main`
2. Tests que **fallen sin el parche** (TDD)
3. Toda optimización nueva implementa `IOptimization` con `Definition` completa + reversibilidad exacta + `PreviewAsync`
4. Si usa comando externo → patrón exacto en `CommandPolicy` + test en `CommandPolicyTests` (anti-inyección)
5. `scripts/verify.ps1` en verde (5 gates + 500+ tests)
6. PR con evidencia, impacto seguridad/compatibilidad, captura `OptimizePage` Before/After

---

## Licencia

Proyecto privado. Todos los derechos reservados. No se concede licencia de uso, copia o distribución sin autorización expresa del titular.

---

<p align="center"><i>CA-O 2.1.5 — Diagnóstico primero, evidencia después, transacción siempre. Sin promesas, solo hechos medibles.</i></p>
