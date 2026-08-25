import { NextRequest, NextResponse } from "next/server";
import { isExecutableOptimizationId, nonExecutableOptimizationIds, nonExecutableReasonById, realCommands, verificationCommands, irreversibleOptimizationIds, securityImpactById, privacyBenefitById, antiCheatWarnings, sessionScopedOptimizationIds, performanceImpactById } from "@/lib/optimization-commands";
import { verifyWithCache } from "@/lib/verify-cache";
import { getOptimizationDetail } from "@/lib/optimization-details";
import { getTaxonomy, taxonomyById } from "@/lib/catalog/taxonomy";
import { getScoredOptimization } from "@/lib/catalog/evidence";
import { evaluateApplicability } from "@/lib/catalog/applicability";
import { getSystemContext } from "@/lib/system-context";
import type { CatalogGroup, CatalogSubgroup, ActionKind, PerformanceImpact as EvidenceImpact, Confidence, EvidenceSource, AdverseEffect } from "@/lib/catalog/types";

export type OptimizationCategory = 'system' | 'network' | 'input' | 'tweaks' | 'powerful' | 'privacy';

// Category metadata for UI
const categoryMetadata: Record<OptimizationCategory, { name: string; description: string; icon: string; color: string }> = {
  system: {
    name: "System",
    description: "System optimizations for Windows stability and performance",
    icon: "Monitor",
    color: "#3B82F6"
  },
  network: {
    name: "Network",
    description: "Network optimizations for faster connectivity",
    icon: "Wifi",
    color: "#10B981"
  },
  input: {
    name: "Input",
    description: "Input device optimizations for better responsiveness",
    icon: "MousePointer2",
    color: "#8B5CF6"
  },
  tweaks: {
    name: "Tweaks",
    description: "Visual and startup tweaks",
    icon: "Sparkles",
    color: "#F59E0B"
  },
  powerful: {
    name: "Powerful",
    description: "Advanced optimizations with more impact",
    icon: "Zap",
    color: "#EF4444"
  },
  privacy: {
    name: "Privacy",
    description: "Privacy controls for Windows data collection and advertising.",
    icon: "Shield",
    color: "#14B8A6"
  }
};

// Optimization definitions with UI-friendly format
export interface OptimizationItem {
  id: string;
  nameKey: string;
  descriptionKey: string;
  name: string;
  description: string;
  whatDoesEs: string;
  whatDoesEn: string;
  whatIsItEs: string;
  whatIsItEn: string;
  whatItAppliesEs: string;
  whatItAppliesEn: string;
  isSafe: boolean;
  securityImpact: 'none' | 'low' | 'medium' | 'high' | 'reduces-security';
  performanceImpact: 'low' | 'medium' | 'high' | 'very-high';
  category: OptimizationCategory;
  icon: string;
  isEnabled: boolean;
  isApplied: boolean;
  requiresRestart: boolean;
  riskLevel: 'safe' | 'medium' | 'high' | 'dangerous';
  reversible: boolean;
  requiresExplicitConfirmation: boolean;
  warningEs?: string;
  warningEn?: string;
  implementationEs: string;
  implementationEn: string;
  verificationCommand: string;
  commands: string[];
  privacyBenefitEs?: string;
  privacyBenefitEn?: string;
  securityExplanationEs: string;
  securityExplanationEn: string;
  performanceExplanationEs: string;
  performanceExplanationEn: string;
  limitationsEs: string;
  limitationsEn: string;
  antiCheatWarningEs?: string;
  antiCheatWarningEn?: string;
  registryPath?: string;
  registryValue?: string;
  command?: string;
  // ---- v2 evidence-based metadata ----
  group: CatalogGroup;
  subgroup: CatalogSubgroup;
  kind: ActionKind;
  expectedImpact: EvidenceImpact;
  confidence: Confidence;
  sources: EvidenceSource[];
  rationaleEs: string;
  rationaleEn: string;
  adverseEffects: AdverseEffect[];
  scoreTotal: number;
  scoreLabel: string;
  sessionAction: boolean;
  nonPersistent: boolean;
  applicable: boolean;
  blockers: Array<{ code: string; es: string; en: string }>;
  warnings: Array<{ code: string; es: string; en: string }>;
}

