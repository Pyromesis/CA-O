/**
 * CA-O catalog taxonomy (v3 - CA-O 2026).
 *
 * Nine top-level groups. Maintenance, repair, diagnostics and cosmetics are
 * NEVER presented as performance optimizations.
 *
 *   performance | gaming | diagnostics | security | privacy
 *   maintenance | repair | tweaks     | experimental
 *
 * Legacy Windows-7/10-era tweaks live under `experimental/legacy` unless a
 * modern Windows 11 justification exists (#53/#54).
 */

import type { TaxonomyEntry } from './types';

type Entry = TaxonomyEntry;

export const taxonomyById: Record<string, Entry> = {
  // ===================== PERFORMANCE =====================
  'disable-search-indexing': { group: 'performance', subgroup: 'storage', kind: 'optimization' },
  'disable-last-access-time': { group: 'performance', subgroup: 'storage', kind: 'optimization' },
  'disable-8dot3-names': { group: 'performance', subgroup: 'storage', kind: 'optimization' },
  'optimize-network-power': { group: 'performance', subgroup: 'network', kind: 'optimization' },
  'mouse-acceleration': { group: 'performance', subgroup: 'input', kind: 'optimization' },
  'touchpad-latency': { group: 'performance', subgroup: 'input', kind: 'optimization' },
  'disable-usb-suspend': { group: 'performance', subgroup: 'input', kind: 'optimization' },
  'power-plan': { group: 'performance', subgroup: 'cpu', kind: 'optimization' },
  'disable-power-throttling': { group: 'performance', subgroup: 'cpu', kind: 'optimization' },
  'max-system-responsiveness': { group: 'performance', subgroup: 'cpu', kind: 'optimization' },
  'optimize-thread-scheduling': { group: 'performance', subgroup: 'cpu', kind: 'optimization' },
  // Advanced power configurations: contextual, desktop+AC only (#27)
  'disable-cpu-idle': { group: 'performance', subgroup: 'cpu', kind: 'optimization' },
  'disable-core-parking': { group: 'performance', subgroup: 'cpu', kind: 'optimization' },
  'optimize-ntfs-memory-usage': { group: 'performance', subgroup: 'storage', kind: 'optimization' },
  'disable-background-apps': { group: 'performance', subgroup: 'cpu', kind: 'optimization' },

  // ===================== GAMING =====================
  'gaming-mode': { group: 'gaming', subgroup: 'windows-gaming', kind: 'optimization' },
  'enable-hags': { group: 'gaming', subgroup: 'gpu-gaming', kind: 'optimization' },
  'windowed-games-optimization': { group: 'gaming', subgroup: 'display-gaming', kind: 'optimization' },
  'disable-game-dvr': { group: 'gaming', subgroup: 'windows-gaming', kind: 'optimization' },
  'disable-xbox-gamebar': { group: 'gaming', subgroup: 'windows-gaming', kind: 'optimization' },
  'disable-controller-gamebar-chord': { group: 'gaming', subgroup: 'windows-gaming', kind: 'optimization' },
  'mouse-polling': { group: 'gaming', subgroup: 'input-gaming', kind: 'guidance' },
  'enable-mouse-raw-input': { group: 'gaming', subgroup: 'input-gaming', kind: 'guidance' },
  'enable-msi-gpu': { group: 'gaming', subgroup: 'gpu-gaming', kind: 'guidance' },

  // ===================== DIAGNOSTICS =====================
  // Memory compression is a diagnostic toggle, never a gaming default (#29)
  'memory-compression': { group: 'diagnostics', subgroup: 'memory', kind: 'diagnostic' },

  // ===================== SECURITY =====================
  'disable-memory-integrity': { group: 'security', subgroup: 'hvci-vbs', kind: 'security-tradeoff' },
  'disable-smb1': { group: 'security', subgroup: 'smb', kind: 'security-hardening' },
  'disable-admin-shares': { group: 'security', subgroup: 'smb', kind: 'security-hardening' },
  'disable-remote-assistance': { group: 'security', subgroup: 'rdp-remote', kind: 'security-hardening' },
  'disable-remote-desktop': { group: 'security', subgroup: 'rdp-remote', kind: 'security-hardening' },
  'require-network-level-auth': { group: 'security', subgroup: 'rdp-remote', kind: 'security-hardening' },
  'disable-llmnr': { group: 'security', subgroup: 'network-hardening', kind: 'security-hardening' },
  'disable-netbios': { group: 'security', subgroup: 'network-hardening', kind: 'security-hardening' },
  'disable-wpad': { group: 'security', subgroup: 'attack-surface', kind: 'security-hardening' },
  'disable-peer-name-resolution': { group: 'security', subgroup: 'attack-surface', kind: 'security-hardening' },
  'restrict-point-and-print': { group: 'security', subgroup: 'driver-security', kind: 'security-hardening' },
  'disable-ssdp-discovery': { group: 'security', subgroup: 'attack-surface', kind: 'security-hardening' },
  'disable-upnp-device-host': { group: 'security', subgroup: 'attack-surface', kind: 'security-hardening' },

  // ===================== PRIVACY =====================
  'disable-telemetry': { group: 'privacy', subgroup: 'telemetry', kind: 'privacy-control' },
  'disable-error-reporting': { group: 'privacy', subgroup: 'telemetry', kind: 'privacy-control' },
  'disable-recall': { group: 'privacy', subgroup: 'ai-features', kind: 'privacy-control' },
  'disable-activity-history': { group: 'privacy', subgroup: 'telemetry', kind: 'privacy-control' },
  'disable-windows-feedback': { group: 'privacy', subgroup: 'telemetry', kind: 'privacy-control' },
  'disable-ceip-tasks': { group: 'privacy', subgroup: 'telemetry', kind: 'privacy-control' },
  'disable-input-personalization': { group: 'privacy', subgroup: 'telemetry', kind: 'privacy-control' },
  'disable-handwriting-data': { group: 'privacy', subgroup: 'telemetry', kind: 'privacy-control' },
  'disable-speech-recognition': { group: 'privacy', subgroup: 'telemetry', kind: 'privacy-control' },
  'disable-clipboard-cloud-sync': { group: 'privacy', subgroup: 'telemetry', kind: 'privacy-control' },
  'disable-click-to-do': { group: 'privacy', subgroup: 'ai-features', kind: 'privacy-control' },
  'disable-advertising-id': { group: 'privacy', subgroup: 'advertising', kind: 'privacy-control' },
  'disable-tailored-experiences': { group: 'privacy', subgroup: 'advertising', kind: 'privacy-control' },
  'disable-app-suggestions': { group: 'privacy', subgroup: 'advertising', kind: 'privacy-control' },
  'disable-cloud-content': { group: 'privacy', subgroup: 'advertising', kind: 'privacy-control' },
  'disable-welcome-experience': { group: 'privacy', subgroup: 'advertising', kind: 'privacy-control' },
  'disable-start-menu-suggestions': { group: 'privacy', subgroup: 'advertising', kind: 'privacy-control' },
  'disable-location-tracking': { group: 'privacy', subgroup: 'location', kind: 'privacy-control' },
  'disable-find-my-device': { group: 'privacy', subgroup: 'location', kind: 'privacy-control' },
  'disable-start-tracking': { group: 'privacy', subgroup: 'personalization', kind: 'privacy-control' },
  'disable-setting-sync': { group: 'privacy', subgroup: 'personalization', kind: 'privacy-control' },
  'disable-spotlight-wallpapers': { group: 'privacy', subgroup: 'personalization', kind: 'privacy-control' },
  'disable-contacts-access': { group: 'privacy', subgroup: 'permissions', kind: 'privacy-control' },
  'disable-calendar-access': { group: 'privacy', subgroup: 'permissions', kind: 'privacy-control' },
  'disable-camera-access': { group: 'privacy', subgroup: 'permissions', kind: 'privacy-control' },
  'disable-microphone-access': { group: 'privacy', subgroup: 'permissions', kind: 'privacy-control' },
  'disable-clipboard-history': { group: 'privacy', subgroup: 'permissions', kind: 'privacy-control' },
  'deny-user-account-information': { group: 'privacy', subgroup: 'permissions', kind: 'privacy-control' },
  'deny-documents-library': { group: 'privacy', subgroup: 'permissions', kind: 'privacy-control' },
  'deny-pictures-library': { group: 'privacy', subgroup: 'permissions', kind: 'privacy-control' },
  'deny-videos-library': { group: 'privacy', subgroup: 'permissions', kind: 'privacy-control' },
  'deny-email-access': { group: 'privacy', subgroup: 'permissions', kind: 'privacy-control' },
  'deny-radios-access': { group: 'privacy', subgroup: 'permissions', kind: 'privacy-control' },
  'deny-human-presence': { group: 'privacy', subgroup: 'permissions', kind: 'privacy-control' },
  'deny-broad-filesystem': { group: 'privacy', subgroup: 'permissions', kind: 'privacy-control' },

  // ===================== MAINTENANCE =====================
  'clear-temp-files': { group: 'maintenance', subgroup: 'cleanup', kind: 'maintenance', repeatableAction: true },
  'registry-cleanup': { group: 'maintenance', subgroup: 'cleanup', kind: 'maintenance', repeatableAction: true },
  'disable-bits': { group: 'maintenance', subgroup: 'services', kind: 'maintenance' },
  'disable-delivery-optimization': { group: 'maintenance', subgroup: 'services', kind: 'maintenance' },
  'disable-automatic-maintenance': { group: 'maintenance', subgroup: 'services', kind: 'maintenance' },
  'disable-hibernation': { group: 'maintenance', subgroup: 'cleanup', kind: 'maintenance' },
  'disable-fast-startup': { group: 'maintenance', subgroup: 'services', kind: 'maintenance' },
  'disable-memory-dumps': { group: 'maintenance', subgroup: 'cleanup', kind: 'maintenance' },
  'disable-edge-startup-boost': { group: 'maintenance', subgroup: 'services', kind: 'maintenance' },
  'disable-print-spooler': { group: 'maintenance', subgroup: 'services', kind: 'maintenance' },
  'disable-hotspot-service': { group: 'maintenance', subgroup: 'services', kind: 'maintenance' },
  'disable-snmp-trap': { group: 'maintenance', subgroup: 'services', kind: 'maintenance' },
  'disable-tablet-input-service': { group: 'maintenance', subgroup: 'services', kind: 'maintenance' },
  'disable-app-readiness': { group: 'maintenance', subgroup: 'services', kind: 'maintenance' },
  'disable-services': { group: 'maintenance', subgroup: 'services', kind: 'maintenance' },
  'speedup-shutdown': { group: 'maintenance', subgroup: 'services', kind: 'maintenance' },
  'no-auto-reboot-active': { group: 'maintenance', subgroup: 'services', kind: 'maintenance' },
  'disable-driver-search': { group: 'maintenance', subgroup: 'services', kind: 'maintenance' },
  'disable-windows-insider': { group: 'maintenance', subgroup: 'services', kind: 'maintenance' },
  'disable-active-probing': { group: 'maintenance', subgroup: 'services', kind: 'maintenance' },
  'optimize-startup': { group: 'maintenance', subgroup: 'startup', kind: 'guidance' },
  'disable-windows-ink': { group: 'maintenance', subgroup: 'services', kind: 'maintenance' },

  // ===================== SYSTEM FEATURES (kept inside maintenance tree) ====
  'disable-cortana': { group: 'maintenance', subgroup: 'windows-features', kind: 'maintenance' },
  'disable-retail-demo': { group: 'maintenance', subgroup: 'windows-features', kind: 'maintenance' },
  'uninstall-copilot': { group: 'maintenance', subgroup: 'windows-features', kind: 'maintenance' },
  'uninstall-bing-search': { group: 'maintenance', subgroup: 'windows-features', kind: 'maintenance' },
  'disable-paint-ai': { group: 'privacy', subgroup: 'ai-features', kind: 'privacy-control' },
  'remove-onedrive': { group: 'maintenance', subgroup: 'windows-features', kind: 'maintenance' },
  'enable-long-paths': { group: 'maintenance', subgroup: 'windows-features', kind: 'maintenance' },

  // ===================== REPAIR =====================
  'winsock-reset': { group: 'repair', subgroup: 'network-repair', kind: 'repair-action', repeatableAction: true },
  'flush-dns': { group: 'repair', subgroup: 'network-repair', kind: 'repair-action', sessionAction: true, repeatableAction: true },
  'reset-network': { group: 'repair', subgroup: 'network-repair', kind: 'repair-action', repeatableAction: true },
  'flush-arp-cache': { group: 'repair', subgroup: 'network-repair', kind: 'repair-action', sessionAction: true, repeatableAction: true },
  'disable-fullscreen-optimizations': { group: 'repair', subgroup: 'troubleshooting', kind: 'repair-action' },
  'disable-multiplane-overlay': { group: 'repair', subgroup: 'troubleshooting', kind: 'repair-action' },

  // ===================== TWEAKS (cosmetics / comfort) =====================
  'disable-widgets': { group: 'tweaks', subgroup: 'taskbar', kind: 'cosmetic' },
  'animations': { group: 'tweaks', subgroup: 'cosmetic', kind: 'cosmetic' },
  'transparency': { group: 'tweaks', subgroup: 'cosmetic', kind: 'cosmetic' },
  'shadows': { group: 'tweaks', subgroup: 'cosmetic', kind: 'cosmetic' },
  'taskbar-icons': { group: 'tweaks', subgroup: 'taskbar', kind: 'cosmetic' },
  'notifications': { group: 'tweaks', subgroup: 'taskbar', kind: 'cosmetic' },
  'show-file-extensions': { group: 'tweaks', subgroup: 'explorer-ui', kind: 'cosmetic' },
  'disable-thumbnails': { group: 'tweaks', subgroup: 'explorer-ui', kind: 'cosmetic' },
  'disable-tooltips': { group: 'tweaks', subgroup: 'explorer-ui', kind: 'cosmetic' },
  'disable-wallpaper-slideshow': { group: 'tweaks', subgroup: 'cosmetic', kind: 'cosmetic' },
  'disable-system-sounds': { group: 'tweaks', subgroup: 'sounds', kind: 'cosmetic' },
  'show-hidden-files': { group: 'tweaks', subgroup: 'explorer-ui', kind: 'cosmetic' },
  'hide-task-view': { group: 'tweaks', subgroup: 'taskbar', kind: 'cosmetic' },
  'disable-taskbar-search': { group: 'tweaks', subgroup: 'taskbar', kind: 'cosmetic' },
  'disable-lock-screen': { group: 'tweaks', subgroup: 'cosmetic', kind: 'cosmetic' },
  'disable-aero-peek': { group: 'tweaks', subgroup: 'taskbar', kind: 'cosmetic' },
  'disable-startup-sound': { group: 'tweaks', subgroup: 'sounds', kind: 'cosmetic' },
  'disable-cast-notifications': { group: 'tweaks', subgroup: 'taskbar', kind: 'cosmetic' },
  'show-seconds-clock': { group: 'tweaks', subgroup: 'taskbar', kind: 'cosmetic' },
  'hide-meet-now': { group: 'tweaks', subgroup: 'taskbar', kind: 'cosmetic' },
  'delay-taskbar-thumbnails': { group: 'tweaks', subgroup: 'taskbar', kind: 'cosmetic' },
  'hide-start-recommended': { group: 'tweaks', subgroup: 'taskbar', kind: 'cosmetic' },
  'disable-drag-full-window': { group: 'tweaks', subgroup: 'cosmetic', kind: 'cosmetic' },
  'never-combine-taskbar-icons': { group: 'tweaks', subgroup: 'taskbar', kind: 'cosmetic' },
  'disable-window-shake': { group: 'tweaks', subgroup: 'quality-of-life', kind: 'cosmetic' },
  'disable-snap-layouts-flyout': { group: 'tweaks', subgroup: 'taskbar', kind: 'cosmetic' },
  'disable-window-arrange-drag': { group: 'tweaks', subgroup: 'quality-of-life', kind: 'cosmetic' },
  'hide-copilot-button': { group: 'tweaks', subgroup: 'taskbar', kind: 'cosmetic' },
  'keyboard-rate': { group: 'tweaks', subgroup: 'quality-of-life', kind: 'cosmetic' },
  'menu-delay': { group: 'tweaks', subgroup: 'quality-of-life', kind: 'cosmetic' },
  'inactive-window-scroll': { group: 'tweaks', subgroup: 'quality-of-life', kind: 'cosmetic' },
  'disable-sticky-keys': { group: 'tweaks', subgroup: 'quality-of-life', kind: 'cosmetic' },
  'disable-filter-keys': { group: 'tweaks', subgroup: 'quality-of-life', kind: 'cosmetic' },
  'disable-toggle-keys': { group: 'tweaks', subgroup: 'quality-of-life', kind: 'cosmetic' },
  'disable-touch-keyboard-autoinvoke': { group: 'tweaks', subgroup: 'quality-of-life', kind: 'cosmetic' },
  'disable-touchpad-edge-swipes': { group: 'tweaks', subgroup: 'quality-of-life', kind: 'cosmetic' },
  'disable-touchpad-threefinger-slide': { group: 'tweaks', subgroup: 'quality-of-life', kind: 'cosmetic' },
  'numlock-on-boot': { group: 'tweaks', subgroup: 'quality-of-life', kind: 'cosmetic' },
  'disable-hover-checkboxes': { group: 'tweaks', subgroup: 'explorer-ui', kind: 'cosmetic' },

  // ===================== EXPERIMENTAL (legacy / no modern evidence) =======
  'disable-superfetch': { group: 'experimental', subgroup: 'legacy', kind: 'diagnostic' },
  'timer-resolution-0-5ms': { group: 'experimental', subgroup: 'advanced-power', kind: 'diagnostic', sessionAction: true, nonPersistent: true },
  'dns-optimization': { group: 'experimental', subgroup: 'legacy', kind: 'optimization' },
  'disable-network-throttling': { group: 'experimental', subgroup: 'legacy', kind: 'optimization' },
  'disable-svchost-split-threshold': { group: 'experimental', subgroup: 'legacy', kind: 'optimization' },
  'static-pagefile': { group: 'experimental', subgroup: 'legacy', kind: 'optimization' },
  'disable-modern-standby': { group: 'experimental', subgroup: 'legacy', kind: 'optimization' },
  'disable-ssl-time-seeding': { group: 'experimental', subgroup: 'legacy', kind: 'optimization' },
};

/** Legacy UI categories kept in sync so the existing views keep rendering. */
export const legacyCategoryByGroup: Record<string, string> = {
  performance: 'Powerful',
  gaming: 'Powerful',
  diagnostics: 'Powerful',
  security: 'System',
  privacy: 'Privacy',
  maintenance: 'System',
  repair: 'Network',
  tweaks: 'Tweaks',
  experimental: 'Powerful',
};

export function getTaxonomy(id: string): Entry | null {
  return taxonomyById[id] ?? null;
}

export function isRepairOrMaintenance(id: string): boolean {
  const entry = taxonomyById[id];
  if (!entry) return false;
  return entry.kind === 'repair-action' || entry.kind === 'maintenance' ||
    entry.group === 'repair' || entry.group === 'maintenance';
}
