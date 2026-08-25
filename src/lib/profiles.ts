/**
 * Predefined optimization profiles (v2).
 *
 * Profiles are INTENT LISTS, not blind execution scripts: the adaptive
 * planner (/api/profiles/plan) resolves each ID against the live machine
 * and drops or defers anything that is not applicable. Security trade-offs
 * are never auto-applied by profiles.
 */

export interface OptimizationProfileDefinition {
  id: string;
  nameKey: string;
  descriptionKey: string;
  icon: string;
  color: string;
  gradient: string;
  glowColor: string;
  optimizationIds: string[];
  category: string;
}

export const predefinedProfiles: OptimizationProfileDefinition[] = [
  {
    id: 'valorant',
    nameKey: 'profileValorantName',
    descriptionKey: 'profileValorantDesc',
    icon: 'Crosshair',
    color: '#ff4655',
    gradient: 'linear-gradient(135deg, #ff4655 0%, #bd3944 100%)',
    glowColor: 'rgba(255, 70, 85, 0.3)',
    optimizationIds: [
      'power-plan',
      'gaming-mode',
      'enable-hags',
      'max-system-responsiveness',
      'windowed-games-optimization',
      'disable-multiplane-overlay',
      'static-pagefile',
      'memory-compression',
      'disable-cpu-idle',
      'enable-core-parking',
      'disable-power-throttling',
      'disable-superfetch',
      'disable-automatic-maintenance',
      'disable-game-dvr',
      'disable-xbox-gamebar',
      'disable-fullscreen-optimizations',
      'disable-edge-startup-boost',
      'disable-widgets',
      'hide-copilot-button',
      'disable-recall',
      'disable-bits',
      'disable-delivery-optimization',
      'disable-network-throttling',
      'optimize-network-power',
      'disable-background-apps',
      'disable-active-probing',
    ],
    category: 'performance',
  },
  {
    id: 'fortnite',
    nameKey: 'profileFortniteName',
    descriptionKey: 'profileFortniteDesc',
    icon: 'Zap',
    color: '#8b5cf6',
    gradient: 'linear-gradient(135deg, #8b5cf6 0%, #6d28d9 100%)',
    glowColor: 'rgba(139, 92, 246, 0.3)',
    // NOTE: disable-memory-integrity was REMOVED on purpose (#6):
    // security trade-offs are never part of gaming profiles.
    optimizationIds: [
      'power-plan',
      'gaming-mode',
      'enable-hags',
      'max-system-responsiveness',
      'windowed-games-optimization',
      'disable-multiplane-overlay',
      'static-pagefile',
      'memory-compression',
      'disable-cpu-idle',
      'enable-core-parking',
      'disable-power-throttling',
      'disable-superfetch',
      'disable-automatic-maintenance',
      'disable-game-dvr',
      'disable-xbox-gamebar',
      'disable-fullscreen-optimizations',
      'disable-edge-startup-boost',
      'disable-widgets',
      'hide-copilot-button',
      'disable-recall',
      'disable-bits',
      'disable-delivery-optimization',
      'disable-network-throttling',
      'optimize-network-power',
      'disable-background-apps',
      'disable-hotspot-service',
      'disable-peer-name-resolution',
    ],
    category: 'performance',
  },
  {
    id: 'clean-system',
    nameKey: 'profileCleanSystemName',
    descriptionKey: 'profileCleanSystemDesc',
    icon: 'Shield',
    color: '#14b8a6',
    gradient: 'linear-gradient(135deg, #14b8a6 0%, #0f766e 100%)',
    glowColor: 'rgba(20, 184, 184, 0.3)',
    optimizationIds: [
      'disable-telemetry',
      'disable-cortana',
      'disable-search-indexing',
      'disable-superfetch',
      'disable-ceip-tasks',
      'disable-error-reporting',
      'disable-delivery-optimization',
      'disable-windows-insider',
      'disable-retail-demo',
      'disable-advertising-id',
      'disable-tailored-experiences',
      'disable-activity-history',
      'disable-windows-feedback',
      'disable-cloud-content',
      'disable-app-suggestions',
      'disable-start-tracking',
      'disable-setting-sync',
      'disable-input-personalization',
      'disable-handwriting-data',
      'disable-speech-recognition',
      'disable-find-my-device',
      'disable-welcome-experience',
      'disable-clipboard-history',
      'disable-clipboard-cloud-sync',
    ],
    category: 'privacy',
  },
  {
    id: 'gaming',
    nameKey: 'profileGamingName',
    descriptionKey: 'profileGamingDesc',
    icon: 'Gamepad2',
    color: '#ef4444',
    gradient: 'linear-gradient(135deg, #ef4444 0%, #dc2626 100%)',
    glowColor: 'rgba(239, 68, 68, 0.3)',
    optimizationIds: [
      'gaming-mode',
      'mouse-acceleration',
      'keyboard-rate',
      'disable-sticky-keys',
      'disable-filter-keys',
      'disable-controller-gamebar-chord',
    ].filter((id) => id !== 'usb-selective-suspend-off-placeholder'),
    category: 'performance',
  },
  {
    id: 'productivity',
    nameKey: 'profileProductivityName',
    descriptionKey: 'profileProductivityDesc',
    icon: 'Briefcase',
    color: '#3b82f6',
    gradient: 'linear-gradient(135deg, #3b82f6 0%, #2563eb 100%)',
    glowColor: 'rgba(59, 130, 246, 0.3)',
    optimizationIds: [
      'animations',
      'transparency',
      'speedup-shutdown',
      'no-auto-reboot-active',
      'show-file-extensions',
      'disable-start-menu-suggestions',
      'disable-taskbar-search',
      'hide-copilot-button',
    ],
    category: 'balanced',
  },
];
