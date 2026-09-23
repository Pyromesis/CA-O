# CA-O Optimization Catalog

Calidad sobre cantidad. **91 optimizaciones verificadas en producción. 4 optimizaciones históricas retiradas del catálogo de producción; no se incluyen en `OptimizationCatalog.All` y quedan en `AllLegacy` solo para trazabilidad.** Cada entrada responde qué cambia, por qué, con qué evidencia, qué riesgo y seguridad afecta, si es reversible y cómo se verifica.

## Summary

Existing optimizations: 18 (histórico)
New optimizations: 50 (histórico)
Total histórico: 68
**Producción verificada: 91** (ninguna entrada parcial; 4 históricas retiradas y excluidas)
**Históricas retiradas: 4** (permanecen en `AllLegacy` solo para trazabilidad)

## Evidence model

- `Official` -> documentacion Microsoft
- `Vendor` -> NVIDIA/AMD/Intel/fabricante
- `Benchmark` -> benchmark reproducible
- `Empirical` -> resultados controlados
- `Heuristic` -> tweak historico sin evidencia solida
- `Unknown` -> dudosa (nunca en Recommended)

## Risk model

`Safe` < `Low` < `Moderate` < `High` < `Critical`. `High/Critical` + `PrivacySecurity` se bloquea con anti-cheat.

## Gaming compatibility

`GameCompatibilityPolicy` con anti-cheats Vanguard/EAC/BattlEye/Faceit/Ricochet. `VBS/HVCI` bloqueado `CAO-GAME-001`.

## Security rules

Nunca se deshabilita silenciosamente Secure Boot/TPM/VBS/HVCI/Defender/firewall. VBS/HVCI requiere Expert + confirmacion.

## Buckets de recomendación

`RecommendationEngine.Classify` asigna cada optimización a un bucket según contexto (Windows 11 para Gaming, SSD, AC, sin throttling, sin anticheat):

- `Recommended` — `Risk` Safe/Low + `ExpectedImpact` distinto de None. Es el trabajo sugerido.
- `Optional` — ya aplicado (`already-applied`/`PendingReboot`), `NotReversible` (mantenimiento), `OneShot` (acción única), `Impact` None, o riesgo Moderate+.
- `Experimental` — evidencia `Heuristic`/`Unknown`, compatibilidad con conflicto potencial, o `ExpertOnly` fuera de modo experto.
- `SecuritySensitive` — `SecurityTradeoff`/`ReducedProtection` (p. ej. `disable-vbs` con anticheat activo).
- `NotApplicable` — precondiciones no cumplidas (p. ej. Gaming sin Windows 11) o ids legacy.

La columna `Batch` indica si la optimización entra en `CatalogProjections.BatchDefault` (67 de 91): los sets `Repair` (14), `Diagnostic` (4) y `Restore` (6) se excluyen del lote automático y viven en sus propias vistas (Solucionar, diagnósticos, restauración).

## Optimization registry — Producción verificada (91)

