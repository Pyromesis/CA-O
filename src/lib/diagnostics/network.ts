/**
 * Network diagnostics engine (#22/#23).
 * Read-only measurements; recommendations, never silent configuration.
 */

import { runPowerShell } from '../powershell-runner';

export interface PingStats {
  medianMs: number | null;
  jitterMs: number | null;
  lossPercent: number;
}

export interface AdapterInfo {
  name: string;
  description: string;
  linkSpeedMbps: number | null;
  connector: string;
}

export interface WifiInfo {
  ssid: string | null;
  band: string | null;
  signalPercent: number | null;
  rxRateMbps: number | null;
  txRateMbps: number | null;
}

export interface BufferbloatReport {
  idleMedianMs: number | null;
  loadedDownloadMedianMs: number | null;
  loadedUploadMedianMs: number | null;
  downloadIncreaseMs: number | null;
  uploadIncreaseMs: number | null;
  grade: 'unknown' | 'good' | 'fair' | 'poor';
}

export interface NetworkDiagnosticsReport {
  takenAt: string;
  gateway: { address: string; ping: PingStats };
  internet: { target: string; ping: PingStats };
  adapters: AdapterInfo[];
  wifi: WifiInfo | null;
  adapterErrors: Array<{ name: string; inboundErrors: number; outboundErrors: number }>;
  bufferbloat: BufferbloatReport;
  healthEs: string;
  healthEn: string;
}

const emptyPing = (): PingStats => ({ medianMs: null, jitterMs: null, lossPercent: 100 });

const GATEWAY_SCRIPT = `
$ErrorActionPreference = 'SilentlyContinue'
$gw = (Get-NetRoute -DestinationPrefix '0.0.0.0/0' | Sort-Object RouteMetric | Select-Object -First 1).NextHop
Write-Output "$gw"
`;

function buildPingScript(target: string, count: number): string {
  return `
$ErrorActionPreference = 'SilentlyContinue'
$results = @()
$pings = ping -n ${count} ${target}
foreach ($line in $pings) {
  if ($line -match '(?:tiempo|time)[=<](\\d+)ms') { $results += [int]$Matches[1] }
}
$results -join ','
`;
}

function parsePing(raw: { success: boolean; output?: string }): PingStats {
  if (!raw.success || !raw.output) return emptyPing();
  const tokens = raw.output.trim().split(',').map(Number).filter((n) => Number.isFinite(n));
  const ok = tokens.filter((t) => t >= 0);
  const loss = tokens.length ? Math.round(((tokens.length - ok.length) / tokens.length) * 100) : 100;
  if (ok.length === 0) return { ...emptyPing(), lossPercent: loss };
  const sorted = [...ok].sort((a, b) => a - b);
  const median = sorted[Math.floor(sorted.length / 2)];
  let jitter: number | null = null;
  if (ok.length >= 3) {
    const diffs = ok.slice(1).map((v, i) => Math.abs(v - ok[i]));
    jitter = Math.round((diffs.reduce((a, b) => a + b, 0) / diffs.length) * 10) / 10;
  }
  return { medianMs: median, jitterMs: jitter, lossPercent: loss };
}

const ADAPTER_SCRIPT = `
$ErrorActionPreference = 'SilentlyContinue'
$out = [ordered]@{}
$out.adapters = @(Get-NetAdapter | Where-Object Status -eq 'Up' | ForEach-Object {
  [ordered]@{
    name = $_.Name
    description = $_.InterfaceDescription
    linkSpeedMbps = [math]::Round($_.Speed / 1e6, 0)
    connector = "$($_.MediaType)"
  }
})
$out.errors = @(Get-NetAdapterStatistics | Where-Object { $_.ReceivedPacketErrors -gt 0 -or $_.OutboundPacketErrors -gt 0 } | ForEach-Object {
  [ordered]@{ name = $_.Name; inErr = $_.ReceivedPacketErrors; outErr = $_.OutboundPacketErrors }
})
$out.wifi = $null
$wlan = netsh wlan show interfaces 2>$null
if ($LASTEXITCODE -eq 0 -and $wlan) {
  $pick = { param($pattern) $line = ($wlan | Select-String $pattern).Line; if ($line -match ':\\s*(.+)$') { $Matches[1].Trim() } else { '' } }
  $numOf = { param($line) if ($line -and $line -match '(\\d+(?:\\.\\d+)?)') { [double]$Matches[1] } else { $null } }
  $signalLine = ($wlan | Select-String 'Se.al|Signal').Line
  $sigPct = $null
  if ($signalLine -match '(\\d+)%') { $sigPct = [int]$Matches[1] }
  $out.wifi = [ordered]@{
    ssid = (& $pick 'SSID')
    band = (& $pick 'Tipo de radio|Radio type')
    signalPercent = $sigPct
    rxRateMbps = (& $numOf ($wlan | Select-String 'Velocidad de recepci|Receive rate').Line)
    txRateMbps = (& $numOf ($wlan | Select-String 'Velocidad de transmisi|Transmit rate').Line)
  }
}
$out | ConvertTo-Json -Depth 4 -Compress
`;

