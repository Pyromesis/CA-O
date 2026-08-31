# CA-O 2.0 — Plataforma nativa de rendimiento, diagnostico y optimizacion para Windows 11

> Principio operativo: diagnosticar primero, recomendar con evidencia, aplicar en transaccion, verificar, revertir si falla.

[![Windows 11](https://img.shields.io/badge/Windows%2011-0078D4?style=flat-square&logo=windows&logoColor=white)](https://www.microsoft.com/windows)
[![.NET 10](https://img.shields.io/badge/.NET%2010-512BD4?style=flat-square&logo=dotnet&logoColor=white)](global.json)
[![WinUI 3](https://img.shields.io/badge/WinUI%203-00B7C3?style=flat-square&logo=windows&logoColor=white)](https://microsoft.github.io/microsoft-ui-xaml/)
[![Build](https://img.shields.io/badge/build-passing-brightgreen?style=flat-square)](https://github.com/Pyromesis/CA-O/actions)
[![Tests](https://img.shields.io/badge/tests-334%20passed-brightgreen?style=flat-square)](#pruebas)
[![Release](https://img.shields.io/badge/release-v2.1.4-blue?style=flat-square)](https://github.com/Pyromesis/CA-O/releases/tag/v2.1.4)
[![License](https://img.shields.io/badge/license-privado-lightgrey?style=flat-square)](#licencia)

**Descargas v2.1.4:** [CA-O-Setup-GUI-x64.zip (92 MB, instalador GUI)](https://github.com/Pyromesis/CA-O/releases/download/v2.1.4/CA-O-Setup-GUI-x64.zip) | [CA-O-2.1.4-win-x64.zip (394 MB, paquete completo offline)](https://github.com/Pyromesis/CA-O/releases/download/v2.1.4/CA-O-2.1.4-win-x64.zip) | [Notas de la version](https://github.com/Pyromesis/CA-O/releases/tag/v2.1.4) | [Documentacion](docs/ARCHITECTURE.md)

---

## Indice

1. [Resumen](#resumen)
2. [Caracteristicas principales](#caracteristicas-principales)
3. [Modelo de seguridad](#modelo-de-seguridad)
4. [Arquitectura](#arquitectura)
5. [Requisitos](#requisitos)
6. [Instalacion](#instalacion)
7. [Guia de uso](#guia-de-uso)
8. [Desarrollo](#desarrollo)
9. [Pruebas](#pruebas)
10. [Estructura del repositorio](#estructura-del-repositorio)
11. [Release y verificacion](#release-y-verificacion)
12. [Solucion de problemas](#solucion-de-problemas)
13. [Documentacion](#documentacion)
14. [Contribuir](#contribuir)
15. [Licencia](#licencia)

---

## Resumen

CA-O 2.0 es una aplicacion nativa Windows escrita en .NET 10 y WinUI 3 (Windows App SDK 2.4) que diagnostica el sistema, genera recomendaciones clasificadas por evidencia y aplica cambios de forma transaccional con capacidad de reversion.

A diferencia de colecciones de tweaks sin validacion, CA-O mide hardware, termicas, red, almacenamiento, drivers y postura de seguridad antes de recomendar. Cada optimizacion pertenece a un bucket (Recommended, Optional, Experimental, SecuritySensitive, NotApplicable, Blocked) y se ejecuta bajo el flujo `PRECHECK -> SNAPSHOT -> APPLY -> VERIFY -> COMMIT` con rollback automatico.

La interfaz requiere elevacion en cada inicio (`app.manifest` `requireAdministrator`) y delega toda mutacion privilegiada al servicio Windows `CAO.Privileged` (SYSTEM) a traves de un Named Pipe autenticado con ACL restrictiva, validacion de esquema y proteccion anti-replay.

---

## Caracteristicas principales

### Diagnostico basado en evidencia

- **Contexto del sistema:** modelo de CPU, carga, frecuencia, GPU y VRAM, memoria, tipo de disco y espacio libre, build de Windows, reinicio pendiente, estado termico ACPI, dispositivos de entrada HID.
- **Red:** latencia, jitter, perdida, benchmark DNS multi-resolver, prueba de bufferbloat idle vs loaded.
- **Seguridad:** Secure Boot, TPM, VBS, HVCI y estado de drivers (firmados, con codigo de problema).
- **Tiempo limite y cancelacion:** proveedores WMI con timeout de 5 segundos y ejecucion paralela con `Task.WhenAll` y `CancellationToken`. Valores no disponibles se informan como no disponibles, nunca se inventan.

### Motor de recomendaciones

- Clasificacion por buckets analizados primero: Recommended, Optional, Experimental, SecuritySensitive, NotApplicable. La clase `Blocked` aplica cuando el sistema de compatibilidad de juegos lo exige.
- Puntuacion 0 a 100 que combina beneficio esperado, nivel de evidencia (`Official`, `Vendor`, `Benchmark`, `Empirical`, `Heuristic`, `Unknown`), riesgo, impacto de seguridad, compatibilidad y reversibilidad.
- Perfiles: Safe, Balanced, Gaming, Competitive, Privacy, Security, Maintenance, Expert y Custom. Cada perfil consulta el `SystemContext` real; ninguno es una lista fija.

### Transacciones y recuperacion

- Toda mutacion se persiste como snapshot antes de aplicar. Flujo: `PRECHECK` (compatibilidad), `SNAPSHOT`, `APPLY`, `VERIFY` (deteccion en vivo), `COMMIT` (entrada en historial). Fallos en `APPLY` o `VERIFY` disparan rollback automatico y verificacion post-reversion.
- Tres capas de recuperacion: punto de restauracion de Windows, snapshots en `snapshots/{txid}/` con `manifest.json` e `integrity.json` (SHA-256), e historial `history.jsonl` con hash-chain.
- Servicio de recuperacion ante caida (`CrashRecoveryService`) marca transacciones sin commit como incompletas y expone candidatos para reversion.

### Gaming consciente

- Deteccion de juegos (Valorant, Fortnite, Apex, CS2, Overwatch 2, League of Legends, Rainbow Six, Call of Duty, Destiny 2) y anti-cheats (Vanguard, EasyAntiCheat, BattlEye, FACEIT, Ricochet) mediante lectura de `HKLM\SYSTEM\CurrentControlSet\Services`.
- Matriz de compatibilidad de 24 entradas con estados SAFE, CAUTION y BLOCKED. Ejemplo: `disable-vbs` se bloquea con codigo `CAO-GAME-001` cuando se detecta Vanguard o EAC, tanto en Core como en el servicio privilegiado. El modo Expert no elude bloqueos criticos.

### Benchmark honesto

- Flujo guiado en cinco pasos: crear linea base, aplicar optimizacion, medir despues, comparar, veredicto. Usa mediana de multiples intentos y suelo de ruido de 3 por ciento. Los veredictos son Mejora medible, Sin mejora medible o Regresion. No se simulan FPS.

### Interfaz

- WinUI 3 con Mica, `NavigationView` y ocho paginas: Panel, Analizar, Optimizar, Gaming, Diagnostico, Benchmark, Restaurar, Historial y Ajustes.
- ViewModels con inyeccion de dependencias (`AppHost` con `Microsoft.Extensions.DependencyInjection`), controles `MetricCard`, `RiskBadge` y `ScoreRing`, y diccionario de localizacion `es-ES`/`en-US`.
- Persistencia de estado: `AnalysisStateStore` (atomico, TTL 24 horas), `SnapshotRepository` (identidad por transaccion) y `StructuredLogger` con correlacion.

---

## Modelo de seguridad

```
CA-O.UI (WinUI 3, requireAdministrator — siempre solicita UAC)
   |  IpcRequest { ProtocolVersion 2, RequestId, Nonce, Timestamp, Operation, TypedPayload }
   |  Validacion: version, frescura 30s, tamaño 64KB, esquema, anti-replay
   v
Named Pipe  \\.\pipe\CA-O.Privileged.v1  — ACL: SYSTEM Full, Administrators R/W, Interactive R/W (conectar no es autorizar)
   |  GetCallerIdentity() via RunAsClient() — SID real, nombre, SessionId, elevacion
   |  IpcRequestValidator + ReplayCache + Authorizer
   v
CA-O.Privileged (SYSTEM) — solo 7 operaciones tipadas
   - ApplyOptimization / RevertOptimization / DetectOptimization / VerifyOptimization / CaptureSnapshot / Ping / GetServiceStatus
   - Catalogo estatico ElevatedCommandCatalog — UseShellExecute=false, timeout 60s, rutas absolutas %SystemRoot%\System32
```

Detalles completos en [docs/SECURITY.md](docs/SECURITY.md) y [docs/THREAT-MODEL.md](docs/THREAT-MODEL.md).

Principios aplicados:

- **PowerShell no existe en la arquitectura de ejecucion.** Todo comando externo debe existir en `CommandPolicy` con rutas absolutas y patrones de argumentos cerrados.
- **Allowlist estricta.** El servicio solo acepta siete operaciones tipadas. No existe operacion generica de ejecucion de comandos.
- **Autorizacion por token.** `AdministratorsOnlyAuthorizer` verifica pertenencia a Administrators, elevacion y `SessionId`. Tokens filtrados o usuarios estandar reciben `CAO-SEC-003`/`CAO-SEC-005`.
- **Protocolo v2 tipado.** Envelope versionado (Version 2) con `RequestId` GUID, nonce alfanumerico 1 a 128, `CreatedAtUtc` con ventana de 30 segundos y limite de tamaño 64 KB request / 256 KB respuesta. Errores estructurados `CAO-XXX-nnn`.
- **Cancelacion segura.** La cancelacion solo se atiende antes de `SNAPSHOT`/`APPLY`. Durante `APPLY` es atomica y se marca como `CancellationDeferred` tras completar verificacion y commit.
- **Cadena de suministro.** CodeQL y auditoria NuGet en CI (`.github/workflows/ci.yml`), Dependabot semanal, `SHA256SUMS.txt` y SBOM CycloneDX (`artifacts/sbom/bom.json`) en cada release.

---

## Arquitectura

```
CA-O.sln  (.NET 10 · LangVersion 13.0 · WinUI 3)
├── src/CA-O.Shared          DTOs, IPC {IpcProtocol v2, Ping}, CaOPaths, ErrorCodes CAO-XXX-nnn
├── src/CA-O.Core            Catalog, Engine transaccional, Scoring, Gaming (GameCompatibilityPolicy), HealthEngine, CrashRecovery
├── src/CA-O.Infrastructure  WMI 5s timeout, SystemAnalysisService, AnalysisStateStore, SnapshotRepository, StructuredLogger, FileSnapshotStore, JsonHistoryLogger, Benchmark
├── src/CA-O.Privileged      Servicio SYSTEM + Named Pipe + ReplayCache + OptimizationEngine
├── src/CA-O.UI              WinUI 3 Mica + 8 ViewModels DI + Controls + paginas con VisualState responsive
├── src/CA-O.InstallerGui    Instalador GUI 680x620, progress, requireAdministrator
├── src/CA-O.Setup            Instalador consola fallback, requireAdministrator
├── src/CA-O.Uninstaller      Desinstalador registrado en ARP
└── tests/ 277 passed         Core 134 · Integration 48 · Security 63 · Infra 17 · Benchmark 7 · UI 8
```

Diagrama de capas:

```
                ┌─────────────────────┐
                │      CA-O.UI        │
                │  WinUI 3 + MVVM     │
                │  (Mica, 8 VMs)      │
                └──────────┬──────────┘
                           │  AnalysisStateStore / SnapshotRepository
                ┌──────────▼──────────┐
                │ SystemAnalysisService (WhenAll + cancellation)
                │ HealthEngine · RecommendationEngine
                └──────────┬──────────┘
                           │
             ┌─────────────┴─────────────┐
             │                           │
┌────────────▼────────────┐   ┌─────────▼──────────┐
│ CA-O.Infrastructure     │   │ CA-O.Privileged   │
│ WMI/NMI/Registry/IO     │   │ IPC v2 + allowlist │
└─────────────────────────┘   └─────────┬──────────┘
                                        │
                                ┌───────▼────────┐
                                │ Windows system │
                                └────────────────┘
```

Documentacion detallada en [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md). Catalogo por optimizacion en [docs/OPTIMIZATION-CATALOG.md](docs/OPTIMIZATION-CATALOG.md), protocolo IPC en [docs/IPC_PROTOCOL.md](docs/IPC_PROTOCOL.md) y transacciones en [docs/TRANSACTIONS.md](docs/TRANSACTIONS.md).

---

## Requisitos

- **Sistema operativo:** Windows 10 1809 (build 17763) o superior, x64. Diseñado y probado para Windows 11 22H2+.
- **Runtime:** .NET SDK 10.0.400 (definido en [global.json](global.json), `rollForward` latestFeature). Los artefactos de release son self-contained y no requieren runtime instalado.
- **Dependencias:** Windows App SDK 2.4.0, Microsoft.WindowsAppRuntime redistribuible incluido en builds self-contained. No se requiere MSIX ni registro de paquete.
- **Privilegios:** instalacion y ejecucion requieren cuenta de administrador con UAC habilitado. El servicio privilegiado se ejecuta como LocalSystem.

---

## Instalacion

### Opcion A — Instalador GUI (recomendado, offline)

1. Descarga [CA-O-2.1.4-win-x64.zip (394 MB)](https://github.com/Pyromesis/CA-O/releases/download/v2.1.4/CA-O-2.1.4-win-x64.zip) desde [Releases](https://github.com/Pyromesis/CA-O/releases/tag/v2.1.4).
2. Descomprime el zip. Mantiene la estructura `ui/`, `service/`, `gui-installer/`, `uninstall/`, `setup/`.
3. Entra en `gui-installer/` y ejecuta `CA-O.InstallerGui.exe` con clic derecho y Ejecutar como administrador. Acepta el prompt de UAC.
4. El instalador muestra Destino `C:\Program Files\CA-O`, opciones de acceso directo en escritorio y menu inicio, y barra de progreso. Al finalizar registra el servicio `CAO.Privileged` (inicio bajo demanda, policy de fallo restart) y crea la entrada ARP para desinstalacion.
5. Pulsa Abrir CA-O para lanzar `C:\Program Files\CA-O\ui\CA-O.UI.exe`.

Destino de instalacion: `C:\Program Files\CA-O\ui\CA-O.UI.exe` y `C:\Program Files\CA-O\service\CA-O.Privileged.exe`. Log del instalador en `%TEMP%\CA-O-Setup-Gui.log`. Logs de la aplicacion en `%LOCALAPPDATA%\CA-O\logs\`.

### Opcion B — Instalador GUI online (92 MB)

1. Descarga [CA-O-Setup-GUI-x64.zip (92 MB)](https://github.com/Pyromesis/CA-O/releases/download/v2.1.4/CA-O-Setup-GUI-x64.zip).
2. Descomprime y ejecuta `CA-O.InstallerGui.exe` como administrador.
3. Si no se encuentra payload local (`ui/` y `service/` adyacentes), el instalador descarga `CA-O-2.1.4-win-x64.zip` desde GitHub Releases (requiere conexion a internet) y continua la instalacion. El archivo exe suelto de 295 KB no funciona aislado fuera del zip.

### Opcion C — Portable ZIP (sin instalador)

Descarga el paquete completo, descomprime y ejecuta `ui/CA-O.UI.exe` directamente como administrador. La UI siempre solicita UAC por su manifiesto `requireAdministrator`. Sin el servicio instalado, la aplicacion opera en modo solo lectura (diagnosticos y benchmark disponibles, optimizaciones requieren servicio).

### Instalacion manual del servicio

Si usas el modo portable y necesitas mutaciones privilegiadas:

```powershell
# PowerShell como administrador
powershell -ExecutionPolicy Bypass -File scripts/install-privileged-service.ps1
sc.exe start CAO.Privileged
sc.exe query CAO.Privileged
```

Desinstalacion: abre `C:\Program Files\CA-O`, entra en `uninstall\` y ejecuta el desinstalador incluido. Tambien disponible en Panel de control > Programas y caracteristicas > CA-O. Para desinstalacion manual avanzada ejecuta `scripts/uninstall.ps1` como administrador.

---

## Guia de uso

### Panel

Health 0 a 100 con desglose por dimensiones (Sistema, Termicas, Red, Almacenamiento, Drivers, Seguridad, Gaming), ultimo analisis, conteo de recomendadas y opcionales, estado de Secure Boot, VBS y HVCI, juegos detectados y estado del servicio. Indica Explicitamente cuando una dimension no pudo medirse.

### Analizar

Ejecuta `SystemAnalysisService` con providers WMI en paralelo. Persiste el resultado en `AnalysisStateStore` y alimenta `UiState` para todas las paginas. Muestra tolerancia a fallos por provider.

### Optimizar

Tarjetas por optimizacion con bucket (Recommended, Optional, Experimental, SecuritySensitive), estado actual frente a objetivo, evidencia, riesgo, impacto de seguridad y compatibilidad, diff antes y despues, y acciones Detalles, Previsualizar, Aplicar y Revertir. El progreso de transaccion muestra `Precheck`, `Snapshot`, `Apply`, `Verify`, `Commit` con rollback verificado. La accion Aplicar recomendadas ejecuta lote con parada al primer fallo.

### Gaming Center

Lista juegos instalados y estado de anti-cheat. La matriz de compatibilidad indica para cada optimizacion si es SAFE, CAUTION o BLOCKED. Con Vanguard, EAC o BattlEye presente, cambios como `disable-vbs` aparecen como bloqueados con codigo `CAO-GAME-001`. La vista muestra conteos de bloqueadas, permitidas y en revision.

### Diagnostico

Seis dimensiones paralelas con interpretacion en lenguaje natural (por ejemplo, CPU Normal o Alta carga, GPU con version de driver y VRAM, disco con tipo y salud). Si una API no esta disponible en el hardware, se informa como no disponible.

### Benchmark

Flujo en cinco pasos con creacion de linea base, medicion antes y despues y comparacion. Reporta CPU, memoria y disco con suelo de ruido de 3 por ciento. Verdictos posibles: Mejora medible, Sin mejora medible, Regresion.

### Historial

Timeline de `history.jsonl` con hash-chain SHA-256. `ReadLast` tolera lineas corruptas y `VerifyIntegrity` informa advertencias en `InfoBar` sin cerrar la aplicacion. Cada entrada incluye `TxId`, `OptimizationId`, resultado de aplicacion y estado de verificacion.

### Restaurar

Repositorio de snapshots `snapshots/{txid}/snapshot.json` y `manifest.json` con `integrity.json`. Lista fecha, optimizacion, `TxId`, conteo de valores y build de Windows. Permite reversion por transaccion y verificacion post-reversion.

### Ajustes

Tema claro, oscuro o sistema, idioma `es-ES` y `en-US` (cambia toda la interfaz al instante), modo Expert con advertencia explicita y estado del servicio con accion de comprobacion.

### Persistencia del análisis

CA-O conserva el último análisis entre sesiones en `%ProgramData%\CA-O\analysis-state.json` con escritura atómica y versionado de schema. Al abrir la app recupera automáticamente el último análisis, muestra su antigüedad y freshness. Cambiar de pestaña no pierde datos: cada página hidrata desde `UiState`/`AnalysisSessionService`.

### Recomendación de actualización

Se recomienda ejecutar un nuevo análisis aproximadamente una vez por semana o después de instalar/desinstalar juegos. Hasta 7 días el análisis se considera actualizado; más de 7 días muestra advertencia "Se recomienda ejecutar un análisis nuevo"; si se detectan cambios en los juegos instalados marca `Stale (GameInventoryChanged)`. El análisis no se ejecuta automáticamente al abrir la app.

### DNS

CA-O puede aplicar la configuración DNS directamente cuando dispone del servicio privilegiado: detecta adaptador activo (ignora Hyper-V/VMware/VPN), crea snapshot del estado previo, aplica `1.1.1.1/1.0.0.1` etc., verifica el DNS activo y hace rollback si la verificación falla. Sin servicio muestra "No fue posible aplicar DNS" sin mandar al usuario a Windows Settings.

### Filtros de optimización

Optimizar permite filtrar por Recommended, Optional y Experimental con contadores dinámicos `Todas (X) | Recomendadas (X) | Opcionales (X) | Experimentales (X)`. El estado del filtro persiste en memoria mientras la app está abierta. "Aplicar recomendadas" solo aplica `Recommended`.

### Desinstalación

La desinstalación manual se encuentra en `C:\Program Files\CA-O\uninstall\` (ejecuta `CA-O.Uninstaller.exe` con elevación). También está disponible desde Aplicaciones instaladas / Programas y características (ARP apunta a `uninstall\CA-O.Uninstaller.exe`). El desinstalador detiene y elimina el servicio `CAO.Privileged`, elimina accesos directos, archivos y la entrada ARP con auto-borrado seguro de la carpeta raíz.

---

## Desarrollo

Requiere .NET SDK 10.0.400 y Windows 10 SDK 10.0.19041. Los paquetes se centralizan en [Directory.Packages.props](Directory.Packages.props) y las propiedades comunes en [Directory.Build.props](Directory.Build.props).

```powershell
# Restaurar y compilar (Debug)
powershell -ExecutionPolicy Bypass -File scripts/build.ps1
# o manualmente
dotnet restore CA-O.sln
dotnet build CA-O.sln -c Debug

# Ejecutar UI (solicitara UAC)
dotnet run --project src/CA-O.UI --configuration Debug

# Verificacion de cinco gates (build Release + contratos + persistencia + E2E)
powershell -ExecutionPolicy Bypass -File scripts/verify.ps1

# Release self-contained con SBOM
powershell -ExecutionPolicy Bypass -File scripts/build-release.ps1
# artefacts en artifacts/release/ (ui/, service/, gui-installer/, uninstall/, setup/) y SHA256SUMS.txt
```

Firma Authenticode: establece `CAO_SIGN_THUMBPRINT` con el thumbprint del certificado en el almacen de usuario. La verificacion de firma y la generacion de SBOM CycloneDX (`artifacts/sbom/bom.json`) son obligatorias para publicar.

---

## Pruebas

277 pruebas en seis suites, todas en Release:

| Suite | Cobertura | Cantidad |
|---|---|---|
| `CA-O.Core.Tests` | Contratos de catalogo, AnalysisStateStore (guardado, carga, corrupcion), GameCompatibility (VBS bloqueado), transacciones, scoring, health | 134 |
| `CA-O.Security.Tests` | Validador IPC (Ping y Apply cross-check), IpcPingTests, inyeccion de catalogo | 63 |
| `CA-O.Integration.Tests` | Flujos E2E (Abrir, Analizar, Persistir, Vanguard, Restore, Benchmark) y recuperacion de journal transaccional | 48 |
| `CA-O.Infrastructure.Tests` | Robustez de historial (lineas malformadas), SnapshotRepository (identidad TX), cache de contexto dual-TTL | 17 |
| `CA-O.Benchmark.Tests` | SystemBenchmarkRunner (suelo 3 por ciento, mediana) | 7 |
| `CA-O.UI.Tests` | ViewModels (Analyze y Dashboard con SystemAnalysisService y correlacion) | 8 |
| **Total** | **Gates 1 a 5 de verify.ps1 mas build-release con gui-installer** | **277** |

Ejecucion:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/test.ps1
# o
dotnet test CA-O.sln -c Release
```

---

## Estructura del repositorio

```
CA-O.sln
├── src/CA-O.Shared/           Contratos, DTOs, IPC v2, CaOPaths, codigos CAO-XXX
├── src/CA-O.Core/             Catalogo, Engine transaccional, Scoring, Gaming, HealthEngine, Rollback
├── src/CA-O.Infrastructure/   WMI, SystemAnalysisService, AnalysisStateStore, SnapshotRepository, Logging, Benchmark
├── src/CA-O.Privileged/       Servicio Windows, pipe con ACL, ReplayCache, OptimizationEngine
├── src/CA-O.UI/               WinUI 3, AppHost DI, ViewModels, Controls, paginas, recursos
├── src/CA-O.InstallerGui/     Instalador grafico 680x620, Mica, progress, requireAdministrator
├── src/CA-O.Setup/            Instalador consola fallback
├── src/CA-O.Uninstaller/      Desinstalador y entrada ARP
├── tests/                     Suites Core, Security, Integration, Infrastructure, Benchmark, UI
├── docs/                      ARCHITECTURE.md, SECURITY.md, THREAT-MODEL.md, OPTIMIZATION-CATALOG.md, IPC_PROTOCOL.md, TRANSACTIONS.md
├── scripts/                   build.ps1, test.ps1, verify.ps1, build-release.ps1, package.ps1, install-privileged-service.ps1
└── .github/workflows/         CI con build, tests, CodeQL y auditoria
```

Versiones centralizadas en `Directory.Packages.props` y `global.json`.

---

## Release y verificacion

- **Version actual:** 2.1.4 — [Releases](https://github.com/Pyromesis/CA-O/releases/tag/v2.1.4)
- **Artefactos:** `CA-O-Setup-GUI-x64.zip` (92 MB, GUI online), `CA-O-2.1.4-win-x64.zip` (343 MB, paquete completo offline con `ui/`, `service/`, `gui-installer/`, `setup/`, `uninstall/`).
- **Manifests:** `artifacts/release/SHA256SUMS.txt` y `artifacts/sbom/bom.json` (CycloneDX 1.7, 71 paquetes) cuando `CycloneDX` esta instalado (`dotnet tool install --global CycloneDX`).
- **Empaquetado:** `scripts/package.ps1` genera zip versionado con hash SHA-256. `scripts/build-release.ps1` publica `ui` y `service` como self-contained y `gui-installer` como self-contained sin single-file (requerido por WinUI 3).

Comprobacion de integridad:

```powershell
Get-FileHash artifacts/CA-O-2.1.4-win-x64.zip -Algorithm SHA256
Get-Content artifacts/release/SHA256SUMS.txt
```

---

## Solucion de problemas

**La aplicacion no abre o no muestra ventana.** Verifica `%LOCALAPPDATA%\CA-O\logs\cao-ui-crash.log` y `cao-installer-crash.log`. Si ves `XAML parsing failed`, usa el zip completo descomprimido y ejecuta como administrador. El exe suelto de 295 KB fuera de su carpeta no inicia por falta de DLLs de Windows App SDK. Cierra procesos elevados colgados con `taskkill /F /IM CA-O.UI.exe` desde una consola elevada.

**Error 404 al instalar con el GUI.** Ocurria en v2.0.4 y anteriores por URL de descarga obsoleta (`v2.0.1/CA-O-2.0.0-...`). Corregido en v2.1.4 con URL `v2.0.5/CA-O-2.0.5-win-x64.zip` y fallback a `latest`. Usa `CA-O-2.1.4-win-x64.zip` para instalacion offline sin descarga, o `CA-O-Setup-GUI-x64.zip` con conexion a internet.

**Servicio no disponible.** Comprueba `sc.exe query CAO.Privileged`. Si no existe, ejecuta `scripts/install-privileged-service.ps1` como administrador. La UI probará conexion al iniciar y muestra Servicio conectado o no disponible en la barra superior.

**Build Release falla con CA1806.** Corregido en v2.1.4 descartando el HRESULT de `MessageBoxW` con `_ = MessageBoxW(...)`.

Logs adicionales: `%TEMP%\CA-O-Setup-Gui.log` del instalador y `%LOCALAPPDATA%\CA-O\logs\cao-ui-structured.log` (JSON estructurado).

---

## Documentacion

- [Arquitectura](docs/ARCHITECTURE.md) — procesos, capas, motores y ciclo de vida transaccional
- [Seguridad](docs/SECURITY.md) — controles IPC, ejecucion externa, autorizacion y cadena de suministro
- [Modelo de amenazas](docs/THREAT-MODEL.md)
- [Catalogo de optimizaciones](docs/OPTIMIZATION-CATALOG.md)
- [Protocolo IPC](docs/IPC_PROTOCOL.md)
- [Transacciones](docs/TRANSACTIONS.md)
- [Contribuir](CONTRIBUTING.md)
- [Changelog](CHANGELOG.md)

---

## Contribuir

Consulta [CONTRIBUTING.md](CONTRIBUTING.md). Flujo: rama `feature/*` desde `main`, tests que fallen sin el parche, `scripts/verify.ps1` en verde y pull request con evidencia y analisis de impacto de seguridad y compatibilidad. Toda optimizacion nueva debe implementar `IOptimization` con metadatos completos y reversibilidad, y registrar su patron exacto en `ElevatedCommandCatalog` si usa comandos externos.

---

## Licencia

Proyecto privado. Todos los derechos reservados. No se concede licencia de uso, copia o distribucion sin autorizacion expresa del titular.



