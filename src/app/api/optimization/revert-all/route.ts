import { NextRequest, NextResponse } from "next/server";
import { revertCommands, revertVerificationCommands, irreversibleOptimizationIds, repeatableOptimizationIds, isExecutableOptimizationId } from "@/lib/optimization-commands";
import { runPowerShell } from "@/lib/powershell-runner";
import { guardOrResponse } from "@/lib/api-security";
import { db } from "@/lib/db";

interface RevertAllRequest {
  ids: string[];
}

interface RevertedOptimization {
  id: string;
  name: string;
  success: boolean;
  changesReverted?: string[];
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
  const guard = guardOrResponse(request, true);
  if (guard) return NextResponse.json(guard.json, { status: guard.status });
  const startTime = Date.now();

  try {
    const body: unknown = await request.json();
    if (!body || typeof body !== 'object' || Array.isArray(body)) {
      return NextResponse.json<ApiResponse>(
        { success: false, error: "Request body must be an object" },
        { status: 400 }
      );
    }
    const requestBody = body as Partial<RevertAllRequest>;
    if (!Array.isArray(requestBody.ids) || requestBody.ids.length === 0 || requestBody.ids.length > 100 || requestBody.ids.some((id) => typeof id !== 'string')) {
      return NextResponse.json<ApiResponse>(
        { success: false, error: "ids must be an array of at most 100 optimization IDs" },
        { status: 400 }
      );
    }

    const ids = [...new Set(requestBody.ids)];

    if (ids.length === 0) {
      return NextResponse.json<ApiResponse>(
        { success: false, error: "No optimization IDs provided" },
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

    const revertedOptimizations: RevertedOptimization[] = [];
    let rebootRequired = false;
    let someFailed = false;

    for (const id of ids) {
      if (repeatableOptimizationIds.has(id)) {
        // Maintenance actions have no persistent state to restore.
        revertedOptimizations.push({
          id,
          name: generateName(id),
          success: true,
          changesReverted: ["No persistent state to restore; available to run again."]
        });
        continue;
      }
      if (irreversibleOptimizationIds.has(id)) {
        revertedOptimizations.push({
          id,
          name: generateName(id),
          success: false,
          error: "This optimization is irreversible and cannot be reverted automatically."
        });
        someFailed = true;
        continue;
      }
      const cmdSet = revertCommands[id];
      if (!cmdSet) {
        revertedOptimizations.push({
          id,
          name: generateName(id),
          success: false,
          error: `No revert commands configured for ${id}`
        });
        someFailed = true;
        continue;
      }

      const revertVerificationCommand = revertVerificationCommands[id];
      if (!revertVerificationCommand) {
        revertedOptimizations.push({
          id,
          name: generateName(id),
          success: false,
          error: "This optimization has no verified automatic revert path yet."
        });
        someFailed = true;
        continue;
      }

      const existing = await db.optimizationState.findUnique({ where: { id } });
      if (!existing?.applied) {
        // Warning but we can try to revert it
        revertedOptimizations.push({
          id,
          name: generateName(id),
          success: true,
          changesReverted: ["No revert needed - not tracked as applied"]
        });
        continue;
      }

      if (id === 'disable-startup-sound' && !existing.snapshot) {
        revertedOptimizations.push({
          id,
          name: generateName(id),
          success: false,
          error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically."
        });
        someFailed = true;
        continue;
      }
      if (id === 'disable-cortana' && !existing.snapshot) {
        revertedOptimizations.push({
          id,
          name: generateName(id),
          success: false,
          error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically."
        });
        someFailed = true;
        continue;
      }
      if (id === 'disable-screen-saver' && !existing.snapshot) {
        revertedOptimizations.push({
          id,
          name: generateName(id),
          success: false,
          error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically."
        });
        someFailed = true;
        continue;
      }
      if (id === 'disable-mouse-trails' && !existing.snapshot) {
        revertedOptimizations.push({
          id,
          name: generateName(id),
          success: false,
          error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically."
        });
        someFailed = true;
        continue;
      }
      if (id === 'hide-task-view' && !existing.snapshot) {
        revertedOptimizations.push({
          id,
          name: generateName(id),
          success: false,
          error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically."
        });
        someFailed = true;
        continue;
      }
      if (id === 'disable-aero-peek' && !existing.snapshot) {
        revertedOptimizations.push({
          id,
          name: generateName(id),
          success: false,
          error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically."
        });
        someFailed = true;
        continue;
      }
      if (id === 'disable-tooltips' && !existing.snapshot) {
        revertedOptimizations.push({
          id,
          name: generateName(id),
          success: false,
          error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically."
        });
        someFailed = true;
        continue;
      }
      if (id === 'disable-wallpaper-slideshow' && !existing.snapshot) {
        revertedOptimizations.push({
          id,
          name: generateName(id),
          success: false,
          error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically."
        });
        someFailed = true;
        continue;
      }
      if (id === 'disable-system-sounds' && !existing.snapshot) {
        revertedOptimizations.push({
          id,
          name: generateName(id),
          success: false,
          error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically."
        });
        someFailed = true;
        continue;
      }
      if (id === 'show-hidden-files' && !existing.snapshot) {
        revertedOptimizations.push({
          id,
          name: generateName(id),
          success: false,
          error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically."
        });
        someFailed = true;
        continue;
      }
      if (id === 'disable-start-menu-suggestions' && !existing.snapshot) {
        revertedOptimizations.push({
          id,
          name: generateName(id),
          success: false,
          error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically."
        });
        someFailed = true;
        continue;
      }
      if (id === 'disable-taskbar-search' && !existing.snapshot) {
        revertedOptimizations.push({
          id,
          name: generateName(id),
          success: false,
          error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically."
        });
        someFailed = true;
        continue;
      }
      if (id === 'show-file-extensions' && !existing.snapshot) {
        revertedOptimizations.push({
          id,
          name: generateName(id),
          success: false,
          error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically."
        });
        someFailed = true;
        continue;
      }
      if (id === 'disable-background-apps' && !existing.snapshot) {
        revertedOptimizations.push({
          id,
          name: generateName(id),
          success: false,
          error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically."
        });
        someFailed = true;
        continue;
      }
      if (id === 'disable-cast-notifications' && !existing.snapshot) {
        revertedOptimizations.push({
          id,
          name: generateName(id),
          success: false,
          error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically."
        });
        someFailed = true;
        continue;
      }
      if (id === 'disable-thumbnails' && !existing.snapshot) {
        revertedOptimizations.push({
          id,
          name: generateName(id),
          success: false,
          error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically."
        });
        someFailed = true;
        continue;
      }
      if (id === 'disable-lock-screen' && !existing.snapshot) {
        revertedOptimizations.push({
          id,
          name: generateName(id),
          success: false,
          error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically."
        });
        someFailed = true;
        continue;
      }
      if (id === 'disable-advertising-id' && !existing.snapshot) {
        revertedOptimizations.push({
          id,
          name: generateName(id),
          success: false,
          error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically."
        });
        someFailed = true;
        continue;
      }
      if (id === 'disable-tailored-experiences' && !existing.snapshot) {
        revertedOptimizations.push({ id, name: generateName(id), success: false, error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically." });
        someFailed = true;
        continue;
      }
      if (id === 'disable-windows-feedback' && !existing.snapshot) {
        revertedOptimizations.push({ id, name: generateName(id), success: false, error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically." });
        someFailed = true;
        continue;
      }
      if (id === 'disable-cloud-content' && !existing.snapshot) {
        revertedOptimizations.push({ id, name: generateName(id), success: false, error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically." });
        someFailed = true;
        continue;
      }
      if (id === 'disable-start-tracking' && !existing.snapshot) {
        revertedOptimizations.push({ id, name: generateName(id), success: false, error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically." });
        someFailed = true;
        continue;
      }
      if (id === 'disable-app-suggestions' && !existing.snapshot) {
        revertedOptimizations.push({ id, name: generateName(id), success: false, error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically." });
        someFailed = true;
        continue;
      }
      if (id === 'disable-setting-sync' && !existing.snapshot) {
        revertedOptimizations.push({ id, name: generateName(id), success: false, error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically." });
        someFailed = true;
        continue;
      }
      if (id === 'disable-handwriting-data' && !existing.snapshot) {
        revertedOptimizations.push({ id, name: generateName(id), success: false, error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically." });
        someFailed = true;
        continue;
      }
      if (id === 'disable-speech-recognition' && !existing.snapshot) {
        revertedOptimizations.push({ id, name: generateName(id), success: false, error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically." });
        someFailed = true;
        continue;
      }
      if (id === 'disable-find-my-device' && !existing.snapshot) {
        revertedOptimizations.push({ id, name: generateName(id), success: false, error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically." });
        someFailed = true;
        continue;
      }
      if (id === 'disable-diagnostic-data' && !existing.snapshot) {
        revertedOptimizations.push({ id, name: generateName(id), success: false, error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically." });
        someFailed = true;
        continue;
      }
      if (id === 'disable-camera-access' && !existing.snapshot) {
        revertedOptimizations.push({ id, name: generateName(id), success: false, error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically." });
        someFailed = true;
        continue;
      }
      if (id === 'disable-microphone-access' && !existing.snapshot) {
        revertedOptimizations.push({ id, name: generateName(id), success: false, error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically." });
        someFailed = true;
        continue;
      }

      try {
        const changesReverted: string[] = [];
        let commandsToRun = cmdSet.commands;
        let verificationToRun = revertVerificationCommands[id];
        if (id === 'disable-startup-sound' && existing.snapshot) {
          const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
          if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'string')) {
            throw new Error('Invalid startup sound snapshot');
          }
          const capturedValue = typeof snapshot.value === 'string' ? snapshot.value : '';
          const escapedValue = capturedValue.replace(/'/g, "''");
          commandsToRun = [{
            description: 'Restoring the captured startup sound value',
            script: snapshot.exists
              ? `Set-ItemProperty -Path 'HKCU:\\AppEvents\\Schemes\\Apps\\.Default\\WindowsLogon' -Name '(Default)' -Value '${escapedValue}' -Force -ErrorAction Stop`
              : "Remove-ItemProperty -Path 'HKCU:\\AppEvents\\Schemes\\Apps\\.Default\\WindowsLogon' -Name '(Default)' -ErrorAction Stop"
          }];
          verificationToRun = snapshot.exists
            ? `if ((Get-ItemProperty 'HKCU:\\AppEvents\\Schemes\\Apps\\.Default\\WindowsLogon' -Name '(Default)' -ErrorAction Stop).'(Default)' -ne '${escapedValue}') { throw 'Captured startup sound value was not restored' }`
            : "if (Get-ItemProperty 'HKCU:\\AppEvents\\Schemes\\Apps\\.Default\\WindowsLogon' -Name '(Default)' -ErrorAction SilentlyContinue) { throw 'Startup sound value was not removed' }";
        }
        if (id === 'disable-cortana' && existing.snapshot) {
          const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
          if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'number')) {
            throw new Error('Invalid Cortana policy snapshot');
          }
          const restoreScript = snapshot.exists
            ? `Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Search' -Name AllowCortana -Value ${snapshot.value} -Type DWord -Force -ErrorAction Stop`
            : "Remove-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Search' -Name AllowCortana -ErrorAction Stop";
          commandsToRun = [{ description: 'Restoring the captured Cortana policy', script: restoreScript }];
          verificationToRun = snapshot.exists
            ? `if ((Get-ItemProperty 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Search' -Name AllowCortana -ErrorAction Stop).AllowCortana -ne ${snapshot.value}) { throw 'Captured Cortana policy was not restored' }`
            : "if (Get-ItemProperty 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Search' -Name AllowCortana -ErrorAction SilentlyContinue) { throw 'Cortana policy was not removed' }";
        }
        if (id === 'disable-screen-saver' && existing.snapshot) {
          const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
          if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'string')) {
            throw new Error('Invalid screen saver snapshot');
          }
          const capturedValue = typeof snapshot.value === 'string' ? snapshot.value : '';
          const escapedValue = capturedValue.replace(/'/g, "''");
          commandsToRun = [{
            description: 'Restoring the captured screen saver value',
            script: snapshot.exists
              ? `Set-ItemProperty -Path 'HKCU:\\Control Panel\\Desktop' -Name ScreenSaveActive -Value '${escapedValue}' -Force -ErrorAction Stop`
              : "Remove-ItemProperty -Path 'HKCU:\\Control Panel\\Desktop' -Name ScreenSaveActive -ErrorAction Stop"
          }];
          verificationToRun = snapshot.exists
            ? `if ((Get-ItemProperty 'HKCU:\\Control Panel\\Desktop' -Name ScreenSaveActive -ErrorAction Stop).ScreenSaveActive -ne '${escapedValue}') { throw 'Captured screen saver value was not restored' }`
            : "if (Get-ItemProperty 'HKCU:\\Control Panel\\Desktop' -Name ScreenSaveActive -ErrorAction SilentlyContinue) { throw 'Screen saver value was not removed' }";
        }
        if (id === 'disable-mouse-trails' && existing.snapshot) {
          const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
          if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'string')) {
            throw new Error('Invalid mouse trails snapshot');
          }
          const capturedValue = typeof snapshot.value === 'string' ? snapshot.value : '';
          const escapedValue = capturedValue.replace(/'/g, "''");
          commandsToRun = [{
            description: 'Restoring the captured mouse trails value',
            script: snapshot.exists
              ? `Set-ItemProperty -Path 'HKCU:\\Control Panel\\Mouse' -Name MouseTrails -Value '${escapedValue}' -Force -ErrorAction Stop`
              : "Remove-ItemProperty -Path 'HKCU:\\Control Panel\\Mouse' -Name MouseTrails -ErrorAction Stop"
          }];
          verificationToRun = snapshot.exists
            ? `if ((Get-ItemProperty 'HKCU:\\Control Panel\\Mouse' -Name MouseTrails -ErrorAction Stop).MouseTrails -ne '${escapedValue}') { throw 'Captured mouse trails value was not restored' }`
            : "if (Get-ItemProperty 'HKCU:\\Control Panel\\Mouse' -Name MouseTrails -ErrorAction SilentlyContinue) { throw 'Mouse trails value was not removed' }";
        }
        if (id === 'hide-task-view' && existing.snapshot) {
          const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
          if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'number')) {
            throw new Error('Invalid Task View snapshot');
          }
          const restoreScript = snapshot.exists
            ? `Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name ShowTaskViewButton -Value ${snapshot.value} -Type DWord -Force -ErrorAction Stop`
            : "Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name ShowTaskViewButton -ErrorAction Stop";
          commandsToRun = [{ description: 'Restoring the captured Task View value', script: restoreScript }];
          verificationToRun = snapshot.exists
            ? `if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name ShowTaskViewButton -ErrorAction Stop).ShowTaskViewButton -ne ${snapshot.value}) { throw 'Captured Task View value was not restored' }`
            : "if (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name ShowTaskViewButton -ErrorAction SilentlyContinue) { throw 'Task View value was not removed' }";
        }
        if (id === 'disable-aero-peek' && existing.snapshot) {
          const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
          if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'number')) {
            throw new Error('Invalid Aero Peek snapshot');
          }
          const restoreScript = snapshot.exists
            ? `Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name DisablePreviewDesktop -Value ${snapshot.value} -Type DWord -Force -ErrorAction Stop`
            : "Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name DisablePreviewDesktop -ErrorAction Stop";
          commandsToRun = [{ description: 'Restoring the captured Aero Peek value', script: restoreScript }];
          verificationToRun = snapshot.exists
            ? `if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name DisablePreviewDesktop -ErrorAction Stop).DisablePreviewDesktop -ne ${snapshot.value}) { throw 'Captured Aero Peek value was not restored' }`
            : "if (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name DisablePreviewDesktop -ErrorAction SilentlyContinue) { throw 'Aero Peek value was not removed' }";
        }
        if (id === 'disable-tooltips' && existing.snapshot) {
          const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
          if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'number')) {
            throw new Error('Invalid tooltips snapshot');
          }
          const restoreScript = snapshot.exists
            ? `Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name ShowInfoTip -Value ${snapshot.value} -Type DWord -Force -ErrorAction Stop`
            : "Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name ShowInfoTip -ErrorAction Stop";
          commandsToRun = [{ description: 'Restoring the captured tooltips value', script: restoreScript }];
          verificationToRun = snapshot.exists
            ? `if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name ShowInfoTip -ErrorAction Stop).ShowInfoTip -ne ${snapshot.value}) { throw 'Captured tooltips value was not restored' }`
            : "if (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name ShowInfoTip -ErrorAction SilentlyContinue) { throw 'Tooltips value was not removed' }";
        }
        if (id === 'disable-wallpaper-slideshow' && existing.snapshot) {
          const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
          if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'number')) {
            throw new Error('Invalid wallpaper slideshow snapshot');
          }
          const restoreScript = snapshot.exists
            ? `Set-ItemProperty -Path 'HKCU:\\Control Panel\\Personalization\\Desktop Slideshow' -Name Interval -Value ${snapshot.value} -Type DWord -Force -ErrorAction Stop`
            : "Remove-ItemProperty -Path 'HKCU:\\Control Panel\\Personalization\\Desktop Slideshow' -Name Interval -ErrorAction Stop";
          commandsToRun = [{ description: 'Restoring the captured wallpaper slideshow interval', script: restoreScript }];
          verificationToRun = snapshot.exists
            ? `if ((Get-ItemProperty 'HKCU:\\Control Panel\\Personalization\\Desktop Slideshow' -Name Interval -ErrorAction Stop).Interval -ne ${snapshot.value}) { throw 'Captured wallpaper slideshow interval was not restored' }`
            : "if (Get-ItemProperty 'HKCU:\\Control Panel\\Personalization\\Desktop Slideshow' -Name Interval -ErrorAction SilentlyContinue) { throw 'Wallpaper slideshow interval was not removed' }";
        }
        if (id === 'disable-system-sounds' && existing.snapshot) {
          const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
          if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'string')) {
            throw new Error('Invalid system sounds snapshot');
          }
          const capturedValue = typeof snapshot.value === 'string' ? snapshot.value : '';
          const escapedValue = capturedValue.replace(/'/g, "''");
          commandsToRun = [{
            description: 'Restoring the captured system sounds value',
            script: snapshot.exists
              ? `Set-ItemProperty -Path 'HKCU:\\AppEvents\\Schemes' -Name '(Default)' -Value '${escapedValue}' -Force -ErrorAction Stop`
              : "Remove-ItemProperty -Path 'HKCU:\\AppEvents\\Schemes' -Name '(Default)' -ErrorAction Stop"
          }];
          verificationToRun = snapshot.exists
            ? `if ((Get-ItemProperty 'HKCU:\\AppEvents\\Schemes' -Name '(Default)' -ErrorAction Stop).'(Default)' -ne '${escapedValue}') { throw 'Captured system sounds value was not restored' }`
            : "if (Get-ItemProperty 'HKCU:\\AppEvents\\Schemes' -Name '(Default)' -ErrorAction SilentlyContinue) { throw 'System sounds value was not removed' }";
        }
        if (id === 'show-hidden-files' && existing.snapshot) {
          const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
          if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'number')) {
            throw new Error('Invalid hidden files snapshot');
          }
          const restoreScript = snapshot.exists
            ? `Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name Hidden -Value ${snapshot.value} -Type DWord -Force -ErrorAction Stop`
            : "Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name Hidden -ErrorAction Stop";
          commandsToRun = [{ description: 'Restoring the captured hidden files value', script: restoreScript }];
          verificationToRun = snapshot.exists
            ? `if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name Hidden -ErrorAction Stop).Hidden -ne ${snapshot.value}) { throw 'Captured hidden files value was not restored' }`
            : "if (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name Hidden -ErrorAction SilentlyContinue) { throw 'Hidden files value was not removed' }";
        }
        if (id === 'disable-start-menu-suggestions' && existing.snapshot) {
          const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
          if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'number')) {
            throw new Error('Invalid Start menu suggestions snapshot');
          }
          const restoreScript = snapshot.exists
            ? `Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager' -Name SystemPaneSuggestionsEnabled -Value ${snapshot.value} -Type DWord -Force -ErrorAction Stop`
            : "Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager' -Name SystemPaneSuggestionsEnabled -ErrorAction Stop";
          commandsToRun = [{ description: 'Restoring the captured Start menu suggestions value', script: restoreScript }];
          verificationToRun = snapshot.exists
            ? `if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager' -Name SystemPaneSuggestionsEnabled -ErrorAction Stop).SystemPaneSuggestionsEnabled -ne ${snapshot.value}) { throw 'Captured Start menu suggestions value was not restored' }`
            : "if (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager' -Name SystemPaneSuggestionsEnabled -ErrorAction SilentlyContinue) { throw 'Start menu suggestions value was not removed' }";
        }
        if (id === 'disable-taskbar-search' && existing.snapshot) {
          const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
          if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'number')) {
            throw new Error('Invalid taskbar search snapshot');
          }
          const restoreScript = snapshot.exists
            ? `Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Search' -Name SearchboxTaskbarMode -Value ${snapshot.value} -Type DWord -Force -ErrorAction Stop`
            : "Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Search' -Name SearchboxTaskbarMode -ErrorAction Stop";
          commandsToRun = [{ description: 'Restoring the captured taskbar search value', script: restoreScript }];
          verificationToRun = snapshot.exists
            ? `if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Search' -Name SearchboxTaskbarMode -ErrorAction Stop).SearchboxTaskbarMode -ne ${snapshot.value}) { throw 'Captured taskbar search value was not restored' }`
            : "if (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Search' -Name SearchboxTaskbarMode -ErrorAction SilentlyContinue) { throw 'Taskbar search value was not removed' }";
        }
        if (id === 'show-file-extensions' && existing.snapshot) {
          const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
          if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'number')) {
            throw new Error('Invalid file extensions snapshot');
          }
          const restoreScript = snapshot.exists
            ? `Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name HideFileExt -Value ${snapshot.value} -Type DWord -Force -ErrorAction Stop; Stop-Process -Name explorer -Force -ErrorAction Stop`
            : "Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name HideFileExt -ErrorAction Stop; Stop-Process -Name explorer -Force -ErrorAction Stop";
          commandsToRun = [{ description: 'Restoring the captured file extensions value', script: restoreScript }];
          verificationToRun = snapshot.exists
            ? `if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name HideFileExt -ErrorAction Stop).HideFileExt -ne ${snapshot.value}) { throw 'Captured file extensions value was not restored' }`
            : "if (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name HideFileExt -ErrorAction SilentlyContinue) { throw 'File extensions value was not removed' }";
        }
        if (id === 'disable-background-apps' && existing.snapshot) {
          const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
          if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'number')) {
            throw new Error('Invalid background apps snapshot');
          }
          const restoreScript = snapshot.exists
            ? `Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\BackgroundAccessApplications' -Name GlobalUserDisabled -Value ${snapshot.value} -Type DWord -Force -ErrorAction Stop`
            : "Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\BackgroundAccessApplications' -Name GlobalUserDisabled -ErrorAction Stop";
          commandsToRun = [{ description: 'Restoring the captured background apps value', script: restoreScript }];
          verificationToRun = snapshot.exists
            ? `if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\BackgroundAccessApplications' -Name GlobalUserDisabled -ErrorAction Stop).GlobalUserDisabled -ne ${snapshot.value}) { throw 'Captured background apps value was not restored' }`
            : "if (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\BackgroundAccessApplications' -Name GlobalUserDisabled -ErrorAction SilentlyContinue) { throw 'Background apps value was not removed' }";
        }
        if (id === 'disable-cast-notifications' && existing.snapshot) {
          const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
          if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'number')) {
            throw new Error('Invalid cast notifications snapshot');
          }
          const restoreScript = snapshot.exists
            ? `Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Casting' -Name AllowWhileLocked -Value ${snapshot.value} -Type DWord -Force -ErrorAction Stop`
            : "Remove-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Casting' -Name AllowWhileLocked -ErrorAction Stop";
          commandsToRun = [{ description: 'Restoring the captured cast notifications value', script: restoreScript }];
          verificationToRun = snapshot.exists
            ? `if ((Get-ItemProperty 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Casting' -Name AllowWhileLocked -ErrorAction Stop).AllowWhileLocked -ne ${snapshot.value}) { throw 'Captured cast notifications value was not restored' }`
            : "if (Get-ItemProperty 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Casting' -Name AllowWhileLocked -ErrorAction SilentlyContinue) { throw 'Cast notifications value was not removed' }";
        }
        if (id === 'disable-thumbnails' && existing.snapshot) {
          const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
          if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'number')) {
            throw new Error('Invalid thumbnails snapshot');
          }
          const restoreScript = snapshot.exists
            ? `Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name IconsOnly -Value ${snapshot.value} -Type DWord -Force -ErrorAction Stop; Stop-Process -Name explorer -Force -ErrorAction Stop`
            : "Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name IconsOnly -ErrorAction Stop; Stop-Process -Name explorer -Force -ErrorAction Stop";
          commandsToRun = [{ description: 'Restoring the captured thumbnails value', script: restoreScript }];
          verificationToRun = snapshot.exists
            ? `if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name IconsOnly -ErrorAction Stop).IconsOnly -ne ${snapshot.value}) { throw 'Captured thumbnails value was not restored' }`
            : "if (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name IconsOnly -ErrorAction SilentlyContinue) { throw 'Thumbnails value was not removed' }";
        }
        if (id === 'disable-lock-screen' && existing.snapshot) {
          const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
          if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'number')) {
            throw new Error('Invalid lock screen snapshot');
          }
          const restoreScript = snapshot.exists
            ? `Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\Personalization' -Name NoLockScreen -Value ${snapshot.value} -Type DWord -Force -ErrorAction Stop`
            : "Remove-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\Personalization' -Name NoLockScreen -ErrorAction Stop";
          commandsToRun = [{ description: 'Restoring the captured lock screen value', script: restoreScript }];
          verificationToRun = snapshot.exists
            ? `if ((Get-ItemProperty 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\Personalization' -Name NoLockScreen -ErrorAction Stop).NoLockScreen -ne ${snapshot.value}) { throw 'Captured lock screen value was not restored' }`
            : "if (Get-ItemProperty 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\Personalization' -Name NoLockScreen -ErrorAction SilentlyContinue) { throw 'Lock screen value was not removed' }";
        }
        if (id === 'disable-advertising-id' && existing.snapshot) {
          const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
          if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'number')) {
            throw new Error('Invalid advertising ID snapshot');
          }
          const restoreScript = snapshot.exists
            ? `Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\AdvertisingInfo' -Name Enabled -Value ${snapshot.value} -Type DWord -Force -ErrorAction Stop`
            : "Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\AdvertisingInfo' -Name Enabled -ErrorAction Stop";
          commandsToRun = [{ description: 'Restoring the captured advertising ID value', script: restoreScript }];
          verificationToRun = snapshot.exists
            ? `if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\AdvertisingInfo' -Name Enabled -ErrorAction Stop).Enabled -ne ${snapshot.value}) { throw 'Captured advertising ID value was not restored' }`
            : "if (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\AdvertisingInfo' -Name Enabled -ErrorAction SilentlyContinue) { throw 'Advertising ID value was not removed' }";
        }
        if (id === 'disable-tailored-experiences' && existing.snapshot) {
          const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
          if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'number')) throw new Error('Invalid tailored experiences snapshot');
          const restoreScript = snapshot.exists ? `Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Privacy' -Name TailoredExperiencesWithDiagnosticDataEnabled -Value ${snapshot.value} -Type DWord -Force -ErrorAction Stop` : "Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Privacy' -Name TailoredExperiencesWithDiagnosticDataEnabled -ErrorAction Stop";
          commandsToRun = [{ description: 'Restoring the captured tailored experiences value', script: restoreScript }];
          verificationToRun = snapshot.exists ? `if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Privacy' -Name TailoredExperiencesWithDiagnosticDataEnabled -ErrorAction Stop).TailoredExperiencesWithDiagnosticDataEnabled -ne ${snapshot.value}) { throw 'Captured tailored experiences value was not restored' }` : "if (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Privacy' -Name TailoredExperiencesWithDiagnosticDataEnabled -ErrorAction SilentlyContinue) { throw 'Tailored experiences value was not removed' }";
        }
        if (id === 'disable-windows-feedback' && existing.snapshot) {
          const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
          if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'number')) throw new Error('Invalid Windows feedback snapshot');
          const restoreScript = snapshot.exists ? `Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Siuf\\Rules' -Name NumberOfSIUFInPeriod -Value ${snapshot.value} -Type DWord -Force -ErrorAction Stop` : "Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Siuf\\Rules' -Name NumberOfSIUFInPeriod -ErrorAction Stop";
          commandsToRun = [{ description: 'Restoring the captured Windows feedback value', script: restoreScript }];
          verificationToRun = snapshot.exists ? `if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Siuf\\Rules' -Name NumberOfSIUFInPeriod -ErrorAction Stop).NumberOfSIUFInPeriod -ne ${snapshot.value}) { throw 'Captured Windows feedback value was not restored' }` : "if (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Siuf\\Rules' -Name NumberOfSIUFInPeriod -ErrorAction SilentlyContinue) { throw 'Windows feedback value was not removed' }";
        }
        if (id === 'disable-cloud-content' && existing.snapshot) {
          const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
          if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'number')) throw new Error('Invalid cloud content snapshot');
          const restoreScript = snapshot.exists ? `Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\CloudContent' -Name DisableConsumerAccountStateContent -Value ${snapshot.value} -Type DWord -Force -ErrorAction Stop` : "Remove-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\CloudContent' -Name DisableConsumerAccountStateContent -ErrorAction Stop";
          commandsToRun = [{ description: 'Restoring the captured cloud content value', script: restoreScript }];
          verificationToRun = snapshot.exists ? `if ((Get-ItemProperty 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\CloudContent' -Name DisableConsumerAccountStateContent -ErrorAction Stop).DisableConsumerAccountStateContent -ne ${snapshot.value}) { throw 'Captured cloud content value was not restored' }` : "if (Get-ItemProperty 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\CloudContent' -Name DisableConsumerAccountStateContent -ErrorAction SilentlyContinue) { throw 'Cloud content value was not removed' }";
        }
        if (id === 'disable-start-tracking' && existing.snapshot) {
          const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
          if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'number')) throw new Error('Invalid Start tracking snapshot');
          const restoreScript = snapshot.exists ? `Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name Start_TrackProgs -Value ${snapshot.value} -Type DWord -Force -ErrorAction Stop` : "Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name Start_TrackProgs -ErrorAction Stop";
          commandsToRun = [{ description: 'Restoring the captured Start tracking value', script: restoreScript }];
          verificationToRun = snapshot.exists ? `if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name Start_TrackProgs -ErrorAction Stop).Start_TrackProgs -ne ${snapshot.value}) { throw 'Captured Start tracking value was not restored' }` : "if (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name Start_TrackProgs -ErrorAction SilentlyContinue) { throw 'Start tracking value was not removed' }";
        }
        if (id === 'disable-app-suggestions' && existing.snapshot) {
          const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
          if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'number')) throw new Error('Invalid app suggestions snapshot');
          const restoreScript = snapshot.exists ? `Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager' -Name SubscribedContent-338388Enabled -Value ${snapshot.value} -Type DWord -Force -ErrorAction Stop` : "Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager' -Name SubscribedContent-338388Enabled -ErrorAction Stop";
          commandsToRun = [{ description: 'Restoring the captured app suggestions value', script: restoreScript }];
          verificationToRun = snapshot.exists ? `if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager' -Name SubscribedContent-338388Enabled -ErrorAction Stop).'SubscribedContent-338388Enabled' -ne ${snapshot.value}) { throw 'Captured app suggestions value was not restored' }` : "if (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager' -Name SubscribedContent-338388Enabled -ErrorAction SilentlyContinue) { throw 'App suggestions value was not removed' }";
        }
        if (id === 'disable-setting-sync' && existing.snapshot) {
          const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
          if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'number')) throw new Error('Invalid settings sync snapshot');
          const restoreScript = snapshot.exists ? `Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\SettingSync' -Name SyncPolicy -Value ${snapshot.value} -Type DWord -Force -ErrorAction Stop` : "Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\SettingSync' -Name SyncPolicy -ErrorAction Stop";
          commandsToRun = [{ description: 'Restoring the captured settings sync value', script: restoreScript }];
          verificationToRun = snapshot.exists ? `if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\SettingSync' -Name SyncPolicy -ErrorAction Stop).SyncPolicy -ne ${snapshot.value}) { throw 'Captured settings sync value was not restored' }` : "if (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\SettingSync' -Name SyncPolicy -ErrorAction SilentlyContinue) { throw 'Settings sync value was not removed' }";
        }
        if (id === 'disable-handwriting-data' && existing.snapshot) {
          const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
          if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'number')) throw new Error('Invalid handwriting data snapshot');
          const restoreScript = snapshot.exists ? `Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\TabletPC' -Name PreventHandwritingDataSharing -Value ${snapshot.value} -Type DWord -Force -ErrorAction Stop` : "Remove-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\TabletPC' -Name PreventHandwritingDataSharing -ErrorAction Stop";
          commandsToRun = [{ description: 'Restoring the captured handwriting data value', script: restoreScript }];
          verificationToRun = snapshot.exists ? `if ((Get-ItemProperty 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\TabletPC' -Name PreventHandwritingDataSharing -ErrorAction Stop).PreventHandwritingDataSharing -ne ${snapshot.value}) { throw 'Captured handwriting data value was not restored' }` : "if (Get-ItemProperty 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\TabletPC' -Name PreventHandwritingDataSharing -ErrorAction SilentlyContinue) { throw 'Handwriting data value was not removed' }";
        }
        if (id === 'disable-speech-recognition' && existing.snapshot) {
          const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
          if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'number')) throw new Error('Invalid speech recognition snapshot');
          const restoreScript = snapshot.exists ? `Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Speech_OneCore\\Settings\\OnlineSpeechPrivacy' -Name HasAccepted -Value ${snapshot.value} -Type DWord -Force -ErrorAction Stop` : "Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Speech_OneCore\\Settings\\OnlineSpeechPrivacy' -Name HasAccepted -ErrorAction Stop";
          commandsToRun = [{ description: 'Restoring the captured speech recognition value', script: restoreScript }];
          verificationToRun = snapshot.exists ? `if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Speech_OneCore\\Settings\\OnlineSpeechPrivacy' -Name HasAccepted -ErrorAction Stop).HasAccepted -ne ${snapshot.value}) { throw 'Captured speech recognition value was not restored' }` : "if (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Speech_OneCore\\Settings\\OnlineSpeechPrivacy' -Name HasAccepted -ErrorAction SilentlyContinue) { throw 'Speech recognition value was not removed' }";
        }
        if (id === 'disable-find-my-device' && existing.snapshot) {
          const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
          if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'number')) throw new Error('Invalid Find My Device snapshot');
          const restoreScript = snapshot.exists ? `Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Settings\\FindMyDevice' -Name LocationSyncEnabled -Value ${snapshot.value} -Type DWord -Force -ErrorAction Stop` : "Remove-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Settings\\FindMyDevice' -Name LocationSyncEnabled -ErrorAction Stop";
          commandsToRun = [{ description: 'Restoring the captured Find My Device value', script: restoreScript }];
          verificationToRun = snapshot.exists ? `if ((Get-ItemProperty 'HKLM:\\SOFTWARE\\Microsoft\\Settings\\FindMyDevice' -Name LocationSyncEnabled -ErrorAction Stop).LocationSyncEnabled -ne ${snapshot.value}) { throw 'Captured Find My Device value was not restored' }` : "if (Get-ItemProperty 'HKLM:\\SOFTWARE\\Microsoft\\Settings\\FindMyDevice' -Name LocationSyncEnabled -ErrorAction SilentlyContinue) { throw 'Find My Device value was not removed' }";
        }
        if (id === 'disable-diagnostic-data' && existing.snapshot) {
          const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
          if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'number')) throw new Error('Invalid diagnostic data snapshot');
          const restoreScript = snapshot.exists ? `Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection' -Name AllowTelemetry -Value ${snapshot.value} -Type DWord -Force -ErrorAction Stop` : "Remove-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection' -Name AllowTelemetry -ErrorAction Stop";
          commandsToRun = [{ description: 'Restoring the captured diagnostic data value', script: restoreScript }];
          verificationToRun = snapshot.exists ? `if ((Get-ItemProperty 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection' -Name AllowTelemetry -ErrorAction Stop).AllowTelemetry -ne ${snapshot.value}) { throw 'Captured diagnostic data value was not restored' }` : "if (Get-ItemProperty 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection' -Name AllowTelemetry -ErrorAction SilentlyContinue) { throw 'Diagnostic data value was not removed' }";
        }
        if (id === 'disable-camera-access' && existing.snapshot) {
          const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
          if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'string')) throw new Error('Invalid camera access snapshot');
          const capturedValue = typeof snapshot.value === 'string' ? snapshot.value : '';
          const escapedValue = capturedValue.replace(/'/g, "''");
          const restoreScript = snapshot.exists ? `Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\webcam' -Name Value -Value '${escapedValue}' -Force -ErrorAction Stop` : "Remove-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\webcam' -Name Value -ErrorAction Stop";
          commandsToRun = [{ description: 'Restoring the captured camera access value', script: restoreScript }];
          verificationToRun = snapshot.exists ? `if ((Get-ItemProperty 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\webcam' -Name Value -ErrorAction Stop).Value -ne '${escapedValue}') { throw 'Captured camera access value was not restored' }` : "if (Get-ItemProperty 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\webcam' -Name Value -ErrorAction SilentlyContinue) { throw 'Camera access value was not removed' }";
        }
        if (id === 'disable-microphone-access' && existing.snapshot) {
          const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
          if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'string')) throw new Error('Invalid microphone access snapshot');
          const capturedValue = typeof snapshot.value === 'string' ? snapshot.value : '';
          const escapedValue = capturedValue.replace(/'/g, "''");
          const restoreScript = snapshot.exists ? `Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\microphone' -Name Value -Value '${escapedValue}' -Force -ErrorAction Stop` : "Remove-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\microphone' -Name Value -ErrorAction Stop";
          commandsToRun = [{ description: 'Restoring the captured microphone access value', script: restoreScript }];
          verificationToRun = snapshot.exists ? `if ((Get-ItemProperty 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\microphone' -Name Value -ErrorAction Stop).Value -ne '${escapedValue}') { throw 'Captured microphone access value was not restored' }` : "if (Get-ItemProperty 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\microphone' -Name Value -ErrorAction SilentlyContinue) { throw 'Microphone access value was not removed' }";
        }
        let hasError = false;
        let errorMessage = "";

        for (const cmd of commandsToRun) {
          const result = await runPowerShell(cmd.script);
          if (result.success) {
            changesReverted.push(`✅ ${cmd.description}: ${result.output || 'Done'}`);
          } else {
            changesReverted.push(`⚠️ ${cmd.description}: ${result.error || 'Failed (may require Admin)'}`);
            hasError = true;
            errorMessage = result.error || 'Failed (may require Admin)';
            break;
          }
        }

        if (!hasError) {
          const verification = await runPowerShell(verificationToRun);
          if (!verification.success) {
            hasError = true;
            errorMessage = verification.error || 'Post-revert verification failed.';
            changesReverted.push(`⚠️ Revert verification: ${errorMessage}`);
          }
        }

        if (!hasError) {
          await db.optimizationState.updateMany({
            where: { id },
            data: { applied: false }
          });

          if (cmdSet.rebootRequired) rebootRequired = true;

          revertedOptimizations.push({
            id,
            name: generateName(id),
            success: true,
            changesReverted
          });
        } else {
          someFailed = true;
          revertedOptimizations.push({
            id,
            name: generateName(id),
            success: false,
            error: errorMessage,
            changesReverted
          });
        }

      } catch (error) {
        someFailed = true;
        revertedOptimizations.push({
          id,
          name: generateName(id),
          success: false,
          error: "Failed to revert optimization"
        });
      }
    }

    const executionTime = Date.now() - startTime;

    return NextResponse.json<ApiResponse>({
      success: !someFailed,
      data: {
        revertedOptimizations,
        rebootRequired,
        executionTime
      }
    });

  } catch (error) {
    console.error("Error reverting all optimizations:", error);
    return NextResponse.json<ApiResponse>(
      { success: false, error: "Failed to revert optimizations" },
      { status: 500 }
    );
  }
}