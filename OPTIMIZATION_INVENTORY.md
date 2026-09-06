# CA-O Optimization Inventory - 68 Total

## Status Legend
- **PRODUCTION** - Fully implemented with real Windows APIs, verified, tested, documented
- **PARTIAL** - Real implementation but needs Verify robustness (marked experimental)
- **STUB** - Only writes to HKCU\Software\CA-O\<id>, doesn't modify Windows
- **MISSING** - Not yet implemented

---

## Phase 1: Core Performance & Privacy (19) - Already Production/Partial

| # | ID | Category | Type | Status | Implementation |
|---|-----|----------|------|--------|----------------|
| 1 | disable-background-apps | Performance | ConfigurationOptimization | PRODUCTION | Registry (HKCU\BackgroundAccessApplications) |
| 2 | disable-copilot | PrivacySecurity | ConfigurationOptimization | PRODUCTION | Registry (HKCU\Policies\WindowsCopilot) |
| 3 | disable-cortana | PrivacySecurity | ConfigurationOptimization | PRODUCTION | Registry (HKLM\Policies\Windows Search) |
| 4 | disable-game-bar-dvr | Gaming | ConfigurationOptimization | PRODUCTION | Registry (2 keys) |
| 5 | disable-suggestions | PrivacySecurity | ConfigurationOptimization | PRODUCTION | Registry (HKCU\ContentDeliveryManager) |
| 6 | disable-telemetry | PrivacySecurity | ConfigurationOptimization | PRODUCTION | Registry (HKLM\Policies\DataCollection) |
| 7 | disable-transparency | Performance | ConfigurationOptimization | PRODUCTION | Registry (HKCU\Themes\Personalize) |
| 8 | disable-visual-effects | Performance | ConfigurationOptimization | PRODUCTION | Registry (HKCU\Explorer\VisualEffects) |
| 9 | disable-widgets | PrivacySecurity | ConfigurationOptimization | PRODUCTION | Registry (HKLM\Policies\Dsh) |
| 10 | enable-game-mode | Gaming | ConfigurationOptimization | PRODUCTION | Registry (HKCU\GameBar) |
| 11 | enable-gpu-scheduling | Gaming | ConfigurationOptimization | PRODUCTION | Registry (HKLM\GraphicsDrivers) |
| 12 | zero-menu-delay | Performance | ConfigurationOptimization | PRODUCTION | Registry (HKCU\Control Panel\Desktop) |
| 13 | disable-onedrive-autostart | PrivacySecurity | ConfigurationOptimization | PRODUCTION | IOptimization direct (HKCU\Run) |
| 14 | disable-search-indexing | Performance | ConfigurationOptimization | PRODUCTION | IOptimization direct (WSearch service) |
| 15 | maximum-power-plan | Performance | ConfigurationOptimization | PRODUCTION | IOptimization direct (powercfg) |
| 16 | disable-hibernate | Storage | ConfigurationOptimization | PARTIAL | IOptimization direct (powercfg) |
| 17 | disable-vbs | Performance | SecuritySensitiveOptimization | PARTIAL | IOptimization direct (bcdedit) |
| 18 | normalize-tcp-autotuning | Network | ConfigurationOptimization | PARTIAL | IOptimization direct (netsh) |
| 19 | optimize-system-drive | Storage | MaintenanceAction | PARTIAL | IOptimization direct (defrag /O) |

---

## Phase 2: Gaming (20-35) - 16 optimizations, ALL STUBS

| # | ID | Category | Type | Status | Notes |
|---|-----|----------|------|--------|-------|
| 20 | enable-windowed-game-optimizations | Gaming | ConfigurationOptimization | PRODUCTION | Real implementation: HKCU\Software\Microsoft\DirectX\UserGpuPreferences\DirectXUserGlobalSettings |
| 21 | enable-vrr | Gaming | ConfigurationOptimization | STUB | HKCU\Software\CA-O\enable-vrr |
| 22 | set-games-high-performance-gpu | Gaming | ConfigurationOptimization | STUB | HKCU\Software\CA-O\set-games-high-performance-gpu |
| 23 | disable-background-game-captures | Gaming | ConfigurationOptimization | STUB | HKCU\Software\CA-O\disable-background-game-captures |
| 24 | disable-game-bar-auto-launch | Gaming | ConfigurationOptimization | STUB | HKCU\Software\CA-O\disable-game-bar-auto-launch |
| 25 | configure-gaming-power-mode-ac | Gaming | ConfigurationOptimization | STUB | HKCU\Software\CA-O\configure-gaming-power-mode-ac |
| 26 | restore-default-gpu-preference | Gaming | RestoreAction | STUB | HKCU\Software\CA-O\restore-default-gpu-preference |
| 27 | enable-auto-hdr | Gaming | GamingDisplayFeature | STUB | HKCU\Software\CA-O\enable-auto-hdr |
| 28 | gaming-display-refresh-rate-audit | Gaming | DiagnosticAction | STUB | HKCU\Software\CA-O\gaming-display-refresh-rate-audit |
| 29 | set-best-performance-ac | Gaming | ConfigurationOptimization | STUB | HKCU\Software\CA-O\set-best-performance-ac |
| 30 | restore-balanced-power-dc | Power | ConfigurationOptimization | STUB | HKCU\Software\CA-O\restore-balanced-power-dc |
| 31 | disable-usb-selective-suspend-ac | Power | ConfigurationOptimization | STUB | HKCU\Software\CA-O\disable-usb-selective-suspend-ac |
| 32 | disable-pcie-link-state-power-saving-ac | Power | ConfigurationOptimization | STUB | HKCU\Software\CA-O\disable-pcie-link-state-power-saving-ac |
| 33 | set-wireless-adapter-max-performance-ac | Power | ConfigurationOptimization | STUB | HKCU\Software\CA-O\set-wireless-adapter-max-performance-ac |
| 34 | restore-power-plan-after-gaming | Power | ConfigurationOptimization | STUB | HKCU\Software\CA-O\restore-power-plan-after-gaming |
| 35 | remove-unused-custom-power-plans | Power | MaintenanceAction | STUB | HKCU\Software\CA-O\remove-unused-custom-power-plans |

