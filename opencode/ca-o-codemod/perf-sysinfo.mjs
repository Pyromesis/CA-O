import fs from 'node:fs';
const p = 'src/app/api/system/info/route.ts';
let s = fs.readFileSync(p, 'utf8');

// 1) sustituir la caché global corta por doble caché
const oldCache = `// Global cache to avoid querying WMI too frequently which can cause spikes
let systemInfoCache: SystemInfoResponse["data"] | null = null;
let lastCacheTime = 0;
const CACHE_TTL = 5000; // 5 seconds cache`;
const newCache = `// Caché dividida: los datos estáticos (GPU, discos, RAM slots, red) son carísimos
// de obtener vía WMI y casi nunca cambian; las métricas dinámicas (uso CPU/RAM,
// temperatura) son baratas y se recalculan en cada petición.
let staticCache: {
  osInfo: Awaited<ReturnType<typeof si.osInfo>>;
  cpu: Awaited<ReturnType<typeof si.cpu>>;
  memLayout: Awaited<ReturnType<typeof si.memLayout>>;
  fsSize: Awaited<ReturnType<typeof si.fsSize>>;
  graphics: Awaited<ReturnType<typeof si.graphics>>;
  netInterfaces: Awaited<ReturnType<typeof si.networkInterfaces>>;
} | null = null;
let staticCacheTime = 0;
const STATIC_TTL = 10 * 60 * 1000; // 10 minutos

let responseCache: SystemInfoResponse["data"] | null = null;
let responseCacheTime = 0;
const RESPONSE_TTL = 2000; // 2s: evita tormentas de sondeos solapados`;
if (!s.includes(oldCache)) throw new Error('cache block not found');
s = s.replace(oldCache, newCache);

// 2) reescribir el cuerpo de getRealSystemInfo
const fnStart = s.indexOf('async function getRealSystemInfo');
const fnEnd = s.indexOf('\n}\n', fnStart) + 3;
if (fnStart < 0 || fnEnd < fnStart) throw new Error('function bounds not found');