// Map of optimization categories to their IDs
const categoryOptimizations: Record<OptimizationCategory, string[]> = {
  system: ['disable-telemetry', 'disable-cortana', 'disable-search-indexing', 'disable-superfetch', 'disable-print-spooler', 'disable-xbox-gamebar', 'optimize-startup', 'clear-temp-files', 'disable-error-reporting', 'disable-delivery-optimization', 'disable-windows-insider', 'disable-retail-demo', 'disable-widgets', 'disable-recall', 'disable-ceip-tasks', 'disable-last-access-time', 'disable-8dot3-names', 'disable-admin-shares', 'disable-remote-assistance', 'disable-remote-desktop', 'speedup-shutdown', 'no-auto-reboot-active', 'disable-driver-search', 'uninstall-copilot', 'uninstall-bing-search', 'disable-paint-ai'],
  network: ['dns-optimization', 'winsock-reset', 'flush-dns', 'reset-network', 'disable-llmnr', 'disable-network-throttling', 'optimize-network-power', 'disable-netbios', 'disable-smb1', 'flush-arp-cache', 'disable-hotspot-service', 'require-network-level-auth', 'disable-wpad', 'disable-active-probing', 'disable-peer-name-resolution', 'restrict-point-and-print', 'disable-ssdp-discovery', 'disable-upnp-device-host', 'disable-snmp-trap'],
  input: ['mouse-acceleration', 'keyboard-rate', 'touchpad-latency', 'mouse-polling', 'menu-delay', 'inactive-window-scroll', 'disable-sticky-keys', 'disable-usb-suspend', 'timer-resolution-0-5ms', 'enable-mouse-raw-input', 'disable-keyboard-filter', 'disable-filter-keys', 'disable-toggle-keys', 'disable-touch-keyboard-autoinvoke', 'disable-controller-gamebar-chord', 'disable-touchpad-edge-swipes', 'disable-touchpad-threefinger-slide', 'disable-windows-ink', 'numlock-on-boot', 'disable-hover-checkboxes', 'disable-tablet-input-service'],
  tweaks: ['animations', 'transparency', 'shadows', 'taskbar-icons', 'notifications', 'show-file-extensions', 'disable-thumbnails', 'disable-tooltips', 'disable-wallpaper-slideshow', 'disable-system-sounds', 'show-hidden-files', 'hide-task-view', 'disable-taskbar-search', 'disable-lock-screen', 'disable-aero-peek', 'disable-startup-sound', 'disable-cast-notifications', 'disable-background-apps', 'disable-start-menu-suggestions', 'show-seconds-clock', 'hide-meet-now', 'delay-taskbar-thumbnails', 'hide-start-recommended', 'disable-drag-full-window', 'never-combine-taskbar-icons', 'disable-window-shake', 'disable-snap-layouts-flyout', 'disable-window-arrange-drag', 'disable-spotlight-wallpapers', 'hide-copilot-button'],
  powerful: ['power-plan', 'gaming-mode', 'disable-services', 'registry-cleanup', 'memory-compression', 'disable-hibernation', 'disable-fast-startup', 'enable-hags', 'disable-bits', 'disable-game-dvr', 'disable-power-throttling', 'enable-msi-gpu', 'disable-cpu-idle', 'enable-core-parking', 'disable-memory-dumps', 'enable-long-paths', 'disable-fullscreen-optimizations', 'optimize-thread-scheduling', 'disable-svchost-split-threshold', 'optimize-ntfs-memory-usage', 'disable-modern-standby', 'disable-edge-startup-boost', 'disable-automatic-maintenance', 'disable-app-readiness', 'disable-ssl-time-seeding', 'max-system-responsiveness', 'disable-memory-integrity', 'windowed-games-optimization', 'disable-multiplane-overlay', 'static-pagefile'],
  privacy: ['disable-advertising-id', 'disable-tailored-experiences', 'disable-activity-history', 'disable-location-tracking', 'disable-windows-feedback', 'remove-onedrive', 'disable-cloud-content', 'disable-app-suggestions', 'disable-start-tracking', 'disable-setting-sync', 'disable-input-personalization', 'disable-handwriting-data', 'disable-speech-recognition', 'disable-find-my-device', 'disable-contacts-access', 'disable-calendar-access', 'disable-camera-access', 'disable-microphone-access', 'disable-welcome-experience', 'disable-clipboard-history', 'disable-clipboard-cloud-sync', 'deny-user-account-information', 'deny-documents-library', 'deny-pictures-library', 'deny-videos-library', 'deny-email-access', 'deny-radios-access', 'deny-human-presence', 'deny-broad-filesystem', 'disable-click-to-do']
};

// Catálogo cacheado a nivel de módulo: los items base nunca cambian en runtime.
// Solo isApplied cambia, y eso se aplica después desde la DB.
let catalogCache: OptimizationItem[] | null = null;

