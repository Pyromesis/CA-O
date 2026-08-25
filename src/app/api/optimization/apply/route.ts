import { NextRequest, NextResponse } from "next/server";
import { realCommands, verificationCommands, originalStateCommands, sessionScopedOptimizationIds, repeatableOptimizationIds, isExecutableOptimizationId, irreversibleOptimizationIds } from "@/lib/optimization-commands";
import { runPowerShell, createSystemRestorePoint } from "@/lib/powershell-runner";
import { db } from "@/lib/db";
import { guardOrResponse } from "@/lib/api-security";
import { evaluateApplicability } from "@/lib/catalog/applicability";
import { getTaxonomy } from "@/lib/catalog/taxonomy";
import { getSystemContext, getHardwareFingerprint } from "@/lib/system-context";

interface ApplyOptimizationRequest {
  optimizationId: string;
  createBackup?: boolean;
  confirmDangerous?: boolean;
  /** Required for security-tradeoff items (e.g. disable-memory-integrity). */
  confirmSecurityChange?: boolean;
  /** Required for experimental items (contested evidence). */
  acknowledgeExperimental?: boolean;
}

interface AppliedOptimization {
  id: string;
  nameKey: string;
  descriptionKey: string;
  name: string;
  description: string;
  category: string;
  icon: string;
  isApplied: boolean;
  isEnabled: boolean;
  requiresRestart: boolean;
  riskLevel: string;
  appliedAt?: string;
}

interface ApiResponse<T = unknown> {
  success: boolean;
  data?: T;
  error?: string;
  message?: string;
  changesMade?: string[];
  timestamp?: string;
  executionTime?: number;
}

/**
 * Behavioral verification (requirement #20): when possible, verify real
 * behavior instead of only a registry value. Runs after the primary
 * verification command for the listed IDs.
 */
const behavioralVerificationById: Record<string, { script: string; failureEs: string; failureEn: string }> = {
  'disable-memory-integrity': {
    script: "$dg = Get-CimInstance -ClassName Win32_DeviceGuard -Namespace root/Microsoft/Windows/DeviceGuard; if ($null -eq $dg) { exit 1 }; if ($dg.SecurityServicesRunning -contains 1) { exit 1 } else { exit 0 }",
    failureEs: 'Windows sigue reportando HVCI en ejecución según DeviceGuard.',
    failureEn: 'Windows still reports HVCI running according to DeviceGuard.',
  },
  'dns-optimization': {
    script: "try { $r = Resolve-DnsName -Name www.microsoft.com -QuickTimeout -ErrorAction Stop; if ($null -eq $r) { exit 1 } else { exit 0 } } catch { exit 1 }",
    failureEs: 'La resolución DNS no funciona tras el cambio (prueba de conectividad fallida).',
    failureEn: 'DNS resolution is broken after the change (connectivity test failed).',
  },
  'disable-hibernation': {
    script: "powercfg /a | Select-String 'Hibernation has been disabled' -Quiet",
    failureEs: 'Windows aún reporta hibernación disponible.',
    failureEn: 'Windows still reports hibernation available.',
  },
};

// Generate display name from ID
function generateName(id: string): string {
  // Convert from snake-case to title case
  return id.split('-').map(part =>
    part.charAt(0).toUpperCase() + part.slice(1)
  ).join(' ');
}

