# CA-O Optimization Catalog

Calidad sobre cantidad (spec 131). **19 optimizaciones verificadas en producción. 49 optimizaciones históricas retiradas del catálogo de producción; no se incluyen en `OptimizationCatalog.All` y quedan en `AllLegacy` solo para trazabilidad.** Cada entrada responde qué cambia, por qué, con qué evidencia, qué riesgo y seguridad afecta, si es reversible y cómo se verifica.

## Summary

Existing optimizations: 18 (histórico)
New optimizations: 50 (histórico)
Total histórico: 68
**Producción verificada: 19** (ninguna entrada parcial; 49 históricas retiradas y excluidas por no modificar Windows real)

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

*49 optimizaciones históricas retiradas del catálogo de producción; permanecen en `AllLegacy` solo para trazabilidad. Ver `OptimizationCatalog.AllLegacy`.*

## Optimizaciones retiradas (históricas; no forman parte del catálogo activo)

- enable-vrr
- set-games-high-performance-gpu
- disable-background-game-captures
- disable-game-bar-auto-launch
- configure-gaming-power-mode-ac
- restore-default-gpu-preference
- enable-auto-hdr
- gaming-display-refresh-rate-audit
- set-best-performance-ac
- restore-balanced-power-dc
- disable-usb-selective-suspend-ac
- disable-pcie-link-state-power-saving-ac
- set-wireless-adapter-max-performance-ac
- restore-power-plan-after-gaming
- remove-unused-custom-power-plans
- ensure-trim-enabled
- retrim-system-ssd
- optimize-hdd-media-aware
- enable-storage-sense
- storage-sense-temp-cleanup
- storage-sense-recycle-bin-policy
- cleanup-windows-temp
- cleanup-delivery-optimization-cache
- windows-component-store-cleanup
- windows-component-store-resetbase
- disk-cleanup-system-files
- free-low-storage-space
- restore-system-managed-pagefile
- enable-rss
- restore-tcp-checksum-offload
- restore-udp-checksum-offload
- restore-large-send-offload
- configure-interrupt-moderation-for-low-latency
- disable-nic-power-saving-ac
- restore-windows-tcp-congestion-default
- flush-dns-cache
- reset-network-stack-repair
- delivery-optimization-bandwidth-profile
- disable-unnecessary-startup-apps
- disable-heavy-startup-apps
- delay-safe-third-party-service-start
- disable-selected-third-party-background-task
- restore-sysmain-default
- restore-windows-search-default
- create-restore-point-before-optimization-batch
- pending-reboot-maintenance
- stale-crash-dump-cleanup
- optimize-startup-recovery-state

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