const NEW_FN = `async function getRealSystemInfo(): Promise<SystemInfoResponse["data"]> {
  const now = Date.now();

  // Respuesta de hace <2s: reutilizar tal cual (los sondeos de 5s del dashboard se solapan)
  if (responseCache && (now - responseCacheTime < RESPONSE_TTL)) {
    return responseCache;
  }

  // Datos estáticos: solo se consultan vía WMI una vez cada 10 minutos
  if (!staticCache || (now - staticCacheTime > STATIC_TTL)) {
    const [osInfo, cpu, memLayout, fsSize, graphics, netInterfaces] = await Promise.all([
      si.osInfo(),
      si.cpu(),
      si.memLayout(),
      si.fsSize(),
      si.graphics(),
      si.networkInterfaces(),
    ]);
    staticCache = { osInfo, cpu, memLayout, fsSize, graphics, netInterfaces };
    staticCacheTime = now;
  }
  const { osInfo, cpu, memLayout, fsSize, graphics, netInterfaces } = staticCache;

  // Métricas dinámicas baratas en paralelo
  const [mem, currentLoad] = await Promise.all([si.mem(), si.currentLoad()]);

  let cpuTemp = 0;
  try {
    const tempInfo = await si.cpuTemperature();
    if (tempInfo && tempInfo.main && tempInfo.main > 0) cpuTemp = tempInfo.main;
  } catch { /* requiere admin en Windows */ }

  const time = si.time();
  const uptimeSeconds = time.uptime;
  const uptimeDays = Math.floor(uptimeSeconds / 86400);
  const remainingHours = Math.floor((uptimeSeconds % 86400) / 3600);
  const uptimeMinutes = Math.floor((uptimeSeconds % 3600) / 60);
  const bootTime = new Date(Date.now() - uptimeSeconds * 1000);

  const slots: MemorySlot[] = memLayout.map((slot, i) => ({
    slot: slot.bank || \`Slot \${i + 1}\`,
    sizeGB: Math.round(slot.size / (1024 ** 3) * 100) / 100,
    type: slot.type || 'Unknown',
    speedMHz: slot.clockSpeed || 0,
    manufacturer: slot.manufacturer || 'Unknown',
  }));

  const disks: DiskInfo[] = fsSize
    .filter(fs => fs.fs !== 'overlay' && fs.size > 0 && fs.mount.match(/^[A-Z]:/))
    .map(fs => ({
      driveLetter: fs.mount,
      label: fs.type,
      totalGB: Math.round(fs.size / (1024 ** 3)),
      usedGB: Math.round(fs.used / (1024 ** 3)),
      freeGB: Math.round(fs.size / (1024 ** 3)) - Math.round(fs.used / (1024 ** 3)),
      usagePercent: fs.use,
      fileSystem: fs.type,
      isSSD: false,
    }));

  const gpuList: GPUInfo[] = graphics.controllers.map(g => ({
    name: g.model || 'Unknown GPU',
    manufacturer: g.vendor || 'Unknown',
    vramMB: g.vram || 0,
    driverVersion: '',
    driverDate: '',
    usage: 0,
    temperature: 0,
  }));

  const networks: NetworkAdapter[] = Array.isArray(netInterfaces) ? netInterfaces
    .filter(n => !n.virtual && n.ip4)
    .map(n => {
      let type: 'Ethernet' | 'Wi-Fi' | 'Bluetooth' = 'Ethernet';
      if (n.type.toLowerCase().includes('wireless')) type = 'Wi-Fi';
      return {
        name: n.ifaceName || n.iface || 'Network Adapter',
        type,
        status: n.operstate === 'up' ? 'Connected' : 'Disconnected',
        ipAddress: n.ip4,
        macAddress: n.mac,
        dnsServers: [],
        speedMbps: n.speed || 0,
      };
    }) : [];

  const displays: DisplayInfo[] = graphics.displays.map(d => ({
    name: d.model || 'Unknown display',
    resolution: { width: d.resolutionX || 0, height: d.resolutionY || 0 },
    refreshRate: d.currentRefreshRate || 0,
    primary: d.main,
    bitDepth: d.pixelDepth || 0,
  }));

  const data = {
    os: {
      name: 'Windows',
      version: osInfo.release,
      build: osInfo.build,
      edition: osInfo.distro,
      architecture: osInfo.arch,
      installDate: 'Unavailable',
      lastBootTime: bootTime.toISOString(),
      uptime: {
        days: uptimeDays,
        hours: remainingHours,
        minutes: uptimeMinutes,
        seconds: Math.floor(uptimeSeconds % 60),
        formatted: formatUptime(uptimeDays, remainingHours, uptimeMinutes),
      },
    },
    cpu: {
      name: \`\${cpu.manufacturer} \${cpu.brand}\`,
      cores: cpu.physicalCores,
      threads: cpu.cores,
      baseClock: \`\${cpu.speed} GHz\`,
      maxClock: \`\${cpu.speedMax} GHz\`,
      usage: Math.round(currentLoad.currentLoad),
      temperature: cpuTemp,
    },
    memory: {
      totalGB: Math.round(mem.total / (1024 ** 3) * 100) / 100,
      totalBytes: mem.total,
      availableGB: Math.round(mem.available / (1024 ** 3) * 100) / 100,
      availableBytes: mem.available,
      usedGB: Math.round((mem.total - mem.available) / (1024 ** 3) * 100) / 100,
      usedBytes: mem.total - mem.available,
      usagePercent: Math.round(((mem.total - mem.available) / mem.total) * 100),
    },
    disks: disks,
    gpu: gpuList,
    network: networks,
    displays: displays,
  };

  responseCache = data;
  responseCacheTime = now;
  return data;
`;
s = s.slice(0, fnStart) + NEW_FN + s.slice(fnEnd);

fs.writeFileSync(p, s);
console.log('system/info route optimizado');
