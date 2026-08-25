/**
 * Evidence records and scoring for the CA-O catalog (v2).
 *
 * Every claim about performance is explicit: what effect is expected, how
 * confident we are, where the evidence comes from, and what adverse effects
 * exist. Items without solid evidence get low confidence or "heuristic"
 * sources — never marketing numbers.
 *
 * Group defaults keep the file manageable; per-ID overrides win.
 */

import type {
  Confidence,
  EvidenceRecord,
  EvidenceSource,
  PerformanceImpact,
  ScoredOptimization,
} from './types';
import { taxonomyById } from './taxonomy';

type EvidenceOverride = Partial<EvidenceRecord> & { id: string };

/** Group-level evidence defaults. Conservative by design. */
const groupDefaults: Record<string, Omit<EvidenceRecord, 'adverseEffects'>> = {
  performance: { expectedImpact: 'workload-dependent', confidence: 'medium', sources: ['microsoft'], rationaleEs: 'Efecto dependiente de la carga de trabajo y del hardware.', rationaleEn: 'Effect depends on workload and hardware.' },
  system: { expectedImpact: 'none', confidence: 'high', sources: ['microsoft'], rationaleEs: 'Cambio documentado de Windows sin impacto directo en rendimiento.', rationaleEn: 'Documented Windows change with no direct performance impact.' },
  security: { expectedImpact: 'none', confidence: 'high', sources: ['microsoft'], rationaleEs: 'Modificación de seguridad; no es una optimización de rendimiento.', rationaleEn: 'Security modification; not a performance optimization.' },
  privacy: { expectedImpact: 'none', confidence: 'high', sources: ['microsoft'], rationaleEs: 'Control de privacidad documentado por Microsoft.', rationaleEn: 'Privacy control documented by Microsoft.' },
  gaming: { expectedImpact: 'workload-dependent', confidence: 'medium', sources: ['microsoft', 'vendor'], rationaleEs: 'Diseñado para juegos; el efecto varía por título y hardware.', rationaleEn: 'Designed for gaming; effect varies per title and hardware.' },
  repair: { expectedImpact: 'diagnostic-only', confidence: 'high', sources: ['microsoft'], rationaleEs: 'Acción de reparación/mantenimiento, no de rendimiento.', rationaleEn: 'Repair/maintenance action, not a performance tweak.' },
  tweaks: { expectedImpact: 'none', confidence: 'high', sources: ['microsoft'], rationaleEs: 'Preferencia visual o de comportamiento de la interfaz.', rationaleEn: 'Visual or behavioral interface preference.' },
  experimental: { expectedImpact: 'diagnostic-only', confidence: 'low', sources: ['heuristic'], rationaleEs: 'Sin evidencia sólida; puede no aportar beneficio real o ser contraproducente según el equipo.', rationaleEn: 'No solid evidence; may provide no real benefit or be counterproductive depending on the machine.' },
};

/**
 * Per-ID overrides. Only entries that deserve nuance are listed here;
 * everything else inherits its group default.
 */
