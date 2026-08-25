/**
 * CA-O catalog taxonomy (v2).
 *
 * Reclassifies every catalog ID into an honest tree:
 * performance / system / security / privacy / gaming / repair / tweaks /
 * experimental — and tags what kind of action each one really is.
 *
 * Rules encoded here:
 * - Maintenance and repair actions are NOT optimizations.
 * - Security trade-offs live under `security`, never under gaming/performance.
 * - Contested folklore (SysMain off, pagefile games, MPO/FSO global toggles,
 *   network throttling index, SVCHOST split) lives under `experimental`
 *   until evidence exists.
 * - Flush/reset/cleanup operations are session repair actions.
 */

import type { TaxonomyEntry } from './types';

type Entry = TaxonomyEntry;

export const taxonomyById: Record<string, Entry> = {
  // ===================== SYSTEM =====================
  'disable-telemetry': { group: 'privacy', subgroup: 'telemetry', kind: 'privacy-control' },
  'disable-cortana': { group: 'system', subgroup: 'windows-features', kind: 'maintenance' },
  'disable-search-indexing': { group: 'performance', subgroup: 'storage', kind: 'optimization' },
  'disable-superfetch': { group: 'experimental', subgroup: 'contested', kind: 'diagnostic' },
  'disable-print-spooler': { group: 'system', subgroup: 'services', kind: 'maintenance' },
  'disable-xbox-gamebar': { group: 'gaming', subgroup: 'windows-gaming', kind: 'optimization' },
  'optimize-startup': { group: 'system', subgroup: 'startup', kind: 'guidance' },
  'clear-temp-files': { group: 'repair', subgroup: 'troubleshooting', kind: 'maintenance', repeatableAction: true },
  'disable-error-reporting': { group: 'privacy', subgroup: 'telemetry', kind: 'privacy-control' },
  'disable-delivery-optimization': { group: 'system', subgroup: 'maintenance', kind: 'maintenance' },
  'disable-windows-insider': { group: 'system', subgroup: 'maintenance', kind: 'maintenance' },
  'disable-retail-demo': { group: 'system', subgroup: 'windows-features', kind: 'maintenance' },
  'disable-widgets': { group: 'tweaks', subgroup: 'taskbar', kind: 'cosmetic' },
  'disable-recall': { group: 'privacy', subgroup: 'telemetry', kind: 'privacy-control' },
  'disable-ceip-tasks': { group: 'privacy', subgroup: 'telemetry', kind: 'privacy-control' },
  'disable-last-access-time': { group: 'performance', subgroup: 'storage', kind: 'optimization' },
  'disable-8dot3-names': { group: 'performance', subgroup: 'storage', kind: 'optimization' },
  'disable-admin-shares': { group: 'security', subgroup: 'smb', kind: 'security-hardening' },
  'disable-remote-assistance': { group: 'security', subgroup: 'rdp-remote', kind: 'security-hardening' },
  'disable-remote-desktop': { group: 'security', subgroup: 'rdp-remote', kind: 'security-hardening' },
  'speedup-shutdown': { group: 'system', subgroup: 'maintenance', kind: 'maintenance' },
  'no-auto-reboot-active': { group: 'system', subgroup: 'maintenance', kind: 'maintenance' },
  'disable-driver-search': { group: 'system', subgroup: 'maintenance', kind: 'maintenance' },
  'uninstall-copilot': { group: 'system', subgroup: 'windows-features', kind: 'maintenance' },
  'uninstall-bing-search': { group: 'system', subgroup: 'windows-features', kind: 'maintenance' },
  'disable-paint-ai': { group: 'system', subgroup: 'windows-features', kind: 'maintenance' },

  // ===================== NETWORK =====================
  // DNS forcing is no longer sold as a performance optimization; it becomes
  // an experimental action that should follow /api/benchmark/dns results.
  'dns-optimization': { group: 'experimental', subgroup: 'contested', kind: 'optimization' },
  'winsock-reset': { group: 'repair', subgroup: 'network-repair', kind: 'repair-action', nonPersistent: false, repeatableAction: true },
  'flush-dns': { group: 'repair', subgroup: 'network-repair', kind: 'repair-action', sessionAction: true, repeatableAction: true },
  'reset-network': { group: 'repair', subgroup: 'network-repair', kind: 'repair-action', repeatableAction: true },
  'flush-arp-cache': { group: 'repair', subgroup: 'network-repair', kind: 'repair-action', sessionAction: true, repeatableAction: true },
  'disable-network-throttling': { group: 'experimental', subgroup: 'contested', kind: 'optimization' },
  'optimize-network-power': { group: 'performance', subgroup: 'network', kind: 'optimization' },
  'disable-hotspot-service': { group: 'system', subgroup: 'services', kind: 'maintenance' },
  'disable-active-probing': { group: 'system', subgroup: 'windows-features', kind: 'maintenance' },
  'disable-snmp-trap': { group: 'system', subgroup: 'services', kind: 'maintenance' },
  // Protocol hardening moves to security
  'disable-llmnr': { group: 'security', subgroup: 'network-hardening', kind: 'security-hardening' },
  'disable-netbios': { group: 'security', subgroup: 'network-hardening', kind: 'security-hardening' },
  'disable-smb1': { group: 'security', subgroup: 'smb', kind: 'security-hardening' },
  'require-network-level-auth': { group: 'security', subgroup: 'rdp-remote', kind: 'security-hardening' },
  'disable-wpad': { group: 'security', subgroup: 'attack-surface', kind: 'security-hardening' },
  'disable-peer-name-resolution': { group: 'security', subgroup: 'attack-surface', kind: 'security-hardening' },
  'restrict-point-and-print': { group: 'security', subgroup: 'driver-security', kind: 'security-hardening' },
  'disable-ssdp-discovery': { group: 'security', subgroup: 'attack-surface', kind: 'security-hardening' },
  'disable-upnp-device-host': { group: 'security', subgroup: 'attack-surface', kind: 'security-hardening' },

  // ===================== INPUT =====================
  'mouse-acceleration': { group: 'performance', subgroup: 'input', kind: 'optimization' },
  'keyboard-rate': { group: 'tweaks', subgroup: 'quality-of-life', kind: 'cosmetic' },
  'touchpad-latency': { group: 'performance', subgroup: 'input', kind: 'optimization' },
  'mouse-polling': { group: 'gaming', subgroup: 'input-gaming', kind: 'guidance' },
  'menu-delay': { group: 'tweaks', subgroup: 'quality-of-life', kind: 'cosmetic' },
  'inactive-window-scroll': { group: 'tweaks', subgroup: 'quality-of-life', kind: 'cosmetic' },
  'disable-sticky-keys': { group: 'tweaks', subgroup: 'quality-of-life', kind: 'cosmetic' },
  'disable-usb-suspend': { group: 'performance', subgroup: 'input', kind: 'optimization' },
  'timer-resolution-0-5ms': { group: 'experimental', subgroup: 'advanced-power', kind: 'diagnostic', sessionAction: true, nonPersistent: true },
  'enable-mouse-raw-input': { group: 'gaming', subgroup: 'input-gaming', kind: 'guidance' },
  'disable-keyboard-filter': { group: 'gaming', subgroup: 'input-gaming', kind: 'guidance' },
  'disable-filter-keys': { group: 'tweaks', subgroup: 'quality-of-life', kind: 'cosmetic' },
  'disable-toggle-keys': { group: 'tweaks', subgroup: 'quality-of-life', kind: 'cosmetic' },
  'disable-touch-keyboard-autoinvoke': { group: 'tweaks', subgroup: 'quality-of-life', kind: 'cosmetic' },
  'disable-controller-gamebar-chord': { group: 'gaming', subgroup: 'windows-gaming', kind: 'optimization' },
  'disable-touchpad-edge-swipes': { group: 'tweaks', subgroup: 'quality-of-life', kind: 'cosmetic' },
  'disable-touchpad-threefinger-slide': { group: 'tweaks', subgroup: 'quality-of-life', kind: 'cosmetic' },
  'disable-windows-ink': { group: 'system', subgroup: 'windows-features', kind: 'maintenance' },
  'numlock-on-boot': { group: 'tweaks', subgroup: 'quality-of-life', kind: 'cosmetic' },
  'disable-hover-checkboxes': { group: 'tweaks', subgroup: 'explorer-ui', kind: 'cosmetic' },
  'disable-tablet-input-service': { group: 'system', subgroup: 'services', kind: 'maintenance' },

  // ===================== TWEAKS (cosmetics) =====================
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
  'disable-background-apps': { group: 'performance', subgroup: 'cpu', kind: 'optimization' },
  'disable-start-menu-suggestions': { group: 'privacy', subgroup: 'advertising', kind: 'privacy-control' },
  'show-seconds-clock': { group: 'tweaks', subgroup: 'taskbar', kind: 'cosmetic' },
  'hide-meet-now': { group: 'tweaks', subgroup: 'taskbar', kind: 'cosmetic' },
  'delay-taskbar-thumbnails': { group: 'tweaks', subgroup: 'taskbar', kind: 'cosmetic' },
  'hide-start-recommended': { group: 'tweaks', subgroup: 'taskbar', kind: 'cosmetic' },
  'disable-drag-full-window': { group: 'tweaks', subgroup: 'cosmetic', kind: 'cosmetic' },
  'never-combine-taskbar-icons': { group: 'tweaks', subgroup: 'taskbar', kind: 'cosmetic' },
  'disable-window-shake': { group: 'tweaks', subgroup: 'quality-of-life', kind: 'cosmetic' },
  'disable-snap-layouts-flyout': { group: 'tweaks', subgroup: 'taskbar', kind: 'cosmetic' },
  'disable-window-arrange-drag': { group: 'tweaks', subgroup: 'quality-of-life', kind: 'cosmetic' },
  'disable-spotlight-wallpapers': { group: 'privacy', subgroup: 'personalization', kind: 'privacy-control' },
  'hide-copilot-button': { group: 'tweaks', subgroup: 'taskbar', kind: 'cosmetic' },

  // ===================== POWERFUL → redistributed =====================
  'power-plan': { group: 'performance', subgroup: 'cpu', kind: 'optimization' },
  'gaming-mode': { group: 'gaming', subgroup: 'windows-gaming', kind: 'optimization' },
  'disable-services': { group: 'system', subgroup: 'services', kind: 'maintenance' },
  'registry-cleanup': { group: 'repair', subgroup: 'troubleshooting', kind: 'maintenance', repeatableAction: true },
  // Memory compression is diagnostics/experimental, never a default gaming tweak (#5)
  'memory-compression': { group: 'experimental', subgroup: 'diagnostics', kind: 'diagnostic' },
  'disable-hibernation': { group: 'system', subgroup: 'maintenance', kind: 'maintenance' },
  'disable-fast-startup': { group: 'system', subgroup: 'maintenance', kind: 'maintenance' },
  'enable-hags': { group: 'gaming', subgroup: 'gpu-gaming', kind: 'optimization' },
  'disable-bits': { group: 'system', subgroup: 'services', kind: 'maintenance' },
  'disable-game-dvr': { group: 'gaming', subgroup: 'windows-gaming', kind: 'optimization' },
  'disable-power-throttling': { group: 'performance', subgroup: 'cpu', kind: 'optimization' },
  'enable-msi-gpu': { group: 'gaming', subgroup: 'gpu-gaming', kind: 'guidance' },
  'disable-cpu-idle': { group: 'experimental', subgroup: 'advanced-power', kind: 'optimization' },
  'enable-core-parking': { group: 'experimental', subgroup: 'advanced-power', kind: 'optimization' },
  'disable-memory-dumps': { group: 'system', subgroup: 'maintenance', kind: 'maintenance' },
  'enable-long-paths': { group: 'system', subgroup: 'windows-features', kind: 'maintenance' },
  // Global FSO/MPO disables are troubleshooting tools for specific GPU issues (#12)
  'disable-fullscreen-optimizations': { group: 'repair', subgroup: 'troubleshooting', kind: 'repair-action' },
  'optimize-thread-scheduling': { group: 'performance', subgroup: 'cpu', kind: 'optimization' },
  'disable-svchost-split-threshold': { group: 'experimental', subgroup: 'contested', kind: 'optimization' },
  'optimize-ntfs-memory-usage': { group: 'performance', subgroup: 'storage', kind: 'optimization' },
  'disable-modern-standby': { group: 'experimental', subgroup: 'hardware-experimental', kind: 'optimization' },
  'disable-edge-startup-boost': { group: 'system', subgroup: 'maintenance', kind: 'maintenance' },
  'disable-automatic-maintenance': { group: 'system', subgroup: 'maintenance', kind: 'maintenance' },
  'disable-app-readiness': { group: 'system', subgroup: 'services', kind: 'maintenance' },
  'disable-ssl-time-seeding': { group: 'experimental', subgroup: 'contested', kind: 'optimization' },
  'max-system-responsiveness': { group: 'performance', subgroup: 'cpu', kind: 'optimization' },
  // HVCI/Memory Integrity is a CRITICAL security modification (#6)
  'disable-memory-integrity': {
    group: 'security',
    subgroup: 'hvci-vbs',
    kind: 'security-tradeoff',
    sessionAction: false,
  },
  'windowed-games-optimization': { group: 'gaming', subgroup: 'display-gaming', kind: 'optimization' },
  'disable-multiplane-overlay': { group: 'repair', subgroup: 'troubleshooting', kind: 'repair-action' },
  'static-pagefile': { group: 'experimental', subgroup: 'hardware-experimental', kind: 'optimization' },

  // ===================== PRIVACY =====================
  'disable-advertising-id': { group: 'privacy', subgroup: 'advertising', kind: 'privacy-control' },
  'disable-tailored-experiences': { group: 'privacy', subgroup: 'advertising', kind: 'privacy-control' },
  'disable-activity-history': { group: 'privacy', subgroup: 'telemetry', kind: 'privacy-control' },
  'disable-location-tracking': { group: 'privacy', subgroup: 'location', kind: 'privacy-control' },
  'disable-windows-feedback': { group: 'privacy', subgroup: 'telemetry', kind: 'privacy-control' },
  'remove-onedrive': { group: 'system', subgroup: 'windows-features', kind: 'maintenance' },
  'disable-cloud-content': { group: 'privacy', subgroup: 'advertising', kind: 'privacy-control' },
  'disable-app-suggestions': { group: 'privacy', subgroup: 'advertising', kind: 'privacy-control' },
  'disable-start-tracking': { group: 'privacy', subgroup: 'personalization', kind: 'privacy-control' },
  'disable-setting-sync': { group: 'privacy', subgroup: 'personalization', kind: 'privacy-control' },
  'disable-input-personalization': { group: 'privacy', subgroup: 'telemetry', kind: 'privacy-control' },
  'disable-handwriting-data': { group: 'privacy', subgroup: 'telemetry', kind: 'privacy-control' },
  'disable-speech-recognition': { group: 'privacy', subgroup: 'telemetry', kind: 'privacy-control' },
  'disable-find-my-device': { group: 'privacy', subgroup: 'location', kind: 'privacy-control' },
  'disable-contacts-access': { group: 'privacy', subgroup: 'permissions', kind: 'privacy-control' },
  'disable-calendar-access': { group: 'privacy', subgroup: 'permissions', kind: 'privacy-control' },
  'disable-camera-access': { group: 'privacy', subgroup: 'permissions', kind: 'privacy-control' },
  'disable-microphone-access': { group: 'privacy', subgroup: 'permissions', kind: 'privacy-control' },
  'disable-welcome-experience': { group: 'privacy', subgroup: 'advertising', kind: 'privacy-control' },
  'disable-clipboard-history': { group: 'privacy', subgroup: 'permissions', kind: 'privacy-control' },
  'disable-clipboard-cloud-sync': { group: 'privacy', subgroup: 'telemetry', kind: 'privacy-control' },
  'deny-user-account-information': { group: 'privacy', subgroup: 'permissions', kind: 'privacy-control' },
  'deny-documents-library': { group: 'privacy', subgroup: 'permissions', kind: 'privacy-control' },
  'deny-pictures-library': { group: 'privacy', subgroup: 'permissions', kind: 'privacy-control' },
  'deny-videos-library': { group: 'privacy', subgroup: 'permissions', kind: 'privacy-control' },
  'deny-email-access': { group: 'privacy', subgroup: 'permissions', kind: 'privacy-control' },
  'deny-radios-access': { group: 'privacy', subgroup: 'permissions', kind: 'privacy-control' },
  'deny-human-presence': { group: 'privacy', subgroup: 'permissions', kind: 'privacy-control' },
  'deny-broad-filesystem': { group: 'privacy', subgroup: 'permissions', kind: 'privacy-control' },
  'disable-click-to-do': { group: 'privacy', subgroup: 'telemetry', kind: 'privacy-control' },
};

/** Legacy UI categories kept in sync so the existing views keep rendering. */
export const legacyCategoryByGroup: Record<string, string> = {
  performance: 'Powerful',
  system: 'System',
  security: 'System',
  privacy: 'Privacy',
  gaming: 'Powerful',
  repair: 'System',
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
    entry.group === 'repair';
}