| Id | Categoria | Impacto | Evidencia | Riesgo | Compatibilidad | Reversible | Flags | Batch |
|---|---|---|---|---|---|---|---|---|
| cleanup-app-caches | Storage | Small | Vendor | Low | Compatible | No | NotReversible | Sí |
| cleanup-browser-code-cache | Storage | Small | Empirical | Low | Compatible | No | NotReversible | Sí |
| cleanup-cbs-logs | Storage | None | Empirical | Low | Compatible | No | NotReversible | Sí |
| cleanup-crash-dumps-extended | Storage | Small | Official | Moderate | Compatible | No | NotReversible | Sí |
| cleanup-delivery-optimization-cache | Storage | Small | Official | Low | Compatible | No | NotReversible | Sí |
| cleanup-outlook-cache | Storage | Small | Empirical | Low | Compatible | No | NotReversible | Sí |
| cleanup-prefetch-stale | Storage | None | Empirical | Low | Compatible | No | NotReversible | Sí |
| cleanup-windows-temp | Storage | Small | Official | Low | Compatible | No | NotReversible | Sí |
| cleanup-windows-update-cache | Storage | Small | Official | Low | Compatible | No | NotReversible | Sí |
| clear-icon-thumbnail-cache | Performance | Tiny | Official | Low | Compatible | No | NotReversible | Sí |
| configure-gaming-power-mode-ac | Gaming | WorkloadDependent | Official | Low | Compatible | Sí | — | Sí |
| configure-interrupt-moderation-for-low-latency | Network | WorkloadDependent | Vendor | Moderate | Conditional | Sí | — | Sí |
| create-restore-point-before-optimization-batch | Storage | None | Official | Safe | Compatible | Sí | — | Sí |
| defragment-hdd-only | Storage | Small | Official | Low | Compatible | No | NotReversible | Sí |
| delay-safe-third-party-service-start | Performance | Small | Empirical | Low | Compatible | Sí | — | Sí |
| delivery-optimization-bandwidth-profile | Network | Tiny | Official | Low | Compatible | Sí | — | Sí |
| disable-background-apps | Performance | Small | Official | Low | Compatible | Sí | — | Sí |
| disable-background-game-captures | Gaming | Small | Vendor | Low | Compatible | Sí | — | Sí |
| disable-bluetooth-absolute-volume | Performance | Tiny | Official | Low | Conditional | Sí | — | No |
| disable-copilot | PrivacySecurity | None | Vendor | Low | Compatible | Sí | — | Sí |
| disable-cortana | PrivacySecurity | None | Official | Low | Compatible | Sí | — | Sí |
| disable-dynamic-tick | Performance | Small | Empirical | Moderate | Conditional | Sí | ExpertOnly, RequiresReboot | Sí |
| disable-game-bar-auto-launch | Gaming | Tiny | Official | Safe | Compatible | Sí | — | Sí |
| disable-game-bar-dvr | Gaming | WorkloadDependent | Vendor | Low | Compatible | Sí | — | Sí |
| disable-heavy-startup-apps | Performance | Small | Empirical | Moderate | Compatible | Sí | — | Sí |
| disable-hibernate | Storage | None | Official | Moderate | Compatible | Sí | — | Sí |
| disable-nagle-tcp-acks | Network | Small | Official | Moderate | Conditional | Sí | RequiresReboot | Sí |
| disable-nic-power-saving-ac | Network | Small | Empirical | Low | Conditional | Sí | — | Sí |
| disable-onedrive-autostart | PrivacySecurity | Tiny | Official | Low | Conditional | Sí | ExpertOnly | Sí |
| disable-pcie-link-state-power-saving-ac | Performance | Small | Official | Low | Compatible | Sí | OneShot | Sí |
| disable-pointer-precision | Gaming | Small | Official | Safe | Compatible | Sí | — | Sí |
| disable-search-indexing | Performance | WorkloadDependent | Empirical | Moderate | Conditional | Sí | RecommendedOnSsd | Sí |
| disable-selected-third-party-background-task | Performance | Small | Empirical | Moderate | Compatible | Sí | — | Sí |
| disable-suggestions | PrivacySecurity | None | Official | Low | Compatible | Sí | — | Sí |
| disable-telemetry | PrivacySecurity | None | Official | Low | Compatible | Sí | — | Sí |
| disable-transparency | Performance | Tiny | Empirical | Safe | Compatible | Sí | — | Sí |
| disable-unnecessary-startup-apps | Performance | Small | Empirical | Low | Compatible | Sí | — | Sí |
| disable-usb-selective-suspend-ac | Performance | WorkloadDependent | Official | Low | Conditional | Sí | OneShot | Sí |
| disable-vbs | Performance | WorkloadDependent | Vendor | Critical | PotentialConflict | Sí | ExpertOnly, SecurityTradeoff, RequiresReboot **[Gated: ExpertOnly+RestorePoint]** | Sí |
| disable-visual-effects | Performance | Tiny | Empirical | Safe | Compatible | Sí | — | Sí |
| disable-widgets | PrivacySecurity | Tiny | Official | Low | Compatible | Sí | — | Sí |
| disable-wifi-background-scan | Network | Small | Official | Moderate | Conditional | Sí | ExpertOnly | Sí |
| enable-auto-hdr | Gaming | None | Official | Safe | Conditional | Sí | — | Sí |
| enable-game-mode | Gaming | WorkloadDependent | Official | Low | Compatible | Sí | — | Sí |
| enable-gpu-scheduling | Gaming | WorkloadDependent | Vendor | Moderate | Conditional | Sí | RequiresReboot | Sí |
| enable-rss | Network | Small | Vendor | Low | Conditional | Sí | — | Sí |
| enable-storage-sense | Storage | Tiny | Official | Low | Compatible | Sí | — | Sí |
| enable-vrr | Gaming | WorkloadDependent | Official | Low | Conditional | Sí | — | Sí |
| enable-windowed-game-optimizations | Gaming | WorkloadDependent | Official | Low | Conditional | Sí | — | Sí |
| ensure-trim-enabled | Storage | Small | Official | Low | Compatible | Sí | — | Sí |
| fix-microphone-access | Performance | Tiny | Official | Low | Compatible | Sí | — | No |
| flush-dns-cache | Network | Tiny | Official | Low | Compatible | Sí | OneShot | No |
| free-low-storage-space | Storage | DiagnosticOnly | Official | Safe | Compatible | Sí | — | No |
| gaming-display-refresh-rate-audit | Gaming | None | Official | Safe | Compatible | Sí | — | No |
| maximum-power-plan | Performance | Small | Official | Low | Compatible | Sí | — | Sí |
| mmcss-system-responsiveness | Gaming | Small | Empirical | Low | Compatible | Sí | — | Sí |
| mouse-driver-queue-trim | Gaming | Tiny | Empirical | Moderate | Conditional | Sí | RequiresReboot | Sí |
| normalize-tcp-autotuning | Network | WorkloadDependent | Official | Low | Conditional | Sí | — | Sí |
| optimize-startup-recovery-state | Storage | DiagnosticOnly | Official | Safe | Compatible | Sí | — | No |
| optimize-system-drive | Storage | None | Official | Low | Compatible | No | NotReversible | Sí |
| pending-reboot-maintenance | Storage | DiagnosticOnly | Official | Safe | Compatible | Sí | — | No |
| recover-windows-explorer | Performance | Small | Official | Low | Compatible | No | NotReversible | No |
| remove-unused-custom-power-plans | Performance | Tiny | Official | Low | Compatible | No | NotReversible | Sí |
| repair-windows-update | Performance | Small | Official | Moderate | Compatible | Sí | — | No |
| reset-network-stack-repair | Network | Small | Official | Moderate | Compatible | No | NotReversible, RequiresReboot | No |
| restart-bluetooth-service | Performance | Small | Official | Low | Compatible | No | NotReversible | No |
| restart-desktop-compositor | Performance | Small | Official | Moderate | Compatible | No | NotReversible | No |
| restart-dns-client | Network | Small | Official | Low | Compatible | No | NotReversible | No |
| restart-print-spooler | Performance | Small | Official | Low | Compatible | No | NotReversible | No |
| restart-windows-audio-services | Performance | Small | Official | Low | Compatible | No | NotReversible | No |
| restart-windows-explorer | Performance | Small | Official | Moderate | Compatible | No | NotReversible | No |
| restart-windows-search | Performance | Small | Official | Low | Compatible | No | NotReversible | No |
| restore-balanced-power-dc | Performance | Small | Official | Low | Compatible | Sí | — | Sí |
| restore-default-gpu-preference | Gaming | Tiny | Official | Low | Compatible | Sí | — | Sí |
| restore-large-send-offload | Network | Tiny | Vendor | Low | Conditional | Sí | — | No |
| restore-sysmain-default | Performance | Small | Official | Low | Compatible | Sí | — | No |
| restore-system-managed-pagefile | Storage | Small | Official | Low | Compatible | Sí | RequiresReboot | No |
| restore-tcp-checksum-offload | Network | Tiny | Vendor | Low | Conditional | Sí | — | No |
| restore-udp-checksum-offload | Network | Tiny | Vendor | Low | Conditional | Sí | — | No |
| restore-windows-search-default | Performance | Small | Official | Low | Compatible | Sí | — | Sí |
| restore-windows-tcp-congestion-default | Network | Small | Official | Low | Compatible | Sí | OneShot | No |
| resync-system-clock | Performance | Tiny | Official | Low | Compatible | No | NotReversible | No |
| retrim-system-ssd | Storage | Small | Official | Low | Compatible | No | NotReversible | Sí |
| set-games-high-performance-gpu | Gaming | WorkloadDependent | Official | Low | Conditional | Sí | — | Sí |
| set-wireless-adapter-max-performance-ac | Performance | Small | Official | Low | Conditional | Sí | OneShot | Sí |
| stale-crash-dump-cleanup | Storage | Tiny | Official | Low | Compatible | No | NotReversible | Sí |
| storage-sense-recycle-bin-policy | Storage | Tiny | Official | Low | Compatible | Sí | — | Sí |
| storage-sense-temp-cleanup | Storage | Tiny | Official | Low | Compatible | Sí | — | Sí |
| windows-component-store-cleanup | Storage | Small | Official | Moderate | Compatible | No | NotReversible | Sí |
| windows-component-store-resetbase | Storage | Moderate | Official | High | Compatible | No | NotReversible, ExpertOnly, OneShot **[Gated: ExpertOnly+RestorePoint]** | Sí |
| zero-menu-delay | Performance | Tiny | Heuristic | Safe | Compatible | Sí | — | Sí |

