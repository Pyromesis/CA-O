/**
 * Lightweight system sampling for before/after comparisons (v2).
 *
 * Measures what can actually be measured quickly from PowerShell:
 * CPU load, available memory, commit charge and compressed memory.
 * FPS/frame-time capture requires a native helper and is out of scope
 * here by design (no fake numbers).
 */

import { runPowerShell } from '../powershell-runner';

export interface SystemSample {
  takenAt: string;
  cpuLoadPercent: number | null;
  ramTotalGB: number | null;
  ramAvailableGB: number | null;
  ramUsedPercent: number | null;
  commitUsedGB: number | null;
  commitLimitGB: number | null;
  memoryCompressionEnabled: boolean | 'unknown';
  compressedMemoryMB: number | null;
}

const SAMPLE_SCRIPT = `
$ErrorActionPreference = 'SilentlyContinue'
$out = [ordered]@{}

$cpu = Get-CimInstance Win32_Processor | Select-Object -First 1
if ($cpu) { $out.cpuLoad = $cpu.LoadPercentage } else { $out.cpuLoad = '' }

$os = Get-CimInstance Win32_OperatingSystem
$out.ramTotalGB = [math]::Round($os.TotalVisibleMemorySize / 1MB, 2)
$out.ramAvailGB = [math]::Round($os.FreePhysicalMemory / 1MB, 2)

$cs = Get-CimInstance Win32_PageFileUsage | Measure-Object -Property AllocatedBaseSize -Sum
$pef = Get-Counter '\\Memory\\Committed Bytes','\\Memory\\Commit Limit' -ErrorAction SilentlyContinue
if ($pef) {
  $committed = $pef.CounterSamples | Where-Object { $_.Path -like '*committed bytes' -and $_.Path -notlike '*limit*' } | Select-Object -First 1
  $limit = $pef.CounterSamples | Where-Object { $_.Path -like '*commit limit' } | Select-Object -First 1
  if ($committed) { $out.commitGB = [math]::Round($committed.CookedValue / 1GB, 2) }
  if ($limit) { $out.commitLimitGB = [math]::Round($limit.CookedValue / 1GB, 2) }
} else { $out.commitGB = ''; $out.commitLimitGB = '' }

try {
  $mm = Get-MMAgent
  $out.memCompression = "$($mm.MemoryCompression)".ToLower()
} catch { $out.memCompression = 'unknown' }

$compressedProc = Get-Process -Name 'Memory Compression' -ErrorAction SilentlyContinue
if ($compressedProc) { $out.compressedMB = [math]::Round($compressedProc.WorkingSet64 / 1MB, 1) } else { $out.compressedMB = '' }

$out | ConvertTo-Json -Compress
`;

export async function takeSystemSample(): Promise<SystemSample> {
  const empty: SystemSample = {
    takenAt: new Date().toISOString(),
    cpuLoadPercent: null,
    ramTotalGB: null,
    ramAvailableGB: null,
    ramUsedPercent: null,
    commitUsedGB: null,
    commitLimitGB: null,
    memoryCompressionEnabled: 'unknown',
    compressedMemoryMB: null,
  };

  if (process.platform !== 'win32') return empty;

  const raw = await runPowerShell(SAMPLE_SCRIPT, false, { timeoutMs: 20_000 });
  if (!raw.success || !raw.output) return empty;

  try {
    const parsed = JSON.parse(raw.output) as Record<string, unknown>;
    const num = (value: unknown): number | null => {
      const n = Number(value);
      return Number.isFinite(n) ? n : null;
    };
    const total = num(parsed.ramTotalGB);
    const avail = num(parsed.ramAvailGB);
    return {
      takenAt: new Date().toISOString(),
      cpuLoadPercent: num(parsed.cpuLoad),
      ramTotalGB: total,
      ramAvailableGB: avail,
      ramUsedPercent: total && avail !== null && avail !== undefined && total > 0
        ? Math.round(((total - avail) / total) * 1000) / 10
        : null,
      commitUsedGB: num(parsed.commitGB),
      commitLimitGB: num(parsed.commitLimitGB),
      memoryCompressionEnabled: parsed.memCompression === true || parsed.memCompression === 'true'
        ? true
        : parsed.memCompression === false || parsed.memCompression === 'false'
          ? false
          : 'unknown',
      compressedMemoryMB: num(parsed.compressedMB),
    };
  } catch {
    return empty;
  }
}
