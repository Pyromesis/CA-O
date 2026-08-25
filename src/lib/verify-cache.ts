import { runPowerShell } from './powershell-runner';

/**
 * Caché compartida de verificaciones entre /api/optimization y
 * /api/optimization/state. Sin ella, abrir la app lanza ~70 procesos
 * de PowerShell duplicados (cada ruta verifica lo mismo).
 */
const cache = new Map<string, { ok: boolean; at: number }>();
const TTL = 30_000; // 30s: suficiente para que ambos endpoints y la hidratación compartan resultado

export async function verifyWithCache(id: string, cmd?: string): Promise<boolean> {
  const hit = cache.get(id);
  const now = Date.now();
  if (hit && now - hit.at < TTL) return hit.ok;
  if (!cmd) return true;
  const v = await runPowerShell(cmd);
  const ok = !!v.success;
  cache.set(id, { ok, at: now });
  return ok;
}
