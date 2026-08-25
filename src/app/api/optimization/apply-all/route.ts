import { NextRequest, NextResponse } from "next/server";
import { realCommands, verificationCommands, originalStateCommands, sessionScopedOptimizationIds, isExecutableOptimizationId, irreversibleOptimizationIds } from "@/lib/optimization-commands";
import { runPowerShell, createSystemRestorePoint } from "@/lib/powershell-runner";
import { db } from "@/lib/db";

interface ApplyAllRequest {
  ids: string[];
  createBackup?: boolean;
  confirmDangerous?: boolean;
}

interface AppliedOptimization {
  id: string;
  name: string;
  success: boolean;
  changesMade?: string[];
  error?: string;
}

interface ApiResponse<T = unknown> {
  success: boolean;
  data?: T;
  error?: string;
  message?: string;
  executionTime?: number;
}

// Generate display name from ID
function generateName(id: string): string {
  return id.split('-').map(part =>
    part.charAt(0).toUpperCase() + part.slice(1)
  ).join(' ');
}

export async function POST(request: NextRequest) {
  const startTime = Date.now();

  try {
    const body: unknown = await request.json();
    if (!body || typeof body !== 'object' || Array.isArray(body)) {
      return NextResponse.json<ApiResponse>(
        { success: false, error: "Request body must be an object" },
        { status: 400 }
      );
    }
    const requestBody = body as Partial<ApplyAllRequest>;
    if (!Array.isArray(requestBody.ids) || requestBody.ids.length === 0 || requestBody.ids.length > 100 || requestBody.ids.some((id) => typeof id !== 'string')) {
      return NextResponse.json<ApiResponse>(
        { success: false, error: "ids must be an array of at most 100 optimization IDs" },
        { status: 400 }
      );
    }

    if (requestBody.createBackup !== undefined && typeof requestBody.createBackup !== 'boolean') {
      return NextResponse.json<ApiResponse>(
        { success: false, error: "createBackup must be a boolean" },
        { status: 400 }
      );
    }
    const ids = [...new Set(requestBody.ids)];
    const createBackup = requestBody.createBackup ?? false;
    const confirmDangerous = requestBody.confirmDangerous ?? false;

    if (ids.length === 0) {
      return NextResponse.json<ApiResponse>(
        { success: false, error: "No optimization IDs provided" },
        { status: 400 }
      );
    }

    if (requestBody.confirmDangerous !== undefined && typeof requestBody.confirmDangerous !== 'boolean') {
      return NextResponse.json<ApiResponse>(
        { success: false, error: "confirmDangerous must be a boolean" },
        { status: 400 }
      );
    }

    const invalidId = ids.find((id) => !isExecutableOptimizationId(id));
    if (invalidId) {
      return NextResponse.json<ApiResponse>(
        { success: false, error: `Optimization '${invalidId}' is not executable` },
        { status: 400 }
      );
    }

    if (ids.some((id) => irreversibleOptimizationIds.has(id)) && !confirmDangerous) {
      return NextResponse.json<ApiResponse>(
        { success: false, error: "This irreversible optimization requires explicit confirmation." },
        { status: 409 }
      );
    }

    let backupWarning: string | undefined;
    if (createBackup) {
      console.log(`Creating system restore point before bulk apply of ${ids.length} optimizations...`);
      const backupResult = await createSystemRestorePoint("CA-O Bulk Backup");
      if (!backupResult.success) {
        backupWarning = backupResult.error || 'Restore point could not be created';
        console.warn('Restore point failed, continuing without backup:', backupWarning);
      }
    }

    const appliedOptimizations: AppliedOptimization[] = [];
    let rebootRequired = false;
    let someFailed = false;

    for (const id of ids) {
      try {
        const existing = await db.optimizationState.findUnique({ where: { id } });
        if (existing?.applied && !sessionScopedOptimizationIds.has(id)) {
          appliedOptimizations.push({
            id,
            name: generateName(id),
            success: false,
            error: "Already applied"
          });
          someFailed = true;
          continue;
        }

        const cmdSet = realCommands[id];

        const changesMade: string[] = [];
        let originalSnapshot: string | undefined;
        let hasError = false;
        let errorMessage = "";

        const captureCommand = originalStateCommands[id];
        if (captureCommand) {
          const captureResult = await runPowerShell(captureCommand);
          if (!captureResult.success || !captureResult.output) {
            throw new Error(captureResult.error || 'Could not capture original Windows state');
          }
          const parsedSnapshot = JSON.parse(captureResult.output) as { exists?: unknown; value?: unknown };
          if (typeof parsedSnapshot.exists !== 'boolean' || (parsedSnapshot.exists && typeof parsedSnapshot.value !== 'string')) {
            throw new Error('Invalid original Windows state snapshot');
          }
          originalSnapshot = JSON.stringify(parsedSnapshot);
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

        if (!hasError) {
          if (!sessionScopedOptimizationIds.has(id)) {
            await db.optimizationState.upsert({
              where: { id },
              update: { applied: true, snapshot: originalSnapshot },
              create: { id, applied: true, snapshot: originalSnapshot }
            });
          }

          if (cmdSet.rebootRequired) rebootRequired = true;

          appliedOptimizations.push({
            id,
            name: generateName(id),
            success: true,
            changesMade
          });
        } else {
          someFailed = true;
          appliedOptimizations.push({
            id,
            name: generateName(id),
            success: false,
            error: errorMessage,
            changesMade
          });
        }

      } catch (error) {
        someFailed = true;
        appliedOptimizations.push({
          id,
          name: generateName(id),
          success: false,
          error: error instanceof Error ? error.message : "Failed to apply optimization"
        });
      }
    }

    const executionTime = Date.now() - startTime;

    return NextResponse.json<ApiResponse>({
      success: !someFailed,
      data: {
        appliedOptimizations,
        rebootRequired,
        executionTime,
        summary: {
          successful: appliedOptimizations.filter(o => o.success).length,
          failed: appliedOptimizations.filter(o => !o.success).length,
          total: ids.length
        }
      }
    });

  } catch (error) {
    console.error("Error applying all optimizations:", error);
    return NextResponse.json<ApiResponse>(
      {
        success: false,
        error: "Failed to apply optimizations",
        message: error instanceof Error ? error.message : "Unknown error occurred"
      },
      { status: 500 }
    );
  }
}