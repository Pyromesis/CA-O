/**
 * Diagnostics overview (#57) + real health score (#58).
 *
 * The health score does NOT count applied tweaks. It starts at 100 and
 * subtracts penalties from measured problems:
 *   thermal headroom, memory pressure, storage health/space, network
 *   quality, security state and known-issue drivers.
 * A PC with zero tweaks can score 95+; a tweaked PC with problems can
 * score far lower.
 */

import { getThermalReport, getStorageReport, takeSystemSample } from './engines';
import { runNetworkDiagnostics } from './network';
import { detectKnownIssues } from './known-issues';
import { getSystemContext } from '../system-context';
import type { SystemContext } from '../catalog/types';

export interface HealthFactor {
  area: 'thermal' | 'memory' | 'storage' | 'network' | 'security' | 'drivers' | 'stability';
  penalty: number;
  es: string;
  en: string;
}

export interface DiagnosticsOverview {
  takenAt: string;
  healthScore: number;
  factors: HealthFactor[];
  bottlenecksEs: string[];
  bottlenecksEn: string[];
  thermal: Awaited<ReturnType<typeof getThermalReport>>;
  memory: { usedPercent: number | null; commitUsedGB: number | null; commitLimitGB: number | null };
  storage: Awaited<ReturnType<typeof getStorageReport>>;
  network: Awaited<ReturnType<typeof runNetworkDiagnostics>>;
  knownIssues: Awaited<ReturnType<typeof detectKnownIssues>>;
  contextSummary: {
    formFactor: string;
    powerSource: string;
    ramGB: number;
    secureBoot: boolean | 'unknown';
    hvciEnabled: boolean | 'unknown';
    antiCheats: Array<{ id: string; name: string; status: string }>;
  };
  noteEs: string;
  noteEn: string;
}

