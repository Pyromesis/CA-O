import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import { 
  OptimizationItem, 
  Category, 
  AppSettings, 
  Language, 
  ThemeMode,
  TroubleshootItem,
  OptimizationProfile
} from '@/types/optimization';
import { predefinedProfiles } from '@/components/ca-o/ProfileSelector';
import { isExecutableOptimizationId, sessionScopedOptimizationIds, realCommands, irreversibleOptimizationIds } from '@/lib/optimization-commands';
import { getTaxonomy } from '@/lib/catalog/taxonomy';
import { apiFetch } from '@/lib/session-client';

// History entry type
export interface HistoryEntry {
  id: string;
  nameKey: string;
  action: 'applied' | 'reverted' | 'pending';
  timestamp: number;
}

export interface TroubleshootResult {
  status: 'completed' | 'partial' | 'failed';
  issuesFound: number;
  issuesFixed: number;
  steps: Array<{ step: number; name: string; status: string; message: string; duration?: number }>;
  recommendations: string[];
  rebootRequired: boolean;
}

// Profile type (imported from component for reusability)
// Re-export for convenience
export type { OptimizationProfile };

const defaultOptimizations: OptimizationItem[] = Object.keys(realCommands)
  .filter(isExecutableOptimizationId)
  .map((id) => {
    const category = (() => {
      if (['disable-telemetry', 'disable-cortana', 'disable-search-indexing', 'disable-superfetch', 'disable-print-spooler', 'disable-xbox-gamebar', 'optimize-startup', 'clear-temp-files', 'disable-error-reporting', 'disable-delivery-optimization', 'disable-windows-insider', 'disable-retail-demo', 'disable-widgets', 'disable-recall', 'disable-ceip-tasks', 'disable-last-access-time', 'disable-8dot3-names', 'disable-admin-shares', 'disable-remote-assistance', 'disable-remote-desktop', 'speedup-shutdown', 'no-auto-reboot-active', 'disable-driver-search', 'uninstall-copilot', 'uninstall-bing-search', 'disable-paint-ai'].includes(id)) return Category.System;
      if (['dns-optimization', 'winsock-reset', 'flush-dns', 'reset-network', 'disable-llmnr', 'disable-network-throttling', 'optimize-network-power', 'disable-netbios', 'disable-smb1', 'flush-arp-cache', 'disable-hotspot-service', 'require-network-level-auth', 'disable-wpad', 'disable-active-probing', 'disable-peer-name-resolution', 'restrict-point-and-print', 'disable-ssdp-discovery', 'disable-upnp-device-host', 'disable-snmp-trap'].includes(id)) return Category.Network;
      if (['mouse-acceleration', 'keyboard-rate', 'touchpad-latency', 'mouse-polling', 'menu-delay', 'inactive-window-scroll', 'disable-sticky-keys', 'disable-usb-suspend', 'timer-resolution-0-5ms', 'enable-mouse-raw-input', 'disable-keyboard-filter', 'disable-filter-keys', 'disable-toggle-keys', 'disable-touch-keyboard-autoinvoke', 'disable-controller-gamebar-chord', 'disable-touchpad-edge-swipes', 'disable-touchpad-threefinger-slide', 'disable-windows-ink', 'numlock-on-boot', 'disable-hover-checkboxes', 'disable-tablet-input-service'].includes(id)) return Category.Input;
      if (['animations', 'transparency', 'shadows', 'taskbar-icons', 'notifications', 'show-file-extensions', 'disable-thumbnails', 'disable-tooltips', 'disable-wallpaper-slideshow', 'disable-system-sounds', 'show-hidden-files', 'hide-task-view', 'disable-taskbar-search', 'disable-lock-screen', 'disable-aero-peek', 'disable-startup-sound', 'disable-cast-notifications', 'disable-background-apps', 'disable-start-menu-suggestions', 'show-seconds-clock', 'hide-meet-now', 'delay-taskbar-thumbnails', 'hide-start-recommended', 'disable-drag-full-window', 'never-combine-taskbar-icons', 'disable-window-shake', 'disable-snap-layouts-flyout', 'disable-window-arrange-drag', 'disable-spotlight-wallpapers', 'hide-copilot-button'].includes(id)) return Category.Tweaks;
      if (['power-plan', 'gaming-mode', 'disable-services', 'registry-cleanup', 'memory-compression', 'disable-hibernation', 'disable-fast-startup', 'enable-hags', 'disable-bits', 'disable-game-dvr', 'disable-power-throttling', 'enable-msi-gpu', 'disable-cpu-idle', 'enable-core-parking', 'disable-memory-dumps', 'enable-long-paths', 'disable-fullscreen-optimizations', 'optimize-thread-scheduling', 'disable-svchost-split-threshold', 'optimize-ntfs-memory-usage', 'disable-modern-standby', 'disable-edge-startup-boost', 'disable-automatic-maintenance', 'disable-app-readiness', 'disable-ssl-time-seeding', 'max-system-responsiveness', 'disable-memory-integrity', 'windowed-games-optimization', 'disable-multiplane-overlay', 'static-pagefile'].includes(id)) return Category.Powerful;
      return Category.Privacy;
    })();

    const nameKey = `tweak${id
      .split('-')
      .map((segment) => segment.charAt(0).toUpperCase() + segment.slice(1))
      .join('')}`;

    return {
      id,
      nameKey,
      descriptionKey: `${nameKey}Desc`,
      category,
      icon: (() => {
        if (category === Category.System) return 'Shield';
        if (category === Category.Network) return 'Wifi';
        if (category === Category.Input) return 'MousePointer2';
        if (category === Category.Tweaks) return 'Sparkles';
        if (category === Category.Powerful) return 'Zap';
        return 'Lock';
      })(),
      isEnabled: false,
      isApplied: false,
      requiresRestart: realCommands[id].rebootRequired,
      riskLevel: irreversibleOptimizationIds.has(id) ? 'high' : realCommands[id].rebootRequired ? 'medium' : 'safe',
      registryPath: undefined,
      registryValue: undefined,
      command: realCommands[id].commands[0]?.script?.trim(),
    };
  });