*4 optimizaciones históricas retiradas del catálogo de producción; permanecen en `AllLegacy` solo para trazabilidad. Ver `OptimizationCatalog.AllLegacy`.*

## Optimizaciones retiradas (históricas; no forman parte del catálogo activo)

- `set-best-performance-ac` (duplicado de `maximum-power-plan`; alias en `RetiredAliases`)
- `optimize-hdd-media-aware` (duplicado de `optimize-system-drive`; alias en `RetiredAliases`)
- `disk-cleanup-system-files` (duplicado exacto de `cleanup-windows-update-cache`; alias en `LegacyIds`)
- `restore-power-plan-after-gaming` (duplicado de `restore-balanced-power-dc`; alias en `LegacyIds`)

## Detailed definitions

### disable-pointer-precision
Ratón 1:1 sin aceleración

### mouse-driver-queue-trim
Cola de ratón 32

### mmcss-system-responsiveness
Prioridad MMCSS al juego

### disable-dynamic-tick
Tick estable bcdedit

### enable-game-mode

#### What changes
`HKCU\Software\Microsoft\GameBar\AllowAutoGameMode=1`

#### Why
Prioriza juegos reduciendo interferencia de fondo.

#### Evidence
Official

#### Applicability
Windows build >= 15063

#### Preconditions
Soportado

