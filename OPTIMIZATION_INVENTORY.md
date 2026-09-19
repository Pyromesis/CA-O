# CA-O Optimization Inventory — 92 production optimizations

> **Estado verificado (revisado contra `OptimizationCatalog.All`):**
> las **92** optimizaciones del catálogo están en **PRODUCTION**.
> No queda ningún STUB. Cada una muta Windows de verdad (Registry API,
> `powercfg`, `netsh`, `schtasks`, `defrag`, `fsutil`, `DISM`, SCM) bajo el
> flujo transaccional `PRECHECK → SNAPSHOT → APPLY → VERIFY → COMMIT`.
>
> Además, las 92 entradas se proyectan en grupos honestos
> (`CatalogProjections`, spec §5.3): **68** de rendimiento real (batch default),
> **14** repairs, **4** diagnósticos de solo lectura y **6** restores.
> Proyección, no borrado: `All` sigue intacto y todo ID sigue resolviendo.
>
> El recuento se valida automáticamente en los tests de catálogo
> (`CA-O.Core.Tests`), por lo que este documento no puede volverse a desincronizar
> del código sin que la CI lo detecte.

## Status legend

- **PRODUCTION** — Mutación real de Windows, con `Detect`/`Capture`/`ApplyAsync`/
  `RevertAsync`/`VerifyAsync` implementados y reversibles (salvo `NotReversible`).
- **LEGACY (retirada)** — Duplicado exacto de otra entrada. Solo existe en
  `AllLegacy` para trazabilidad docs ↔ código. **No se aplica.**

---

## Resumen

| Status | Count |
|--------|-------|
| PRODUCTION | 92 |
| LEGACY (retirada por duplicada) | 3 |
| **STUB / falsa** | **0** |

> **Nota histórica:** una versión anterior de este archivo listaba 48 entradas
> como `STUB` ("solo escribe a `HKCU\Software\CA-O\<id>`"). Eso **ya no es
> cierto**: aquellas entradas se implementaron de forma real y se promocionaron
> a producción. Una búsqueda de `Software\CA-O` en `src/CA-O.Core/Optimizations`
> devuelve un único resultado legítimo: el índice de revert de
> `disable-selected-third-party-background-task` (la mutación real vive en el
> Programador de tareas; la clave solo recuerda *qué* tareas reactivar).

---

## Catálogo por categoría (92 producción)

### Performance (8)

| ID | Implementación real | Reversible |
|---|---|---|
| `disable-background-apps` | Registry `HKCU\BackgroundAccessApplications` + `GlobalUserDisabled` | ✅ |
| `disable-visual-effects` | `VisualFXSetting=2` + ajustes avanzados | ✅ |
| `disable-transparency` | `EnableTransparency=0` (DWM) | ✅ |
| `zero-menu-delay` | `MenuShowDelay=0` | ✅ |
| `disable-dynamic-tick` | `bcdedit /set disabledynamictick yes` | ✅ |
| `disable-search-indexing` | Servicio `WSearch` (SCM) + Registry | ✅ |
| `maximum-power-plan` | `powercfg /setactive` (GUID Alto rendimiento) | ✅ |
| `disable-vbs` | `bcdedit /set hypervisorlaunchtype off` — **BLOCKED con Vanguard/EAC** | ✅ |

### Privacy & Security (6)

| ID | Implementación real | Reversible |
|---|---|---|
| `disable-telemetry` | `AllowTelemetry=0` (HKLM políticas) | ✅ |
| `disable-cortana` | `AllowCortana=0` | ✅ |
| `disable-widgets` | `TaskbarDa=0` | ✅ |
| `disable-copilot` | HKCU+HKLM WindowsCopilot | ✅ |
| `disable-suggestions` | `ContentDeliveryManager` | ✅ |
| `disable-onedrive-autostart` | `Run` key | ✅ |

### Gaming (14)

