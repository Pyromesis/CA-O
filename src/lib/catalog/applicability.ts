/**
 * Applicability engine (v2).
 *
 * Decides whether an optimization may run on THIS machine, and which
 * confirmations it requires. The Apply endpoint enforces these gates
 * server-side; the UI only mirrors them.
 */

import type { ApplicabilityRule, SystemContext } from './types';
import { taxonomyById } from './taxonomy';

export interface Blocker {
  code:
    | 'not-applicable-context'
    | 'security-confirmation-required'
    | 'experimental-acknowledgement-required'
    | 'anti-cheat-risk'
    | 'conflict-applied';
  es: string;
  en: string;
}

export interface Warning {
  code: 'adverse-effect' | 'anti-cheat-note' | 'context-caution' | 'non-persistent';
  es: string;
  en: string;
}

export interface ApplicabilityResult {
  id: string;
  applicable: boolean;
  blockers: Blocker[];
  warnings: Warning[];
}

/** Per-ID applicability rules. Absent = generally applicable. */
const rulesById: Record<string, ApplicabilityRule> = {
  // Advanced power tweaks only make sense on AC-powered desktops (#11)
  'disable-cpu-idle': {
    requires: { formFactor: ['desktop'], powerSource: ['ac'] },
    prerequisiteNoteEs: 'Solo recomendado en PC de escritorio conectado a la corriente.',
    prerequisiteNoteEn: 'Only recommended on AC-powered desktop PCs.',
  },
  'enable-core-parking': {
    requires: { formFactor: ['desktop'], powerSource: ['ac'] },
    prerequisiteNoteEs: 'Ajuste avanzado para escritorio enchufado.',
    prerequisiteNoteEn: 'Advanced tweak for powered desktops.',
  },
  'disable-power-throttling': {
    requires: { powerSource: ['ac'] },
    prerequisiteNoteEs: 'En batería aumentaría el consumo sin beneficio real.',
    prerequisiteNoteEn: 'On battery it would increase drain with no real benefit.',
  },
  // Memory diagnostics require ample RAM (#5)
  'memory-compression': {
    requires: { minRamGB: 32 },
    requiresExperimentalAcknowledgement: true,
    conflictsWith: [],
    prerequisiteNoteEs: 'Requiere 32 GB o más de RAM y presión de memoria medida baja. Revisa /api/benchmark/system antes de decidir.',
    prerequisiteNoteEn: 'Requires 32 GB+ RAM and measured low memory pressure. Check /api/benchmark/system before deciding.',
  },
  'static-pagefile': {
    requires: { minRamGB: 32 },
    requiresExperimentalAcknowledgement: true,
    prerequisiteNoteEs: 'Dimensiona el pagefile mirando tu commit pico; con menos de 32 GB esto arriesga cierres inesperados.',
    prerequisiteNoteEn: 'Size the pagefile from your peak commit; under 32 GB this risks unexpected crashes.',
  },
  'disable-superfetch': { requiresExperimentalAcknowledgement: true },
  'disable-network-throttling': { requiresExperimentalAcknowledgement: true },
  'disable-svchost-split-threshold': { requiresExperimentalAcknowledgement: true },
  'disable-modern-standby': { requiresExperimentalAcknowledgement: true },
  'disable-ssl-time-seeding': { requiresExperimentalAcknowledgement: true },
  'timer-resolution-0-5ms': {
    requiresExperimentalAcknowledgement: true,
    requires: { powerSource: ['ac'] },
  },
  'dns-optimization': { requiresExperimentalAcknowledgement: true },

  // Touch-specific maintenance only when no touch hardware exists
  'disable-tablet-input-service': {
    requires: { touchscreen: false },
    prerequisiteNoteEs: 'Solo si el equipo no tiene pantalla táctil ni lápiz digitalizador.',
    prerequisiteNoteEn: 'Only if the machine has no touchscreen or digitizer pen.',
  },
  'disable-windows-ink': { requires: { touchscreen: false } },

  // CRITICAL security modification (#6)
  'disable-memory-integrity': {
    requiresSecurityConfirmation: true,
    antiCheatSensitivity: ['vanguard'],
    prerequisiteNoteEs: 'CRÍTICO: reduce la seguridad del kernel. No se incluye en perfiles gaming. Requiere explicación, confirmación explícita, snapshot y reinicio.',
    prerequisiteNoteEn: 'CRITICAL: reduces kernel security. Not included in gaming profiles. Requires explanation, explicit confirmation, snapshot and reboot.',
  },

  // USB suspend off is counterproductive on battery
  'disable-usb-suspend': {
    requires: { powerSource: ['ac'] },
    prerequisiteNoteEs: 'Desactivar la suspensión USB en batería reduce la autonomía.',
    prerequisiteNoteEn: 'Disabling USB suspend on battery reduces runtime.',
  },
};

