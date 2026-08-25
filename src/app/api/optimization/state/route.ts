import { NextResponse } from "next/server";
import { db } from "@/lib/db";
import { verifyWithCache } from "@/lib/verify-cache";
import { sessionScopedOptimizationIds, verificationCommands, realCommands } from "@/lib/optimization-commands";

/** GET /api/optimization/state  →  { [id]: true|false } */
export async function GET() {
  try {
    const rows = await db.optimizationState.findMany();
    const stateMap: Record<string, boolean> = {};

    // Podar filas de optimizaciones que ya no existen en el catálogo
    const pruned = new Set<string>();
    for (const row of rows) {
      if (!realCommands[row.id]) {
        pruned.add(row.id);
        if (row.applied) {
          await db.optimizationState.updateMany({
            where: { id: row.id },
            data: { applied: false }
          });
        }
      }
    }

    // Verificación en paralelo por lotes (mucho más rápida que secuencial)
    const CHUNK = 6;
    const activeRows = rows.filter(r => r.applied && !sessionScopedOptimizationIds.has(r.id) && !pruned.has(r.id));
    for (let i = 0; i < activeRows.length; i += CHUNK) {
      const batch = activeRows.slice(i, i + CHUNK);
      await Promise.all(batch.map(async (row) => {
        const ok = await verifyWithCache(row.id, verificationCommands[row.id]);
        stateMap[row.id] = ok;
        if (!ok) {
          await db.optimizationState.updateMany({
            where: { id: row.id },
            data: { applied: false }
          });
        }
      }));
    }

    // Los no aplicados explícitamente van a false
    for (const row of rows) {
      if (!(row.id in stateMap)) stateMap[row.id] = false;
    }
    for (const id of pruned) stateMap[id] = false;

    return NextResponse.json({ success: true, data: stateMap });
  } catch (error) {
    console.error("Error fetching optimization state:", error);
    return NextResponse.json({ success: false, data: {} }, { status: 500 });
  }
}
