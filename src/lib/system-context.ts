/**
 * Live Windows context detection (v2).
 *
 * Collects hardware, OS, power and security state plus known anti-cheat
 * presence. Results are cached for a few minutes because most of this
 * information cannot change without a reboot or physical action.
 */

import { runPowerShell } from './powershell-runner';
import type { SystemContext, DetectedAntiCheat, GpuInfo, GpuVendor, GpuCapabilities } from './catalog/types';

export function detectGpuVendor(name: string): GpuVendor {
  if (/nvidia|geforce|quadro|rtx|gtx/i.test(name)) return 'nvidia';
  if (/amd|radeon|rx \d|vega|navi/i.test(name)) return 'amd';
  if (/intel|iris|uhd graphics|hd graphics|arc/i.test(name)) return 'intel';
  return 'other';
}

/**
 * Vendor capability notes. These are DRIVER-LEVEL features; CA-O never
 * tries to replicate them with registry hacks (#17/#18).
 */
export function describeGpuCapabilities(gpu: GpuInfo): GpuCapabilities {
  if (gpu.vendor === 'nvidia') {
    return {
      vendor: 'nvidia',
      reflexOrAntiLag: 'NVIDIA Reflex: se activa dentro de cada juego compatible (no existe ajuste global de Windows).',
      variableRateSync: 'G-SYNC/VRR: configúralo en el Panel de control de NVIDIA y en el menú del monitor.',
      hagsSupported: true,
      frameGenerationNote: 'DLSS Frame Generation requiere HAGS activado y tarjetas RTX 40+.',
    };
  }
  if (gpu.vendor === 'amd') {
    return {
      vendor: 'amd',
      reflexOrAntiLag: 'AMD Anti-Lag / Anti-Lag 2: se habilita en Adrenalin o dentro del juego (AL2 requiere soporte del título).',
      variableRateSync: 'FreeSync/Adaptive-Sync: actívalo en Adrenalin y en el monitor.',
      hagsSupported: true,
      frameGenerationNote: 'HYPR-RX agrupa Anti-Lag + Radeon Boost + RSR; se configura en Adrenalin.',
    };
  }
  if (gpu.vendor === 'intel') {
    return {
      vendor: 'intel',
      reflexOrAntiLag: 'Intel presenta XeLL/XeSS en Arc; sin equivalente Reflex global.',
      variableRateSync: 'VRR depende del panel de Intel Graphics y del monitor.',
      hagsSupported: true,
      frameGenerationNote: 'XeSS funciona en Arc e iGPU recientes; Frame Generation solo en Arc.',
    };
  }
  return {
    vendor: 'other',
    reflexOrAntiLag: 'Fabricante no reconocido.',
    variableRateSync: 'VRR según el fabricante.',
    hagsSupported: 'unknown',
    frameGenerationNote: '',
  };
}

const CACHE_TTL_MS = 5 * 60 * 1000;
let cache: { context: SystemContext; at: number } | null = null;

interface AntiCheatDefinition {
  id: string;
  name: string;
  services: string[];
  processes: string[];
  /** Security features this anti-cheat expects enabled on modern Windows. */
  requiresSecureBoot?: boolean;
  noteEs: string;
  noteEn: string;
}

