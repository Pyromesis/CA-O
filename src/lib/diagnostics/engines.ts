/**
 * Diagnostics engines (CA-O 2026).
 *
 * All read-only. Every module returns honest data: when Windows cannot
 * provide a metric, the field is null — never fabricated.
 */

import { runPowerShell } from '../powershell-runner';
import { takeSystemSample } from '../benchmarks/system-sample';

// ---------------------------------------------------------------- thermal
export interface ThermalReport {
  cpuTempC: number | null;
  gpuTempC: number | null;
  source: string;
  throttlingSuspected: boolean;
  noteEs: string;
  noteEn: string;
}

const THERMAL_SCRIPT = `
$ErrorActionPreference = 'SilentlyContinue'
$out = [ordered]@{}
$t = $null
try { $t = Get-CimInstance -Namespace root/wmi -ClassName MSAcpi_ThermalZoneTemperature -ErrorAction Stop } catch { }
if ($t) {
  $temps = @($t | ForEach-Object { ($_.CurrentTemperature / 10) - 273.15 } | Where-Object { $_ -gt 0 -and $_ -lt 120 })
  if ($temps.Count -gt 0) { $out.cpuTemp = [math]::Round(($temps | Measure-Object -Maximum).Maximum, 1) }
}
$nvidiaSmi = Join-Path $env:ProgramFiles 'NVIDIA Corporation\\NVSMI\\nvidia-smi.exe'
$nvidiaSmi2 = 'nvidia-smi'
$gpuTemp = $null
foreach ($tool in @($nvidiaSmi, $nvidiaSmi2)) {
  try {
    $line = & $tool --query-gpu=temperature.gpu --format=csv,noheader 2>$null | Select-Object -First 1
    if ($LASTEXITCODE -eq 0 -and $line -match '\\d+') { $gpuTemp = [double]$Matches[0]; break }
  } catch { }
}
if ($null -ne $gpuTemp) { $out.gpuTemp = $gpuTemp }
$out | ConvertTo-Json -Compress
`;

export async function getThermalReport(): Promise<ThermalReport> {
  const empty: ThermalReport = {
    cpuTempC: null, gpuTempC: null, source: 'unavailable', throttlingSuspected: false,
    noteEs: 'Este equipo no expone temperaturas vía WMI/nvidia-smi. Usa HWiNFO o el software del fabricante.',
    noteEn: 'This machine does not expose temperatures via WMI/nvidia-smi. Use HWiNFO or vendor software.',
  };
  if (process.platform !== 'win32') return empty;

  const raw = await runPowerShell(THERMAL_SCRIPT, false, { timeoutMs: 25_000 });
  if (!raw.success || !raw.output) return empty;
  try {
    const parsed = JSON.parse(raw.output) as Record<string, unknown>;
    const cpu = Number(parsed.cpuTemp);
    const gpu = Number(parsed.gpuTemp);
    const cpuTempC = Number.isFinite(cpu) ? cpu : null;
    const gpuTempC = Number.isFinite(gpu) ? gpu : null;
    const hottest = Math.max(cpuTempC ?? 0, gpuTempC ?? 0);
    const available = cpuTempC !== null || gpuTempC !== null;
    return {
      cpuTempC,
      gpuTempC,
      source: available ? 'wmi+nvidia-smi' : 'unavailable',
      // Heuristic only: sustained temps above ~85 °C usually mean loss of boost.
      throttlingSuspected: hottest >= 85,
      noteEs: available
        ? (hottest >= 85
          ? 'CUELLO DE BOTELLA TÉRMICO PROBABLE: por encima de ~85 °C la CPU/GPU recorta frecuencias. NO añadas más tweaks de rendimiento; revisa flujo de aire, curva de ventiladores, limpieza y pasta térmica.'
          : 'Temperaturas dentro de rango razonable.')
        : empty.noteEs,
      noteEn: available
        ? (hottest >= 85
          ? 'PROBABLE THERMAL BOTTLENECK: above ~85 °C CPU/GPU cut clocks. DO NOT add more performance tweaks; review airflow, fan curve, dust and thermal paste.'
          : 'Temperatures within a reasonable range.')
        : empty.noteEn,
    };
  } catch {
    return empty;
  }
}

// ---------------------------------------------------------------- storage
export interface StorageDisk {
  model: string;
  mediaType: 'SSD' | 'HDD' | 'Unspecified';
  busType: string;
  health: string;
  sizeGB: number;
}
export interface StorageVolume { letter: string; label: string; freeGB: number; totalGB: number }
export interface StorageReport {
  disks: StorageDisk[];
  volumes: StorageVolume[];
  lowestFreePercent: number | null;
  noteEs: string;
  noteEn: string;
}

const STORAGE_SCRIPT = `
$ErrorActionPreference = 'SilentlyContinue'
$out = [ordered]@{}
$out.disks = @(Get-PhysicalDisk | ForEach-Object {
  [ordered]@{
    model = $_.FriendlyName
    mediaType = "$($_.MediaType)"
    busType = "$($_.BusType)"
    health = "$($_.HealthStatus)"
    sizeGB = [math]::Round($_.Size / 1GB, 0)
  }
})
$out.volumes = @(Get-Volume | Where-Object { $_.DriveLetter } | ForEach-Object {
  [ordered]@{
    letter = "$($_.DriveLetter)"
    label = "$($_.FileSystemLabel)"
    freeGB = [math]::Round($_.SizeRemaining / 1GB, 2)
    totalGB = [math]::Round($_.Size / 1GB, 2)
  }
})
$out | ConvertTo-Json -Depth 3 -Compress
`;