const overrides: EvidenceOverride[] = [
  // ---- High-confidence, documented effects ----
  { id: 'mouse-acceleration', expectedImpact: 'small', confidence: 'high', sources: ['vendor', 'empirical'], rationaleEs: 'Desactivar la aceleración del puntero produce movimiento 1:1 predecible; es consenso de fabricantes de ratones gaming.', rationaleEn: 'Disabling pointer acceleration yields predictable 1:1 movement; consensus among gaming mouse vendors.', adverseEffects: [] },
  { id: 'disable-usb-suspend', expectedImpact: 'workload-dependent', confidence: 'medium', sources: ['microsoft', 'empirical'], rationaleEs: 'Evita la suspensión selectiva USB; puede reducir microcortes en dispositivos, a costa de consumo en portátiles.', rationaleEn: 'Avoids USB selective suspend; can reduce device hiccups at the cost of power draw on laptops.', adverseEffects: [{ es: 'Aumenta el consumo en portátiles con batería.', en: 'Increases power draw on battery-powered laptops.' }] },
  { id: 'disable-last-access-time', expectedImpact: 'small', confidence: 'medium', sources: ['microsoft'], rationaleEs: 'Microsoft documenta esta opción para volúmenes con muchos archivos; evita escrituras de metadatos NTFS.', rationaleEn: 'Microsoft documents this for high-file-count volumes; avoids NTFS metadata writes.', adverseEffects: [] },
  { id: 'disable-8dot3-names', expectedImpact: 'small', confidence: 'medium', sources: ['microsoft'], rationaleEs: 'Reduce entradas de nombres cortos en directorios con muchos archivos; relevante solo en escenarios concretos.', rationaleEn: 'Reduces short-name entries in directories with many files; only relevant in specific scenarios.', adverseEffects: [{ es: 'Instaladores muy antiguos (16 bits) pueden fallar.', en: 'Very old (16-bit) installers may fail.' }] },
  { id: 'optimize-ntfs-memory-usage', expectedImpact: 'workload-dependent', confidence: 'low', sources: ['microsoft'], rationaleEs: 'Aumenta la caché de memoria NTFS; útil en servidores de archivos, dudoso en equipos de juego.', rationaleEn: 'Increases NTFS memory usage; useful on file servers, dubious on gaming machines.', adverseEffects: [{ es: 'Consume más RAM para metadatos.', en: 'Uses more RAM for metadata.' }] },
  { id: 'power-plan', expectedImpact: 'workload-dependent', confidence: 'high', sources: ['microsoft'], rationaleEs: 'Los planes cambian el comportamiento de boost/ahorro; Ultimate Performance mantiene clocks altos a costa de consumo y temperatura. En batería, Balanced es correcto.', rationaleEn: 'Plans change boost/parking behavior; Ultimate Performance keeps clocks high at the cost of heat and power. On battery, Balanced is correct.', adverseEffects: [{ es: 'Más calor y consumo; peor duración de batería.', en: 'More heat and power draw; worse battery life.' }] },
  { id: 'max-system-responsiveness', expectedImpact: 'workload-dependent', confidence: 'medium', sources: ['microsoft'], rationaleEs: 'SystemResponsiveness=0 prioriza multimedia en MMCSS; documentado, pero el efecto en juegos modernos es limitado.', rationaleEn: 'SystemResponsiveness=0 prioritizes multimedia in MMCSS; documented, but limited effect in modern games.', adverseEffects: [{ es: 'Puede reducir suavidad de audio en segundo plano en cargas extremas.', en: 'May reduce background audio smoothness under extreme load.' }] },
  { id: 'gaming-mode', expectedImpact: 'workload-dependent', confidence: 'medium', sources: ['microsoft'], rationaleEs: 'Game Mode ajusta la planificación en primer plano durante el juego; efecto moderado y dependiente del título.', rationaleEn: 'Game Mode adjusts foreground scheduling while gaming; moderate, title-dependent effect.', adverseEffects: [] },
  { id: 'disable-game-dvr', expectedImpact: 'small', confidence: 'high', sources: ['microsoft', 'empirical'], rationaleEs: 'GameDVR graba en segundo plano usando recursos de GPU/CPU; desactivarlo elimina esa sobrecarga.', rationaleEn: 'GameDVR records in the background using GPU/CPU resources; disabling removes that overhead.', adverseEffects: [{ es: 'Pierdes clips automáticos y grabación con Win+G.', en: 'You lose automatic clips and Win+G recording.' }] },
  { id: 'windowed-games-optimization', expectedImpact: 'workload-dependent', confidence: 'medium', sources: ['microsoft'], rationaleEs: 'Permite Auto HDR y modelos de presentación modernos en juegos ventana; beneficioso cuando el título lo soporta.', rationaleEn: 'Enables Auto HDR and modern presentation models in windowed games; beneficial when the title supports it.', adverseEffects: [] },
  { id: 'enable-hags', expectedImpact: 'workload-dependent', confidence: 'medium', sources: ['vendor'], rationaleEs: 'HAGS reduce overhead de cola GPU en algunos títulos y es requisito de DLSS Frame Generation; en otros no cambia nada o empeora.', rationaleEn: 'HAGS reduces GPU queue overhead in some titles and is required for DLSS Frame Generation; in others it changes nothing or regresses.', adverseEffects: [{ es: 'En configuraciones antiguas puede causar inestabilidad; reversible.', en: 'On older setups it may cause instability; reversible.' }] },

  // ---- Contested folklore moved to honest low/unknown confidence ----
  { id: 'disable-superfetch', expectedImpact: 'diagnostic-only', confidence: 'low', sources: ['heuristic'], rationaleEs: 'SysMain precarga en RAM libre; en SSD modernos rara vez es un cuello de botella. Desactivarlo puede empeorar arranques de aplicaciones.', rationaleEn: 'SysMain prefetches into free RAM; on modern SSDs it is rarely a bottleneck. Disabling can worsen app launch times.', adverseEffects: [{ es: 'Arranques de apps potencialmente más lentos.', en: 'Potentially slower app launches.' }] },
  { id: 'memory-compression', expectedImpact: 'diagnostic-only', confidence: 'low', sources: ['heuristic'], rationaleEs: 'La compresión evita pagado a disco bajo presión de memoria. Desactivarla solo tiene sentido en equipos con RAM holgada y presión medida baja.', rationaleEn: 'Compression avoids disk paging under memory pressure. Disabling only makes sense on machines with ample RAM and measured low pressure.', adverseEffects: [{ es: 'Bajo presión de memoria: más paging a disco y stutters.', en: 'Under memory pressure: more disk paging and stutter.' }] },
  { id: 'static-pagefile', expectedImpact: 'diagnostic-only', confidence: 'low', sources: ['heuristic'], rationaleEs: 'Un pagefile fijo evita redimensionados, pero mal dimensionado provoca fallos de commit. Requiere RAM amplia y seguimiento del commit pico.', rationaleEn: 'A fixed pagefile avoids resize events, but wrong sizing causes commit failures. Requires ample RAM and peak-commit tracking.', adverseEffects: [{ es: 'Commit insuficiente si se subestima: cierres inesperados.', en: 'Insufficient commit if undersized: unexpected crashes.' }] },
  { id: 'disable-network-throttling', expectedImpact: 'diagnostic-only', confidence: 'low', sources: ['heuristic'], rationaleEs: 'NetworkThrottlingIndex afecta al throttling multimedia de red (10 paquetes/ms); el escenario que penaliza es raro hoy en día.', rationaleEn: 'NetworkThrottlingIndex affects multimedia network throttling (10 packets/ms); the affected scenario is rare today.', adverseEffects: [] },
  { id: 'timer-resolution-0-5ms', expectedImpact: 'workload-dependent', confidence: 'low', sources: ['empirical'], rationaleEs: 'Algunos juegos antiguos mejoran su frame pacing con timers bajos; Windows 11 ya gestiona esto por proceso. Coste energético medible.', rationaleEn: 'Some older games improve frame pacing with low timers; Windows 11 already manages this per-process. Measurable energy cost.', adverseEffects: [{ es: 'Mayor consumo energético; efecto no persistente.', en: 'Higher energy use; non-persistent effect.' }] },
  { id: 'disable-svchost-split-threshold', expectedImpact: 'diagnostic-only', confidence: 'low', sources: ['heuristic'], rationaleEs: 'Reducir el umbral agrupa servicios en menos procesos; ahorra algo de RAM pero reduce aislamiento y diagnosticabilidad.', rationaleEn: 'Lowering the threshold groups services into fewer processes; saves some RAM but reduces isolation and diagnosability.', adverseEffects: [{ es: 'Un servicio colgado puede llevarse a otros del mismo proceso.', en: 'One hung service can take down others in the same process.' }] },
  { id: 'disable-cpu-idle', expectedImpact: 'workload-dependent', confidence: 'low', sources: ['heuristic'], rationaleEs: 'Elevar el estado mínimo del procesador mantiene clocks altos; solo defendible en escritorio con refrigeración adecuada.', rationaleEn: 'Raising minimum processor state keeps clocks high; only defensible on desktops with adequate cooling.', adverseEffects: [{ es: 'Más temperatura, consumo y posiblemente throttling térmico neto.', en: 'More heat, power, and possibly net thermal throttling.' }] },
  { id: 'enable-core-parking', expectedImpact: 'workload-dependent', confidence: 'low', sources: ['heuristic'], rationaleEs: 'Gestión de aparcado de núcleos; los planes modernos ya lo hacen bien. Ajuste fino solo para escritorio enchufado.', rationaleEn: 'Core parking management; modern plans already handle it well. Fine-tuning only for powered desktops.', adverseEffects: [] },
  { id: 'disable-power-throttling', expectedImpact: 'workload-dependent', confidence: 'low', sources: ['microsoft'], rationaleEs: 'Power Throttling ahorra energía limitando procesos en segundo plano; desactivarlo sube consumo sin beneficio claro en juegos.', rationaleEn: 'Power Throttling saves energy by limiting background processes; disabling raises power use with no clear gaming benefit.', adverseEffects: [{ es: 'Peor autonomía en portátiles.', en: 'Worse battery life on laptops.' }] },
  { id: 'disable-modern-standby', expectedImpact: 'diagnostic-only', confidence: 'low', sources: ['heuristic'], rationaleEs: 'Cambiar S0 por sueño clásico puede arreglar drenaje en equipos concretos, pero romperá el dormir en otros. Solo diagnóstico avanzado.', rationaleEn: 'Switching S0 to classic sleep can fix drain on specific machines but will break sleep on others. Advanced diagnostics only.', adverseEffects: [{ es: 'Puede impedir que el equipo duerma correctamente.', en: 'May prevent the machine from sleeping correctly.' }] },
  { id: 'disable-ssl-time-seeding', expectedImpact: 'none', confidence: 'unknown', sources: ['heuristic'], rationaleEs: 'Tweak histórico sin evidencia moderna; se conserva como experimental.', rationaleEn: 'Legacy tweak without modern evidence; kept as experimental.', adverseEffects: [] },
  { id: 'dns-optimization', expectedImpact: 'workload-dependent', confidence: 'medium', sources: ['benchmark'], rationaleEs: 'El DNS solo afecta al tiempo de resolución inicial, no al ping en partida. Usa /api/benchmark/dns para elegir proveedor con datos.', rationaleEn: 'DNS only affects initial resolution time, not in-game ping. Use /api/benchmark/dns to pick a provider with data.', adverseEffects: [{ es: 'Forzar Cloudflare puede romper DNS corporativo/filtrado parental.', en: 'Forcing Cloudflare can break corporate/parental DNS.' }] },

  // ---- Security-relevant trade-offs ----
  { id: 'disable-memory-integrity', expectedImpact: 'workload-dependent', confidence: 'low', sources: ['empirical', 'vendor'], rationaleEs: 'Desactivar HVCI puede dar algún FPS en CPU afectadas por overhead de virtualización, pero elimina una protección kernel activa. Riot recomienda mantener Secure Boot y las funciones de seguridad habilitadas.', rationaleEn: 'Disabling HVCI can yield some FPS on CPUs affected by virtualization overhead, but removes an active kernel protection. Riot recommends keeping Secure Boot and security features enabled.', adverseEffects: [{ es: 'Elimina protección contra drivers maliciosos/exploits kernel; VALORANT/Vanguard pueden exigir Secure Boot.', en: 'Removes protection against malicious drivers/kernel exploits; VALORANT/Vanguard may require Secure Boot.' }] },
  { id: 'disable-smb1', expectedImpact: 'none', confidence: 'high', sources: ['microsoft'], rationaleEs: 'Quitar SMBv1 cierra vectores de ataque conocidos ( WannaCry ); cero coste de rendimiento moderno.', rationaleEn: 'Removing SMBv1 closes known attack vectors (WannaCry); zero modern performance cost.', adverseEffects: [{ es: 'Pierde compatibilidad con NAS/equipos antiquísimos.', en: 'Loses compatibility with ancient NAS/devices.' }] },

  // ---- Repair actions ----
  { id: 'winsock-reset', expectedImpact: 'diagnostic-only', confidence: 'high', sources: ['microsoft'], rationaleEs: 'Restablece el catálogo de red; acción de reparación tras daños, no una optimización.', rationaleEn: 'Resets the network catalog; a repair action after corruption, not an optimization.', adverseEffects: [{ es: 'Requiere reinicio; VPN/LSP de terceros se reinstalarán.', en: 'Requires reboot; third-party VPN/LSPs will re-register.' }] },
  { id: 'clear-temp-files', expectedImpact: 'none', confidence: 'high', sources: ['microsoft'], rationaleEs: 'Libera espacio en disco; mantenimiento básico, sin relación directa con FPS.', rationaleEn: 'Frees disk space; basic maintenance, no direct FPS relation.', adverseEffects: [] },
];

