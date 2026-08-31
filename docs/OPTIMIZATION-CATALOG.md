# CA-O Optimization Catalog

Calidad sobre cantidad (spec 131). **19 optimizaciones verificadas en producción (15 IMPLEMENTED + 4 PARTIALLY_IMPLEMENTED con Verify tolerante). 49 STUBs `HKCU\Software\CA-O` retirados del catálogo de producción (ver `OptimizationCatalog.All` vs `AllLegacy` 68 histórico).** Cada entrada responde que cambia, por que, con que evidencia, que riesgo y seguridad afecta, si es reversible y como se verifica.

## Summary

Existing optimizations: 18 (histórico)
New optimizations: 50 (histórico)
Total histórico: 68
**Producción verificada: 19** (15 IMPLEMENTED + 4 PARTIALLY con Verify tolerante, DISM ResetBase y 48 STUBs excluidos por no modificar Windows real)

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

## Optimization registry — Producción verificada (19)

| Id | Categoria | Impacto | Evidencia | Riesgo | Compatibilidad | Reversible | Flags |
|---|---|---|---|---|---|---|---|
| disable-background-apps | Performance | Small | Official | Low | Compatible | Si | — |
| disable-copilot | PrivacySecurity | None | Official | Low | Compatible | Si | — |
| disable-cortana | PrivacySecurity | None | Official | Low | Compatible | Si | — |
| disable-game-bar-dvr | Gaming | WorkloadDependent | Vendor | Low | Compatible | Si | — |
| disable-suggestions | PrivacySecurity | None | Official | Low | Compatible | Si | — |
| disable-telemetry | PrivacySecurity | None | Official | Low | Compatible | Si | — |
| disable-transparency | Performance | Tiny | Empirical | Safe | Compatible | Si | — |
| disable-visual-effects | Performance | Tiny | Empirical | Safe | Compatible | Si | — |
| disable-widgets | PrivacySecurity | Tiny | Official | Low | Compatible | Si | — |
| enable-game-mode | Gaming | WorkloadDependent | Official | Low | Compatible | Si | — |
| enable-gpu-scheduling | Gaming | WorkloadDependent | Vendor | Moderate | Conditional | Si | RequiresReboot |
| zero-menu-delay | Performance | Tiny | Heuristic | Safe | Compatible | Si | — |
| disable-onedrive-autostart | PrivacySecurity | Tiny | Official | Low | Conditional | Si | ExpertOnly |
| disable-search-indexing | Performance | WorkloadDependent | Empirical | Moderate | Conditional | Si | RecommendedOnSsd |
| maximum-power-plan | Performance | Small | Official | Low | Compatible | Si | — |
| disable-hibernate | Storage | None | Official | Moderate | Compatible | Si | — |
| disable-vbs | Performance | WorkloadDependent | Vendor | Critical | PotentialConflict | Si | ExpertOnly, SecurityTradeoff, RequiresReboot |
| normalize-tcp-autotuning | Network | WorkloadDependent | Official | Low | Conditional | Si | — |
| optimize-system-drive | Storage | None | Official | Low | Compatible | No | NotReversible |

*49 optimizaciones STUB `HKCU\Software\CA-O` retiradas del catálogo de producción; permanecen en `AllLegacy` solo para auditoría. Ver `OptimizationCatalog.AllLegacy`.*

## Optimizaciones retiradas (STUB - no verificables)