export async function getStorageReport(): Promise<StorageReport> {
  const empty: StorageReport = { disks: [], volumes: [], lowestFreePercent: null, noteEs: '', noteEn: '' };
  if (process.platform !== 'win32') return empty;

  const raw = await runPowerShell(STORAGE_SCRIPT, false, { timeoutMs: 30_000 });
  if (!raw.success || !raw.output) return empty;
  try {
    const parsed = JSON.parse(raw.output) as { disks?: StorageDisk[]; volumes?: StorageVolume[] };
    const disks = (parsed.disks ?? []).map((d) => ({
      ...d,
      mediaType: (d.mediaType === 'SSD' || d.mediaType === 'HDD' ? d.mediaType : 'Unspecified') as StorageDisk['mediaType'],
    }));
    const volumes = parsed.volumes ?? [];
    let lowestFreePercent: number | null = null;
    for (const v of volumes) {
      if (v.totalGB > 0) {
        const pct = Math.round((v.freeGB / v.totalGB) * 1000) / 10;
        lowestFreePercent = lowestFreePercent === null ? pct : Math.min(lowestFreePercent, pct);
      }
    }
    return {
      disks,
      volumes,
      lowestFreePercent,
      noteEs: lowestFreePercent !== null && lowestFreePercent < 10
        ? 'ADVERTENCIA: menos del 10 % libre en al menos un volumen; SSD saturados pierden rendimiento de escritura.'
        : 'Almacenamiento OK.',
      noteEn: lowestFreePercent !== null && lowestFreePercent < 10
        ? 'WARNING: less than 10% free on at least one volume; full SSDs lose write performance.'
        : 'Storage OK.',
    };
  } catch {
    return empty;
  }
}

// ---------------------------------------------------------------- input
export interface InputReport {
  mouseSpeed: number | null;          // 1 = EPP disabled (1:1), 0 = enhanced precision on
  enhancedPointerPrecision: boolean | 'unknown';
  usbSelectiveSuspendAc: string | null;
  pointerDevices: string[];
  aggregateDpcRate: number | null;    // DPCs/sec (system-wide); per-driver needs ETW/xperf
  noteEs: string;
  noteEn: string;
}

const INPUT_SCRIPT_FINAL = `
$ErrorActionPreference = 'SilentlyContinue'
$out = [ordered]@{}
$m = Get-ItemProperty 'HKCU:\\Control Panel\\Mouse'
$out.mouseSpeed = $m.MouseSpeed
$out.pointerDevices = @(Get-CimInstance Win32_PointingDevice | ForEach-Object { $_.Caption }) | Select-Object -Unique
$dpc = Get-Counter '\\Processor(_Total)\\DPCs Queued/sec' -SampleInterval 1 -MaxSamples 1 -ErrorAction SilentlyContinue
if ($dpc) { $out.dpcRate = [math]::Round(($dpc.CounterSamples | Select-Object -First 1).CookedValue, 1) }
$out | ConvertTo-Json -Compress
`;

export async function getInputReport(): Promise<InputReport> {
  const base: InputReport = {
    mouseSpeed: null,
    enhancedPointerPrecision: 'unknown',
    usbSelectiveSuspendAc: null,
    pointerDevices: [],
    aggregateDpcRate: null,
    noteEs: '',
    noteEn: '',
  };
  if (process.platform !== 'win32') return base;

  const raw = await runPowerShell(INPUT_SCRIPT_FINAL, false, { timeoutMs: 20_000 });
  if (!raw.success || !raw.output) return base;
  try {
    const parsed = JSON.parse(raw.output) as Record<string, unknown>;
    const speed = Number(parsed.mouseSpeed);
    const epp: boolean | 'unknown' = Number.isFinite(speed) && (speed === 0 || speed === 1)
      ? speed !== 1   // MouseSpeed=1 => "Enhance pointer precision" OFF in modern Windows
      : 'unknown';
    return {
      ...base,
      mouseSpeed: Number.isFinite(speed) ? speed : null,
      enhancedPointerPrecision: epp,
      pointerDevices: Array.isArray(parsed.pointerDevices) ? parsed.pointerDevices.map(String) : [],
      aggregateDpcRate: Number.isFinite(Number(parsed.dpcRate)) ? Number(parsed.dpcRate) : null,
      noteEs: 'El polling rate lo controla el firmware del ratón (software del fabricante). El DPC agregado no identifica el driver causante: eso requiere tracing ETW (LatencyMon/xperf).',
      noteEn: 'Polling rate is controlled by mouse firmware (vendor tool). Aggregate DPC rate cannot identify the offending driver: that requires ETW tracing (LatencyMon/xperf).',
    };
  } catch {
    return base;
  }
}

// ---------------------------------------------------------------- memory pressure helper (re-export)
export { takeSystemSample };