---

## Phase 3: Storage (36-48) - 13 optimizations, ALL STUBS

| # | ID | Category | Type | Status | Notes |
|---|-----|----------|------|--------|-------|
| 36 | ensure-trim-enabled | Storage | ConfigurationOptimization | STUB | HKCU\Software\CA-O\ensure-trim-enabled |
| 37 | retrim-system-ssd | Storage | MaintenanceAction | STUB | HKCU\Software\CA-O\retrim-system-ssd |
| 38 | optimize-hdd-media-aware | Storage | MaintenanceAction | STUB | HKCU\Software\CA-O\optimize-hdd-media-aware |
| 39 | enable-storage-sense | Storage | ConfigurationOptimization | STUB | HKCU\Software\CA-O\enable-storage-sense |
| 40 | storage-sense-temp-cleanup | Storage | ConfigurationOptimization | STUB | HKCU\Software\CA-O\storage-sense-temp-cleanup |
| 41 | storage-sense-recycle-bin-policy | Storage | ConfigurationOptimization | STUB | HKCU\Software\CA-O\storage-sense-recycle-bin-policy |
| 42 | cleanup-windows-temp | Storage | MaintenanceAction | STUB | HKCU\Software\CA-O\cleanup-windows-temp |
| 43 | cleanup-delivery-optimization-cache | Storage | MaintenanceAction | STUB | HKCU\Software\CA-O\cleanup-delivery-optimization-cache |
| 44 | windows-component-store-cleanup | Storage | MaintenanceAction | STUB | HKCU\Software\CA-O\windows-component-store-cleanup |
| 45 | windows-component-store-resetbase | Storage | ConfigurationOptimization | STUB | HKCU\Software\CA-O\windows-component-store-resetbase (IRREVERSIBLE) |
| 46 | disk-cleanup-system-files | Storage | MaintenanceAction | STUB | HKCU\Software\CA-O\disk-cleanup-system-files |
| 47 | free-low-storage-space | Storage | MaintenanceAction | STUB | HKCU\Software\CA-O\free-low-storage-space |
| 48 | restore-system-managed-pagefile | Storage | ConfigurationOptimization | STUB | HKCU\Software\CA-O\restore-system-managed-pagefile |

---

## Phase 4: Networking (49-58) - 10 optimizations, ALL STUBS

| # | ID | Category | Type | Status | Notes |
|---|-----|----------|------|--------|-------|
| 49 | enable-rss | Network | ConfigurationOptimization | STUB | HKCU\Software\CA-O\enable-rss |
| 50 | restore-tcp-checksum-offload | Network | ConfigurationOptimization | STUB | HKCU\Software\CA-O\restore-tcp-checksum-offload |
| 51 | restore-udp-checksum-offload | Network | ConfigurationOptimization | STUB | HKCU\Software\CA-O\restore-udp-checksum-offload |
| 52 | restore-large-send-offload | Network | ConfigurationOptimization | STUB | HKCU\Software\CA-O\restore-large-send-offload |
| 53 | configure-interrupt-moderation-for-low-latency | Network | ConfigurationOptimization | STUB | HKCU\Software\CA-O\configure-interrupt-moderation-for-low-latency |
| 54 | disable-nic-power-saving-ac | Network | ConfigurationOptimization | STUB | HKCU\Software\CA-O\disable-nic-power-saving-ac |
| 55 | restore-windows-tcp-congestion-default | Network | ConfigurationOptimization | STUB | HKCU\Software\CA-O\restore-windows-tcp-congestion-default |
| 56 | flush-dns-cache | Network | MaintenanceAction | STUB | HKCU\Software\CA-O\flush-dns-cache |
| 57 | reset-network-stack-repair | Network | RepairAction | STUB | HKCU\Software\CA-O\reset-network-stack-repair |
| 58 | delivery-optimization-bandwidth-profile | Network | ConfigurationOptimization | STUB | HKCU\Software\CA-O\delivery-optimization-bandwidth-profile |

