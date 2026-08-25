import { NextRequest, NextResponse } from "next/server";
import { guardOrResponse } from "@/lib/api-security";
import { getThermalReport, getInputReport } from "@/lib/diagnostics/engines";
import { runNetworkDiagnostics } from "@/lib/diagnostics/network";
import { buildDiagnosticsOverview } from "@/lib/diagnostics/overview";

/**
 * GET /api/diagnostics/[area]
 *   overview  - full diagnostics + real health score (#57/#58). Slow (~1-2 min with network).
 *   overview?fast=1 - skips the slow network/bufferbloat part.
 *   thermal   - CPU/GPU temperatures + throttling heuristic (#28)
 *   input     - pointer precision, DPC rate, devices (#14/#15)
 *   network   - ping/jitter/loss/wifi/bufferbloat (#22/#23)
 */
export async function GET(
  request: NextRequest,
  { params }: { params: Promise<{ area: string }> }
) {
  // Read-only diagnostics still rate-limited (heavy PowerShell work).
  const guard = guardOrResponse(request as NextRequest, true);
  if (guard) return NextResponse.json(guard.json, { status: guard.status });

  const { area } = await params;
  try {
    switch (area) {
      case 'overview': {
        const fast = new URL(request.url).searchParams.get('fast') === '1';
        const report = await buildDiagnosticsOverview(!fast);
        return NextResponse.json({ success: true, data: report });
      }
      case 'thermal': {
        return NextResponse.json({ success: true, data: await getThermalReport() });
      }
      case 'input': {
        return NextResponse.json({ success: true, data: await getInputReport() });
      }
      case 'network': {
        return NextResponse.json({ success: true, data: await runNetworkDiagnostics() });
      }
      default:
        return NextResponse.json(
          { success: false, error: `Unknown diagnostics area '${area}'. Use overview|thermal|input|network.` },
          { status: 404 }
        );
    }
  } catch (error) {
    console.error(`Diagnostics ${area} failed:`, error);
    return NextResponse.json(
      { success: false, error: error instanceof Error ? error.message : 'Diagnostics failed.' },
      { status: 500 }
    );
  }
}
