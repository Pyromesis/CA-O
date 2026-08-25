import fs from 'node:fs';

// ── 1) system/info: precargar WMI al arrancar + stale-while-revalidate ──
{
  const p = 'src/app/api/system/info/route.ts';
  let s = fs.readFileSync(p, 'utf8');

  // Precalentar en el arranque del módulo (fire-and-forget)
  if (!s.includes('// Precalentar WMI al cargar el módulo')) {
    s = s.replace(
      'async function getRealSystemInfo()',
      `// Precalentar WMI al cargar el módulo: cuando el cliente pida, ya está listo
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
async function getRealSystemInfo()`
    );
  }

  // Stale-while-revalidate: si hay caché expirada, devolverla YA y refrescar en background
  if (!s.includes('stale-while-revalidate')) {
    s = s.replace(
      `  // Datos estáticos: solo se consultan vía WMI una vez cada 10 minutos
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
  }`,
      `  // Stale-while-revalidate: si hay caché (aunque expirada), devolver YA y refrescar en background
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
  }`
    );
  }

  fs.writeFileSync(p, s);
  console.log('system/info: prewarm + stale-while-revalidate');
}

// ── 2) optimization: SIN verificación en GET (solo DB, instantáneo) ──
{
  const p = 'src/app/api/optimization/route.ts';
  let s = fs.readFileSync(p, 'utf8');

  const oldLoop = `    const rows = await db.optimizationState.findMany();
    const appliedStateMap: Record<string, { appliedAt: string }> = {};
    const activeRows = rows.filter(r => r.applied && !sessionScopedOptimizationIds.has(r.id));
    const CHUNK = 6;
    for (let i = 0; i < activeRows.length; i += CHUNK) {
      const batch = activeRows.slice(i, i + CHUNK);
      await Promise.all(batch.map(async (row) => {
        const ok = await verifyWithCache(row.id, verificationCommands[row.id]);
        if (!ok) {
          await db.optimizationState.updateMany({
            where: { id: row.id },
            data: { applied: false }
          });
          return;
        }
        appliedStateMap[row.id] = { appliedAt: row.updatedAt.toISOString() };
      }));
    }`;
  const NEW = `    // Estado desde la DB directamente (instantáneo). La verificación real
    // la hace /api/optimization/state que el cliente llama por separado.
    const rows = await db.optimizationState.findMany();
    const appliedStateMap: Record<string, { appliedAt: string }> = {};
    for (const row of rows) {
      if (row.applied && !sessionScopedOptimizationIds.has(row.id)) {
        appliedStateMap[row.id] = { appliedAt: row.updatedAt.toISOString() };
      }
    }`;
  if (s.includes(oldLoop)) {
    s = s.replace(oldLoop, NEW);
    console.log('optimization GET: verificación movida a /state');
  } else {
    console.log('optimization loop pattern not found, checking...');
  }

  fs.writeFileSync(p, s);
}
