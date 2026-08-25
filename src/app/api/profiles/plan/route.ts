import { NextRequest, NextResponse } from "next/server";
import { predefinedProfiles } from "@/lib/profiles";
import { isExecutableOptimizationId } from "@/lib/optimization-commands";
import { evaluateApplicability } from "@/lib/catalog/applicability";
import { getTaxonomy } from "@/lib/catalog/taxonomy";
import { getScoredOptimization } from "@/lib/catalog/evidence";
import { getSystemContext } from "@/lib/system-context";

/** Safe-mode criteria (#11): low risk + decent confidence only. */
function passesSafeMode(id: string): boolean {
  const taxonomy = getTaxonomy(id);
  if (!taxonomy) return false;
  if (
    taxonomy.group === 'experimental' ||
    taxonomy.group === 'security' ||
    taxonomy.group === 'repair' ||
    taxonomy.kind === 'security-tradeoff'
  ) {
    return false;
  }
  const scored = getScoredOptimization(id);
  return scored.score.total >= 60 && scored.confidence !== 'low' && scored.confidence !== 'unknown';
}

/**
 * POST /api/profiles/plan  { profileId, safeMode?: boolean }
 *
 * Adaptive profile planning (v2): resolves a profile against the live
 * machine and returns which items should apply, be skipped (with reason),
 * or require explicit confirmation. Security-tradeoff items are NEVER
 * auto-approved by profiles. With safeMode=true only low-risk,
 * high-confidence items survive.
 */
export async function POST(request: NextRequest) {
  try {
    const body: unknown = await request.json();
    if (!body || typeof body !== 'object' || Array.isArray(body)) {
      return NextResponse.json(
        { success: false, error: "Request body must be an object" },
        { status: 400 }
      );
    }
    const { profileId, safeMode } = body as { profileId?: string; safeMode?: boolean };
    if (typeof profileId !== 'string' || !profileId) {
      return NextResponse.json({ success: false, error: "Missing profileId" }, { status: 400 });
    }

    const profile = predefinedProfiles.find((p) => p.id === profileId);
    if (!profile) {
      return NextResponse.json({ success: false, error: `Unknown profile '${profileId}'` }, { status: 404 });
    }

    const ctx = await getSystemContext();
    const plan = [] as Array<{
      id: string;
      status: 'apply' | 'skip' | 'requires-confirmation';
      group?: string;
      kind?: string;
      reasonEs?: string;
      reasonEn?: string;
    }>;

    for (const id of profile.optimizationIds) {
      if (!isExecutableOptimizationId(id)) continue;

      const taxonomy = getTaxonomy(id);
      const result = await evaluateApplicability(id, ctx);

      // Security trade-offs are excluded from adaptive profiles by design (#6).
      if (taxonomy?.kind === 'security-tradeoff') {
        plan.push({
          id,
          status: 'skip',
          group: taxonomy.group,
          kind: taxonomy.kind,
          reasonEs: 'Cambio de seguridad crítico; los perfiles no lo aplican nunca automáticamente. Aplícalo individualmente si lo entiendes y lo aceptas.',
          reasonEn: 'Critical security change; profiles never auto-apply it. Apply it individually if you understand and accept it.',
        });
        continue;
      }

      if (!result.applicable) {
        plan.push({
          id,
          status: 'skip',
          group: taxonomy?.group,
          kind: taxonomy?.kind,
          reasonEs: result.blockers.map((b) => b.es).join(' | '),
          reasonEn: result.blockers.map((b) => b.en).join(' | '),
        });
        continue;
      }

      // Safe mode (#11): only low-risk, decent-confidence items.
      if (safeMode === true && !passesSafeMode(id)) {
        plan.push({
          id,
          status: 'skip',
          group: taxonomy?.group,
          kind: taxonomy?.kind,
          reasonEs: 'Modo seguro: excluido por riesgo o confianza insuficiente.',
          reasonEn: 'Safe mode: excluded due to risk or insufficient confidence.',
        });
        continue;
      }

      plan.push({
        id,
        status: 'apply',
        group: taxonomy?.group,
        kind: taxonomy?.kind,
        reasonEs: undefined,
        reasonEn: undefined,
      });
    }

    return NextResponse.json({
      success: true,
      data: {
        profileId: profile.id,
        contextSummary: {
          formFactor: ctx.hardware.formFactor,
          powerSource: ctx.power.powerSource,
          ramGB: ctx.hardware.ramGB,
          secureBoot: ctx.security.secureBoot,
          hvciEnabled: ctx.security.hvciEnabled,
          antiCheats: ctx.antiCheats,
        },
        plan,
        summary: {
          total: plan.length,
          toApply: plan.filter((p) => p.status === 'apply').length,
          skipped: plan.filter((p) => p.status === 'skip').length,
        },
      },
      timestamp: new Date().toISOString(),
    });
  } catch (error) {
    console.error('Profile planning failed:', error);
    return NextResponse.json(
      { success: false, error: error instanceof Error ? error.message : 'Profile planning failed.' },
      { status: 500 }
    );
  }
}