export async function POST(request: NextRequest) {
  const startTime = Date.now();

  const guard = guardOrResponse(request, true);
  if (guard) return NextResponse.json(guard.json, { status: guard.status });

  try {
    const body: unknown = await request.json();
    if (!body || typeof body !== 'object' || Array.isArray(body)) {
      return NextResponse.json<ApiResponse>(
        { success: false, error: "Request body must be an object" },
        { status: 400 }
      );
    }

    const requestBody = body as Partial<ApplyOptimizationRequest>;
    if (typeof requestBody.optimizationId !== 'string' || requestBody.optimizationId.trim().length === 0) {
      return NextResponse.json<ApiResponse>(
        { success: false, error: "Missing required field: optimizationId" },
        { status: 400 }
      );
    }
    if (requestBody.createBackup !== undefined && typeof requestBody.createBackup !== 'boolean') {
      return NextResponse.json<ApiResponse>(
        { success: false, error: "createBackup must be a boolean" },
        { status: 400 }
      );
    }
    if (requestBody.confirmDangerous !== undefined && typeof requestBody.confirmDangerous !== 'boolean') {
      return NextResponse.json<ApiResponse>(
        { success: false, error: "confirmDangerous must be a boolean" },
        { status: 400 }
      );
    }
    if (requestBody.confirmSecurityChange !== undefined && typeof requestBody.confirmSecurityChange !== 'boolean') {
      return NextResponse.json<ApiResponse>(
        { success: false, error: "confirmSecurityChange must be a boolean" },
        { status: 400 }
      );
    }
    if (requestBody.acknowledgeExperimental !== undefined && typeof requestBody.acknowledgeExperimental !== 'boolean') {
      return NextResponse.json<ApiResponse>(
        { success: false, error: "acknowledgeExperimental must be a boolean" },
        { status: 400 }
      );
    }

    const optimizationId = requestBody.optimizationId;
    const id = optimizationId;
    const createBackup = requestBody.createBackup ?? false;
    const confirmDangerous = requestBody.confirmDangerous ?? false;

    if (!id) {
      return NextResponse.json<ApiResponse>(
        { success: false, error: "Missing required field: optimizationId" },
        { status: 400 }
      );
    }

    if (!isExecutableOptimizationId(id)) {
      return NextResponse.json<ApiResponse>(
        {
          success: false,
          error: `No commands configured for optimization '${id}'`,
          message: "This optimization ID is not recognized"
        },
        { status: 404 }
      );
    }

    if (irreversibleOptimizationIds.has(id) && !confirmDangerous) {
      return NextResponse.json<ApiResponse>(
        { success: false, error: "This irreversible optimization requires explicit confirmation." },
        { status: 409 }
      );
    }

    // ---- v2 applicability gate (context + anti-cheat + experimentals) ----
    const ctx = await getSystemContext();
    const applicability = await evaluateApplicability(id, ctx, {
      confirmSecurityChange: requestBody.confirmSecurityChange,
      acknowledgeExperimental: requestBody.acknowledgeExperimental,
    });
    if (!applicability.applicable) {
      return NextResponse.json<ApiResponse>(
        {
          success: false,
          error: "This optimization cannot be applied on this machine right now.",
          message: applicability.blockers.length
            ? applicability.blockers.map((b) => `${b.es} / ${b.en}`).join(' | ')
            : undefined,
          data: {
            blockers: applicability.blockers,
            warnings: applicability.warnings,
            systemContext: {
              formFactor: ctx.hardware.formFactor,
              powerSource: ctx.power.powerSource,
              ramGB: ctx.hardware.ramGB,
              secureBoot: ctx.security.secureBoot,
              hvciEnabled: ctx.security.hvciEnabled,
              antiCheats: ctx.antiCheats.map((c) => c.id),
            },
          },
        },
        { status: 422 }
      );
    }

    const cmdSet = realCommands[id];

    // Check existing state
    const existing = await db.optimizationState.findUnique({
      where: { id: id }
    });

    if (existing?.applied && !sessionScopedOptimizationIds.has(id) && !repeatableOptimizationIds.has(id)) {
      return NextResponse.json<ApiResponse>(
        {
          success: false,
          error: `Optimization '${id}' is already applied`,
          message: "This optimization has already been applied. Use revert to undo it."
        },
        { status: 409 }
      );
    }

    let backupWarning: string | undefined;
    if (createBackup) {
      console.log(`Creating system restore point before applying ${id}...`);
      const backupResult = await createSystemRestorePoint(`CA-O Backup: ${id}`);
      if (!backupResult.success) {
        backupWarning = backupResult.error || 'Restore point could not be created';
        console.warn('Restore point failed, continuing without backup:', backupWarning);
      }
    }

    const changesMade: string[] = [];
    let originalSnapshot: string | undefined;
    let snapshotMeta: Record<string, unknown> | undefined;
    let hasError = false;
    let errorMessage = "";

    const captureCommand = originalStateCommands[id];
    if (captureCommand) {
      const captureResult = await runPowerShell(captureCommand);
      if (!captureResult.success || !captureResult.output) {
        return NextResponse.json<ApiResponse>(
          { success: false, error: "Could not capture the original Windows state; no changes were applied.", message: captureResult.error },
          { status: 503 }
        );
      }
      try {
        const parsedSnapshot = JSON.parse(captureResult.output);
        if (typeof parsedSnapshot !== 'object' || parsedSnapshot === null || typeof parsedSnapshot.exists !== 'boolean') {
          throw new Error('Invalid original state snapshot');
        }
        originalSnapshot = JSON.stringify(parsedSnapshot);
        // v2 structured provenance (#18)
        const [fingerprint] = await Promise.all([getHardwareFingerprint()]);
        snapshotMeta = {
          capturedAt: new Date().toISOString(),
          windowsBuild: ctx.os.build,
          windowsEdition: ctx.os.edition,
          hardwareFingerprint: fingerprint,
          elevatedSession: ctx.security.elevatedSession,
        };
      } catch {
        return NextResponse.json<ApiResponse>(
          { success: false, error: "Could not parse the original Windows state; no changes were applied." },
          { status: 503 }
        );
      }
    }

    for (const cmd of cmdSet.commands) {
      const result = await runPowerShell(cmd.script);
      if (result.success) {
        changesMade.push(`✅ ${cmd.description}: ${result.output || 'Done'}`);
      } else {
        changesMade.push(`⚠️ ${cmd.description}: ${result.error || 'Failed (may require Admin)'}`);
        hasError = true;
        errorMessage = result.error || 'Failed (may require Admin)';
        break;
      }
    }

    if (!hasError) {
      const verificationCommand = verificationCommands[id];
      if (!verificationCommand) {
        hasError = true;
        errorMessage = 'No post-application verification is configured for this optimization.';
      } else {
        const verification = await runPowerShell(verificationCommand);
        if (!verification.success) {
          hasError = true;
          errorMessage = verification.error || 'Post-application verification failed.';
          changesMade.push(`⚠️ Verification: ${errorMessage}`);
        }
      }
    }

    // v2 behavioral check on top of the registry/config verification.
    if (!hasError && behavioralVerificationById[id]) {
      const behavioral = await runPowerShell(behavioralVerificationById[id].script);
      if (!behavioral.success) {
        hasError = true;
        errorMessage = `Behavioral verification failed: ${behavioral.error || behavioralVerificationById[id].failureEn}`;
        changesMade.push(`⚠️ Behavioral verification: ${behavioralVerificationById[id].failureEs}`);
      }
    }

    const executionTime = Date.now() - startTime;

    if (!hasError) {
      // Update DB state
      if (!sessionScopedOptimizationIds.has(id)) {
        await db.optimizationState.upsert({
          where: { id: optimizationId },
          update: { applied: true, snapshot: originalSnapshot, meta: snapshotMeta },
          create: { id: optimizationId, applied: true, snapshot: originalSnapshot, meta: snapshotMeta }
        });
      }

      const taxonomy = getTaxonomy(optimizationId);

      return NextResponse.json<ApiResponse>({
        success: true,
        message: backupWarning,
        data: {
          backupWarning,
          optimization: {
            id: optimizationId,
            name: generateName(optimizationId),
            description: `Applied ${generateName(optimizationId)} optimization`,
            category: Object.keys(realCommands).find(k => k === optimizationId)?.split('-')[0] || 'system',
            icon: 'Shield',
            isApplied: true,
            isEnabled: true,
            requiresRestart: cmdSet.rebootRequired,
            riskLevel: irreversibleOptimizationIds.has(optimizationId) ? 'dangerous' : cmdSet.rebootRequired ? 'medium' : 'safe',
            appliedAt: new Date().toISOString()
          },
          group: taxonomy?.group,
          kind: taxonomy?.kind,
          warnings: applicability.warnings,
          snapshotMeta,
          changesMade,
          rebootRequired: cmdSet.rebootRequired,
          executionTime
        }
      });
    } else {
      return NextResponse.json<ApiResponse>(
        {
          success: false,
          error: `Failed to apply '${id}'.`,
          message: `${errorMessage} Review the command output and run the app as Administrator if Windows denied access.`,
          changesMade,
          executionTime
        },
        { status: 500 }
      );
    }

  } catch (error) {
    console.error("Error applying optimization:", error);
    return NextResponse.json<ApiResponse>(
      {
        success: false,
        error: "Failed to apply optimization",
        message: error instanceof Error ? error.message : "Unknown error occurred",
        timestamp: new Date().toISOString()
      },
      { status: 500 }
    );
  }
}