`disable-game-bar-dvr`, `enable-gpu-scheduling` (HAGS, RequiresReboot),
`enable-game-mode`, `disable-pointer-precision`, `mouse-driver-queue-trim`,
`mmcss-system-responsiveness`, `enable-windowed-game-optimizations`,
`enable-vrr` (`VRROptimizeEnable=1`), `set-games-high-performance-gpu`,
`disable-background-game-captures`, `disable-game-bar-auto-launch`,
`configure-gaming-power-mode-ac`, `restore-default-gpu-preference`,
`enable-auto-hdr`, `gaming-display-refresh-rate-audit` **[Diagnostic]** (diagnóstico).

### Power (6)

`restore-balanced-power-dc`, `disable-usb-selective-suspend-ac`,
`disable-pcie-link-state-power-saving-ac`,
`set-wireless-adapter-max-performance-ac`, `restore-power-plan-after-gaming`,
`remove-unused-custom-power-plans`.

### Storage (21)

`disable-hibernate` (`powercfg /h off`), `optimize-system-drive` (`defrag /O`,
no reversible), `ensure-trim-enabled` (`fsutil`), `retrim-system-ssd`,
`enable-storage-sense`, `storage-sense-temp-cleanup`,
`storage-sense-recycle-bin-policy`, `cleanup-windows-temp`,
`cleanup-delivery-optimization-cache`, `windows-component-store-cleanup` (DISM),
`windows-component-store-resetbase` (DISM `/ResetBase`, **irreversible**),
`free-low-storage-space` **[Diagnostic]**,
`restore-system-managed-pagefile` **[Restore]**, `defragment-hdd-only`,
`cleanup-windows-update-cache`, `cleanup-app-caches`,
`cleanup-prefetch-stale` (`%SystemRoot%\Prefetch\*.pf` +30d),
`cleanup-cbs-logs` (`%SystemRoot%\Logs\CBS\*.log` +30d),
`cleanup-crash-dumps-extended` (LiveKernelReports + MEMORY.DMP + CrashDumps por usuario, +30d),
`cleanup-outlook-cache` (Content.Outlook por usuario, +7d),
`cleanup-browser-code-cache` (Chrome/Edge/Teams solo cachés regenerables, +1d).

> **Nota:** `disk-cleanup-system-files` (`cleanmgr /sagerun`) está **RETIRADA**:
> duplicado exacto de `cleanup-windows-update-cache`
> (mismo `SoftwareDistribution\Download`). Solo existe en
> `OptimizationCatalog.AllLegacy` + `LegacyIds` como alias de compatibilidad.

### Network (13)

`normalize-tcp-autotuning` (`netsh`), `enable-rss`,
`restore-tcp-checksum-offload` **[Restore]**, `restore-udp-checksum-offload` **[Restore]**,
`restore-large-send-offload` **[Restore]**, `configure-interrupt-moderation-for-low-latency`,
`disable-nic-power-saving-ac`, `restore-windows-tcp-congestion-default` **[Restore]**,
`flush-dns-cache` **[Repair]** (`ipconfig /flushdns`), `reset-network-stack-repair`
**[Repair]** (`netsh winsock/tcp reset`, RequiresReboot),
`delivery-optimization-bandwidth-profile`, `disable-nagle-tcp-acks`,
`disable-wifi-background-scan`.

### Startup (6)

`disable-unnecessary-startup-apps`, `disable-heavy-startup-apps`,
`delay-safe-third-party-service-start`, `disable-selected-third-party-background-task`
(`schtasks /Change /DISABLE` real), `restore-sysmain-default` **[Restore]**,
`restore-windows-search-default`.

### System / Maintenance (4)

`create-restore-point-before-optimization-batch` (`SRSetRestorePoint`),
`pending-reboot-maintenance` **[Diagnostic]** (diagnóstico), `stale-crash-dump-cleanup`,
`optimize-startup-recovery-state` **[Diagnostic]** (diagnóstico).