#### Current-state detection
Lee registro

#### Apply
Escribe registro

#### Verify
Re-lee registro

#### Rollback
Restaura snapshot

#### Risks
Low

#### Anti-cheat
Safe

#### Benchmark
n/a salvo WorkloadDependent

#### UI behavior
Card con Current->Target, Bucket

### enable-windowed-game-optimizations

#### What changes
`HKCU\Software\Microsoft\DirectX\UserGpuPreferences\DirectXUserGlobalSettings = "SwapEffectUpgradeEnable=1;"`

#### Why
Habilita optimizaciones de DWM para juegos DX10/11 en modo ventana/borderless, reduciendo latencia de presentación y habilitando Auto HDR y VRR para juegos en ventana.

#### Evidence
Official (Microsoft Learn: "Optimizations for windowed games in Windows 11")

#### Applicability
Windows 11 22H2+ (build >= 22621)

#### Preconditions
Windows build >= 22621, DirectX 10/11 games

#### Current-state detection
Lee `HKCU\Software\Microsoft\DirectX\UserGpuPreferences\DirectXUserGlobalSettings` y normaliza `SwapEffectUpgradeEnable=1`

#### Apply
Escribe `SwapEffectUpgradeEnable=1;` en el valor `DirectXUserGlobalSettings`

#### Verify
Re-lee registro y normaliza; confirma `AppliedByCao` si está en 1

#### Rollback
Restaura valor exacto original (incluyendo eliminar si no existía)

#### Risks
Low - solo afecta presentación DWM de juegos windowed