function buildEvidenceRecord(id: string): EvidenceRecord {
  const override = overrides.find((entry) => entry.id === id);
  const entry = taxonomyById[id];
  const base = groupDefaults[entry?.group ?? 'system'] ?? groupDefaults.system;
  return {
    expectedImpact: (override?.expectedImpact ?? base.expectedImpact) as PerformanceImpact,
    confidence: (override?.confidence ?? base.confidence) as Confidence,
    sources: (override?.sources ?? base.sources) as EvidenceSource[],
    rationaleEs: override?.rationaleEs ?? base.rationaleEs,
    rationaleEn: override?.rationaleEn ?? base.rationaleEn,
    adverseEffects: override?.adverseEffects ?? [],
  };
}

const impactBenefit: Record<PerformanceImpact, number> = {
  none: 0,
  small: 8,
  medium: 14,
  'workload-dependent': 10,
  'diagnostic-only': 4,
};

const confidenceScore: Record<Confidence, number> = { high: 25, medium: 17, low: 9, unknown: 3 };
const sourceScore: Record<EvidenceSource, number> = {
  microsoft: 25, vendor: 22, benchmark: 20, empirical: 12, heuristic: 4,
};

/**
 * Optimization score (0..100). Deliberately conservative: claims without
 * evidence cannot score high even if the tweak is harmless.
 */
