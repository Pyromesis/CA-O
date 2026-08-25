/**
 * DNS benchmark (v2).
 *
 * Replaces the old "force Cloudflare" approach: measure real latency to
 * several resolvers from THIS machine and report. Never changes the
 * system DNS automatically.
 */

import { runPowerShell } from '../powershell-runner';

export interface DnsProviderResult {
  providerId: string;
  name: string;
  ip: string;
  samples: number[];
  medianMs: number | null;
  jitterMs: number | null;
  timeoutRate: number;
}

export interface DnsBenchmarkResult {
  ranAt: string;
  queriesPerProvider: number;
  results: DnsProviderResult[];
  recommended: { providerId: string; name: string; medianMs: number } | null;
  noteEs: string;
  noteEn: string;
}

const PROVIDERS = [
  { id: 'system', name: 'System default', ip: '' },
  { id: 'cloudflare', name: 'Cloudflare', ip: '1.1.1.1' },
  { id: 'google', name: 'Google', ip: '8.8.8.8' },
  { id: 'quad9', name: 'Quad9', ip: '9.9.9.9' },
];

function median(values: number[]): number {
  if (values.length === 0) return Number.NaN;
  const sorted = [...values].sort((a, b) => a - b);
  const mid = Math.floor(sorted.length / 2);
  return sorted.length % 2 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
}

/** Build one PowerShell probe script for a resolver; returns ms as text or TIMEOUT. */
function buildProbeScript(ip: string, domain: string, queries: number): string {
  const serverArg = ip ? `-Server ${ip} ` : '';
  return `
$ErrorActionPreference = 'SilentlyContinue'
$results = @()
for ($i = 0; $i -lt ${queries}; $i++) {
  $label = "bench-$(Get-Random)-${'$'}i.example.org"
  $t = [System.Diagnostics.Stopwatch]::StartNew()
  $r = Resolve-DnsName -Name $label -Type A ${serverArg}-DnsOnly -NoHostsFile -QuickTimeout
  $t.Stop()
  if ($null -ne $r) { $results += $t.Elapsed.TotalMilliseconds } else { $results += -1 }
}
$results -join ','
`;
}

export async function runDnsBenchmark(queriesPerProvider = 6): Promise<DnsBenchmarkResult> {
  const safeQueries = Math.min(Math.max(queriesPerProvider, 3), 10);
  // Unique label per query forces a real resolution instead of cache hits.
  const domain = `cao-bench-${Date.now()}.example.org`;

  const results: DnsProviderResult[] = [];
  for (const provider of PROVIDERS) {
    const script = buildProbeScript(provider.ip, domain, safeQueries);
    const raw = await runPowerShell(script, false, { timeoutMs: 60_000 });
    if (!raw.success || !raw.output) {
      results.push({
        providerId: provider.id, name: provider.name, ip: provider.ip || '(dhcp)',
        samples: [], medianMs: null, jitterMs: null, timeoutRate: 1,
      });
      continue;
    }
    const samples = raw.output.trim().split(',').map(Number).filter((n) => Number.isFinite(n));
    const okSamples = samples.filter((s) => s >= 0);
    const timeouts = samples.filter((s) => s < 0).length;
    const med = okSamples.length > 0 ? Math.round(median(okSamples) * 10) / 10 : null;
    let jitter: number | null = null;
    if (okSamples.length >= 2) {
      const diffs = okSamples.slice(1).map((v, i) => Math.abs(v - okSamples[i]));
      jitter = Math.round(median(diffs) * 10) / 10;
    }
    results.push({
      providerId: provider.id,
      name: provider.name,
      ip: provider.ip || '(system)',
      samples: okSamples.map((s) => Math.round(s * 10) / 10),
      medianMs: med,
      jitterMs: jitter,
      timeoutRate: samples.length ? Math.round((timeouts / samples.length) * 100) / 100 : 1,
    });
  }

  const ranked = results
    .filter((r) => r.medianMs !== null && r.timeoutRate < 0.5)
    .sort((a, b) => (a.medianMs ?? Infinity) - (b.medianMs ?? Infinity));

  return {
    ranAt: new Date().toISOString(),
    queriesPerProvider: safeQueries,
    results,
    recommended: ranked[0]
      ? { providerId: ranked[0].providerId, name: ranked[0].name, medianMs: ranked[0].medianMs as number }
      : null,
    noteEs: 'El DNS solo afecta al tiempo de resolución de nombres (primera conexión), no al ping dentro del juego. CA-O no cambia tu DNS automáticamente.',
    noteEn: 'DNS only affects name-resolution time (first connection), not in-game ping. CA-O never changes your DNS automatically.',
  };
}
