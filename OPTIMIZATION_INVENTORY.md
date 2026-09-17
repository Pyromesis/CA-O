# CA-O Optimization Inventory — 88 production optimizations

> **Estado verificado (revisado contra `OptimizationCatalog.All`):**
> las **88** optimizaciones del catálogo están en **PRODUCTION**.
> No queda ningún STUB. Cada una muta Windows de verdad (Registry API,
> `powercfg`, `netsh`, `schtasks`, `defrag`, `fsutil`, `DISM`, SCM) bajo el
> flujo transaccional `PRECHECK → SNAPSHOT → APPLY → VERIFY → COMMIT`.
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
| PRODUCTION | 88 |
| LEGACY (retirada por duplicada) | 2 |
| **STUB / falsa** | **0** |

> **Nota histórica:** una versión anterior de este archivo listaba 48 entradas
> como `STUB` ("solo escribe a `HKCU\Software\CA-O\<id>`"). Eso **ya no es
> cierto**: aquellas entradas se implementaron de forma real y se promocionaron
> a producción. Una búsqueda de `Software\CA-O` en `src/CA-O.Core/Optimizations`
> devuelve un único resultado legítimo: el índice de revert de
> `disable-selected-third-party-background-task` (la mutación real vive en el
> Programador de tareas; la clave solo recuerda *qué* tareas reactivar).

---

## Catálogo por categoría (88 producción)

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
`enable-auto-hdr`, `gaming-display-refresh-rate-audit` (diagnóstico).

### Power (6)

`restore-balanced-power-dc`, `disable-usb-selective-suspend-ac`,
`disable-pcie-link-state-power-saving-ac`,
`set-wireless-adapter-max-performance-ac`, `restore-power-plan-after-gaming`,
`remove-unused-custom-power-plans`.

### Storage (17)

`disable-hibernate` (`powercfg /h off`), `optimize-system-drive` (`defrag /O`,
no reversible), `ensure-trim-enabled` (`fsutil`), `retrim-system-ssd`,
`enable-storage-sense`, `storage-sense-temp-cleanup`,
`storage-sense-recycle-bin-policy`, `cleanup-windows-temp`,
`cleanup-delivery-optimization-cache`, `windows-component-store-cleanup` (DISM),
`windows-component-store-resetbase` (DISM `/ResetBase`, **irreversible**),
`disk-cleanup-system-files`, `free-low-storage-space`,
`restore-system-managed-pagefile`, `defragment-hdd-only`,
`cleanup-windows-update-cache`, `cleanup-app-caches`.

### Network (13)

`normalize-tcp-autotuning` (`netsh`), `enable-rss`,
`restore-tcp-checksum-offload`, `restore-udp-checksum-offload`,
`restore-large-send-offload`, `configure-interrupt-moderation-for-low-latency`,
`disable-nic-power-saving-ac`, `restore-windows-tcp-congestion-default`,
`flush-dns-cache` (`ipconfig /flushdns`), `reset-network-stack-repair`
(`netsh winsock/tcp reset`, RequiresReboot),
`delivery-optimization-bandwidth-profile`, `disable-nagle-tcp-acks`,
`disable-wifi-background-scan`.

### Startup (6)

`disable-unnecessary-startup-apps`, `disable-heavy-startup-apps`,
`delay-safe-third-party-service-start`, `disable-selected-third-party-background-task`
(`schtasks /Change /DISABLE` real), `restore-sysmain-default`,
`restore-windows-search-default`.

### System / Maintenance (4)

`create-restore-point-before-optimization-batch` (`SRSetRestorePoint`),
`pending-reboot-maintenance` (diagnóstico), `stale-crash-dump-cleanup`,
`optimize-startup-recovery-state` (diagnóstico).

### Troubleshoot (14)

`restart-windows-audio-services`, `disable-bluetooth-absolute-volume`,
`fix-microphone-access`, `restart-desktop-compositor`, `clear-icon-thumbnail-cache`,
`repair-windows-update`, `resync-system-clock` (`w32tm /resync`),
`restart-print-spooler`, `restart-bluetooth-service`, `restart-dns-client`,
`restart-windows-search`, `restart-windows-explorer`, `recover-windows-explorer`.

---

## Entradas LEGACY (2) — retiradas por duplicado exacto

| ID | Duplica a |
|---|---|
| `optimize-hdd-media-aware` | `optimize-system-drive` (ambas `defrag C: /O`) |
| `set-best-performance-ac` | `maximum-power-plan` (ambas plan Alto rendimiento) |

Estas dos existen **solo** en `OptimizationCatalog.AllLegacy` y en
`LegacyIds` para trazabilidad; `IsProductionId(id)` devuelve `false` y nunca se
ofrecen al usuario.

---

## Cómo se garantiza que no haya optimizaciones falsas

1. **Contrato `IOptimization`** — cada entrada implementa `Definition`,
   `Detect`, `Capture`, `ApplyAsync`, `RevertAsync`, `PreviewAsync` y
   `VerifyAsync` (cuando aplica). No hay implementación "solo escribe un marker".
2. **Verificación en vivo** — `VerifyAsync` relee el estado real de Windows
   después de aplicar. `Unknown` **nunca** es éxito → rollback automático.
3. **Tests de catálogo** — `CA-O.Core.Tests` valida el recuento (88), los IDs
   únicos y que cada `Definition` tiene evidencia y riesgo declarados.
4. **Guard de codificación** — `EncodingConsistencyTests` asegura que los
   strings orientados al usuario no contienen mojibake.
5. **Allowlist de comandos** — todo comando externo pasa por `CommandPolicy`
   (rutas absolutas `%SystemRoot%\System32`, argumentos cerrados, sin shell).
