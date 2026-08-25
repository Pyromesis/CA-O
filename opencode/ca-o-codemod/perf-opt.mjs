import fs from 'node:fs';
const p = 'src/app/api/optimization/route.ts';
let s = fs.readFileSync(p, 'utf8');

// import del cache compartido
s = s.replace(
  'import { runPowerShell } from "@/lib/powershell-runner";',
  'import { verifyWithCache } from "@/lib/verify-cache";'
);

// bucle secuencial → paralelo con caché compartida
const OLD = `    const rows = await db.optimizationState.findMany();
    const appliedStateMap: Record<string, { appliedAt: string }> = {};
    for (const row of rows) {
      if (row.applied && !sessionScopedOptimizationIds.has(row.id)) {
        const verificationCommand = verificationCommands[row.id];
        const verification = verificationCommand
          ? await runPowerShell(verificationCommand)
          : { success: true };

        if (!verification.success) {
          await db.optimizationState.updateMany({
            where: { id: row.id },
            data: { applied: false }
          });
          continue;
        }
        appliedStateMap[row.id] = { appliedAt: row.updatedAt.toISOString() };
      }
    }`;
const NEW = `    const rows = await db.optimizationState.findMany();
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
if (!s.includes(OLD)) throw new Error('optimization route loop not found');
s = s.replace(OLD, NEW);
fs.writeFileSync(p, s);
console.log('optimization route: paralelo + cache');
