<div align="center">

# CA-O 2.0

### Plataforma nativa de rendimiento, diagnóstico y optimización para **Windows 11**

> Diagnostica primero · Recomienda con evidencia · Aplica en transacción · Verifica · Revierte si falla

[![Windows 11](https://img.shields.io/badge/Windows%2011-0078D4?style=for-the-badge&logo=windows&logoColor=white)](https://www.microsoft.com/windows)
[![.NET 10](https://img.shields.io/badge/.NET%2010-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](global.json)
[![WinUI 3](https://img.shields.io/badge/WinUI%203-00B7C3?style=for-the-badge&logo=windows&logoColor=white)](https://microsoft.github.io/microsoft-ui-xaml/)
[![Build](https://img.shields.io/badge/build-passing-brightgreen?style=flat-square)](https://github.com/Pyromesis/CA-O/actions)
[![Tests](https://img.shields.io/badge/tests-245%20passed-brightgreen?style=flat-square)](#)
[![Release](https://img.shields.io/badge/release-v2.0.1-blue?style=flat-square)](https://github.com/Pyromesis/CA-O/releases/tag/v2.0.1)
[![License](https://img.shields.io/badge/license-privado-lightgrey?style=flat-square)](#licencia)

**[Descargar Instalador GUI](https://github.com/Pyromesis/CA-O/releases/download/v2.0.1/CA-O-Setup-GUI-2.0.1-x64.exe)** · **[ZIP Portable](https://github.com/Pyromesis/CA-O/releases/tag/v2.0.1)** · **[Documentación](docs/ARCHITECTURE.md)**

<img src="https://via.placeholder.com/1100x520/0A1931/FFFFFF?text=CA-O+2.0+%7C+Mica+%2B+WinUI+3+%7C+Dashboard+con+Health+86%2F100" alt="CA-O Hero" width="100%"/>

</div>

---

## Por que CA-O

| CA-O es | CA-O no es |
|---|---|
| Evidence-driven: mide hardware, red, termico, drivers, DPC/ISR y seguridad antes de recomendar | Coleccion de tweaks sin validacion |
| Transaccional: `PRECHECK -> SNAPSHOT -> APPLY -> VERIFY -> COMMIT`, rollback verificado | Promesas de "+300% FPS" sin benchmark |
| Seguro: UI con admin (requireAdministrator) -> UAC siempre + Named Pipe con ACL + nonce + replay-guard -> servicio `SYSTEM` con allowlist | Ejecucion de `powershell -Command $userInput` |
| Gaming consciente: bloquea `disable-vbs/hypervisor-off` si Vanguard/EAC/BattlEye detectado | Desactivar seguridad para inflar metricas |
| Benchmark honesto: suelo de ruido 3% — "sin mejora medible" es un veredicto valido | Comandos arbitrarios sin control |

---

## Modelo de seguridad (privilegio minimo)

```
CA-O.UI (WinUI 3, requireAdministrator — siempre pide UAC)
   │  IpcRequest { ProtocolVersion 2, RequestId, Nonce, Timestamp, Operation, TypedPayload }
   │  Validacion: version, frescura 30s, tamano 64KB, esquema, anti-replay
   ▼
Named Pipe  \\.\pipe\CA-O.Privileged.v1  — ACL: SYSTEM Full, Administrators R/W, Interactive R/W (conectar != autorizar)
   │  GetCallerIdentity() via RunAsClient() -> SID real + SessionId + elevacion (ahora siempre elevada)
   │  IpcRequestValidator + ReplayCache + Authorizer
   ▼
CA-O.Privileged (SYSTEM) — solo 7 operaciones tipadas
   • ApplyOptimization / RevertOptimization / DetectOptimization / VerifyOptimization / CaptureSnapshot / Ping / GetServiceStatus
   • Catalogo estatico ElevatedCommandCatalog — UseShellExecute=false, timeout 60s, sin PATH hijacking
```

> **PowerShell no existe en la arquitectura.** Todo comando externo esta en `CommandPolicy` (`powercfg.exe`, `bcdedit.exe`, `netsh.exe`, `wpr.exe` con rutas `%SystemRoot%\System32` absolutas).

---

## Filosofia

1. **Diagnostico primero** — nada se recomienda sin medir
2. **Evidencia clasificada** — `Official / Vendor / Benchmark / Empirical / Heuristic / Unknown` + `Risk` + `SecurityImpact` + `Compatibility`
3. **Buckets, nunca "optimizar todo"** — `Recommended · Optional · Experimental · SecuritySensitive · NotApplicable · Blocked`
4. **Anti-cheat primero** — Vanguard/EAC/BattlEye/FACEIT/Ricochet bloquean cambios que reducen seguridad (`CAO-GAME-001`)
5. **Benchmark con suelo** — `+-3%` ruido, mediana de trials, veredicto `Mejora medible / Sin mejora / Regresion`
6. **Rollback en 3 capas** — Restore Point Windows + `snapshots/{txid}/` + `history.jsonl` con hash-chain SHA-256

---

## Instalacion en 1 click

### Opcion A — Instalador GUI (recomendado)

1. Descarga **[CA-O-Setup-GUI-2.0.1-x64.exe](https://github.com/Pyromesis/CA-O/releases/download/v2.0.1/CA-O-Setup-GUI-2.0.1-x64.exe)** (134 MB, self-contained, **pide UAC siempre**)
2. Doble click -> **Si** en UAC -> veras:

> ```
> Se instalara en: C:\Program Files\CA-O
> Progreso [████████████████████] 100% — Instalacion completada!
> ```

3. Se crea **Acceso en Escritorio + Menu Inicio** y el servicio `CAO.Privileged` (demand start) — se lanza CA-O automaticamente.

> **Destino:** `C:\Program Files\CA-O\ui\CA-O.UI.exe` · Servicio: `CAO.Privileged` · Log: `%TEMP%\CA-O-Setup-Gui.log`

### Opcion B — Portable ZIP

Descarga el ZIP de `v2.0.1` y descomprime — no requiere instalacion, pero **siempre pedira UAC** al ejecutar `CA-O.UI.exe` (manifest `requireAdministrator`).

### Servicio privilegiado (si usas ZIP)

```powershell
# PowerShell como administrador
powershell -File scripts/install-privileged-service.ps1
sc.exe start CAO.Privileged
```

> **Nuevo en 2.0.1+:** la UI **siempre pide UAC** (`app.manifest` `requireAdministrator`). Ejecuta elevada aunque abras el ZIP/portable — veras el prompt de Windows al iniciar. Las mutaciones siguen viajando por el pipe al servicio `SYSTEM`; sin servicio la app queda en modo solo lectura (diagnosticos/benchmark).

---

## Arquitectura

```text
CA-O.sln  (.NET 10 · LangVersion 13.0 · WinUI 3)
├── src/CA-O.Shared          DTOs, IPC {IpcProtocol v2, Ping}, CaOPaths, ErrorCodes CAO-XXX-nnn
├── src/CA-O.Core            Catalog · Engine (Transaction) · Scoring · Gaming (GameCompatibilityPolicy) · HealthEngine
├── src/CA-O.Infrastructure  WMI 5s timeout · Storage · Security · Thermal · RegistryAccessor · SystemAnalysisService · AnalysisStateStore · SnapshotRepository · StructuredLogger
├── src/CA-O.Privileged      Named Pipe + ACL + ReplayCache + Ping/GetServiceStatus + OptimizationEngine
├── src/CA-O.UI              WinUI 3 Mica + NavigationView + 8 ViewModels (DI) + Controls (MetricCard/RiskBadge/ScoreRing)
├── src/CA-O.InstallerGui    Instalador GUI 680x620, responsive, progress, requireAdministrator
└── tests/ 245 passed        Core 134 · Integration 46 · Security 33 · Infra 17 · Benchmark 7 · UI 8
```

Diagrama conceptual:

```text
                ┌─────────────────────┐
                │      CA-O.UI        │
                │  WinUI 3 + MVVM     │
                │  (Mica, 8 VMs)      │
                └──────────┬──────────┘
                           │  AnalysisStateStore (atomico) / SnapshotRepository
                ┌──────────▼──────────┐
                │  SystemAnalysisService  (WhenAll + cancellation)
                │  HealthEngine · RecommendationEngine
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

Detalle en [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) y catalogo por optimizacion en [`docs/OPTIMIZATION-CATALOG.md`](docs/OPTIMIZATION-CATALOG.md).

---

## Dashboard — el centro en <5s

`Health 86/100` con desglose `System · Thermals · Network · Storage · Drivers · Security · Gaming` + `Ultimo analisis: hoy 20:31` + `4 recomendadas · 3 opcionales` + `Secure Boot/VBS/HVCI` + `Valorant + Vanguard protegido` + `Servicio conectado`. Botones con `AutomationProperties` y `VisualState` responsive 1280x720 -> 3840x2160.

---

## Optimizar — tarjetas de alta calidad

`RECOMENDADA · OPCIONAL · EXPERIMENTAL · RIESGO SEGURIDAD` · `Actual -> Objetivo` · `Evidencia / Riesgo / Seguridad / Compatibilidad` · `Antes/Despues` diff · Acciones `Detalles · Previsualizar · Aplicar · Revertir` con `TransactionProgress: Precheck -> Snapshot -> Apply -> Verify -> Commit` y `Rollback verificado`.

`Aplicar recomendadas` ejecuta lote transaccional con parada al primer fallo (spec 124).

---

## Gaming Center — protege, no rompe

Detecta `Valorant, Fortnite, Apex, CS2, Overwatch 2, LoL, R6, CoD, Destiny 2` + `Vanguard/EAC/BattlEye/FACEIT/Ricochet` (lectura `HKLM\SYSTEM\CurrentControlSet\Services`).

Matriz 24:

| Optimizacion | Vanguard | EAC | Safe? |
|---|---|---|---|
| `disable-vbs` | **BLOQUEADA** | **BLOQUEADA** | `CAO-GAME-001` en Core **y** Privileged |
| `gpu-scheduling` | Permitida | Permitida | SAFE |
| `transparency` | Permitida | Permitida | SAFE |

Gaming VM muestra `3 bloqueadas · 5 permitidas · 2 revision`.

---

## Diagnostics — util, no 2 metricas

Paralelo `WhenAll` + `CancellationToken` (5s WMI timeout): **CPU** (modelo, carga, frecuencia, interpretacion `Normal/Alta carga`), **GPU** (driver, VRAM), **RAM**, **Disco** (tipo, libre, salud), **Windows** (build, pending reboot), **Seguridad** (Secure Boot/TPM/VBS/HVCI), **Drivers** (firmados/problemCode), **Input** (HID, mouse accel). Si no disponible -> `No disponible en este hardware/API`, nunca `0` inventado.

---

## Benchmark — flujo guiado honesto

`Paso 1 Crear linea base -> Paso 2 Aplicar optimizacion -> Paso 3 Medir despues -> Paso 4 Comparar -> Paso 5 Veredicto` con `CPU +1.7% / Memoria +0.8% (suelo +-3%) -> Sin mejora medible — sin evidencia para mantener`. No simula FPS.

---

## Historial y Restore — nunca cierran la app

* **History** `history.jsonl` JSONL con hash-chain: `ReadLast` tolera lineas corruptas, `VerifyIntegrity` muestra `2 entradas no pudieron leerse` en `InfoBar`, nunca crash (19).
* **Restore** usa `SnapshotRepository` (`snapshots/{txid}/snapshot.json + manifest.json + integrity.json` SHA-256) — lista `2026-08-26 20:31 — DisableTelemetry — TX:82af… — 3 valores — build 22631`.

---

## Requisitos y desarrollo

- **Windows 10 1809+** (objetivo Windows 11) x64 · **.NET SDK 10.0** (`global.json`) · **Windows App SDK 2.4**
- **Microsoft.UI.Xaml 2.4** · `CommunityToolkit.Mvvm 8.4`

```powershell
powershell -File scripts\build.ps1            # restore + build
powershell -File scripts\test.ps1             # 245 tests
dotnet run --project src\CA-O.UI              # lanzar (siempre pide UAC — manifest requireAdministrator)

# Release
powershell -File scripts\verify.ps1           # 5 gates (build+tests+contratos+persistencia+E2E)
powershell -File scripts\build-release.ps1    # publica ui+service+gui-setup + SHA256SUMS + bom.json (SBOM CycloneDX 1.7, 71 packages)
powershell -File scripts\package.ps1          # zip 197 MB + SHA256
```

Signing: `CAO_SIGN_THUMBPRINT` en almacen usuario -> `Get-AuthenticodeSignature Valid` + `SBOM` obligatorio.

---

## Anti-cheat

Vanguard/EAC/BattlEye/FACEIT/Ricochet detectados por lectura de servicios. Con anti-cheat:

* Cambios que reducen seguridad -> **bloqueados** (`SecuritySensitive` o `Blocked`, requieren Expert + confirmacion, y `CAO-GAME-001` en servicio)
* Deteccion conservadora, sin garantias futuras de terceros.

---

## Release

**`v2.0.1`** — 10 assets en [Releases](https://github.com/Pyromesis/CA-O/releases/tag/v2.0.1):

* `CA-O-Setup-GUI-2.0.1-x64.exe` 135 MB (instalador GUI, requireAdministrator)
* `CA-O-Setup-2.0.0-x64.exe` 94 MB (consola)
* `CA-O-2.0.0-20260826-xxxx-win-x64.zip` 64 MB + `selfcontained` 127 MB + `release` 197 MB (con SBOM)
* `SHA256SUMS.txt` + `bom.json` (CycloneDX)

---

## Contribuir

Ver `CONTRIBUTING.md` — flujo `feature/* -> PR -> verify.ps1` debe pasar.

## Licencia

Proyecto privado.

---

<div align="center">

**Hecho con WinUI 3 · Mica · MVVM · Transacciones · Gaming consciente**

*Analizar realmente analiza. Optimizar realmente optimiza. Verificar realmente verifica.*

</div>
