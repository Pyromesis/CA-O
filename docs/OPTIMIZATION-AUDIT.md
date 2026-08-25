# CA-O — Auditoría individual del catálogo (2026)

Generado automáticamente por `scripts/generate-audit.mjs`. 155 IDs clasificados.

| Disposición | Cantidad |
|---|---|
| KEEP | 72 |
| MOVE_TO_MAINTENANCE | 16 |
| MOVE_TO_SECURITY | 13 |
| MOVE_TO_TWEAKS | 12 |
| MOVE_TO_GAMING | 9 |
| MOVE_TO_PERFORMANCE | 8 |
| MOVE_TO_EXPERIMENTAL | 8 |
| MOVE_TO_PRIVACY | 7 |
| MOVE_TO_REPAIR | 6 |
| KEEP_BUT_CONTEXTUAL | 3 |
| MOVE_TO_DIAGNOSTICS | 1 |

| ID | Categoría antigua | Grupo nuevo | Subgrupo | Tipo | Impacto esperado | Confianza | Contextual | Disposición |
|---|---|---|---|---|---|---|---|---|
| `animations` | Tweaks | tweaks | cosmetic | cosmetic | none | high | - | KEEP |
| `clear-temp-files` | System | maintenance | cleanup | maintenance | none | high | - | KEEP |
| `delay-taskbar-thumbnails` | Tweaks | tweaks | taskbar | cosmetic | none | high | - | KEEP |
| `deny-broad-filesystem` | Privacy | privacy | permissions | privacy-control | none | high | - | KEEP |
| `deny-documents-library` | Privacy | privacy | permissions | privacy-control | none | high | - | KEEP |
| `deny-email-access` | Privacy | privacy | permissions | privacy-control | none | high | - | KEEP |
| `deny-human-presence` | Privacy | privacy | permissions | privacy-control | none | high | - | KEEP |
| `deny-pictures-library` | Privacy | privacy | permissions | privacy-control | none | high | - | KEEP |
| `deny-radios-access` | Privacy | privacy | permissions | privacy-control | none | high | - | KEEP |
| `deny-user-account-information` | Privacy | privacy | permissions | privacy-control | none | high | - | KEEP |
| `deny-videos-library` | Privacy | privacy | permissions | privacy-control | none | high | - | KEEP |
| `disable-8dot3-names` | System | performance | storage | optimization | small | medium | - | MOVE_TO_PERFORMANCE |
| `disable-active-probing` | Network | maintenance | services | maintenance | none | high | - | MOVE_TO_MAINTENANCE |
| `disable-activity-history` | Privacy | privacy | telemetry | privacy-control | none | high | - | KEEP |
| `disable-admin-shares` | System | security | smb | security-hardening | none | high | - | MOVE_TO_SECURITY |
| `disable-advertising-id` | Privacy | privacy | advertising | privacy-control | none | high | - | KEEP |
| `disable-aero-peek` | Tweaks | tweaks | taskbar | cosmetic | none | high | - | KEEP |
| `disable-app-readiness` | Powerful | maintenance | services | maintenance | none | high | - | MOVE_TO_MAINTENANCE |
| `disable-app-suggestions` | Privacy | privacy | advertising | privacy-control | none | high | - | KEEP |
| `disable-automatic-maintenance` | Powerful | maintenance | services | maintenance | none | high | - | MOVE_TO_MAINTENANCE |
| `disable-background-apps` | Tweaks | performance | cpu | optimization | workload-dependent | medium | - | MOVE_TO_PERFORMANCE |
| `disable-bits` | Powerful | maintenance | services | maintenance | none | high | - | MOVE_TO_MAINTENANCE |
| `disable-calendar-access` | Privacy | privacy | permissions | privacy-control | none | high | - | KEEP |
| `disable-camera-access` | Privacy | privacy | permissions | privacy-control | none | high | - | KEEP |
| `disable-cast-notifications` | Tweaks | tweaks | taskbar | cosmetic | none | high | - | KEEP |
| `disable-ceip-tasks` | System | privacy | telemetry | privacy-control | none | high | - | MOVE_TO_PRIVACY |
| `disable-click-to-do` | Privacy | privacy | ai-features | privacy-control | none | high | - | KEEP |
| `disable-clipboard-cloud-sync` | Privacy | privacy | telemetry | privacy-control | none | high | - | KEEP |
| `disable-clipboard-history` | Privacy | privacy | permissions | privacy-control | none | high | - | KEEP |
| `disable-cloud-content` | Privacy | privacy | advertising | privacy-control | none | high | - | KEEP |
| `disable-contacts-access` | Privacy | privacy | permissions | privacy-control | none | high | - | KEEP |
| `disable-controller-gamebar-chord` | Input | gaming | windows-gaming | optimization | workload-dependent | medium | - | MOVE_TO_GAMING |
| `disable-core-parking` | Powerful | performance | cpu | optimization | workload-dependent | low | yes | KEEP_BUT_CONTEXTUAL |
| `disable-cortana` | System | maintenance | windows-features | maintenance | none | high | - | KEEP |
| `disable-cpu-idle` | Powerful | performance | cpu | optimization | workload-dependent | low | yes | KEEP_BUT_CONTEXTUAL |
| `disable-delivery-optimization` | System | maintenance | services | maintenance | none | high | - | KEEP |
| `disable-drag-full-window` | Tweaks | tweaks | cosmetic | cosmetic | none | high | - | KEEP |
| `disable-driver-search` | System | maintenance | services | maintenance | none | high | - | KEEP |
| `disable-edge-startup-boost` | Powerful | maintenance | services | maintenance | none | high | - | MOVE_TO_MAINTENANCE |
| `disable-error-reporting` | System | privacy | telemetry | privacy-control | none | high | - | MOVE_TO_PRIVACY |
| `disable-fast-startup` | Powerful | maintenance | services | maintenance | none | high | - | MOVE_TO_MAINTENANCE |
| `disable-filter-keys` | Input | tweaks | quality-of-life | cosmetic | none | high | - | MOVE_TO_TWEAKS |
| `disable-find-my-device` | Privacy | privacy | location | privacy-control | none | high | - | KEEP |
| `disable-fullscreen-optimizations` | Powerful | repair | troubleshooting | repair-action | diagnostic-only | high | - | MOVE_TO_REPAIR |
| `disable-game-dvr` | Powerful | gaming | windows-gaming | optimization | small | high | - | MOVE_TO_GAMING |
| `disable-handwriting-data` | Privacy | privacy | telemetry | privacy-control | none | high | - | KEEP |
| `disable-hibernation` | Powerful | maintenance | cleanup | maintenance | none | high | - | MOVE_TO_MAINTENANCE |
| `disable-hotspot-service` | Network | maintenance | services | maintenance | none | high | - | MOVE_TO_MAINTENANCE |
| `disable-hover-checkboxes` | Input | tweaks | explorer-ui | cosmetic | none | high | - | MOVE_TO_TWEAKS |
| `disable-input-personalization` | Privacy | privacy | telemetry | privacy-control | none | high | - | KEEP |
| `disable-last-access-time` | System | performance | storage | optimization | small | medium | - | MOVE_TO_PERFORMANCE |
| `disable-llmnr` | Network | security | network-hardening | security-hardening | none | high | - | MOVE_TO_SECURITY |
| `disable-location-tracking` | Privacy | privacy | location | privacy-control | none | high | - | KEEP |
| `disable-lock-screen` | Tweaks | tweaks | cosmetic | cosmetic | none | high | - | KEEP |
| `disable-memory-dumps` | Powerful | maintenance | cleanup | maintenance | none | high | - | MOVE_TO_MAINTENANCE |
| `disable-memory-integrity` | Powerful | security | hvci-vbs | security-tradeoff | workload-dependent | low | yes | MOVE_TO_SECURITY |
| `disable-microphone-access` | Privacy | privacy | permissions | privacy-control | none | high | - | KEEP |
| `disable-modern-standby` | Powerful | experimental | legacy | optimization | diagnostic-only | low | yes | MOVE_TO_EXPERIMENTAL |
| `disable-multiplane-overlay` | Powerful | repair | troubleshooting | repair-action | diagnostic-only | high | - | MOVE_TO_REPAIR |
| `disable-netbios` | Network | security | network-hardening | security-hardening | none | high | - | MOVE_TO_SECURITY |
| `disable-network-throttling` | Network | experimental | legacy | optimization | diagnostic-only | low | yes | MOVE_TO_EXPERIMENTAL |
| `disable-paint-ai` | System | privacy | ai-features | privacy-control | none | high | - | MOVE_TO_PRIVACY |
| `disable-peer-name-resolution` | Network | security | attack-surface | security-hardening | none | high | - | MOVE_TO_SECURITY |
| `disable-power-throttling` | Powerful | performance | cpu | optimization | workload-dependent | low | yes | KEEP_BUT_CONTEXTUAL |
| `disable-print-spooler` | System | maintenance | services | maintenance | none | high | - | KEEP |
| `disable-recall` | System | privacy | ai-features | privacy-control | none | high | - | MOVE_TO_PRIVACY |
| `disable-remote-assistance` | System | security | rdp-remote | security-hardening | none | high | - | MOVE_TO_SECURITY |
| `disable-remote-desktop` | System | security | rdp-remote | security-hardening | none | high | - | MOVE_TO_SECURITY |
| `disable-retail-demo` | System | maintenance | windows-features | maintenance | none | high | - | KEEP |
| `disable-search-indexing` | System | performance | storage | optimization | workload-dependent | medium | - | MOVE_TO_PERFORMANCE |
| `disable-services` | Powerful | maintenance | services | maintenance | none | high | - | MOVE_TO_MAINTENANCE |
| `disable-setting-sync` | Privacy | privacy | personalization | privacy-control | none | high | - | KEEP |
| `disable-smb1` | Network | security | smb | security-hardening | none | high | - | MOVE_TO_SECURITY |
| `disable-snap-layouts-flyout` | Tweaks | tweaks | taskbar | cosmetic | none | high | - | KEEP |
| `disable-snmp-trap` | Network | maintenance | services | maintenance | none | high | - | MOVE_TO_MAINTENANCE |
| `disable-speech-recognition` | Privacy | privacy | telemetry | privacy-control | none | high | - | KEEP |
| `disable-spotlight-wallpapers` | Tweaks | privacy | personalization | privacy-control | none | high | - | MOVE_TO_PRIVACY |
| `disable-ssdp-discovery` | Network | security | attack-surface | security-hardening | none | high | - | MOVE_TO_SECURITY |
| `disable-ssl-time-seeding` | Powerful | experimental | legacy | optimization | none | unknown | yes | MOVE_TO_EXPERIMENTAL |
| `disable-start-menu-suggestions` | Tweaks | privacy | advertising | privacy-control | none | high | - | MOVE_TO_PRIVACY |
| `disable-start-tracking` | Privacy | privacy | personalization | privacy-control | none | high | - | KEEP |
| `disable-startup-sound` | Tweaks | tweaks | sounds | cosmetic | none | high | - | KEEP |
| `disable-sticky-keys` | Input | tweaks | quality-of-life | cosmetic | none | high | - | MOVE_TO_TWEAKS |
| `disable-superfetch` | System | experimental | legacy | diagnostic | diagnostic-only | low | yes | MOVE_TO_EXPERIMENTAL |
| `disable-svchost-split-threshold` | Powerful | experimental | legacy | optimization | diagnostic-only | low | yes | MOVE_TO_EXPERIMENTAL |
| `disable-system-sounds` | Tweaks | tweaks | sounds | cosmetic | none | high | - | KEEP |
| `disable-tablet-input-service` | Input | maintenance | services | maintenance | none | high | yes | MOVE_TO_MAINTENANCE |
| `disable-tailored-experiences` | Privacy | privacy | advertising | privacy-control | none | high | - | KEEP |
| `disable-taskbar-search` | Tweaks | tweaks | taskbar | cosmetic | none | high | - | KEEP |
| `disable-telemetry` | System | privacy | telemetry | privacy-control | none | high | - | MOVE_TO_PRIVACY |
| `disable-thumbnails` | Tweaks | tweaks | explorer-ui | cosmetic | none | high | - | KEEP |
| `disable-toggle-keys` | Input | tweaks | quality-of-life | cosmetic | none | high | - | MOVE_TO_TWEAKS |
| `disable-tooltips` | Tweaks | tweaks | explorer-ui | cosmetic | none | high | - | KEEP |
| `disable-touch-keyboard-autoinvoke` | Input | tweaks | quality-of-life | cosmetic | none | high | - | MOVE_TO_TWEAKS |
| `disable-touchpad-edge-swipes` | Input | tweaks | quality-of-life | cosmetic | none | high | - | MOVE_TO_TWEAKS |
| `disable-touchpad-threefinger-slide` | Input | tweaks | quality-of-life | cosmetic | none | high | - | MOVE_TO_TWEAKS |
| `disable-upnp-device-host` | Network | security | attack-surface | security-hardening | none | high | - | MOVE_TO_SECURITY |
| `disable-usb-suspend` | Input | performance | input | optimization | workload-dependent | medium | yes | MOVE_TO_PERFORMANCE |
| `disable-wallpaper-slideshow` | Tweaks | tweaks | cosmetic | cosmetic | none | high | - | KEEP |
| `disable-welcome-experience` | Privacy | privacy | advertising | privacy-control | none | high | - | KEEP |
| `disable-widgets` | System | tweaks | taskbar | cosmetic | none | high | - | MOVE_TO_TWEAKS |
| `disable-window-arrange-drag` | Tweaks | tweaks | quality-of-life | cosmetic | none | high | - | KEEP |
| `disable-window-shake` | Tweaks | tweaks | quality-of-life | cosmetic | none | high | - | KEEP |
| `disable-windows-feedback` | Privacy | privacy | telemetry | privacy-control | none | high | - | KEEP |
| `disable-windows-ink` | Input | maintenance | services | maintenance | none | high | yes | MOVE_TO_MAINTENANCE |
| `disable-windows-insider` | System | maintenance | services | maintenance | none | high | - | KEEP |
| `disable-wpad` | Network | security | attack-surface | security-hardening | none | high | - | MOVE_TO_SECURITY |
| `disable-xbox-gamebar` | System | gaming | windows-gaming | optimization | workload-dependent | medium | - | MOVE_TO_GAMING |
| `dns-optimization` | Network | experimental | legacy | optimization | workload-dependent | medium | yes | MOVE_TO_EXPERIMENTAL |
| `enable-hags` | Powerful | gaming | gpu-gaming | optimization | workload-dependent | medium | - | MOVE_TO_GAMING |
| `enable-long-paths` | Powerful | maintenance | windows-features | maintenance | none | high | - | MOVE_TO_MAINTENANCE |
| `enable-mouse-raw-input` | Input | gaming | input-gaming | guidance | workload-dependent | medium | - | MOVE_TO_GAMING |
| `enable-msi-gpu` | Powerful | gaming | gpu-gaming | guidance | workload-dependent | medium | - | MOVE_TO_GAMING |
| `flush-arp-cache` | Network | repair | network-repair | repair-action | diagnostic-only | high | - | MOVE_TO_REPAIR |
| `flush-dns` | Network | repair | network-repair | repair-action | diagnostic-only | high | - | MOVE_TO_REPAIR |
| `gaming-mode` | Powerful | gaming | windows-gaming | optimization | workload-dependent | medium | - | MOVE_TO_GAMING |
| `hide-copilot-button` | Tweaks | tweaks | taskbar | cosmetic | none | high | - | KEEP |
| `hide-meet-now` | Tweaks | tweaks | taskbar | cosmetic | none | high | - | KEEP |
| `hide-start-recommended` | Tweaks | tweaks | taskbar | cosmetic | none | high | - | KEEP |
| `hide-task-view` | Tweaks | tweaks | taskbar | cosmetic | none | high | - | KEEP |
| `inactive-window-scroll` | Input | tweaks | quality-of-life | cosmetic | none | high | - | MOVE_TO_TWEAKS |
| `keyboard-rate` | Input | tweaks | quality-of-life | cosmetic | none | high | - | MOVE_TO_TWEAKS |
| `max-system-responsiveness` | Powerful | performance | cpu | optimization | workload-dependent | medium | - | KEEP |
| `memory-compression` | Powerful | diagnostics | memory | diagnostic | diagnostic-only | low | yes | MOVE_TO_DIAGNOSTICS |
| `menu-delay` | Input | tweaks | quality-of-life | cosmetic | none | high | - | MOVE_TO_TWEAKS |
| `mouse-acceleration` | Input | performance | input | optimization | small | high | - | MOVE_TO_PERFORMANCE |
| `mouse-polling` | Input | gaming | input-gaming | guidance | workload-dependent | medium | - | MOVE_TO_GAMING |
| `never-combine-taskbar-icons` | Tweaks | tweaks | taskbar | cosmetic | none | high | - | KEEP |
| `no-auto-reboot-active` | System | maintenance | services | maintenance | none | high | - | KEEP |
| `notifications` | Tweaks | tweaks | taskbar | cosmetic | none | high | - | KEEP |
| `numlock-on-boot` | Input | tweaks | quality-of-life | cosmetic | none | high | - | MOVE_TO_TWEAKS |
| `optimize-network-power` | Network | performance | network | optimization | workload-dependent | medium | - | MOVE_TO_PERFORMANCE |
| `optimize-ntfs-memory-usage` | Powerful | performance | storage | optimization | workload-dependent | low | - | KEEP |
| `optimize-startup` | System | maintenance | startup | guidance | none | high | - | KEEP |
| `optimize-thread-scheduling` | Powerful | performance | cpu | optimization | workload-dependent | medium | - | KEEP |
| `power-plan` | Powerful | performance | cpu | optimization | workload-dependent | high | - | KEEP |
| `registry-cleanup` | Powerful | maintenance | cleanup | maintenance | none | high | - | MOVE_TO_MAINTENANCE |
| `remove-onedrive` | Privacy | maintenance | windows-features | maintenance | none | high | - | MOVE_TO_MAINTENANCE |
| `require-network-level-auth` | Network | security | rdp-remote | security-hardening | none | high | - | MOVE_TO_SECURITY |
| `reset-network` | Network | repair | network-repair | repair-action | diagnostic-only | high | - | MOVE_TO_REPAIR |
| `restrict-point-and-print` | Network | security | driver-security | security-hardening | none | high | - | MOVE_TO_SECURITY |
| `shadows` | Tweaks | tweaks | cosmetic | cosmetic | none | high | - | KEEP |
| `show-file-extensions` | Tweaks | tweaks | explorer-ui | cosmetic | none | high | - | KEEP |
| `show-hidden-files` | Tweaks | tweaks | explorer-ui | cosmetic | none | high | - | KEEP |
| `show-seconds-clock` | Tweaks | tweaks | taskbar | cosmetic | none | high | - | KEEP |
| `speedup-shutdown` | System | maintenance | services | maintenance | none | high | - | KEEP |
| `static-pagefile` | Powerful | experimental | legacy | optimization | diagnostic-only | low | yes | MOVE_TO_EXPERIMENTAL |
| `taskbar-icons` | Tweaks | tweaks | taskbar | cosmetic | none | high | - | KEEP |
| `timer-resolution-0-5ms` | Input | experimental | advanced-power | diagnostic | workload-dependent | low | yes | MOVE_TO_EXPERIMENTAL |
| `touchpad-latency` | Input | performance | input | optimization | workload-dependent | medium | - | MOVE_TO_PERFORMANCE |
| `transparency` | Tweaks | tweaks | cosmetic | cosmetic | none | high | - | KEEP |
| `uninstall-bing-search` | System | maintenance | windows-features | maintenance | none | high | - | KEEP |
| `uninstall-copilot` | System | maintenance | windows-features | maintenance | none | high | - | KEEP |
| `windowed-games-optimization` | Powerful | gaming | display-gaming | optimization | workload-dependent | medium | - | MOVE_TO_GAMING |
| `winsock-reset` | Network | repair | network-repair | repair-action | diagnostic-only | high | - | MOVE_TO_REPAIR |