### Troubleshoot (14)

`restart-windows-audio-services` **[Repair]**, `disable-bluetooth-absolute-volume` **[Repair]**,
`fix-microphone-access` **[Repair]**, `restart-desktop-compositor` **[Repair]**, `clear-icon-thumbnail-cache`,
`repair-windows-update` **[Repair]**, `resync-system-clock` **[Repair]** (`w32tm /resync`),
`restart-print-spooler` **[Repair]**, `restart-bluetooth-service` **[Repair]**, `restart-dns-client` **[Repair]**,
`restart-windows-search` **[Repair]**, `restart-windows-explorer` **[Repair]**, `recover-windows-explorer` **[Repair]**.

---

## Proyecciones (spec §5.3) — `CatalogProjections`

El catálogo **no se borra**: `OptimizationCatalog.All` sigue con las 92 entradas y
todo ID sigue resolviendo (motor, Preview, Resolve). Las proyecciones solo filtran
*qué entra en el batch* y *qué es diagnóstico/repair/restore*:

| Proyección | Count | Qué es |
|---|---|---|
| `BatchDefault` | **68** | Rendimiento real: el batch (Optimize/Analyze) aplica solo estas. Excluye repairs, diagnósticos y restores |
| `RepairActions` (`RepairIds`) | **14** | Reparos de troubleshooting (restarts de servicios, flush/reset de red, WU, reloj, micro/explorer/compositor) — fuera del batch |
| `Diagnostics` (`DiagnosticIds`) | **4** | `gaming-display-refresh-rate-audit`, `free-low-storage-space`, `pending-reboot-maintenance`, `optimize-startup-recovery-state` — read-only, `Detect` real |
| `Restores` (`RestoreIds`) | **6** | Devuelven defaults (offloads TCP, congestión, SysMain, pagefile) — fuera del batch |
| **Total `All`** | **92** | Partición exacta y validada por `CatalogProjectionTests` |

Entradas **gated** (no se ofertan nunca en Recommended; requieren modo Expert y, si
mutan, restore point obligatorio): `disable-vbs` y `windows-component-store-resetbase`
(ExpertOnly + RequiresRestorePoint); `reset-network-stack-repair` lleva
RequiresRestorePoint.

---

## Entradas LEGACY (3) — retiradas por duplicado exacto

| ID | Duplica a |
|---|---|
| `optimize-hdd-media-aware` | `optimize-system-drive` (ambas `defrag C: /O`) |
| `set-best-performance-ac` | `maximum-power-plan` (ambas plan Alto rendimiento) |
| `disk-cleanup-system-files` | `cleanup-windows-update-cache` (ambas `SoftwareDistribution\Download`) |

Estas tres existen **solo** en `OptimizationCatalog.AllLegacy` y en
`LegacyIds` para trazabilidad; `IsProductionId(id)` devuelve `false` y nunca se
ofrecen al usuario.

---

## Cómo se garantiza que no haya optimizaciones falsas

1. **Contrato `IOptimization`** — cada entrada implementa `Definition`,
   `Detect`, `Capture`, `ApplyAsync`, `RevertAsync`, `PreviewAsync` y
   `VerifyAsync` (cuando aplica). No hay implementación "solo escribe un marker".
2. **Verificación en vivo** — `VerifyAsync` relee el estado real de Windows
   después de aplicar. `Unknown` **nunca** es éxito → rollback automático.
3. **Tests de catálogo** — `CA-O.Core.Tests` valida el recuento (92), los IDs
   únicos y que cada `Definition` tiene evidencia y riesgo declarados.
4. **Guard de codificación** — `EncodingConsistencyTests` asegura que los
   strings orientados al usuario no contienen mojibake.
5. **Allowlist de comandos** — todo comando externo pasa por `CommandPolicy`
   (rutas absolutas `%SystemRoot%\System32`, argumentos cerrados, sin shell).