#### Anti-cheat
Safe - no modifica seguridad del kernel

#### Benchmark
WorkloadDependent - beneficio en latencia frame-time para DX10/11 windowed/borderless

#### UI behavior
Card con Current->Target, Bucket Gaming, requiere reiniciar juego

### enable-vrr
Gestiona VRR solo si display compatible

### set-games-high-performance-gpu
Asigna GPU dedicada

### disable-background-game-captures
Separa Game DVR de capturas

### disable-game-bar-auto-launch
Evita inicio automatico

### configure-gaming-power-mode-ac
AC -> Best Performance

### restore-default-gpu-preference
Restaura preferencia

### enable-auto-hdr
Visual, no FPS

### gaming-display-refresh-rate-audit **[Diagnostic]**
Audita Hz

### disable-usb-selective-suspend-ac
Solo Competitive AC (powercfg, acción única)

### disable-pcie-link-state-power-saving-ac
Solo AC PCIe (powercfg, acción única)

### set-wireless-adapter-max-performance-ac
Wi-Fi max rendimiento (powercfg, acción única)

### restore-balanced-power-dc
DC -> Balanced

### remove-unused-custom-power-plans
Detecta huerfanos

### ensure-trim-enabled
TRIM en SSD

### retrim-system-ssd
ReTrim SSD

### optimize-system-drive
defrag /O segun medio

### enable-storage-sense
Storage Sense

### storage-sense-temp-cleanup
Temporales

### storage-sense-recycle-bin-policy
Papelera 7-90 dias

### cleanup-windows-temp
Temporales Windows

### cleanup-delivery-optimization-cache
Cache DO

### defragment-hdd-only
Desfragmenta HDD, jamás SSD

### cleanup-windows-update-cache
Caché WU (para + borra + arranca)

### cleanup-app-caches
Cachés Discord/Spotify/Slack (nunca sesiones)

### cleanup-prefetch-stale
Prefetch *.pf +30d (solo espacio)

### cleanup-cbs-logs
CBS *.log +30d (solo espacio)

### cleanup-crash-dumps-extended
LiveKernelReports + MEMORY.DMP + CrashDumps por usuario (+30d)

### cleanup-outlook-cache
Adjuntos temp Outlook Content.Outlook +7d

### cleanup-browser-code-cache
Cachés regenerables Chrome/Edge/Teams (nunca sesiones)

### windows-component-store-cleanup
DISM StartComponentCleanup

### windows-component-store-resetbase **[Gated: ExpertOnly+RestorePoint]**
DISM ResetBase, irreversible, acción única

### free-low-storage-space **[Diagnostic]**
Umbrales 10/15/20%

### restore-system-managed-pagefile **[Restore]**
System Managed

### enable-rss
RSS

### restore-tcp-checksum-offload **[Restore]**
TCP offload

### restore-udp-checksum-offload **[Restore]**
UDP offload

### restore-large-send-offload **[Restore]**
LSO

### configure-interrupt-moderation-for-low-latency
Solo Competitive

### disable-nic-power-saving-ac
NIC AC

### restore-windows-tcp-congestion-default **[Restore]**
Congestion TCP (netsh, acción única)

### flush-dns-cache **[Repair]**
Limpieza DNS (ipconfig, acción única)

### reset-network-stack-repair **[Repair]**
Winsock/TCP

### delivery-optimization-bandwidth-profile
DO perfil

### disable-nagle-tcp-acks
Sin Nagle: ACK inmediato

### disable-wifi-background-scan
Wi-Fi sin barridos (Expert)

### disable-unnecessary-startup-apps
Startup classification

### disable-heavy-startup-apps
High impact

### delay-safe-third-party-service-start
Delayed auto

### disable-selected-third-party-background-task
Disable tareas

### restore-sysmain-default **[Restore]**
Restaura SysMain

### restore-windows-search-default
Restaura Search

### create-restore-point-before-optimization-batch
SRSetRestorePoint

### pending-reboot-maintenance **[Diagnostic]**
Detecta reboot

### stale-crash-dump-cleanup
Dumps antiguos

### optimize-startup-recovery-state **[Diagnostic]**
Audita boot