/**
 * Bufferbloat probe (#23): ping samples taken by a background job while the
 * foreground saturates down (or up) against Cloudflare speed endpoints.
 */
const BLOAT_SCRIPT_TEMPLATE = `
$ErrorActionPreference = 'SilentlyContinue'
$mode = '__MODE__'
$idleJob = Start-Job {
  $samples = @()
  for ($i = 0; $i -lt 14; $i++) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $r = ping -n 1 1.1.1.1 | Out-Null
    $sw.Stop()
    if ($LASTEXITCODE -eq 0) { $samples += $sw.Elapsed.TotalMilliseconds }
    Start-Sleep -Milliseconds 250
  }
  $samples -join ','
}
Start-Sleep -Milliseconds 400
try {
  if ($mode -eq 'down') {
    Invoke-WebRequest -Uri 'http://speed.cloudflare.com/__down?bytes=30000000' -UseBasicParsing -TimeoutSec 20 | Out-Null
  } else {
    $payload = New-Object byte[] 6000000
    Invoke-WebRequest -Uri 'http://speed.cloudflare.com/__up' -Method Post -Body $payload -UseBasicParsing -TimeoutSec 20 | Out-Null
  }
} catch { }
Wait-Job $idleJob -Timeout 40 | Out-Null
$result = Receive-Job $idleJob
Remove-Job $idleJob -Force -ErrorAction SilentlyContinue
"$result"
`;

