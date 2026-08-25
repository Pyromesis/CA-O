import { NextRequest, NextResponse } from "next/server";
import { takeSystemSample } from "@/lib/benchmarks/system-sample";
import { guardOrResponse } from "@/lib/api-security";

/**
 * POST /api/benchmark/system
 * Lightweight before/after sampling: CPU load, RAM, commit charge and
 * memory compression state. FPS/frame-time capture needs a native helper
 * and is intentionally not simulated.
 */
export async function POST(request: NextRequest) {
  const guard = guardOrResponse(request, true);
  if (guard) return NextResponse.json(guard.json, { status: guard.status });

  try {
    const sample = await takeSystemSample();
    return NextResponse.json({
      success: true,
      data: sample,
      note: {
        es: 'Métricas reales del sistema en este instante. CA-O no inventa datos de FPS; la captura por juego requiere un componente nativo futuro.',
        en: 'Real instant system metrics. CA-O does not fabricate FPS numbers; per-game capture requires a future native helper.',
      },
      timestamp: new Date().toISOString(),
    });
  } catch (error) {
    console.error('System sample failed:', error);
    return NextResponse.json(
      { success: false, error: error instanceof Error ? error.message : 'System sample failed.' },
      { status: 500 }
    );
  }
}