// Default troubleshooting items
const defaultTroubleshootItems: TroubleshootItem[] = [
  {
    id: 'restore-audio',
    nameKey: 'restoreAudio',
    descriptionKey: 'restoreAudioDesc',
    icon: 'Volume2',
    action: 'restore-audio'
  },
  {
    id: 'restore-bluetooth',
    nameKey: 'restoreBluetooth',
    descriptionKey: 'restoreBluetoothDesc',
    icon: 'Bluetooth',
    action: 'restore-bluetooth'
  },
  {
    id: 'restore-network',
    nameKey: 'restoreNetwork',
    descriptionKey: 'restoreNetworkDesc',
    icon: 'Wifi',
    action: 'restore-network'
  },
  {
    id: 'restore-windows-update',
    nameKey: 'restoreWindowsUpdate',
    descriptionKey: 'restoreWindowsUpdateDesc',
    icon: 'Download',
    action: 'restore-windows-update'
  },
  {
    id: 'restore-display',
    nameKey: 'restoreDisplay',
    descriptionKey: 'restoreDisplayDesc',
    icon: 'Monitor',
    action: 'restore-display'
  },
  {
    id: 'create-restore-point',
    nameKey: 'createRestorePoint',
    descriptionKey: 'createRestorePointDesc',
    icon: 'Save',
    action: 'create-restore-point'
  },
  {
    id: 'restore-all',
    nameKey: 'restoreAll',
    descriptionKey: 'restoreAllDesc',
    icon: 'Undo2',
    action: 'restore-all'
  },
  {
    id: 'repair-system-files',
    nameKey: 'repairSystemFiles',
    descriptionKey: 'repairSystemFilesDesc',
    icon: 'Wrench',
    action: 'repair-system-files'
  },
  {
    id: 'reset-store-cache',
    nameKey: 'resetStoreCache',
    descriptionKey: 'resetStoreCacheDesc',
    icon: 'Package',
    action: 'reset-store-cache'
  },
  {
    id: 'restart-explorer',
    nameKey: 'restartExplorer',
    descriptionKey: 'restartExplorerDesc',
    icon: 'RefreshCw',
    action: 'restart-explorer'
  },
  {
    id: 'flush-dns-cache',
    nameKey: 'flushDnsCache',
    descriptionKey: 'flushDnsCacheDesc',
    icon: 'Globe',
    action: 'flush-dns-cache'
  },
  {
    id: 'clean-temp-junk',
    nameKey: 'cleanTempJunk',
    descriptionKey: 'cleanTempJunkDesc',
    icon: 'FolderOpen',
    action: 'clean-temp-junk'
  },
];