---

## Phase 5: Startup/Services (59-64) - 6 optimizations, ALL STUBS

| # | ID | Category | Type | Status | Notes |
|---|-----|----------|------|--------|-------|
| 59 | disable-unnecessary-startup-apps | Startup | ConfigurationOptimization | STUB | HKCU\Software\CA-O\disable-unnecessary-startup-apps |
| 60 | disable-heavy-startup-apps | Startup | ConfigurationOptimization | STUB | HKCU\Software\CA-O\disable-heavy-startup-apps |
| 61 | delay-safe-third-party-service-start | Startup | ConfigurationOptimization | STUB | HKCU\Software\CA-O\delay-safe-third-party-service-start |
| 62 | disable-selected-third-party-background-task | Startup | ConfigurationOptimization | STUB | HKCU\Software\CA-O\disable-selected-third-party-background-task |
| 63 | restore-sysmain-default | Startup | RestoreAction | STUB | HKCU\Software\CA-O\restore-sysmain-default |
| 64 | restore-windows-search-default | Startup | RestoreAction | STUB | HKCU\Software\CA-O\restore-windows-search-default |

---

## Phase 6: Safety/Diagnostics (65-68) - 4 optimizations, ALL STUBS

| # | ID | Category | Type | Status | Notes |
|---|-----|----------|------|--------|-------|
| 65 | create-restore-point-before-optimization-batch | System | SafetyInfrastructure | STUB | HKCU\Software\CA-O\create-restore-point-before-optimization-batch |
| 66 | pending-reboot-maintenance | System | DiagnosticAction | STUB | HKCU\Software\CA-O\pending-reboot-maintenance |
| 67 | stale-crash-dump-cleanup | System | MaintenanceAction | STUB | HKCU\Software\CA-O\stale-crash-dump-cleanup |
| 68 | optimize-startup-recovery-state | System | DiagnosticAction | STUB | HKCU\Software\CA-O\optimize-startup-recovery-state |

---

## Summary

| Status | Count |
|--------|-------|
| PRODUCTION | 16 |
| PARTIAL | 4 |
| STUB | 48 |
| **Total** | **68** |

---

## Implementation Order (per spec)

### FASE 2 — Gaming (20-35)
1. enable-windowed-game-optimizations
2. enable-vrr
3. set-games-high-performance-gpu
4. disable-background-game-captures
5. disable-game-bar-auto-launch
6. configure-gaming-power-mode-ac
7. restore-default-gpu-preference
8. enable-auto-hdr
9. gaming-display-refresh-rate-audit
10. set-best-performance-ac
11. restore-balanced-power-dc
12. disable-usb-selective-suspend-ac
13. disable-pcie-link-state-power-saving-ac
14. set-wireless-adapter-max-performance-ac
15. restore-power-plan-after-gaming
16. remove-unused-custom-power-plans

### FASE 3 — Storage (36-48)
17. ensure-trim-enabled
18. retrim-system-ssd
19. optimize-hdd-media-aware
20. enable-storage-sense
21. storage-sense-temp-cleanup
22. storage-sense-recycle-bin-policy
23. cleanup-windows-temp
24. cleanup-delivery-optimization-cache
25. windows-component-store-cleanup
26. windows-component-store-resetbase (IRREVERSIBLE)
27. disk-cleanup-system-files
28. free-low-storage-space
29. restore-system-managed-pagefile

### FASE 4 — Networking (49-58)
30. enable-rss
31. restore-tcp-checksum-offload
32. restore-udp-checksum-offload
33. restore-large-send-offload
34. configure-interrupt-moderation-for-low-latency
35. disable-nic-power-saving-ac
36. restore-windows-tcp-congestion-default
37. flush-dns-cache
38. reset-network-stack-repair
39. delivery-optimization-bandwidth-profile

### FASE 5 — Startup/Services (59-64)
40. disable-unnecessary-startup-apps
41. disable-heavy-startup-apps
42. delay-safe-third-party-service-start
43. disable-selected-third-party-background-task
44. restore-sysmain-default
45. restore-windows-search-default

### FASE 6 — Safety/Diagnostics (65-68)
46. create-restore-point-before-optimization-batch
47. pending-reboot-maintenance
48. stale-crash-dump-cleanup
49. optimize-startup-recovery-state

---

## Next Step: Research & Implement #21 enable-vrr