> `enable-windowed-game-optimizations`, `enable-vrr`, `set-games-high-performance-gpu`, `disable-background-game-captures`, `disable-game-bar-auto-launch`, `configure-gaming-power-mode-ac`, `restore-default-gpu-preference`, `enable-auto-hdr`, `gaming-display-refresh-rate-audit`, `set-best-performance-ac`, `restore-balanced-power-dc`, `disable-usb-selective-suspend-ac`, `disable-pcie-link-state-power-saving-ac`, `set-wireless-adapter-max-performance-ac`, `restore-power-plan-after-gaming`, `remove-unused-custom-power-plans`, `ensure-trim-enabled`, `retrim-system-ssd`, `optimize-hdd-media-aware`, `enable-storage-sense`, `storage-sense-temp-cleanup`, `storage-sense-recycle-bin-policy`, `cleanup-windows-temp`, `cleanup-delivery-optimization-cache`, `windows-component-store-cleanup`, `windows-component-store-resetbase`, `disk-cleanup-system-files`, `free-low-storage-space`, `restore-system-managed-pagefile`, `enable-rss`, `restore-tcp-checksum-offload`, `restore-udp-checksum-offload`, `restore-large-send-offload`, `configure-interrupt-moderation-for-low-latency`, `disable-nic-power-saving-ac`, `restore-windows-tcp-congestion-default`, `flush-dns-cache`, `reset-network-stack-repair`, `delivery-optimization-bandwidth-profile`, `disable-unnecessary-startup-apps`, `disable-heavy-startup-apps`, `delay-safe-third-party-service-start`, `disable-selected-third-party-background-task`, `restore-sysmain-default`, `restore-windows-search-default`, `create-restore-point-before-optimization-batch`, `pending-reboot-maintenance`, `stale-crash-dump-cleanup`, `optimize-startup-recovery-state` — todas escribían `HKCU\Software\CA-O\<id>` sin modificar Windows real.
| disable-search-indexing | Performance | WorkloadDependent | Empirical | Moderate | Conditional | Si | RecommendedOnSsd |
| disable-background-apps | Performance | Small | Official | Low | Compatible | Si | — |
| zero-menu-delay | Performance | Tiny | Heuristic | Safe | Compatible | Si | — |
| disable-transparency | Performance | Tiny | Empirical | Safe | Compatible | Si | — |
| disable-telemetry | PrivacySecurity | None | Official | Low | Compatible | Si | — |
| disable-cortana | PrivacySecurity | None | Official | Low | Compatible | Si | — |
| disable-widgets | PrivacySecurity | Tiny | Official | Low | Compatible | Si | — |
| disable-copilot | PrivacySecurity | None | Vendor | Low | Compatible | Si | — |
| disable-suggestions | PrivacySecurity | None | Official | Low | Compatible | Si | — |
| disable-onedrive-autostart | PrivacySecurity | Tiny | Official | Low | Conditional | Si | ExpertOnly |
| disable-game-bar-dvr | Gaming | WorkloadDependent | Vendor | Low | Compatible | Si | — |
| enable-gpu-scheduling | Gaming | WorkloadDependent | Vendor | Moderate | Conditional | Si | RequiresReboot |
| normalize-tcp-autotuning | Network | WorkloadDependent | Official | Low | Conditional | Si | — |
| disable-hibernate | Storage | None | Official | Moderate | Compatible | Si | — |
| optimize-system-drive | Storage | None | Official | Low | Compatible | No | NotReversible |

## Detailed definitions

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
Similar para DX10/11 windowed

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

### gaming-display-refresh-rate-audit
Audita Hz

### set-best-performance-ac
AC -> Best Performance

### restore-balanced-power-dc
DC -> Balanced

### disable-usb-selective-suspend-ac
Solo Competitive AC

### disable-pcie-link-state-power-saving-ac
Solo AC PCIe

### set-wireless-adapter-max-performance-ac
Wi-Fi max rendimiento

### restore-power-plan-after-gaming
Guarda modo previo

### remove-unused-custom-power-plans
Detecta huerfanos

### ensure-trim-enabled
TRIM en SSD

### retrim-system-ssd
ReTrim SSD

### optimize-hdd-media-aware
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

### windows-component-store-cleanup
DISM StartComponentCleanup

### windows-component-store-resetbase
DISM ResetBase, irreversible

### disk-cleanup-system-files
cleanmgr

### free-low-storage-space
Umbrales 10/15/20%

### restore-system-managed-pagefile
System Managed

### enable-rss
RSS

### restore-tcp-checksum-offload
TCP offload

### restore-udp-checksum-offload
UDP offload

### restore-large-send-offload
LSO

### configure-interrupt-moderation-for-low-latency
Solo Competitive

### disable-nic-power-saving-ac
NIC AC

### restore-windows-tcp-congestion-default
Congestion TCP

### flush-dns-cache
Maintenance tiny

### reset-network-stack-repair
Winsock/TCP

### delivery-optimization-bandwidth-profile
DO perfil

### disable-unnecessary-startup-apps
Startup classification

### disable-heavy-startup-apps
High impact

### delay-safe-third-party-service-start
Delayed auto

### disable-selected-third-party-background-task
Disable tareas

### restore-sysmain-default
Restaura SysMain

### restore-windows-search-default
Restaura Search

### create-restore-point-before-optimization-batch
SRSetRestorePoint

### pending-reboot-maintenance
Detecta reboot

### stale-crash-dump-cleanup
Dumps antiguos

### optimize-startup-recovery-state
Audita boot

