import { NextRequest, NextResponse } from "next/server";
import { runDnsBenchmark } from "@/lib/benchmarks/dns-benchmark";
import { guardOrResponse } from "@/lib/api-security";

/**
 * POST /api/benchmark/dns  { queriesPerProvider?: number }
 *
 * Measures DNS latency/jitter/timeouts for common resolvers from this
 * machine and reports the best performer. Never changes system DNS.
 */
export async function POST(request: NextRequest) {
  const guard = guardOrResponse(request, true);
  if (guard) return NextResponse.json(guard.json, { status: guard.status });

  try {
    let queriesPerProvider = 6;
    const body: unknown = await request.json().catch(() => null);
    if (body && typeof body === 'object' && !Array.isArray(body)) {
      const q = (body as { queriesPerProvider?: unknown }).queriesPerProvider;
      if (typeof q === 'number' && Number.isFinite(q)) queriesPerProvider = Math.floor(q);
    }

    const result = await runDnsBenchmark(queriesPerProvider);

    return NextResponse.json({
      success: true,
      data: result,
      timestamp: new Date().toISOString(),
    });
  } catch (error) {
    console.error('DNS benchmark failed:', error);
    return NextResponse.json(
      { success: false, error: error instanceof Error ? error.message : 'DNS benchmark failed.' },
      { status: 500 }
    );
  }
}