export const ANTI_CHEATS: AntiCheatDefinition[] = [
  {
    id: 'vanguard',
    name: 'Riot Vanguard (VALORANT)',
    services: ['vgc', 'vgk'],
    processes: ['vgtray'],
    requiresSecureBoot: true,
    noteEs: 'Desde 2024 VALORANT exige TPM 2.0 y Secure Boot en Windows 11. Riot recomienda mantener HVCI/Integridad de memoria activada; desactivar funciones de seguridad puede impedir competir.',
    noteEn: 'Since 2024 VALORANT requires TPM 2.0 and Secure Boot on Windows 11. Riot recommends keeping HVCI/Memory Integrity enabled; disabling security features may block competitive play.',
  },
  {
    id: 'eac',
    name: 'Easy Anti-Cheat',
    services: ['EasyAntiCheat', 'EasyAntiCheat_EOS'],
    processes: [],
    noteEs: 'No se conocen conflictos con los cambios del catálogo de CA-O.',
    noteEn: 'No known conflicts with CA-O catalog changes.',
  },
  {
    id: 'battleye',
    name: 'BattlEye',
    services: ['BEService'],
    processes: ['BEService'],
    noteEs: 'No se conocen conflictos con los cambios del catálogo de CA-O.',
    noteEn: 'No known conflicts with CA-O catalog changes.',
  },
  {
    id: 'faceit',
    name: 'FACEIT AC',
    services: ['faceit'],
    processes: ['faceitclient'],
    noteEs: 'FACEIT es estricto con software de kernel y depuradores; el catálogo de CA-O no toca ninguno.',
    noteEn: 'FACEIT is strict about kernel software and debuggers; the CA-O catalog touches neither.',
  },
  {
    id: 'nprotect',
    name: 'nProtect GameGuard',
    services: ['GameMon'],
    processes: ['GameMon'],
    noteEs: 'Sin conflictos conocidos con este catálogo.',
    noteEn: 'No known conflicts with this catalog.',
  },
];

const CONTEXT_SCRIPT = `
$ErrorActionPreference = 'SilentlyContinue'
$result = [ordered]@{}

$os = Get-CimInstance Win32_OperatingSystem | Select-Object -First 1
$cv = Get-ItemProperty 'HKLM:\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion'
$result.osCaption = $os.Caption
$result.osBuild = "$($cv.CurrentBuildNumber).$($cv.UBR)"
$result.displayVersion = $cv.DisplayVersion
$result.edition = $cv.EditionID

$cpu = Get-CimInstance Win32_Processor | Select-Object -First 1
$result.cpuName = $cpu.Name.Trim()
$result.cores = $cpu.NumberOfCores
$result.threads = $cpu.NumberOfLogicalProcessors
$ramBytes = (Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory
$result.ramGB = [math]::Round($ramBytes / 1GB, 1)

$gpus = @(Get-CimInstance Win32_VideoController | ForEach-Object {
  [ordered]@{ name = $_.Name; driver = [string]$_.DriverVersion; vramMB = [math]::Round($_.AdapterRAM / 1MB, 0) }
})
$result.gpus = $gpus

$chassis = @(Get-CimInstance Win32_SystemEnclosure | ForEach-Object { $_.ChassisTypes } | Select-Object -First 1)
$laptopChassis = @(8,9,10,11,12,14,18,21,30,31,32)
$desktopChassis = @(3,4,5,6,7,15,16)
if ($chassis.Count -gt 0 -and $laptopChassis -contains $chassis[0]) { $result.formFactor = 'laptop' }
elseif ($chassis.Count -gt 0 -and $desktopChassis -contains $chassis[0]) { $result.formFactor = 'desktop' }
else { $result.formFactor = 'unknown' }

$touch = Get-CimInstance Win32_PnPEntity -ErrorAction SilentlyContinue | Where-Object { $_.Name -match 'HID-compliant touch screen' }
$result.touchscreen = [bool]$touch

$battery = Get-CimInstance Win32_Battery
$result.batteryPresent = [bool]$battery
if ($battery) {
  if ($battery.BatteryStatus -in 2,6,7,8,9) { $result.powerSource = 'ac' } else { $result.powerSource = 'battery' }
} else { $result.powerSource = 'ac' }

$scheme = (powercfg /getactivescheme) -join ' '
$result.activeScheme = $scheme

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
$result.elevated = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

try { $sb = Confirm-SecureBootUEFI } catch { $sb = $null }
$result.secureBoot = if ($null -eq $sb) { 'unknown' } else { "$sb".ToLower() }

$tpm = $null
try { $tpm = Get-Tpm -ErrorAction Stop } catch { }
if (-not $tpm) {
  try { $tpmWmi = Get-CimInstance -Namespace Root/CIMV2/Security/MicrosoftTpm -ClassName Win32_Tpm -ErrorAction Stop; if ($tpmWmi) { $result.tpmPresent = 'true'; $result.tpmMajor = [string]$tpmWmi.SpecVersion.Split(',')[0] } else { $result.tpmPresent = 'false'; $result.tpmMajor = '' } } catch { $result.tpmPresent = 'unknown'; $result.tpmMajor = '' }
} else {
  $present = $tpm.TpmPresent
  $result.tpmPresent = if ($present) { 'true' } else { 'false' }
  $spec = ''
  try { $tpmWmi2 = Get-CimInstance -Namespace Root/CIMV2/Security/MicrosoftTpm -ClassName Win32_Tpm -ErrorAction Stop; $spec = [string]$tpmWmi2.SpecVersion.Split(',')[0] } catch { }
  $result.tpmMajor = $spec
}

$dg = $null
try { $dg = Get-CimInstance -ClassName Win32_DeviceGuard -Namespace root/Microsoft/Windows/DeviceGuard -ErrorAction Stop } catch { }
if ($dg) {
  if ($dg.SecurityServicesRunning -contains 2 -or $dg.VirtualizationBasedSecurityStatus -eq 2) { $result.vbs = 'true' }
  elseif ($dg.VirtualizationBasedSecurityStatus -eq 0) { $result.vbs = 'false' }
  else { $result.vbs = 'unknown' }
  if ($dg.SecurityServicesRunning -contains 1) { $result.hvci = 'true' }
  elseif ($dg.SecurityServicesConfigured -contains 1) { $result.hvci = 'configured-not-running' }
  else { $result.hvci = 'false' }
} else { $result.vbs = 'unknown'; $result.hvci = 'unknown' }

$result.hypervisor = if ((Get-CimInstance Win32_ComputerSystem).HypervisorPresent) { 'true' } else { 'false' }

$bd = (Get-ItemProperty 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\CI\\Config' -Name VulnerableDriverBlocklistEnable).VulnerableDriverBlocklistEnable
if ($null -ne $bd) { $result.driverBlocklist = "$(if ($bd -eq 1) { 'true' } else { 'false' })" } else { $result.driverBlocklist = 'unknown' }

$services = @(Get-Service -ErrorAction SilentlyContinue)
$runningProcs = @(Get-Process -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Name -Unique)
$foundCheats = @()
$cheatDefs = @(${ANTI_CHEATS.map(
  (c) => `@{ Id='${c.id}'; Services=@(${c.services.map((s) => `'${s}'`).join(',')}); Processes=@(${c.processes.map((p) => `'${p}'`).join(',')}) }`,
).join(',')});
foreach ($def in $cheatDefs) {
  $hit = $false
  foreach ($svc in $def.Services) { $s = $services | Where-Object { $_.Name -eq $svc }; if ($s) { $hit = $true; break } }
  if (-not $hit) { foreach ($p in $def.Processes) { if ($runningProcs -contains $p) { $hit = $true; break } } }
  if ($hit) { $foundCheats += $def.Id }
}
$result.antiCheats = $foundCheats -join ','

$result | ConvertTo-Json -Compress
`;