// Generate full optimization items from commands
function generateOptimizations(): OptimizationItem[] {
  if (catalogCache) return catalogCache;
  const items: OptimizationItem[] = [];

  for (const [category, ids] of Object.entries(categoryOptimizations) as [OptimizationCategory, string[]][]) {
    for (const id of ids) {
      if (isExecutableOptimizationId(id)) {
        const cmdSet = realCommands[id];
        const securityImpact = securityImpactById[id] || 'none';
        const performanceImpact = performanceImpactById[id] || (irreversibleOptimizationIds.has(id) ? 'high' as const : 'medium' as const);
        const detail = getOptimizationDetail(
          id,
          category,
          securityImpact,
          performanceImpact,
          cmdSet.commands.map((command) => command.description).join('; '),
        );
        const nameKey = `tweak${id.charAt(0).toUpperCase() + id.slice(1).replace(/-/g, '')}`;
        const descriptionKey = `${nameKey}Desc`;
        const displayName = nameKey.replace(/([A-Z])/g, ' $1').replace(/^./, str => str.toUpperCase());

        items.push({
          id,
          nameKey,
          descriptionKey,
          name: displayName,
          description: descriptionKey,
          ...detail,
          isSafe: !irreversibleOptimizationIds.has(id),
          securityImpact,
          performanceImpact,
          category,
          icon: categoryMetadata[category].icon,
          isEnabled: false,
          isApplied: false,
          requiresRestart: cmdSet.rebootRequired,
          riskLevel: irreversibleOptimizationIds.has(id) ? 'dangerous' : cmdSet.rebootRequired ? 'medium' : 'safe',
          reversible: !irreversibleOptimizationIds.has(id),
          requiresExplicitConfirmation: irreversibleOptimizationIds.has(id),
          warningEs: irreversibleOptimizationIds.has(id) ? 'Esta optimización no se puede revertir automáticamente.' : id === 'timer-resolution-0-5ms' ? 'El valor depende de Windows y del hardware; no es permanente tras cerrar la aplicación o reiniciar.' : undefined,
          warningEn: irreversibleOptimizationIds.has(id) ? 'This optimization cannot be reverted automatically.' : id === 'timer-resolution-0-5ms' ? 'The value depends on Windows and hardware; it is not permanent after closing the app or restarting.' : undefined,
          implementationEs: `Ejecuta PowerShell como administrador y verifica el registro, servicio o configuración modificada. Comando: ${cmdSet.commands[0]?.description || 'No disponible'}.`,
          implementationEn: `Runs PowerShell as administrator and verifies the changed registry, service or setting. Command: ${cmdSet.commands[0]?.description || 'Not available'}.`,
          verificationCommand: verificationCommands[id],
          commands: cmdSet.commands.map((command) => command.script.trim()),
          privacyBenefitEs: privacyBenefitById[id]?.es,
          privacyBenefitEn: privacyBenefitById[id]?.en,
          antiCheatWarningEs: antiCheatWarnings[id]?.es,
          antiCheatWarningEn: antiCheatWarnings[id]?.en,
          command: cmdSet.commands[0]?.script?.split('\n')[0]?.trim() || '',
          ...((): {
            group: CatalogGroup; subgroup: CatalogSubgroup; kind: ActionKind;
            expectedImpact: EvidenceImpact; confidence: Confidence; sources: EvidenceSource[];
            rationaleEs: string; rationaleEn: string; adverseEffects: AdverseEffect[];
            scoreTotal: number; scoreLabel: string; sessionAction: boolean; nonPersistent: boolean;
          } => {
            const taxonomy = getTaxonomy(id) ?? { group: 'system' as CatalogGroup, subgroup: 'windows-features' as CatalogSubgroup, kind: 'maintenance' as ActionKind };
            const scored = getScoredOptimization(id);
            return {
              group: taxonomy.group,
              subgroup: taxonomy.subgroup,
              kind: taxonomy.kind,
              expectedImpact: scored.expectedImpact,
              confidence: scored.confidence,
              sources: scored.sources,
              rationaleEs: scored.rationaleEs,
              rationaleEn: scored.rationaleEn,
              adverseEffects: scored.adverseEffects,
              scoreTotal: scored.score.total,
              scoreLabel: (scored.score as { label?: string }).label ?? 'conditional',
              sessionAction: taxonomy.sessionAction ?? false,
              nonPersistent: taxonomy.nonPersistent ?? false,
            };
          })(),
          applicable: true,
          blockers: [],
          warnings: []
        });
      }
    }
  }

  catalogCache = items;
  return items;
}

// Fetch DB state for applied optimizations
import { db } from "@/lib/db";