interface AppState {
  // Navigation
  currentView: 'splash' | 'main' | 'dashboard' | 'optimization' | 'troubleshooting' | 'settings';
  selectedCategory: Category | null;
  
  // Data
  optimizations: OptimizationItem[];
  troubleshootItems: TroubleshootItem[];
  
  // History
  history: HistoryEntry[];
  
  // Settings
  settings: AppSettings;
  
  // Onboarding
  showOnboarding: boolean;
  onboardingCompleted: boolean;
  
  // UI State
  isLoading: boolean;
  isProcessing: boolean;
  showHelpModal: boolean;
  showHistoryPanel: boolean;
  
  // Profile State
  profiles: OptimizationProfile[];
  selectedProfile: string | null;
  
  // Actions
  setCurrentView: (view: AppState['currentView']) => void;
  setSelectedCategory: (category: Category | null) => void;
  
  toggleOptimization: (id: string) => void;
  applyOptimization: (id: string, createBackup?: boolean, confirmDangerous?: boolean) => Promise<void>;
  applyAllInCategory: (category: Category, createBackup?: boolean) => Promise<void>;
  revertOptimization: (id: string) => Promise<void>;
  revertAll: () => Promise<void>;
  
  executeTroubleshoot: (action: string) => Promise<TroubleshootResult>;
  
  // History Actions
  addToHistory: (id: string, nameKey: string, action: 'applied' | 'reverted' | 'pending') => void;
  clearHistory: () => void;
  
  updateSettings: (settings: Partial<AppSettings>) => void;
  
  setLoading: (loading: boolean) => void;
  setProcessing: (processing: boolean) => void;
  setShowHelpModal: (show: boolean) => void;
  setShowHistoryPanel: (show: boolean) => void;
  
  // Onboarding Actions
  completeOnboarding: () => void;
  setShowOnboarding: (show: boolean) => void;
  
  // Profile Actions
  setSelectedProfile: (profileId: string | null) => void;
  applyProfile: (profileId: string, onProgress?: (done: number, total: number, current: string) => void) => Promise<void>;
  
  getOptimizationsByCategory: (category: Category) => OptimizationItem[];
  getAppliedCount: () => number;
  getTotalCount: () => number;
  hydrateFromDB: () => Promise<void>;
  loadOptimizations: () => Promise<void>;
}