export function scoreOptimization(id: string): ScoredOptimization['score'] & { label: string } {
  const record = buildEvidenceRecord(id);
  const entry = taxonomyById[id];
  const kind = entry?.kind ?? 'optimization';

  const bestSource = record.sources.reduce((best, s) => Math.max(best, sourceScore[s]), 0);
  const evidence = Math.round(Math.min(25, bestSource));
  const confidence = confidenceScore[record.confidence];
  const expectedBenefit = impactBenefit[record.expectedImpact];

  let safety = 20;
  if (kind === 'security-tradeoff') safety = 2;
  else if (kind === 'repair-action') safety = 16;
  else if (record.adverseEffects.length > 0) safety = 12;

  let compatibility = 10;
  if (kind === 'security-tradeoff') compatibility = 1;

  const total = Math.max(0, Math.min(100, evidence + confidence + expectedBenefit + safety + compatibility));
  const label = total >= 75 ? 'recommended' : total >= 55 ? 'conditional' : total >= 35 ? 'caution' : 'not-recommended';

  return { evidence, confidence, expectedBenefit, safety, compatibility, total, label };
}

export function getEvidence(id: string): EvidenceRecord {
  return buildEvidenceRecord(id);
}

export function getScoredOptimization(id: string): ScoredOptimization {
  return { ...getEvidence(id), score: scoreOptimization(id) };
}