export async function runNetworkDiagnostics(): Promise<NetworkDiagnosticsReport> {
  const report: NetworkDiagnosticsReport = {
    takenAt: new Date().toISOString(),
    gateway: { address: '', ping: emptyPing() },
    internet: { target: '1.1.1.1', ping: emptyPing() },
    adapters: [],
    wifi: null,
    adapterErrors: [],
    bufferbloat: {
      idleMedianMs: null, loadedDownloadMedianMs: null, loadedUploadMedianMs: null,
      downloadIncreaseMs: null, uploadIncreaseMs: null, grade: 'unknown',
    },
    healthEs: '',
    healthEn: '',
  };
  if (process.platform !== 'win32') return report;

  const gwRes = await runPowerShell(GATEWAY_SCRIPT, false, { timeoutMs: 15_000 });
  report.gateway.address = gwRes.success && gwRes.output ? gwRes.output.trim() : '';

  const [gwPing, inetPing, adapterRaw] = await Promise.all([
    report.gateway.address
      ? runPowerShell(buildPingScript(report.gateway.address, 8), false, { timeoutMs: 30_000 })
      : Promise.resolve({ success: false }),
    runPowerShell(buildPingScript('1.1.1.1', 8), false, { timeoutMs: 30_000 }),
    runPowerShell(ADAPTER_SCRIPT, false, { timeoutMs: 25_000 }),
  ]);

  report.gateway.ping = gwPing.success ? parsePing(gwPing as { success: boolean; output?: string }) : emptyPing();
  report.internet.ping = parsePing(inetPing);

  if (adapterRaw.success && (adapterRaw as { output?: string }).output) {
    try {
      const parsed = JSON.parse((adapterRaw as { output: string }).output) as Record<string, unknown>;
      report.adapters = ((parsed.adapters ?? []) as AdapterInfo[]).map((a) => ({
        ...a,
        linkSpeedMbps: Number.isFinite(Number(a.linkSpeedMbps)) ? Number(a.linkSpeedMbps) : null,
      }));
      report.adapterErrors = ((parsed.errors ?? []) as Array<{ name: string; inErr: number; outErr: number }>).map((e) => ({
        name: e.name,
        inboundErrors: Number(e.inErr) || 0,
        outboundErrors: Number(e.outErr) || 0,
      }));
      const wifi = parsed.wifi as Record<string, unknown> | null;
      if (wifi && typeof wifi === 'object' && Object.keys(wifi).length > 0) {
        report.wifi = {
          ssid: (wifi.ssid as string) || null,
          band: (wifi.band as string) || null,
          signalPercent: Number.isFinite(Number(wifi.signalPercent)) ? Number(wifi.signalPercent) : null,
          rxRateMbps: Number.isFinite(Number(wifi.rxRateMbps)) ? Number(wifi.rxRateMbps) : null,
          txRateMbps: Number.isFinite(Number(wifi.txRateMbps)) ? Number(wifi.txRateMbps) : null,
        };
      }
    } catch {
      /* keep defaults */
    }
  }

  // ---- bufferbloat measurement
  try {
    const idleMedian = report.internet.ping.medianMs;
    const runMode = async (mode: 'down' | 'up'): Promise<PingStats> =>
      parsePing(await runPowerShell(BLOAT_SCRIPT_TEMPLATE.replace('__MODE__', mode), false, { timeoutMs: 120_000 }));

    const downLoaded = await runMode('down');
    const upLoaded = await runMode('up');
    const bloat = report.bufferbloat;
    bloat.idleMedianMs = idleMedian;
    bloat.loadedDownloadMedianMs = downLoaded.medianMs;
    bloat.loadedUploadMedianMs = upLoaded.medianMs;
    if (idleMedian !== null && downLoaded.medianMs !== null) {
      bloat.downloadIncreaseMs = Math.max(0, Math.round(downLoaded.medianMs - idleMedian));
    }
    if (idleMedian !== null && upLoaded.medianMs !== null) {
      bloat.uploadIncreaseMs = Math.max(0, Math.round(upLoaded.medianMs - idleMedian));
    }
    const worst = Math.max(bloat.downloadIncreaseMs ?? 0, bloat.uploadIncreaseMs ?? 0);
    bloat.grade = worst <= 30 ? 'good' : worst <= 60 ? 'fair' : 'poor';
  } catch {
    /* leave unknown */
  }

  // ---- health summary (#22): PC vs Router vs ISP
  const es: string[] = [];
  const en: string[] = [];
  const bloatWorst = Math.max(report.bufferbloat.downloadIncreaseMs ?? 0, report.bufferbloat.uploadIncreaseMs ?? 0);

  if (report.gateway.ping.lossPercent >= 50 && report.internet.ping.lossPercent >= 50) {
    es.push('Sin respuesta del router ni de Internet: revisa cable/Wi-Fi y el router.');
    en.push('No response from router or Internet: check cabling/Wi-Fi and the router.');
  } else if (report.gateway.ping.lossPercent > 2) {
    es.push('Pérdida hacia el ROUTER: problema local (Wi-Fi débil o cable).');
    en.push('Loss to the ROUTER: local problem (weak Wi-Fi or cable).');
  } else if (report.internet.ping.lossPercent > 2) {
    es.push('Pérdida hacia Internet con el router estable: posible problema del ISP.');
    en.push('Internet loss with stable router: possible ISP problem.');
  }
  if (report.bufferbloat.grade === 'poor') {
    es.push(`Bufferbloat alto (+${bloatWorst} ms bajo carga): activa SQM/QoS en el router; no se arregla con tweaks de registro.`);
    en.push(`High bufferbloat (+${bloatWorst} ms under load): enable SQM/QoS on the router; registry tweaks cannot fix this.`);
  }
  if (report.wifi?.signalPercent !== null && report.wifi?.signalPercent !== undefined && report.wifi.signalPercent < 60) {
    es.push('Señal Wi-Fi baja (<60 %): acércate al router o usa Ethernet.');
    en.push('Weak Wi-Fi signal (<60%): move closer or use Ethernet.');
  }
  report.healthEs = es.join(' ') || 'Red sin problemas evidentes en esta medición.';
  report.healthEn = en.join(' ') || 'Network shows no obvious problems in this measurement.';
  return report;
}