export const useAppStore = create<AppState>()(
  persist(
    (set, get) => ({
      // Initial State
      currentView: 'dashboard',
      selectedCategory: null,
      
      optimizations: defaultOptimizations.filter((optimization) => isExecutableOptimizationId(optimization.id)),
      troubleshootItems: defaultTroubleshootItems,
      
      settings: {
        theme: 'dark',
        language: 'es',
        autoApplySafeTweaks: false,
        confirmBeforeApply: true,
        showNotifications: true,
        soundEffectsEnabled: true
      },
      
      isLoading: false,
      isProcessing: false,
      showHelpModal: false,
      showHistoryPanel: false,
      showOnboarding: false,
      onboardingCompleted: false,
      history: [],
      
      // Profiles
      profiles: predefinedProfiles,
      selectedProfile: null,
      
      // Navigation Actions
      setCurrentView: (view) => set({ currentView: view }),
      setSelectedCategory: (category) => set({ selectedCategory: category }),
      
      // Optimization Actions
      toggleOptimization: (id) => {
        set((state) => ({
          optimizations: state.optimizations.map((opt) =>
            opt.id === id ? { ...opt, isEnabled: !opt.isEnabled } : opt
          )
        }));
      },
      
      applyOptimization: async (id, createBackup = false, confirmDangerous = false) => {
        set({ isProcessing: true });
        const state = get();
        const optimization = state.optimizations.find(opt => opt.id === id);

        try {
          // Call API to apply optimization
          const response = await apiFetch('/api/optimization/apply', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ optimizationId: id, createBackup, confirmDangerous })
          });

          const responseData = await response.json();

          if (responseData.success) {
            set((state) => ({
              optimizations: state.optimizations.map((opt) =>
                opt.id === id ? { ...opt, isApplied: true, isEnabled: true } : opt
              ),
              isProcessing: false,
              history: [
                {
                  id,
                  nameKey: optimization?.nameKey || id,
                  action: 'applied' as const,
                  timestamp: Date.now()
                },
                ...state.history
              ].slice(0, 100)
            }));
          } else {
            console.error('API returned error:', responseData.error);
            set({ isProcessing: false });
            throw new Error(responseData.message || responseData.error || 'Failed to apply optimization');
          }
        } catch (error) {
          console.error('Error applying optimization:', error);
          set({ isProcessing: false });
          throw error;
        }
      },
      
      applyAllInCategory: async (category, createBackup = false) => {
        set({ isProcessing: true });
        const state = get();
        const categoryOpts = state.optimizations.filter(
          (opt) => opt.category === category && !opt.isApplied
        );

        const appliedIds = categoryOpts.map((o) => o.id);

        if (appliedIds.length === 0) {
          set({ isProcessing: false });
          return;
        }

        try {
          const response = await apiFetch('/api/optimization/apply-all', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ ids: appliedIds, createBackup })
          });

          const responseData = await response.json();

          if (response.ok) {
            const appliedIdsFromApi = new Set(
              (responseData.data?.appliedOptimizations || [])
                .filter((item: { success?: boolean }) => item.success)
                .map((item: { id: string }) => item.id)
            );
            const appliedOpts = categoryOpts.filter(opt => appliedIdsFromApi.has(opt.id));
            const newHistoryEntries: HistoryEntry[] = appliedOpts.map(opt => ({
              id: opt.id,
              nameKey: opt.nameKey,
              action: 'applied' as const,
              timestamp: Date.now()
            }));
            set((state) => ({
              optimizations: state.optimizations.map((opt) =>
                appliedIdsFromApi.has(opt.id)
                  ? { ...opt, isApplied: true, isEnabled: true }
                  : opt
              ),
              isProcessing: false,
              history: [...newHistoryEntries, ...state.history].slice(0, 100)
            }));

            if (!responseData.success) {
              throw new Error(responseData.error || 'Some optimizations could not be applied');
            }
          } else {
            console.error('API returned error:', responseData.error);
            set({ isProcessing: false });
            const failedItems = (responseData.data?.appliedOptimizations || [])
              .filter((item: { success?: boolean }) => !item.success)
              .map((item: { id?: string; error?: string }) => `${item.id || 'unknown'}: ${item.error || 'unknown error'}`)
              .join('; ');
            throw new Error(responseData.message || failedItems || responseData.error || 'Failed to apply optimizations');
          }
        } catch (error) {
          console.error('Error applying optimizations:', error);
          set({ isProcessing: false });
          throw error;
        }
      },
      
      revertOptimization: async (id) => {
        set({ isProcessing: true });
        const state = get();
        const optimization = state.optimizations.find(opt => opt.id === id);

        try {
          const response = await apiFetch('/api/optimization/revert', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ optimizationId: id })
          });

          const responseData = await response.json();

          if (responseData.success) {
            set((state) => ({
              optimizations: state.optimizations.map((opt) =>
                opt.id === id ? { ...opt, isApplied: false, isEnabled: false } : opt
              ),
              isProcessing: false,
              history: [
                {
                  id,
                  nameKey: optimization?.nameKey || id,
                  action: 'reverted' as const,
                  timestamp: Date.now()
                },
                ...state.history
              ].slice(0, 100)
            }));
          } else {
            console.error('API returned error:', responseData.error);
            set({ isProcessing: false });
            throw new Error(responseData.error || 'Failed to revert optimization');
          }
        } catch (error) {
          console.error('Error reverting optimization:', error);
          set({ isProcessing: false });
          throw error;
        }
      },
      
      revertAll: async () => {
        set({ isProcessing: true });
        const state = get();
        const appliedOpts = state.optimizations.filter(opt => opt.isApplied);
        
        const appliedIds = appliedOpts.map(o => o.id);
        
        if (appliedIds.length === 0) {
          set({ isProcessing: false });
          return;
        }
        
        try {
          const response = await apiFetch('/api/optimization/revert-all', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ ids: appliedIds })
          });

          const responseData = await response.json();
          if (!response.ok) {
            throw new Error(responseData.error || 'Failed to revert optimizations');
          }

          const revertedIds = new Set(
            (responseData.data?.revertedOptimizations || [])
              .filter((item: { success?: boolean }) => item.success)
              .map((item: { id: string }) => item.id)
          );
          const revertedOpts = appliedOpts.filter(opt => revertedIds.has(opt.id));
          
          const newHistoryEntries: HistoryEntry[] = revertedOpts.map(opt => ({
            id: opt.id,
            nameKey: opt.nameKey,
            action: 'reverted' as const,
            timestamp: Date.now()
          }));
          
          set((state) => ({
            optimizations: state.optimizations.map((opt) => ({
              ...opt,
              isApplied: revertedIds.has(opt.id) ? false : opt.isApplied,
              isEnabled: revertedIds.has(opt.id) ? false : opt.isEnabled
            })),
            isProcessing: false,
            history: [...newHistoryEntries, ...state.history].slice(0, 100)
          }));

          if (!responseData.success) {
            throw new Error(responseData.error || 'Some optimizations could not be reverted');
          }
        } catch (error) {
          console.error('Error reverting all:', error);
          set({ isProcessing: false });
          throw error;
        }
      },
      
      revertAllInCategory: async (category) => {
        set({ isProcessing: true });
        const state = get();
        const categoryOpts = state.optimizations.filter(
          (opt) => opt.category === category && opt.isApplied
        );
        
        const appliedIds = categoryOpts.map((o) => o.id);
        
        if (appliedIds.length === 0) {
          set({ isProcessing: false });
          return;
        }
        
        try {
          const response = await apiFetch('/api/optimization/revert-all', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ ids: appliedIds })
          });

          const responseData = await response.json();
          if (!response.ok) {
            throw new Error(responseData.error || 'Failed to revert optimizations');
          }

          const revertedIds = new Set(
            (responseData.data?.revertedOptimizations || [])
              .filter((item: { success?: boolean }) => item.success)
              .map((item: { id: string }) => item.id)
          );
          const revertedOpts = categoryOpts.filter(opt => revertedIds.has(opt.id));
          
          const newHistoryEntries: HistoryEntry[] = revertedOpts.map(opt => ({
            id: opt.id,
            nameKey: opt.nameKey,
            action: 'reverted' as const,
            timestamp: Date.now()
          }));
          set((state) => ({
            optimizations: state.optimizations.map((opt) =>
              revertedIds.has(opt.id)
                ? { ...opt, isApplied: false, isEnabled: false }
                : opt
            ),
            isProcessing: false,
            history: [...newHistoryEntries, ...state.history].slice(0, 100)
          }));

          if (!responseData.success) {
            throw new Error(responseData.error || 'Some optimizations could not be reverted');
          }
        } catch (error) {
          console.error('Error reverting optimizations:', error);
          set({ isProcessing: false });
        }
      },
      
      // History Actions
      addToHistory: (id, nameKey, action) => {
        set((state) => ({
          history: [
            { id, nameKey, action, timestamp: Date.now() },
            ...state.history
          ].slice(0, 100)
        }));
      },
      clearHistory: () => {
        set({ history: [] });
      },
      
      // Troubleshooting Actions
      executeTroubleshoot: async (action) => {
        set({ isProcessing: true });
        
        try {
          const response = await apiFetch('/api/troubleshoot/execute', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ action })
          });
          
          const responseData = await response.json();
          if (!response.ok || !responseData.success) {
            throw new Error(responseData.error || 'Failed to execute troubleshoot action');
          }

          const result = responseData.data as TroubleshootResult | undefined;
          if (!result) throw new Error('Troubleshoot response did not include a result');
          if (result.status === 'failed') {
            throw new Error('Troubleshoot action failed');
          }
          return result;
        } catch (error) {
          console.error('Error executing troubleshoot:', error);
          throw error;
        } finally {
          set({ isProcessing: false });
        }
      },
      
      // Settings Actions
      updateSettings: (newSettings) => {
        set((state) => ({
          settings: { ...state.settings, ...newSettings }
        }));
      },
      
      // UI State Actions
      setLoading: (loading) => set({ isLoading: loading }),
      setProcessing: (processing) => set({ isProcessing: processing }),
      setShowHelpModal: (show) => set({ showHelpModal: show }),
      setShowHistoryPanel: (show) => set({ showHistoryPanel: show }),
      
      // Onboarding Actions
      completeOnboarding: () => {
        if (typeof window !== 'undefined') {
          localStorage.setItem('ca-o-onboarding-complete', 'true');
        }
        set({ showOnboarding: false, onboardingCompleted: true });
        // Persist the flag server-side too: the packaged app may change its
        // local origin between launches, which would drop localStorage.
        apiFetch('/api/app-state', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ onboardingCompleted: true })
        }).catch(() => {});
      },
      setShowOnboarding: (show) => set({ showOnboarding: show }),
      
      // Profile Actions
      setSelectedProfile: (profileId) => set({ selectedProfile: profileId }),
      
      applyProfile: async (profileId, onProgress) => {
        const state = get();
        const profile = state.profiles.find(p => p.id === profileId);

        if (!profile) return;

        set({ isProcessing: true, selectedProfile: profileId });

        try {
          // v2 adaptive planning: resolve the profile against the live machine.
          // Security trade-offs and non-applicable items are skipped server-side
          // with a documented reason; profiles never bypass confirmations.
          const planRes = await apiFetch('/api/profiles/plan', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ profileId })
          }).catch(() => null);

          let candidateIds = profile.optimizationIds.filter((optId) =>
            isExecutableOptimizationId(optId) &&
            !state.optimizations.find((opt) => opt.id === optId)?.isApplied
          );

          if (planRes?.ok) {
            const planJson = await planRes.json().catch(() => null);
            const planItems: Array<{ id: string; status: string }> = planJson?.data?.plan;
            if (Array.isArray(planItems)) {
              const applicableSet = new Set(
                planItems.filter((item) => item.status === 'apply').map((item) => item.id)
              );
              candidateIds = candidateIds.filter((id) => applicableSet.has(id));
            }
          } else {
            // Planner unavailable: conservative fallback that excludes
            // security trade-offs and experimental items entirely.
            candidateIds = candidateIds.filter((optId) => {
              const entry = getTaxonomy(optId);
              if (!entry) return false;
              return entry.group !== 'security' && entry.group !== 'experimental' && entry.kind !== 'security-tradeoff';
            });
          }

          if (candidateIds.length === 0) return;

          // 1) Punto de restauración único y acotado antes de tocar nada
          if (onProgress) onProgress(0, candidateIds.length, '__backup__');
          try {
            const rp = await apiFetch('/api/troubleshoot/execute', {
              method: 'POST',
              headers: { 'Content-Type': 'application/json' },
              body: JSON.stringify({ action: 'create-restore-point' })
            });
            if (!rp.ok) console.warn('Restore point failed; continuing without backup');
          } catch { /* sin backup, seguimos */ }

          // 2) Aplicación una por una con progreso en vivo. Sin confirmaciones
          // en ciego: el plan adaptativo ya filtró lo no aplicable y los
          // cambios de seguridad nunca se envían desde perfiles.
          let done = 0;
          const failed: string[] = [];
          for (const optId of candidateIds) {
            const optName = state.optimizations.find((opt) => opt.id === optId)?.nameKey || optId;
            if (onProgress) onProgress(done, candidateIds.length, optName);
            try {
              const res = await apiFetch('/api/optimization/apply', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ optimizationId: optId, createBackup: false })
              });
              const json = await res.json().catch(() => ({}));
              if (res.ok && json.success) {
                set((cs) => ({
                  optimizations: cs.optimizations.map((o) =>
                    o.id === optId ? { ...o, isApplied: true, isEnabled: true } : o
                  ),
                  history: [
                    { id: optId, nameKey: optName, action: 'applied' as const, timestamp: Date.now() },
                    ...cs.history
                  ].slice(0, 100)
                }));
              } else if (res.status === 409) {
                // Ya aplicada en otra sesión: sincronizar bandera y seguir
                set((cs) => ({
                  optimizations: cs.optimizations.map((o) =>
                    o.id === optId ? { ...o, isApplied: true, isEnabled: true } : o
                  )
                }));
              } else {
                failed.push(optId);
                set((cs) => ({
                  optimizations: cs.optimizations.map((o) =>
                    o.id === optId ? { ...o, isApplied: false, isEnabled: false } : o
                  )
                }));
              }
            } catch {
              failed.push(optId);
            }
            done++;
            if (onProgress) onProgress(done, candidateIds.length, optName);
          }

          if (failed.length > 0) {
            throw new Error(`${failed.length} optimizaciones no pudieron aplicarse: ${failed.join(', ')}`);
          }
        } finally {
          set({ isProcessing: false });
        }
      },
      
      // Helper Getters
      getOptimizationsByCategory: (category) => {
        return get().optimizations.filter((opt) => opt.category === category);
      },
      
      getAppliedCount: () => {
        return get().optimizations.filter((opt) => opt.isApplied).length;
      },
      
      getTotalCount: () => {
        return get().optimizations.length;
      },

      hydrateFromDB: async () => {
        // Persistent UI flags live server-side so they survive origin
        // (port) changes in the packaged Electron app.
        let serverOnboardingCompleted = false;
        try {
          try {
            const appStateRes = await apiFetch('/api/app-state');
            if (appStateRes.ok) {
              const appStateJson = await appStateRes.json();
              serverOnboardingCompleted = appStateJson?.data?.onboardingCompleted === true;
              if (serverOnboardingCompleted && typeof window !== 'undefined' &&
                localStorage.getItem('ca-o-onboarding-complete') !== 'true') {
                localStorage.setItem('ca-o-onboarding-complete', 'true');
              }
            }
          } catch {
            // App-state endpoint unavailable; fall back to localStorage only.
          }

          const res = await apiFetch('/api/optimization/state');
          if (!res.ok) return;
          const json = await res.json();

          // Handle both old and new response formats
          let stateMap: Record<string, boolean> = {};
          if (json.data) {
            if (typeof json.data === 'object' && !Array.isArray(json.data)) {
              // New format: { data: { optimizations: [...], appliedState: [...] } }
              if (Array.isArray(json.data.appliedState)) {
                json.data.appliedState.forEach((item: any) => {
                  if (item.id && item.appliedAt && !sessionScopedOptimizationIds.has(item.id)) {
                    stateMap[item.id] = true;
                  }
                });
              }
              // Also update optimizations from API
              if (Array.isArray(json.data.optimizations)) {
                const apiOptimizations = json.data.optimizations.map((opt: any) => {
                  const localOpt = get().optimizations.find(o => o.id === opt.id);
                  if (localOpt) {
                    return {
                      ...localOpt,
                      isApplied: sessionScopedOptimizationIds.has(opt.id) ? false : opt.isApplied || stateMap[opt.id] || false,
                      isEnabled: sessionScopedOptimizationIds.has(opt.id) ? false : opt.isApplied || stateMap[opt.id] || false
                    };
                  }
                  return null;
                }).filter(Boolean);

                set(state => ({
                  optimizations: apiOptimizations.length > 0 ? apiOptimizations : state.optimizations
                }));
              }
            } else {
              // Old format: { data: { id1: true, id2: false } }
              stateMap = json.data as Record<string, boolean>;
            }
          }

          set((state) => {
            const optimizationsUpdated = state.optimizations.map((opt) => ({
              ...opt,
              isApplied: sessionScopedOptimizationIds.has(opt.id) ? false : stateMap[opt.id] === true,
              isEnabled: sessionScopedOptimizationIds.has(opt.id) ? false : stateMap[opt.id] === true,
            }));

            // Check if this is first visit: if no optimizations are applied and no flag says otherwise
            const hasAnyApplied = optimizationsUpdated.some(opt => opt.isApplied);
            const hasSeenOnboarding = typeof window !== 'undefined' && localStorage.getItem('ca-o-onboarding-complete') === 'true';
            const onboardingCompleted = state.onboardingCompleted || hasAnyApplied || hasSeenOnboarding || serverOnboardingCompleted;

            // Mark onboarding as seen if there are applied optimizations (user has used the app)
            if (onboardingCompleted && typeof window !== 'undefined' && !hasSeenOnboarding) {
              localStorage.setItem('ca-o-onboarding-complete', 'true');
            }

            return {
              optimizations: optimizationsUpdated,
              onboardingCompleted,
              showOnboarding: !onboardingCompleted,
            };
          });
        } catch {
          // silently fail – offline or first run
          // Set showOnboarding based on localStorage and the server flag
          if (typeof window !== 'undefined') {
            const hasSeenOnboarding = localStorage.getItem('ca-o-onboarding-complete') === 'true';
            set((state) => ({
              onboardingCompleted: state.onboardingCompleted || hasSeenOnboarding || serverOnboardingCompleted,
              showOnboarding: !(state.onboardingCompleted || hasSeenOnboarding || serverOnboardingCompleted),
            }));
          }
        }
      },

      // Load optimizations from API on initialization
      loadOptimizations: async () => {
        try {
          const res = await apiFetch('/api/optimization');
          if (!res.ok) return;
          const json = await res.json();

          if (json.success && json.data?.optimizations) {
            const apiOptimizations = json.data.optimizations.map((opt: any) => {
              // Find matching local optimization by ID
              const localOpt = defaultOptimizations.find(o => o.id === opt.id);
              if (localOpt) {
                // API wins over local defaults so v2 metadata (group/kind/
                // evidence/score/applicable/blockers) reaches the UI.
                return {
                  ...localOpt,
                  ...opt,
                  isApplied: sessionScopedOptimizationIds.has(opt.id) ? false : opt.isApplied || false,
                  isEnabled: sessionScopedOptimizationIds.has(opt.id) ? false : opt.isApplied || false
                };
              }
              // Return as-is if it's a new format from the API
              return opt;
            });

            set({ optimizations: apiOptimizations });

            // Store applied state from API
            if (json.data.appliedState) {
              const appliedIds = json.data.appliedState.map((item: any) => item.id);
              set((state) => ({
                optimizations: state.optimizations.map((opt) => ({
                  ...opt,
                  isApplied: appliedIds.includes(opt.id),
                  isEnabled: appliedIds.includes(opt.id)
                }))
              }));
            }
          }
        } catch (error) {
          console.error('Error loading optimizations:', error);
        }
      }
    }),
    {
      name: 'ca-o-storage',
      partialize: (state) => ({
        optimizations: state.optimizations,
        settings: state.settings,
        history: state.history,
        onboardingCompleted: state.onboardingCompleted
      }),
      // El catálogo vive en código: al rehidratar, conserva solo las banderas
      // isApplied/isEnabled del snapshot anterior y toma el resto del catálogo
      // fresco. Así los cambios de catálogo nunca dejan la UI desincronizada.
      merge: (persisted, current) => {
        const p = (persisted ?? {}) as Partial<AppState>;
        const old = Array.isArray(p.optimizations) ? p.optimizations : [];
        const merged = current.optimizations.map((d) => {
          const o = old.find((x) => x.id === d.id);
          return o ? { ...d, isApplied: !!o.isApplied, isEnabled: !!o.isEnabled } : d;
        });
        return { ...current, ...p, optimizations: merged };
      }
    }
  )
);
