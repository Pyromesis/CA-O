import { NextResponse } from "next/server";
import si from "systeminformation";

// Types for system information
interface CPUInfo {
  name: string;
  cores: number;
  threads: number;
  baseClock: string;
  maxClock: string;
  usage: number;
  temperature: number;
}

interface MemoryInfo {
  totalGB: number;
  totalBytes: number;
  availableGB: number;
  availableBytes: number;
  usedGB: number;
  usedBytes: number;
  usagePercent: number;
  slots: MemorySlot[];
}

interface MemorySlot {
  slot: string;
  sizeGB: number;
  type: string;
  speedMHz: number;
  manufacturer: string;
}

interface DiskInfo {
  driveLetter: string;
  label: string;
  totalGB: number;
  freeGB: number;
  usedGB: number;
  usagePercent: number;
  fileSystem: string;
  isSSD: boolean;
}

interface GPUInfo {
  name: string;
  manufacturer: string;
  vramMB: number;
  driverVersion: string;
  driverDate: string;
  usage: number;
  temperature: number;
}

interface NetworkAdapter {
  name: string;
  type: "Ethernet" | "Wi-Fi" | "Bluetooth";
  status: "Connected" | "Disconnected" | "Disabled";
  ipAddress?: string;
  macAddress: string;
  dnsServers: string[];
  speedMbps: number;
}

interface DisplayInfo {
  name: string;
  resolution: { width: number; height: number };
  refreshRate: number;
  primary: boolean;
  bitDepth: number;
}

interface SystemInfoResponse {
  success: boolean;
  data?: {
    os: {
      name: string;
      version: string;
      build: string;
      edition: string;
      architecture: string;
      installDate: string;
      lastBootTime: string;
      uptime: {
        days: number;
        hours: number;
        minutes: number;
        seconds: number;
        formatted: string;
      };
    };
    cpu: CPUInfo;
    memory: MemoryInfo;
    disks: DiskInfo[];
    gpu: GPUInfo[];
    network: NetworkAdapter[];
    displays: DisplayInfo[];
    systemHealth: {
      overallStatus: "Good" | "Fair" | "Poor" | "Critical";
      temperatureStatus: "Normal" | "Elevated" | "High" | "Critical";
      performanceScore: number;
      recommendations: string[];
    };
    timestamp: string;
  };
  error?: string;
}

// Helper functions
function formatUptime(days: number, hours: number, minutes: number): string {
  if (days > 0) return `${days}d ${hours}h ${minutes}m`;
  if (hours > 0) return `${hours}h ${minutes}m`;
  return `${minutes}m`;
}

// Caché dividida: los datos estáticos (GPU, discos, RAM slots, red) son carísimos
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
const RESPONSE_TTL = 2000; // 2s: evita tormentas de sondeos solapados

// Precalentar WMI al cargar el módulo: cuando el cliente pida, ya está listo
const warmup = (async () => {
  try {
    const [osInfo, cpu, memLayout, fsSize, graphics, netInterfaces] = await Promise.all([
      si.osInfo(), si.cpu(), si.memLayout(), si.fsSize(), si.graphics(), si.networkInterfaces(),
    ]);
    staticCache = { osInfo, cpu, memLayout, fsSize, graphics, netInterfaces };
    staticCacheTime = Date.now();
    console.log('[system/info] static WMI cache pre-warmed');
  } catch { /* will retry on first request */ }
})();

// Precalentar WMI al cargar el módulo
async function getRealSystemInfo(): Promise<SystemInfoResponse["data"]> {
  const now = Date.now();

  // Respuesta de hace <2s: reutilizar tal cual (los sondeos de 5s del dashboard se solapan)
  if (responseCache && (now - responseCacheTime < RESPONSE_TTL)) {
    return responseCache;
  }

  // Stale-while-revalidate: si hay caché (aunque expirada), devolver YA y refrescar en background
  if (!staticCache) {
    // Primera vez: esperar el warmup (ya se disparó al cargar el módulo)
    await warmup;
  } else if (now - staticCacheTime > STATIC_TTL) {
    // Expirada: devolver la vieja YA y refrescar en background
    const stale = staticCache;
    (async () => {
      try {
        const [osInfo, cpu, memLayout, fsSize, graphics, netInterfaces] = await Promise.all([
          si.osInfo(), si.cpu(), si.memLayout(), si.fsSize(), si.graphics(), si.networkInterfaces(),
        ]);
        staticCache = { osInfo, cpu, memLayout, fsSize, graphics, netInterfaces };
        staticCacheTime = Date.now();
        responseCache = null; // invalidar la respuesta compuesta
      } catch { staticCache = stale; /* mantener la vieja si falla */ }
    })();
  }
  const sc = staticCache!;
  const { osInfo, cpu, memLayout, fsSize, graphics, netInterfaces } = sc;

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
    slot: slot.bank || `Slot ${i + 1}`,
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
      name: `${cpu.manufacturer} ${cpu.brand}`,
      cores: cpu.physicalCores,
      threads: cpu.cores,
      baseClock: `${cpu.speed} GHz`,
      maxClock: `${cpu.speedMax} GHz`,
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
      slots: slots,
    },
    disks: disks,
    gpu: gpuList,
    network: networks,
    displays: displays,
    systemHealth: {
      overallStatus: (cpuTemp > 85 ? 'Critical' : cpuTemp > 70 ? 'High' : Math.round(currentLoad.currentLoad) > 90 ? 'Fair' : 'Good') as 'Good' | 'Fair' | 'Poor' | 'Critical',
      temperatureStatus: (cpuTemp > 85 ? 'Critical' : cpuTemp > 70 ? 'High' : cpuTemp > 55 ? 'Elevated' : 'Normal') as 'Normal' | 'Elevated' | 'High' | 'Critical',
      performanceScore: Math.max(0, Math.min(100, Math.round(100 - currentLoad.currentLoad * 0.6 - (mem.total - mem.available) / mem.total * 40))),
      recommendations: [],
    },
    timestamp: new Date().toISOString(),
  };

  responseCache = data;
  responseCacheTime = now;
  return data;
}

// GET handler - Get system information
export async function GET(): Promise<NextResponse<SystemInfoResponse>> {
  try {
    const systemInfo = await getRealSystemInfo();
    
    return NextResponse.json({
      success: true,
      data: systemInfo
    });
    
  } catch (error) {
    console.error("Error fetching system information:", error);
    
    return NextResponse.json(
      {
        success: false,
        error: "Failed to retrieve system information",
        message: error instanceof Error ? error.message : "Unknown error occurred"
      },
      { status: 500 }
    );
  }
}