export async function buildDiagnosticsOverview(includeNetwork = true): Promise<DiagnosticsOverview> {
  const ctx: SystemContext = await getSystemContext();
  const [thermal, storage, memory] = await Promise.all([
    getThermalReport(),
    getStorageReport(),
    takeSystemSample(),
  ]);
  // Network diagnostics are slow (~1-2 min with bufferbloat); opt-in.
  const network = includeNetwork ? await runNetworkDiagnostics() : null;
  const knownIssues = await detectKnownIssues();

  const factors: HealthFactor[] = [];

  // ---- thermal (#28)
  const hottest = Math.max(thermal.cpuTempC ?? 0, thermal.gpuTempC ?? 0);
  if (hottest >= 85) {
    factors.push({
      area: 'thermal', penalty: 15,
      es: `Cuello de botella térmico probable (${hottest} °C). No añadas tweaks; revisa refrigeración.`,
      en: `Probable thermal bottleneck (${hottest} °C). Do not add tweaks; review cooling.`,
    });
  } else if (hottest >= 75) {
    factors.push({
      area: 'thermal', penalty: 6,
      es: `Temperaturas altas (${hottest} °C).`,
      en: `High temperatures (${hottest} °C).`,
    });
  }

  // ---- memory pressure (#29)
  if (memory.ramUsedPercent !== null && memory.ramUsedPercent > 90) {
    factors.push({
      area: 'memory', penalty: 8,
      es: `Presión de memoria alta (${memory.ramUsedPercent}% usada).`,
      en: `High memory pressure (${memory.ramUsedPercent}% used).`,
    });
  }
  if (memory.commitUsedGB !== null && memory.commitLimitGB !== null && memory.commitLimitGB > 0) {
    const commitPct = (memory.commitUsedGB / memory.commitLimitGB) * 100;
    if (commitPct > 90) {
      factors.push({
        area: 'memory', penalty: 8,
        es: `Commit al ${Math.round(commitPct)}% del límite: riesgo de fallos por memoria insuficiente.`,
        en: `Commit at ${Math.round(commitPct)}% of limit: out-of-memory risk.`,
      });
    }
  }

  // ---- storage (#30)
  const unhealthy = storage.disks.filter((d) => d.health === 'Unhealthy');
  const warning = storage.disks.filter((d) => d.health === 'Warning');
  for (const disk of unhealthy) {
    factors.push({ area: 'storage', penalty: 25, es: `Disco ${disk.model}: SALUD CRÍTICA. Copia tus datos ya.`, en: `Disk ${disk.model}: UNHEALTHY. Back up your data now.` });
  }
  for (const disk of warning) {
    factors.push({ area: 'storage', penalty: 10, es: `Disco ${disk.model}: advertencia de salud.`, en: `Disk ${disk.model}: health warning.` });
  }
  if (storage.lowestFreePercent !== null && storage.lowestFreePercent < 10) {
    factors.push({
      area: 'storage', penalty: 8,
      es: `Solo ${storage.lowestFreePercent}% libre en el volumen más lleno.`,
      en: `Only ${storage.lowestFreePercent}% free on the fullest volume.`,
    });
  }

  // ---- network (#22/#23)
  if (network) {
    if (network.internet.ping.lossPercent > 2) {
      factors.push({ area: 'network', penalty: Math.min(12, Math.round(network.internet.ping.lossPercent)), es: `Pérdida de paquetes a Internet: ${network.internet.ping.lossPercent}%.`, en: `Packet loss to Internet: ${network.internet.ping.lossPercent}%.` });
    }
    if (network.bufferbloat.grade === 'poor') {
      factors.push({ area: 'network', penalty: 6, es: 'Bufferbloat alto: activa SQM/QoS en el router.', en: 'High bufferbloat: enable SQM/QoS on the router.' });
    }
  }

  // ---- security state
  if (ctx.os.majorVersion >= 11 && ctx.security.secureBoot === false) {
    factors.push({ area: 'security', penalty: 6, es: 'Secure Boot desactivado en Windows 11 (requisito para Vanguard y base de arranque seguro).', en: 'Secure Boot off on Windows 11 (Vanguard requirement and secure boot baseline).' });
  }
  if (ctx.security.hvciEnabled === false) {
    factors.push({ area: 'security', penalty: 4, es: 'HVCI/Integridad de memoria desactivada.', en: 'HVCI/Memory Integrity disabled.' });
  }

  // ---- known-issue drivers (#39)
  const criticalIssues = knownIssues.filter((k) => k.severity === 'critical');
  const warningIssues = knownIssues.filter((k) => k.severity === 'warning');
  for (const issue of criticalIssues) {
    factors.push({ area: 'drivers', penalty: 15, es: `${issue.titleEs} — ${issue.detailEs}`, en: `${issue.titleEn} — ${issue.detailEn}` });
  }
  if (warningIssues.length > 0) {
    factors.push({
      area: 'drivers', penalty: Math.min(10, warningIssues.length * 5),
      es: `${warningIssues.length} grupo(s) de drivers kernel problemáticos detectados.`,
      en: `${warningIssues.length} problematic kernel-driver group(s) detected.`,
    });
  }

  const totalPenalty = factors.reduce((sum, f) => sum + f.penalty, 0);
  const healthScore = Math.max(0, Math.min(100, 100 - totalPenalty));

  return {
    takenAt: new Date().toISOString(),
    healthScore,
    factors,
    bottlenecksEs: factors.map((f) => f.es),
    bottlenecksEn: factors.map((f) => f.en),
    thermal,
    memory: {
      usedPercent: memory.ramUsedPercent,
      commitUsedGB: memory.commitUsedGB,
      commitLimitGB: memory.commitLimitGB,
    },
    storage,
    network: (network ?? {
      takenAt: '', gateway: { address: '', ping: { medianMs: null, jitterMs: null, lossPercent: 100 } },
      internet: { target: '1.1.1.1', ping: { medianMs: null, jitterMs: null, lossPercent: 100 } },
      adapters: [], wifi: null, adapterErrors: [],
      bufferbloat: { idleMedianMs: null, loadedDownloadMedianMs: null, loadedUploadMedianMs: null, downloadIncreaseMs: null, uploadIncreaseMs: null, grade: 'unknown' as const },
      healthEs: 'Diagnóstico de red no ejecutado (rápido).', healthEn: 'Network diagnostics skipped (fast mode).',
    }),
    knownIssues,
    contextSummary: {
      formFactor: ctx.hardware.formFactor,
      powerSource: ctx.power.powerSource,
      ramGB: ctx.hardware.ramGB,
      secureBoot: ctx.security.secureBoot,
      hvciEnabled: ctx.security.hvciEnabled,
      antiCheats: ctx.antiCheats.map((c) => ({ id: c.id, name: c.name, status: c.status })),
    },
    noteEs: 'El score NO premia tweaks aplicados: penaliza problemas medidos (térmica, memoria, disco, red, seguridad, drivers). Un equipo sin cambios puede tener 95+. FPS/frame-time requieren el helper nativo futuro.',
    noteEn: 'The score does NOT reward applied tweaks: it penalizes measured problems (thermal, memory, disk, network, security, drivers). A stock PC can score 95+. FPS/frame-time need the future native helper.',
  };
}
