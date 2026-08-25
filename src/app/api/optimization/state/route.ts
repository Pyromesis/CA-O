import { NextRequest, NextResponse } from "next/server";
import { db } from "@/lib/db";
import { verifyWithCache } from "@/lib/verify-cache";
import { sessionScopedOptimizationIds, verificationCommands, realCommands } from "@/lib/optimization-commands";
import { guardOrResponse } from "@/lib/api-security";

/**
 * GET /api/optimization/state
 *
 * v2: returns desired vs actual state per optimization so external Windows
 * changes (drift) can be detected and reconciled.
 *  - desired: what CA-O recorded (db)
 *  - actual : live re-verification against Windows (cached 30s)
 */
export async function GET(request: NextRequest) {
  const guard = guardOrResponse(request, false);
  if (guard) return NextResponse.json(guard.json, { status: guard.status });
  try {
    const rows = await db.optimizationState.findMany();

    interface StateRow {
      id: string;
      desired: boolean;
      actual: boolean;
      lastVerifiedAt?: string;
      lastError?: string;
      drift: boolean;
    }
    const detailed: Record<string, StateRow> = {};
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
        let ok: boolean;
        try {
          ok = await verifyWithCache(row.id, verificationCommands[row.id]);
        } catch (verifyError) {
          ok = false;
          detailed[row.id] = {
            id: row.id,
            desired: true,
            actual: false,
            lastVerifiedAt: new Date().toISOString(),
            lastError: verifyError instanceof Error ? verifyError.message : String(verifyError),
            drift: true,
          };
          return;
        }
        stateMap[row.id] = ok;
        detailed[row.id] = {
          id: row.id,
          desired: true,
          actual: ok,
          lastVerifiedAt: new Date().toISOString(),
          drift: !ok,
        };
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

    const drifted = Object.values(detailed).filter((r) => r.drift).map((r) => r.id);

    return NextResponse.json({
      success: true,
      data: stateMap,
      meta: {
        details: detailed,
        drifted,
        reconciledAt: new Date().toISOString(),
        note: 'desired = CA-O record; actual = live verification; drifted items were auto-reconciled to actual.',
      },
    });
  } catch (error) {
    console.error("Error fetching optimization state:", error);
    return NextResponse.json({ success: false, data: {} }, { status: 500 });
  }
}