// GET handler - List all optimizations
export async function GET(request: NextRequest) {
  try {
    const { searchParams } = new URL(request.url);
    const category = searchParams.get("category") as OptimizationCategory | null;

    // Generate optimizations list
    let optimizations = generateOptimizations();

    // Fetch applied state from db
    // Estado desde la DB directamente (instantáneo). La verificación real
    // la hace /api/optimization/state que el cliente llama por separado.
    const rows = await db.optimizationState.findMany();
    const appliedStateMap: Record<string, { appliedAt: string }> = {};
    for (const row of rows) {
      if (row.applied && !sessionScopedOptimizationIds.has(row.id)) {
        appliedStateMap[row.id] = { appliedAt: row.updatedAt.toISOString() };
      }
    }

    // Update with applied state
    optimizations = optimizations.map(opt => ({
      ...opt,
      isApplied: opt.id in appliedStateMap || false
    }));

    // v2: evaluate applicability against the live machine context.
    let systemContextSummary: unknown = null;
    try {
      const ctx = await getSystemContext();
      const evaluated = await Promise.all(
        optimizations.map(async (opt) => ({
          opt,
          result: await evaluateApplicability(opt.id, ctx),
        })),
      );
      optimizations = optimizations.map((opt) => {
        const match = evaluated.find((entry) => entry.opt.id === opt.id)?.result;
        if (!match) return opt;
        return {
          ...opt,
          applicable: match.applicable,
          blockers: match.blockers.map((b) => ({ code: b.code, es: b.es, en: b.en })),
          warnings: match.warnings.map((w) => ({ code: w.code, es: w.es, en: w.en })),
        };
      });
      systemContextSummary = {
        osVersion: ctx.os.displayVersion || ctx.os.caption,
        build: ctx.os.build,
        formFactor: ctx.hardware.formFactor,
        ramGB: ctx.hardware.ramGB,
        powerSource: ctx.power.powerSource,
        elevatedSession: ctx.security.elevatedSession,
        secureBoot: ctx.security.secureBoot,
        hvciEnabled: ctx.security.hvciEnabled,
        antiCheats: ctx.antiCheats.map((c) => c.id),
      };
    } catch (contextError) {
      console.warn('System context unavailable; applicability not evaluated:', contextError);
    }

    // Filter by category if specified
    let filtered = optimizations;
    if (category && categoryMetadata[category]) {
      filtered = optimizations.filter(opt => opt.category === category);
    }

    // Calculate summary
    const activeIds = new Set(optimizations.map((optimization) => optimization.id));
    const appliedCount = Object.keys(appliedStateMap).filter((id) => activeIds.has(id)).length;

    const responseCategoryMetadata = Object.fromEntries(
      Object.entries(categoryMetadata).map(([categoryId, metadata]) => [
        categoryId,
        { ...metadata, itemCount: optimizations.filter((optimization) => optimization.category === categoryId).length },
      ]),
    );

    return NextResponse.json({
      success: true,
      data: {
        optimizations: filtered,
        total: filtered.length,
        categories: {
          system: filtered.filter(o => o.category === "system").length,
          network: filtered.filter(o => o.category === "network").length,
          input: filtered.filter(o => o.category === "input").length,
          tweaks: filtered.filter(o => o.category === "tweaks").length,
          powerful: filtered.filter(o => o.category === "powerful").length
          ,privacy: filtered.filter(o => o.category === "privacy").length
        },
        summary: {
          totalOptimizations: optimizations.length,
          appliedCount,
          pendingCount: optimizations.length - appliedCount
        },
        guidanceOptimizations: [...nonExecutableOptimizationIds].map((id) => ({
          id,
          executable: false,
          reasonEs: nonExecutableReasonById[id]?.es || 'No hay automatización segura disponible.',
          reasonEn: nonExecutableReasonById[id]?.en || 'No safe automation is available.'
        })),
        categoryMetadata: responseCategoryMetadata,
        appliedState: Object.keys(appliedStateMap).map(id => ({
          id,
          appliedAt: appliedStateMap[id].appliedAt
        })),
        systemContext: systemContextSummary,
        taxonomyNote: 'group/subgroup/kind + evidence-based impact. kind=repair-action|maintenance|cosmetic items are not performance optimizations.'
      },
      timestamp: new Date().toISOString()
    });
  } catch (error) {
    console.error("Error fetching optimizations:", error);
    return NextResponse.json(
      {
        success: false,
        error: "Failed to fetch optimizations",
        message: error instanceof Error ? error.message : "Unknown error occurred",
        timestamp: new Date().toISOString()
      },
      { status: 500 }
    );
  }
}