/** Contextual anti-cheat conflicts that cannot be expressed statically. */
function evaluateAntiCheatConflicts(
  id: string,
  rule: ApplicabilityRule,
  ctx: SystemContext,
  blockers: Blocker[],
  warnings: Warning[],
): void {
  const vanguard = ctx.antiCheats.find((c) => c.id === 'vanguard');
  const anyAntiCheat = ctx.antiCheats.length > 0;

  if (id === 'disable-memory-integrity') {
    if (vanguard && ctx.security.secureBoot === false) {
      blockers.push({
        code: 'anti-cheat-risk',
        es: `Vanguard detectado y Secure Boot DESACTIVADO: VALORANT puede negarse a arrancar o bloquear la cuenta en partida clasificatoria.`,
        en: `Vanguard detected and Secure Boot OFF: VALORANT may refuse to start or block ranked play.`,
      });
    } else if (vanguard) {
      warnings.push({
        code: 'anti-cheat-note',
        es: 'Riot Vanguard detectado: Riot recomienda mantener HVCI activada. Desactivarla es bajo tu responsabilidad y puede afectar partidas competitivas.',
        en: 'Riot Vanguard detected: Riot recommends keeping HVCI enabled. Disabling it is at your own risk and may affect competitive matches.',
      });
    }
    if (ctx.security.hypervisorPresent === true) {
      warnings.push({
        code: 'anti-cheat-note',
        es: 'Hipervisor activo (WSL2/Docker/Sandbox posiblemente): desactivar VBS/HVCI puede romper esas cargas.',
        en: 'Hypervisor active (possibly WSL2/Docker/Sandbox): disabling VBS/HVCI can break those workloads.',
      });
    }
  }

  if (rule.antiCheatSensitivity?.length && !anyAntiCheat) {
    warnings.push({
      code: 'anti-cheat-note',
      es: 'No se han detectado anti-cheats instalados ahora mismo.',
      en: 'No anti-cheats are currently detected.',
    });
  }
}

/** Evaluate one ID against the live machine context. */
export async function evaluateApplicability(
  id: string,
  ctx: SystemContext,
  flags: { confirmSecurityChange?: boolean; acknowledgeExperimental?: boolean } = {},
): Promise<ApplicabilityResult> {
  const entry = taxonomyById[id];
  const rule = rulesById[id] ?? {};
  const blockers: Blocker[] = [];
  const warnings: Warning[] = [];

  if (entry?.kind === 'security-tradeoff' && !flags.confirmSecurityChange && !blockers.length) {
    blockers.push({
      code: 'security-confirmation-required',
      es: 'Este cambio reduce una función de seguridad de Windows y exige confirmación específica (confirmSecurityChange).',
      en: 'This change reduces a Windows security feature and requires an explicit confirmation (confirmSecurityChange).',
    });
  }

  if ((entry?.group === 'experimental' || rule.requiresExperimentalAcknowledgement) && !flags.acknowledgeExperimental) {
    blockers.push({
      code: 'experimental-acknowledgement-required',
      es: 'Elemento experimental: sin evidencia sólida. Requiere acknowledgeExperimental=true.',
      en: 'Experimental item without solid evidence. Requires acknowledgeExperimental=true.',
    });
  }

  const req = rule.requires;
  if (req) {
    const hw = ctx.hardware;
    const pw = ctx.power;
    const fail = (es: string, en: string) => blockers.push({ code: 'not-applicable-context', es, en });
    const warnUnknown = (whatEs: string, whatEn: string) =>
      warnings.push({ code: 'context-caution', es: `${whatEs} no se pudo detectar; evalúalo manualmente.`, en: `${whatEn} could not be detected; evaluate manually.` });

    if (req.formFactor && req.formFactor.length > 0) {
      if (hw.formFactor === 'unknown') warnUnknown('El factor de forma', 'Form factor');
      else if (!req.formFactor.includes(hw.formFactor)) {
        fail(
          `Este ajuste solo aplica a: ${req.formFactor.join('/')}. Detectado: ${hw.formFactor}.`,
          `This tweak only applies to: ${req.formFactor.join('/')}. Detected: ${hw.formFactor}.`,
        );
      }
    }
    if (req.powerSource && req.powerSource.length > 0) {
      if (pw.powerSource === 'unknown') warnUnknown('La fuente de alimentación', 'Power source');
      else if (!req.powerSource.includes(pw.powerSource)) {
        fail(
          `Requiere alimentación: ${req.powerSource.join('/')}. Detectado: ${pw.powerSource}.`,
          `Requires power source: ${req.powerSource.join('/')}. Detected: ${pw.powerSource}.`,
        );
      }
    }
    if (req.minRamGB !== undefined) {
      if (!hw.ramGB) warnUnknown('La cantidad de RAM', 'RAM amount');
      else if (hw.ramGB < req.minRamGB) {
        fail(
          `Requiere al menos ${req.minRamGB} GB de RAM (detectados ${hw.ramGB} GB).`,
          `Requires at least ${req.minRamGB} GB of RAM (detected ${hw.ramGB} GB).`,
        );
      }
    }
    if (req.touchscreen === false && hw.touchscreen) {
      fail(
        'El equipo tiene pantalla táctil; este cambio rompería funciones de entrada táctil.',
        'The machine has a touchscreen; this change would break touch input features.',
      );
    }
  }

  evaluateAntiCheatConflicts(id, rule, ctx, blockers, warnings);

  return { id, applicable: blockers.length === 0, blockers, warnings };
}

export function getApplicabilityRule(id: string): ApplicabilityRule {
  return rulesById[id] ?? {};
}
