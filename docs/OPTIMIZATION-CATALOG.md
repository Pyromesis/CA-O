# CA-O Optimization Catalog

Calidad sobre cantidad (spec 131). **68 optimizaciones catalogadas (18 existentes + 50 nuevas)**. Cada entrada responde que cambia, por que, con que evidencia, que riesgo y seguridad afecta, si es reversible y como se verifica.

## Summary

Existing optimizations: 18
New optimizations: 50
Total: 68

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

## Optimization registry

| Id | Categoria | Impacto | Evidencia | Riesgo | Compatibilidad | Reversible | Flags |
|---|---|---|---|---|---|---|---|
| enable-game-mode | Gaming | WorkloadDependent | Official | Low | Compatible | Si | — |
| enable-windowed-game-optimizations | Gaming | WorkloadDependent | Official | Low | Compatible | Si | — |
| enable-vrr | Gaming | WorkloadDependent | Vendor | Low | Conditional | Si | — |
| set-games-high-performance-gpu | Gaming | WorkloadDependent | Official | Low | Conditional | Si | — |
| disable-background-game-captures | Gaming | Small | Vendor | Low | Compatible | Si | — |
| disable-game-bar-auto-launch | Gaming | Tiny | Official | Safe | Compatible | Si | — |
| configure-gaming-power-mode-ac | Gaming | Small | Official | Low | Compatible | Si | — |
| restore-default-gpu-preference | Gaming | Tiny | Official | Low | Compatible | Si | — |
| enable-auto-hdr | Gaming | None | Vendor | Low | Conditional | Si | — |
| gaming-display-refresh-rate-audit | Gaming | None | Official | Safe | Compatible | Si | — |
| set-best-performance-ac | Performance | Small | Official | Low | Compatible | Si | — |
| restore-balanced-power-dc | Performance | Small | Official | Low | Compatible | Si | — |
| disable-usb-selective-suspend-ac | Performance | WorkloadDependent | Official | Moderate | Conditional | Si | — |
| disable-pcie-link-state-power-saving-ac | Performance | WorkloadDependent | Official | Moderate | Conditional | Si | — |
| set-wireless-adapter-max-performance-ac | Performance | Small | Vendor | Low | Conditional | Si | — |
| restore-power-plan-after-gaming | Performance | Small | Official | Low | Compatible | Si | — |
| remove-unused-custom-power-plans | Performance | None | Official | Low | Compatible | Si | — |
| ensure-trim-enabled | Storage | Small | Official | Low | Compatible | Si | — |
| retrim-system-ssd | Storage | Small | Official | Low | Compatible | Si | — |
| optimize-hdd-media-aware | Storage | Small | Official | Low | Compatible | Si | — |
| enable-storage-sense | Storage | Tiny | Official | Low | Compatible | Si | — |
| storage-sense-temp-cleanup | Storage | Tiny | Official | Low | Compatible | Si | — |
| storage-sense-recycle-bin-policy | Storage | Tiny | Official | Low | Compatible | Si | — |
| cleanup-windows-temp | Storage | Tiny | Heuristic | Low | Compatible | Si | — |
| cleanup-delivery-optimization-cache | Storage | Small | Official | Low | Compatible | Si | — |
| windows-component-store-cleanup | Storage | Small | Official | Moderate | Compatible | Si | — |
| windows-component-store-resetbase | Storage | Moderate | Official | High | Compatible | No | NotReversible, ExpertOnly |
| disk-cleanup-system-files | Storage | Small | Official | Low | Compatible | Si | — |
| free-low-storage-space | Storage | Small | Official | Low | Compatible | Si | — |
| restore-system-managed-pagefile | Storage | Small | Official | Low | Compatible | Si | — |
| enable-rss | Network | Small | Vendor | Low | Conditional | Si | — |
| restore-tcp-checksum-offload | Network | Small | Vendor | Low | Conditional | Si | — |
| restore-udp-checksum-offload | Network | Small | Vendor | Low | Conditional | Si | — |
| restore-large-send-offload | Network | Small | Vendor | Low | Conditional | Si | — |
| configure-interrupt-moderation-for-low-latency | Network | WorkloadDependent | Vendor | Moderate | Conditional | Si | — |
| disable-nic-power-saving-ac | Network | Small | Vendor | Low | Conditional | Si | — |
| restore-windows-tcp-congestion-default | Network | Small | Official | Low | Compatible | Si | — |
| flush-dns-cache | Network | Tiny | Official | Low | Compatible | Si | — |
| reset-network-stack-repair | Network | Small | Official | Moderate | Compatible | Si | — |
| delivery-optimization-bandwidth-profile | Network | Small | Official | Low | Compatible | Si | — |
| disable-unnecessary-startup-apps | Performance | Small | Official | Low | Compatible | Si | — |
| disable-heavy-startup-apps | Performance | Small | Official | Moderate | Compatible | Si | — |
| delay-safe-third-party-service-start | Performance | Small | Official | Low | Compatible | Si | — |
| disable-selected-third-party-background-task | Performance | Small | Official | Low | Compatible | Si | — |
| restore-sysmain-default | Performance | Small | Official | Low | Compatible | Si | — |
| restore-windows-search-default | Performance | Small | Official | Low | Compatible | Si | — |
| create-restore-point-before-optimization-batch | Storage | None | Official | Low | Compatible | Si | — |
| pending-reboot-maintenance | Storage | None | Official | Low | Compatible | Si | — |
| stale-crash-dump-cleanup | Storage | Tiny | Official | Low | Compatible | Si | — |
| optimize-startup-recovery-state | Storage | None | Official | Low | Compatible | Si | — |
| disable-vbs | Performance | WorkloadDependent | Vendor | Critical | PotentialConflict | Si | ExpertOnly, SecurityTradeoff, RequiresReboot |
| maximum-power-plan | Performance | Small | Official | Low | Compatible | Si | — |
| disable-visual-effects | Performance | Tiny | Empirical | Safe | Compatible | Si | — |
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