function parseBoolish(value: unknown): boolean | 'unknown' {
  if (value === true || value === 'true') return true;
  if (value === false || value === 'false') return false;
  return 'unknown';
}

export async function getSystemContext(forceRefresh = false): Promise<SystemContext> {
  const now = Date.now();
  if (!forceRefresh && cache && now - cache.at < CACHE_TTL_MS) return cache.context;

  const empty: SystemContext = {
    collectedAt: new Date().toISOString(),
    os: { caption: '', build: '', displayVersion: '', edition: '', majorVersion: 10 },
    hardware: {
      cpuName: '', cpuVendor: 'other', cores: 0, threads: 0, ramGB: 0,
      gpuNames: [], gpus: [], primaryGpuCapabilities: null,
      formFactor: 'unknown', touchscreen: false,
    },
    power: { powerSource: 'unknown', batteryPresent: false, activeScheme: '' },
    security: {
      elevatedSession: false, secureBoot: 'unknown', tpmPresent: 'unknown', tpmVersionMajor: null,
      vbsEnabled: 'unknown', hvciEnabled: 'unknown', hypervisorPresent: 'unknown',
      vulnerableDriverBlocklist: 'unknown',
    },
    antiCheats: [],
  };

  if (process.platform !== 'win32') return empty;

  const script = CONTEXT_SCRIPT;
  const raw = await runPowerShell(script, false, { timeoutMs: 30_000 });
  if (!raw.success || !raw.output) return empty;

  let parsed: Record<string, unknown>;
  try {
    parsed = JSON.parse(raw.output) as Record<string, unknown>;
  } catch {
    return empty;
  }

  const osRaw = parsed as Record<string, unknown>;
  const majorFromBuild = parseInt(String(osRaw.osBuild ?? '10.0').split('.')[0] ?? '10', 10);
  const cheatIds = String(osRaw.antiCheats ?? '').split(',').filter(Boolean);

  // GPU parsing + vendor classification (#17/#18)
  const rawGpus = Array.isArray(osRaw.gpus) ? osRaw.gpus : [];
  const gpus = rawGpus.map((raw): GpuInfo => {
    const entry = (raw ?? {}) as Record<string, unknown>;
    const name = String(entry.name ?? '');
    return {
      name,
      vendor: detectGpuVendor(name),
      driverVersion: String(entry.driver ?? ''),
      vramMB: Number(entry.vramMB ?? 0) || 0,
    };
  }).filter((gpu) => gpu.name);
  const gpuNames = gpus.map((g) => g.name);

  const context: SystemContext = {
    collectedAt: new Date().toISOString(),
    os: {
      caption: String(osRaw.osCaption ?? ''),
      build: String(osRaw.osBuild ?? ''),
      displayVersion: String(osRaw.displayVersion ?? ''),
      edition: String(osRaw.edition ?? ''),
      majorVersion: Number.isFinite(majorFromBuild) && majorFromBuild >= 22000 ? 11 : 10,
    },
    hardware: {
      cpuName: String(osRaw.cpuName ?? ''),
      cpuVendor: /amd|ryzen/i.test(String(osRaw.cpuName ?? '')) ? 'amd' : /intel/i.test(String(osRaw.cpuName ?? '')) ? 'intel' : 'other',
      cores: Number(osRaw.cores ?? 0),
      threads: Number(osRaw.LogicalProcessors ?? osRaw.cores ?? 0),
      ramGB: Number(osRaw.ramGB ?? 0),
      gpuNames,
      gpus,
      primaryGpuCapabilities: gpus.length ? describeGpuCapabilities(gpus[0]) : null,
      formFactor: (osRaw.formFactor as SystemContext['hardware']['formFactor']) ?? 'unknown',
      touchscreen: osRaw.touchscreen === true,
    },
    power: {
      powerSource: (osRaw.powerSource as SystemContext['power']['powerSource']) ?? 'unknown',
      batteryPresent: osRaw.batteryPresent === true,
      activeScheme: String(osRaw.activeScheme ?? ''),
    },
    security: {
      elevatedSession: osRaw.elevated === true,
      secureBoot: parseBoolish(osRaw.secureBoot),
      tpmPresent: parseBoolish(osRaw.tpmPresent),
      tpmVersionMajor: osRaw.tpmMajor ? parseInt(String(osRaw.tpmMajor), 10) : null,
      vbsEnabled: parseBoolish(osRaw.vbs),
      hvciEnabled: parseBoolish(osRaw.hvci),
      hypervisorPresent: parseBoolish(osRaw.hypervisor),
      vulnerableDriverBlocklist: parseBoolish(osRaw.driverBlocklist),
    },
    antiCheats: cheatIds.map((id): DetectedAntiCheat => {
      const def = ANTI_CHEATS.find((c) => c.id === id);
      return { id, name: def?.name ?? id, status: def?.requiresSecureBoot ? 'potential-conflict' : 'no-known-conflict' };
    }),
  };

  cache = { context, at: now };
  return context;
}

/** Short fingerprint of the machine for snapshot provenance. */
export async function getHardwareFingerprint(): Promise<string> {
  if (process.platform !== 'win32') return 'non-windows';
  const res = await runPowerShell(
    "(Get-CimInstance Win32_Processor | Select-Object -First 1).ProcessorId; (Get-CimInstance Win32_BaseBoard | Select-Object -First 1).SerialNumber",
    false,
    { timeoutMs: 15_000 },
  );
  if (!res.success || !res.output) return 'unknown';
  return Buffer.from(res.output.trim()).toString('base64url').slice(0, 24);
}
