import { NextRequest, NextResponse } from "next/server";
import { revertCommands, revertVerificationCommands, irreversibleOptimizationIds, sessionScopedOptimizationIds, repeatableOptimizationIds, isExecutableOptimizationId } from "@/lib/optimization-commands";
import { runPowerShell } from "@/lib/powershell-runner";
import { guardOrResponse } from "@/lib/api-security";
import { db } from "@/lib/db";

interface RevertOptimizationRequest {
  optimizationId: string;
}

interface RevertedOptimization {
  id: string;
  name: string;
  changesReverted: string[];
  success: boolean;
  error?: string;
}

interface ApiResponse<T = unknown> {
  success: boolean;
  data?: T;
  error?: string;
  message?: string;
  changesReverted?: string[];
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

    const requestBody = body as Partial<RevertOptimizationRequest>;

    if (typeof requestBody.optimizationId !== 'string' || requestBody.optimizationId.trim().length === 0) {
      return NextResponse.json<ApiResponse>(
        { success: false, error: "Missing required field: optimizationId" },
        { status: 400 }
      );
    }

    const optimizationId = requestBody.optimizationId;

    if (irreversibleOptimizationIds.has(optimizationId) && !repeatableOptimizationIds.has(optimizationId)) {
      return NextResponse.json<ApiResponse>(
        { success: false, error: "This optimization is irreversible and cannot be reverted automatically." },
        { status: 409 }
      );
    }

    // Repeatable maintenance actions (temp cleanup, DNS flush, timer
    // resolution) have no persistent Windows state to restore: reverting only
    // clears the tracked state so the action can be offered again.
    if (repeatableOptimizationIds.has(optimizationId)) {
      if (!sessionScopedOptimizationIds.has(optimizationId)) {
        await db.optimizationState.upsert({
          where: { id: optimizationId },
          update: { applied: false },
          create: { id: optimizationId, applied: false }
        });
      }
      return NextResponse.json<ApiResponse>({
        success: true,
        data: {
          id: optimizationId,
          name: generateName(optimizationId),
          changesReverted: ["Maintenance action has no persistent state to restore; it is available to run again."],
          success: true
        },
        message: "This maintenance action can be run again at any time.",
        executionTime: Date.now() - startTime
      });
    }

    if (!isExecutableOptimizationId(optimizationId)) {
      return NextResponse.json<ApiResponse>(
        {
          success: false,
          error: `Optimization '${optimizationId}' is not executable`,
          message: "This optimization is guidance-only or is missing a complete execution contract"
        },
        { status: 400 }
      );
    }

    const cmdSet = revertCommands[optimizationId];
    if (!cmdSet) {
      return NextResponse.json<ApiResponse>(
        {
          success: false,
          error: `No revert commands configured for optimization '${optimizationId}'`,
          message: "This optimization ID is not recognized or doesn't have revert commands"
        },
        { status: 404 }
      );
    }

    const revertVerificationCommand = revertVerificationCommands[optimizationId];
    if (!revertVerificationCommand) {
      return NextResponse.json<ApiResponse>(
        { success: false, error: "This optimization has no verified automatic revert path yet." },
        { status: 501 }
      );
    }

    const existing = await db.optimizationState.findUnique({
      where: { id: optimizationId }
    });

    if (!existing || !existing.applied) {
      console.warn(`Optimization '${optimizationId}' was not tracked as applied, reverting anyway.`);
    }

    if (optimizationId === 'disable-startup-sound' && existing?.applied && !existing.snapshot) {
      return NextResponse.json<ApiResponse>(
        { success: false, error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically." },
        { status: 409 }
      );
    }
    if (optimizationId === 'disable-cortana' && existing?.applied && !existing.snapshot) {
      return NextResponse.json<ApiResponse>(
        { success: false, error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically." },
        { status: 409 }
      );
    }

    const changesReverted: string[] = [];
    let commandsToRun = cmdSet.commands;
    let verificationToRun = revertVerificationCommand;
    if (optimizationId === 'disable-startup-sound' && existing?.snapshot) {
      let snapshot: { exists: boolean; value?: string };
      try {
        snapshot = JSON.parse(existing.snapshot) as { exists: boolean; value?: string };
        if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'string')) {
          throw new Error('Invalid startup sound snapshot');
        }
      } catch {
        return NextResponse.json<ApiResponse>(
          { success: false, error: "The original Windows state snapshot is invalid; revert was not attempted." },
          { status: 409 }
        );
      }
      const escapedValue = (snapshot.value || '').replace(/'/g, "''");
      const restoreScript = snapshot.exists
        ? `Set-ItemProperty -Path 'HKCU:\\AppEvents\\Schemes\\Apps\\.Default\\WindowsLogon' -Name '(Default)' -Value '${escapedValue}' -Force -ErrorAction Stop`
        : "Remove-ItemProperty -Path 'HKCU:\\AppEvents\\Schemes\\Apps\\.Default\\WindowsLogon' -Name '(Default)' -ErrorAction Stop";
      commandsToRun = [{ description: 'Restoring the captured startup sound value', script: restoreScript }];
      verificationToRun = snapshot.exists
        ? `if ((Get-ItemProperty 'HKCU:\\AppEvents\\Schemes\\Apps\\.Default\\WindowsLogon' -Name '(Default)' -ErrorAction Stop).'(Default)' -ne '${escapedValue}') { throw 'Captured startup sound value was not restored' }`
        : "if (Get-ItemProperty 'HKCU:\\AppEvents\\Schemes\\Apps\\.Default\\WindowsLogon' -Name '(Default)' -ErrorAction SilentlyContinue) { throw 'Startup sound value was not removed' }";
    }
    if (optimizationId === 'disable-cortana' && existing?.snapshot) {
      const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
      if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'number')) {
        return NextResponse.json<ApiResponse>(
          { success: false, error: "The original Cortana policy snapshot is invalid; revert was not attempted." },
          { status: 409 }
        );
      }
      const restoreScript = snapshot.exists
        ? `Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Search' -Name AllowCortana -Value ${snapshot.value} -Type DWord -Force -ErrorAction Stop`
        : "Remove-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Search' -Name AllowCortana -ErrorAction Stop";
      commandsToRun = [{ description: 'Restoring the captured Cortana policy', script: restoreScript }];
      verificationToRun = snapshot.exists
        ? `if ((Get-ItemProperty 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Search' -Name AllowCortana -ErrorAction Stop).AllowCortana -ne ${snapshot.value}) { throw 'Captured Cortana policy was not restored' }`
        : "if (Get-ItemProperty 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Search' -Name AllowCortana -ErrorAction SilentlyContinue) { throw 'Cortana policy was not removed' }";
    }
    if (optimizationId === 'dns-optimization' && existing?.snapshot) {
      let snapshot: { exists: boolean; value?: string };
      try {
        snapshot = JSON.parse(existing.snapshot) as { exists: boolean; value?: string };
        if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'string')) {
          throw new Error('Invalid DNS snapshot');
        }
      } catch {
        return NextResponse.json<ApiResponse>(
          { success: false, error: "The original DNS snapshot is invalid; revert was not attempted." },
          { status: 409 }
        );
      }
      const escapedBaseline = (snapshot.value || '').replace(/'/g, "''");
      commandsToRun = [{
        description: 'Restoring captured DNS servers per network adapter',
        script: `$baseline = '${escapedBaseline}'; $records = @($baseline | ConvertFrom-Json); foreach ($record in $records) { $servers = @($record.servers); if ($servers.Count -eq 0) { Set-DnsClientServerAddress -InterfaceIndex $record.interfaceIndex -ResetServerAddresses -ErrorAction Stop } else { Set-DnsClientServerAddress -InterfaceIndex $record.interfaceIndex -ServerAddresses $servers -ErrorAction Stop } }`,
      }];
      verificationToRun = `$baseline = '${escapedBaseline}'; $records = @($baseline | ConvertFrom-Json); foreach ($record in $records) { $actual = @(Get-DnsClientServerAddress -InterfaceIndex $record.interfaceIndex -AddressFamily IPv4 -ErrorAction Stop).ServerAddresses; $expected = @($record.servers); if (($actual -join ',') -ne ($expected -join ',')) { throw "DNS baseline was not restored for interface $($record.interfaceIndex)" } }`;
    }
    if (optimizationId === 'disable-screen-saver' && existing?.applied && !existing.snapshot) {
      return NextResponse.json<ApiResponse>(
        { success: false, error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically." },
        { status: 409 }
      );
    }
    if (optimizationId === 'disable-mouse-trails' && existing?.applied && !existing.snapshot) {
      return NextResponse.json<ApiResponse>(
        { success: false, error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically." },
        { status: 409 }
      );
    }
    if (optimizationId === 'hide-task-view' && existing?.applied && !existing.snapshot) {
      return NextResponse.json<ApiResponse>(
        { success: false, error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically." },
        { status: 409 }
      );
    }
    if (optimizationId === 'disable-aero-peek' && existing?.applied && !existing.snapshot) {
      return NextResponse.json<ApiResponse>(
        { success: false, error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically." },
        { status: 409 }
      );
    }
    if (optimizationId === 'disable-tooltips' && existing?.applied && !existing.snapshot) {
      return NextResponse.json<ApiResponse>(
        { success: false, error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically." },
        { status: 409 }
      );
    }
    if (optimizationId === 'disable-wallpaper-slideshow' && existing?.applied && !existing.snapshot) {
      return NextResponse.json<ApiResponse>(
        { success: false, error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically." },
        { status: 409 }
      );
    }
    if (optimizationId === 'disable-system-sounds' && existing?.applied && !existing.snapshot) {
      return NextResponse.json<ApiResponse>(
        { success: false, error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically." },
        { status: 409 }
      );
    }
    if (optimizationId === 'show-hidden-files' && existing?.applied && !existing.snapshot) {
      return NextResponse.json<ApiResponse>(
        { success: false, error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically." },
        { status: 409 }
      );
    }
    if (optimizationId === 'disable-start-menu-suggestions' && existing?.applied && !existing.snapshot) {
      return NextResponse.json<ApiResponse>(
        { success: false, error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically." },
        { status: 409 }
      );
    }
    if (optimizationId === 'disable-taskbar-search' && existing?.applied && !existing.snapshot) {
      return NextResponse.json<ApiResponse>(
        { success: false, error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically." },
        { status: 409 }
      );
    }
    if (optimizationId === 'show-file-extensions' && existing?.applied && !existing.snapshot) {
      return NextResponse.json<ApiResponse>(
        { success: false, error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically." },
        { status: 409 }
      );
    }
    if (optimizationId === 'disable-background-apps' && existing?.applied && !existing.snapshot) {
      return NextResponse.json<ApiResponse>(
        { success: false, error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically." },
        { status: 409 }
      );
    }
    if (optimizationId === 'disable-cast-notifications' && existing?.applied && !existing.snapshot) {
      return NextResponse.json<ApiResponse>(
        { success: false, error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically." },
        { status: 409 }
      );
    }
    if (optimizationId === 'disable-thumbnails' && existing?.applied && !existing.snapshot) {
      return NextResponse.json<ApiResponse>(
        { success: false, error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically." },
        { status: 409 }
      );
    }
    if (optimizationId === 'disable-lock-screen' && existing?.applied && !existing.snapshot) {
      return NextResponse.json<ApiResponse>(
        { success: false, error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically." },
        { status: 409 }
      );
    }
    if (optimizationId === 'disable-advertising-id' && existing?.applied && !existing.snapshot) {
      return NextResponse.json<ApiResponse>(
        { success: false, error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically." },
        { status: 409 }
      );
    }
    if (optimizationId === 'disable-tailored-experiences' && existing?.applied && !existing.snapshot) {
      return NextResponse.json<ApiResponse>({ success: false, error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically." }, { status: 409 });
    }
    if (optimizationId === 'disable-windows-feedback' && existing?.applied && !existing.snapshot) {
      return NextResponse.json<ApiResponse>({ success: false, error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically." }, { status: 409 });
    }
    if (optimizationId === 'disable-cloud-content' && existing?.applied && !existing.snapshot) {
      return NextResponse.json<ApiResponse>({ success: false, error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically." }, { status: 409 });
    }
    if (optimizationId === 'disable-start-tracking' && existing?.applied && !existing.snapshot) {
      return NextResponse.json<ApiResponse>({ success: false, error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically." }, { status: 409 });
    }
    if (optimizationId === 'disable-app-suggestions' && existing?.applied && !existing.snapshot) {
      return NextResponse.json<ApiResponse>({ success: false, error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically." }, { status: 409 });
    }
    if (optimizationId === 'disable-setting-sync' && existing?.applied && !existing.snapshot) {
      return NextResponse.json<ApiResponse>({ success: false, error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically." }, { status: 409 });
    }
    if (optimizationId === 'disable-handwriting-data' && existing?.applied && !existing.snapshot) {
      return NextResponse.json<ApiResponse>({ success: false, error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically." }, { status: 409 });
    }
    if (optimizationId === 'disable-speech-recognition' && existing?.applied && !existing.snapshot) {
      return NextResponse.json<ApiResponse>({ success: false, error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically." }, { status: 409 });
    }
    if (optimizationId === 'disable-find-my-device' && existing?.applied && !existing.snapshot) {
      return NextResponse.json<ApiResponse>({ success: false, error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically." }, { status: 409 });
    }
    if (optimizationId === 'disable-diagnostic-data' && existing?.applied && !existing.snapshot) {
      return NextResponse.json<ApiResponse>({ success: false, error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically." }, { status: 409 });
    }
    if (optimizationId === 'disable-camera-access' && existing?.applied && !existing.snapshot) {
      return NextResponse.json<ApiResponse>({ success: false, error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically." }, { status: 409 });
    }
    if (optimizationId === 'disable-microphone-access' && existing?.applied && !existing.snapshot) {
      return NextResponse.json<ApiResponse>({ success: false, error: "This optimization was applied before original-state capture was available; it cannot be safely reverted automatically." }, { status: 409 });
    }
    if (optimizationId === 'disable-screen-saver' && existing?.snapshot) {
      const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
      if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'string')) {
        return NextResponse.json<ApiResponse>(
          { success: false, error: "The original screen saver snapshot is invalid; revert was not attempted." },
          { status: 409 }
        );
      }
      const escapedValue = typeof snapshot.value === 'string' ? snapshot.value.replace(/'/g, "''") : '';
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
    if (optimizationId === 'disable-mouse-trails' && existing?.snapshot) {
      const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
      if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'string')) {
        return NextResponse.json<ApiResponse>(
          { success: false, error: "The original mouse trails snapshot is invalid; revert was not attempted." },
          { status: 409 }
        );
      }
      const escapedValue = typeof snapshot.value === 'string' ? snapshot.value.replace(/'/g, "''") : '';
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
    if (optimizationId === 'hide-task-view' && existing?.snapshot) {
      const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
      if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'number')) {
        return NextResponse.json<ApiResponse>(
          { success: false, error: "The original Task View snapshot is invalid; revert was not attempted." },
          { status: 409 }
        );
      }
      const restoreScript = snapshot.exists
        ? `Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name ShowTaskViewButton -Value ${snapshot.value} -Type DWord -Force -ErrorAction Stop`
        : "Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name ShowTaskViewButton -ErrorAction Stop";
      commandsToRun = [{ description: 'Restoring the captured Task View value', script: restoreScript }];
      verificationToRun = snapshot.exists
        ? `if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name ShowTaskViewButton -ErrorAction Stop).ShowTaskViewButton -ne ${snapshot.value}) { throw 'Captured Task View value was not restored' }`
        : "if (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name ShowTaskViewButton -ErrorAction SilentlyContinue) { throw 'Task View value was not removed' }";
    }
    if (optimizationId === 'disable-aero-peek' && existing?.snapshot) {
      const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
      if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'number')) {
        return NextResponse.json<ApiResponse>(
          { success: false, error: "The original Aero Peek snapshot is invalid; revert was not attempted." },
          { status: 409 }
        );
      }
      const restoreScript = snapshot.exists
        ? `Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name DisablePreviewDesktop -Value ${snapshot.value} -Type DWord -Force -ErrorAction Stop`
        : "Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name DisablePreviewDesktop -ErrorAction Stop";
      commandsToRun = [{ description: 'Restoring the captured Aero Peek value', script: restoreScript }];
      verificationToRun = snapshot.exists
        ? `if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name DisablePreviewDesktop -ErrorAction Stop).DisablePreviewDesktop -ne ${snapshot.value}) { throw 'Captured Aero Peek value was not restored' }`
        : "if (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name DisablePreviewDesktop -ErrorAction SilentlyContinue) { throw 'Aero Peek value was not removed' }";
    }
    if (optimizationId === 'disable-tooltips' && existing?.snapshot) {
      const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
      if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'number')) {
        return NextResponse.json<ApiResponse>(
          { success: false, error: "The original tooltips snapshot is invalid; revert was not attempted." },
          { status: 409 }
        );
      }
      const restoreScript = snapshot.exists
        ? `Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name ShowInfoTip -Value ${snapshot.value} -Type DWord -Force -ErrorAction Stop`
        : "Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name ShowInfoTip -ErrorAction Stop";
      commandsToRun = [{ description: 'Restoring the captured tooltips value', script: restoreScript }];
      verificationToRun = snapshot.exists
        ? `if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name ShowInfoTip -ErrorAction Stop).ShowInfoTip -ne ${snapshot.value}) { throw 'Captured tooltips value was not restored' }`
        : "if (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name ShowInfoTip -ErrorAction SilentlyContinue) { throw 'Tooltips value was not removed' }";
    }
    if (optimizationId === 'disable-wallpaper-slideshow' && existing?.snapshot) {
      const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
      if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'number')) {
        return NextResponse.json<ApiResponse>(
          { success: false, error: "The original wallpaper slideshow snapshot is invalid; revert was not attempted." },
          { status: 409 }
        );
      }
      const restoreScript = snapshot.exists
        ? `Set-ItemProperty -Path 'HKCU:\\Control Panel\\Personalization\\Desktop Slideshow' -Name Interval -Value ${snapshot.value} -Type DWord -Force -ErrorAction Stop`
        : "Remove-ItemProperty -Path 'HKCU:\\Control Panel\\Personalization\\Desktop Slideshow' -Name Interval -ErrorAction Stop";
      commandsToRun = [{ description: 'Restoring the captured wallpaper slideshow interval', script: restoreScript }];
      verificationToRun = snapshot.exists
        ? `if ((Get-ItemProperty 'HKCU:\\Control Panel\\Personalization\\Desktop Slideshow' -Name Interval -ErrorAction Stop).Interval -ne ${snapshot.value}) { throw 'Captured wallpaper slideshow interval was not restored' }`
        : "if (Get-ItemProperty 'HKCU:\\Control Panel\\Personalization\\Desktop Slideshow' -Name Interval -ErrorAction SilentlyContinue) { throw 'Wallpaper slideshow interval was not removed' }";
    }
    if (optimizationId === 'disable-system-sounds' && existing?.snapshot) {
      const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
      if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'string')) {
        return NextResponse.json<ApiResponse>(
          { success: false, error: "The original system sounds snapshot is invalid; revert was not attempted." },
          { status: 409 }
        );
      }
      const escapedValue = typeof snapshot.value === 'string' ? snapshot.value.replace(/'/g, "''") : '';
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
    if (optimizationId === 'show-hidden-files' && existing?.snapshot) {
      const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
      if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'number')) {
        return NextResponse.json<ApiResponse>(
          { success: false, error: "The original hidden files snapshot is invalid; revert was not attempted." },
          { status: 409 }
        );
      }
      const restoreScript = snapshot.exists
        ? `Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name Hidden -Value ${snapshot.value} -Type DWord -Force -ErrorAction Stop`
        : "Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name Hidden -ErrorAction Stop";
      commandsToRun = [{ description: 'Restoring the captured hidden files value', script: restoreScript }];
      verificationToRun = snapshot.exists
        ? `if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name Hidden -ErrorAction Stop).Hidden -ne ${snapshot.value}) { throw 'Captured hidden files value was not restored' }`
        : "if (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name Hidden -ErrorAction SilentlyContinue) { throw 'Hidden files value was not removed' }";
    }
    if (optimizationId === 'disable-start-menu-suggestions' && existing?.snapshot) {
      const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
      if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'number')) {
        return NextResponse.json<ApiResponse>(
          { success: false, error: "The original Start menu suggestions snapshot is invalid; revert was not attempted." },
          { status: 409 }
        );
      }
      const restoreScript = snapshot.exists
        ? `Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager' -Name SystemPaneSuggestionsEnabled -Value ${snapshot.value} -Type DWord -Force -ErrorAction Stop`
        : "Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager' -Name SystemPaneSuggestionsEnabled -ErrorAction Stop";
      commandsToRun = [{ description: 'Restoring the captured Start menu suggestions value', script: restoreScript }];
      verificationToRun = snapshot.exists
        ? `if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager' -Name SystemPaneSuggestionsEnabled -ErrorAction Stop).SystemPaneSuggestionsEnabled -ne ${snapshot.value}) { throw 'Captured Start menu suggestions value was not restored' }`
        : "if (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager' -Name SystemPaneSuggestionsEnabled -ErrorAction SilentlyContinue) { throw 'Start menu suggestions value was not removed' }";
    }
    if (optimizationId === 'disable-taskbar-search' && existing?.snapshot) {
      const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
      if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'number')) {
        return NextResponse.json<ApiResponse>(
          { success: false, error: "The original taskbar search snapshot is invalid; revert was not attempted." },
          { status: 409 }
        );
      }
      const restoreScript = snapshot.exists
        ? `Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Search' -Name SearchboxTaskbarMode -Value ${snapshot.value} -Type DWord -Force -ErrorAction Stop`
        : "Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Search' -Name SearchboxTaskbarMode -ErrorAction Stop";
      commandsToRun = [{ description: 'Restoring the captured taskbar search value', script: restoreScript }];
      verificationToRun = snapshot.exists
        ? `if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Search' -Name SearchboxTaskbarMode -ErrorAction Stop).SearchboxTaskbarMode -ne ${snapshot.value}) { throw 'Captured taskbar search value was not restored' }`
        : "if (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Search' -Name SearchboxTaskbarMode -ErrorAction SilentlyContinue) { throw 'Taskbar search value was not removed' }";
    }
    if (optimizationId === 'show-file-extensions' && existing?.snapshot) {
      const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
      if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'number')) {
        return NextResponse.json<ApiResponse>(
          { success: false, error: "The original file extensions snapshot is invalid; revert was not attempted." },
          { status: 409 }
        );
      }
      const restoreScript = snapshot.exists
        ? `Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name HideFileExt -Value ${snapshot.value} -Type DWord -Force -ErrorAction Stop; Stop-Process -Name explorer -Force -ErrorAction Stop`
        : "Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name HideFileExt -ErrorAction Stop; Stop-Process -Name explorer -Force -ErrorAction Stop";
      commandsToRun = [{ description: 'Restoring the captured file extensions value', script: restoreScript }];
      verificationToRun = snapshot.exists
        ? `if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name HideFileExt -ErrorAction Stop).HideFileExt -ne ${snapshot.value}) { throw 'Captured file extensions value was not restored' }`
        : "if (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name HideFileExt -ErrorAction SilentlyContinue) { throw 'File extensions value was not removed' }";
    }
    if (optimizationId === 'disable-background-apps' && existing?.snapshot) {
      const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
      if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'number')) {
        return NextResponse.json<ApiResponse>(
          { success: false, error: "The original background apps snapshot is invalid; revert was not attempted." },
          { status: 409 }
        );
      }
      const restoreScript = snapshot.exists
        ? `Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\BackgroundAccessApplications' -Name GlobalUserDisabled -Value ${snapshot.value} -Type DWord -Force -ErrorAction Stop`
        : "Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\BackgroundAccessApplications' -Name GlobalUserDisabled -ErrorAction Stop";
      commandsToRun = [{ description: 'Restoring the captured background apps value', script: restoreScript }];
      verificationToRun = snapshot.exists
        ? `if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\BackgroundAccessApplications' -Name GlobalUserDisabled -ErrorAction Stop).GlobalUserDisabled -ne ${snapshot.value}) { throw 'Captured background apps value was not restored' }`
        : "if (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\BackgroundAccessApplications' -Name GlobalUserDisabled -ErrorAction SilentlyContinue) { throw 'Background apps value was not removed' }";
    }
    if (optimizationId === 'disable-cast-notifications' && existing?.snapshot) {
      const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
      if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'number')) {
        return NextResponse.json<ApiResponse>(
          { success: false, error: "The original cast notifications snapshot is invalid; revert was not attempted." },
          { status: 409 }
        );
      }
      const restoreScript = snapshot.exists
        ? `Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Casting' -Name AllowWhileLocked -Value ${snapshot.value} -Type DWord -Force -ErrorAction Stop`
        : "Remove-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Casting' -Name AllowWhileLocked -ErrorAction Stop";
      commandsToRun = [{ description: 'Restoring the captured cast notifications value', script: restoreScript }];
      verificationToRun = snapshot.exists
        ? `if ((Get-ItemProperty 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Casting' -Name AllowWhileLocked -ErrorAction Stop).AllowWhileLocked -ne ${snapshot.value}) { throw 'Captured cast notifications value was not restored' }`
        : "if (Get-ItemProperty 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Casting' -Name AllowWhileLocked -ErrorAction SilentlyContinue) { throw 'Cast notifications value was not removed' }";
    }
    if (optimizationId === 'disable-thumbnails' && existing?.snapshot) {
      const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
      if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'number')) {
        return NextResponse.json<ApiResponse>(
          { success: false, error: "The original thumbnails snapshot is invalid; revert was not attempted." },
          { status: 409 }
        );
      }
      const restoreScript = snapshot.exists
        ? `Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name IconsOnly -Value ${snapshot.value} -Type DWord -Force -ErrorAction Stop; Stop-Process -Name explorer -Force -ErrorAction Stop`
        : "Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name IconsOnly -ErrorAction Stop; Stop-Process -Name explorer -Force -ErrorAction Stop";
      commandsToRun = [{ description: 'Restoring the captured thumbnails value', script: restoreScript }];
      verificationToRun = snapshot.exists
        ? `if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name IconsOnly -ErrorAction Stop).IconsOnly -ne ${snapshot.value}) { throw 'Captured thumbnails value was not restored' }`
        : "if (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name IconsOnly -ErrorAction SilentlyContinue) { throw 'Thumbnails value was not removed' }";
    }
    if (optimizationId === 'disable-lock-screen' && existing?.snapshot) {
      const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
      if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'number')) {
        return NextResponse.json<ApiResponse>(
          { success: false, error: "The original lock screen snapshot is invalid; revert was not attempted." },
          { status: 409 }
        );
      }
      const restoreScript = snapshot.exists
        ? `Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\Personalization' -Name NoLockScreen -Value ${snapshot.value} -Type DWord -Force -ErrorAction Stop`
        : "Remove-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\Personalization' -Name NoLockScreen -ErrorAction Stop";
      commandsToRun = [{ description: 'Restoring the captured lock screen value', script: restoreScript }];
      verificationToRun = snapshot.exists
        ? `if ((Get-ItemProperty 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\Personalization' -Name NoLockScreen -ErrorAction Stop).NoLockScreen -ne ${snapshot.value}) { throw 'Captured lock screen value was not restored' }`
        : "if (Get-ItemProperty 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\Personalization' -Name NoLockScreen -ErrorAction SilentlyContinue) { throw 'Lock screen value was not removed' }";
    }
    if (optimizationId === 'disable-advertising-id' && existing?.snapshot) {
      const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
      if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'number')) {
        return NextResponse.json<ApiResponse>(
          { success: false, error: "The original advertising ID snapshot is invalid; revert was not attempted." },
          { status: 409 }
        );
      }
      const restoreScript = snapshot.exists
        ? `Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\AdvertisingInfo' -Name Enabled -Value ${snapshot.value} -Type DWord -Force -ErrorAction Stop`
        : "Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\AdvertisingInfo' -Name Enabled -ErrorAction Stop";
      commandsToRun = [{ description: 'Restoring the captured advertising ID value', script: restoreScript }];
      verificationToRun = snapshot.exists
        ? `if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\AdvertisingInfo' -Name Enabled -ErrorAction Stop).Enabled -ne ${snapshot.value}) { throw 'Captured advertising ID value was not restored' }`
        : "if (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\AdvertisingInfo' -Name Enabled -ErrorAction SilentlyContinue) { throw 'Advertising ID value was not removed' }";
    }
    if (optimizationId === 'disable-tailored-experiences' && existing?.snapshot) {
      const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
      if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'number')) return NextResponse.json<ApiResponse>({ success: false, error: "The original tailored experiences snapshot is invalid; revert was not attempted." }, { status: 409 });
      const restoreScript = snapshot.exists ? `Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Privacy' -Name TailoredExperiencesWithDiagnosticDataEnabled -Value ${snapshot.value} -Type DWord -Force -ErrorAction Stop` : "Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Privacy' -Name TailoredExperiencesWithDiagnosticDataEnabled -ErrorAction Stop";
      commandsToRun = [{ description: 'Restoring the captured tailored experiences value', script: restoreScript }];
      verificationToRun = snapshot.exists ? `if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Privacy' -Name TailoredExperiencesWithDiagnosticDataEnabled -ErrorAction Stop).TailoredExperiencesWithDiagnosticDataEnabled -ne ${snapshot.value}) { throw 'Captured tailored experiences value was not restored' }` : "if (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Privacy' -Name TailoredExperiencesWithDiagnosticDataEnabled -ErrorAction SilentlyContinue) { throw 'Tailored experiences value was not removed' }";
    }
    if (optimizationId === 'disable-windows-feedback' && existing?.snapshot) {
      const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
      if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'number')) return NextResponse.json<ApiResponse>({ success: false, error: "The original Windows feedback snapshot is invalid; revert was not attempted." }, { status: 409 });
      const restoreScript = snapshot.exists ? `Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Siuf\\Rules' -Name NumberOfSIUFInPeriod -Value ${snapshot.value} -Type DWord -Force -ErrorAction Stop` : "Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Siuf\\Rules' -Name NumberOfSIUFInPeriod -ErrorAction Stop";
      commandsToRun = [{ description: 'Restoring the captured Windows feedback value', script: restoreScript }];
      verificationToRun = snapshot.exists ? `if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Siuf\\Rules' -Name NumberOfSIUFInPeriod -ErrorAction Stop).NumberOfSIUFInPeriod -ne ${snapshot.value}) { throw 'Captured Windows feedback value was not restored' }` : "if (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Siuf\\Rules' -Name NumberOfSIUFInPeriod -ErrorAction SilentlyContinue) { throw 'Windows feedback value was not removed' }";
    }
    if (optimizationId === 'disable-cloud-content' && existing?.snapshot) {
      const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
      if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'number')) return NextResponse.json<ApiResponse>({ success: false, error: "The original cloud content snapshot is invalid; revert was not attempted." }, { status: 409 });
      const restoreScript = snapshot.exists ? `Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\CloudContent' -Name DisableConsumerAccountStateContent -Value ${snapshot.value} -Type DWord -Force -ErrorAction Stop` : "Remove-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\CloudContent' -Name DisableConsumerAccountStateContent -ErrorAction Stop";
      commandsToRun = [{ description: 'Restoring the captured cloud content value', script: restoreScript }];
      verificationToRun = snapshot.exists ? `if ((Get-ItemProperty 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\CloudContent' -Name DisableConsumerAccountStateContent -ErrorAction Stop).DisableConsumerAccountStateContent -ne ${snapshot.value}) { throw 'Captured cloud content value was not restored' }` : "if (Get-ItemProperty 'HKLM:\\Software\\Policies\\Microsoft\\Windows\\CloudContent' -Name DisableConsumerAccountStateContent -ErrorAction SilentlyContinue) { throw 'Cloud content value was not removed' }";
    }
    if (optimizationId === 'disable-start-tracking' && existing?.snapshot) {
      const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
      if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'number')) return NextResponse.json<ApiResponse>({ success: false, error: "The original Start tracking snapshot is invalid; revert was not attempted." }, { status: 409 });
      const restoreScript = snapshot.exists ? `Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name Start_TrackProgs -Value ${snapshot.value} -Type DWord -Force -ErrorAction Stop` : "Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name Start_TrackProgs -ErrorAction Stop";
      commandsToRun = [{ description: 'Restoring the captured Start tracking value', script: restoreScript }];
      verificationToRun = snapshot.exists ? `if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name Start_TrackProgs -ErrorAction Stop).Start_TrackProgs -ne ${snapshot.value}) { throw 'Captured Start tracking value was not restored' }` : "if (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced' -Name Start_TrackProgs -ErrorAction SilentlyContinue) { throw 'Start tracking value was not removed' }";
    }
    if (optimizationId === 'disable-app-suggestions' && existing?.snapshot) {
      const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
      if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'number')) return NextResponse.json<ApiResponse>({ success: false, error: "The original app suggestions snapshot is invalid; revert was not attempted." }, { status: 409 });
      const restoreScript = snapshot.exists ? `Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager' -Name SubscribedContent-338388Enabled -Value ${snapshot.value} -Type DWord -Force -ErrorAction Stop` : "Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager' -Name SubscribedContent-338388Enabled -ErrorAction Stop";
      commandsToRun = [{ description: 'Restoring the captured app suggestions value', script: restoreScript }];
      verificationToRun = snapshot.exists ? `if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager' -Name SubscribedContent-338388Enabled -ErrorAction Stop).'SubscribedContent-338388Enabled' -ne ${snapshot.value}) { throw 'Captured app suggestions value was not restored' }` : "if (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager' -Name SubscribedContent-338388Enabled -ErrorAction SilentlyContinue) { throw 'App suggestions value was not removed' }";
    }
    if (optimizationId === 'disable-setting-sync' && existing?.snapshot) {
      const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
      if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'number')) return NextResponse.json<ApiResponse>({ success: false, error: "The original settings sync snapshot is invalid; revert was not attempted." }, { status: 409 });
      const restoreScript = snapshot.exists ? `Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\SettingSync' -Name SyncPolicy -Value ${snapshot.value} -Type DWord -Force -ErrorAction Stop` : "Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\SettingSync' -Name SyncPolicy -ErrorAction Stop";
      commandsToRun = [{ description: 'Restoring the captured settings sync value', script: restoreScript }];
      verificationToRun = snapshot.exists ? `if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\SettingSync' -Name SyncPolicy -ErrorAction Stop).SyncPolicy -ne ${snapshot.value}) { throw 'Captured settings sync value was not restored' }` : "if (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\SettingSync' -Name SyncPolicy -ErrorAction SilentlyContinue) { throw 'Settings sync value was not removed' }";
    }
    if (optimizationId === 'disable-handwriting-data' && existing?.snapshot) {
      const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
      if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'number')) return NextResponse.json<ApiResponse>({ success: false, error: "The original handwriting data snapshot is invalid; revert was not attempted." }, { status: 409 });
      const restoreScript = snapshot.exists ? `Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\TabletPC' -Name PreventHandwritingDataSharing -Value ${snapshot.value} -Type DWord -Force -ErrorAction Stop` : "Remove-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\TabletPC' -Name PreventHandwritingDataSharing -ErrorAction Stop";
      commandsToRun = [{ description: 'Restoring the captured handwriting data value', script: restoreScript }];
      verificationToRun = snapshot.exists ? `if ((Get-ItemProperty 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\TabletPC' -Name PreventHandwritingDataSharing -ErrorAction Stop).PreventHandwritingDataSharing -ne ${snapshot.value}) { throw 'Captured handwriting data value was not restored' }` : "if (Get-ItemProperty 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\TabletPC' -Name PreventHandwritingDataSharing -ErrorAction SilentlyContinue) { throw 'Handwriting data value was not removed' }";
    }
    if (optimizationId === 'disable-speech-recognition' && existing?.snapshot) {
      const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
      if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'number')) return NextResponse.json<ApiResponse>({ success: false, error: "The original speech recognition snapshot is invalid; revert was not attempted." }, { status: 409 });
      const restoreScript = snapshot.exists ? `Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Speech_OneCore\\Settings\\OnlineSpeechPrivacy' -Name HasAccepted -Value ${snapshot.value} -Type DWord -Force -ErrorAction Stop` : "Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Speech_OneCore\\Settings\\OnlineSpeechPrivacy' -Name HasAccepted -ErrorAction Stop";
      commandsToRun = [{ description: 'Restoring the captured speech recognition value', script: restoreScript }];
      verificationToRun = snapshot.exists ? `if ((Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Speech_OneCore\\Settings\\OnlineSpeechPrivacy' -Name HasAccepted -ErrorAction Stop).HasAccepted -ne ${snapshot.value}) { throw 'Captured speech recognition value was not restored' }` : "if (Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Speech_OneCore\\Settings\\OnlineSpeechPrivacy' -Name HasAccepted -ErrorAction SilentlyContinue) { throw 'Speech recognition value was not removed' }";
    }
    if (optimizationId === 'disable-find-my-device' && existing?.snapshot) {
      const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
      if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'number')) return NextResponse.json<ApiResponse>({ success: false, error: "The original Find My Device snapshot is invalid; revert was not attempted." }, { status: 409 });
      const restoreScript = snapshot.exists ? `Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Settings\\FindMyDevice' -Name LocationSyncEnabled -Value ${snapshot.value} -Type DWord -Force -ErrorAction Stop` : "Remove-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Settings\\FindMyDevice' -Name LocationSyncEnabled -ErrorAction Stop";
      commandsToRun = [{ description: 'Restoring the captured Find My Device value', script: restoreScript }];
      verificationToRun = snapshot.exists ? `if ((Get-ItemProperty 'HKLM:\\SOFTWARE\\Microsoft\\Settings\\FindMyDevice' -Name LocationSyncEnabled -ErrorAction Stop).LocationSyncEnabled -ne ${snapshot.value}) { throw 'Captured Find My Device value was not restored' }` : "if (Get-ItemProperty 'HKLM:\\SOFTWARE\\Microsoft\\Settings\\FindMyDevice' -Name LocationSyncEnabled -ErrorAction SilentlyContinue) { throw 'Find My Device value was not removed' }";
    }
    if (optimizationId === 'disable-diagnostic-data' && existing?.snapshot) {
      const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
      if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'number')) return NextResponse.json<ApiResponse>({ success: false, error: "The original diagnostic data snapshot is invalid; revert was not attempted." }, { status: 409 });
      const restoreScript = snapshot.exists ? `Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection' -Name AllowTelemetry -Value ${snapshot.value} -Type DWord -Force -ErrorAction Stop` : "Remove-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection' -Name AllowTelemetry -ErrorAction Stop";
      commandsToRun = [{ description: 'Restoring the captured diagnostic data value', script: restoreScript }];
      verificationToRun = snapshot.exists ? `if ((Get-ItemProperty 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection' -Name AllowTelemetry -ErrorAction Stop).AllowTelemetry -ne ${snapshot.value}) { throw 'Captured diagnostic data value was not restored' }` : "if (Get-ItemProperty 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection' -Name AllowTelemetry -ErrorAction SilentlyContinue) { throw 'Diagnostic data value was not removed' }";
    }
    if (optimizationId === 'disable-camera-access' && existing?.snapshot) {
      const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
      if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'string')) return NextResponse.json<ApiResponse>({ success: false, error: "The original camera access snapshot is invalid; revert was not attempted." }, { status: 409 });
      const escapedValue = typeof snapshot.value === 'string' ? snapshot.value.replace(/'/g, "''") : '';
      const restoreScript = snapshot.exists ? `Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\webcam' -Name Value -Value '${escapedValue}' -Force -ErrorAction Stop` : "Remove-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\webcam' -Name Value -ErrorAction Stop";
      commandsToRun = [{ description: 'Restoring the captured camera access value', script: restoreScript }];
      verificationToRun = snapshot.exists ? `if ((Get-ItemProperty 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\webcam' -Name Value -ErrorAction Stop).Value -ne '${escapedValue}') { throw 'Captured camera access value was not restored' }` : "if (Get-ItemProperty 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\webcam' -Name Value -ErrorAction SilentlyContinue) { throw 'Camera access value was not removed' }";
    }
    if (optimizationId === 'disable-microphone-access' && existing?.snapshot) {
      const snapshot = JSON.parse(existing.snapshot) as { exists?: unknown; value?: unknown };
      if (typeof snapshot.exists !== 'boolean' || (snapshot.exists && typeof snapshot.value !== 'string')) return NextResponse.json<ApiResponse>({ success: false, error: "The original microphone access snapshot is invalid; revert was not attempted." }, { status: 409 });
      const escapedValue = typeof snapshot.value === 'string' ? snapshot.value.replace(/'/g, "''") : '';
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

    const executionTime = Date.now() - startTime;

    // Only mark as reverted if NO errors occurred
    if (!hasError) {
      if (!sessionScopedOptimizationIds.has(optimizationId)) {
        await db.optimizationState.updateMany({
          where: { id: optimizationId },
          data: { applied: false }
        });
      }

      return NextResponse.json<ApiResponse>({
        success: true,
        data: {
          optimization: {
            id: optimizationId,
            name: generateName(optimizationId),
            applied: false
          },
          changesReverted,
          rebootRequired: cmdSet.rebootRequired,
          executionTime
        }
      });
    } else {
      return NextResponse.json<ApiResponse>(
        {
          success: false,
          error: "Failed to revert optimization. Please run the app as Administrator.",
          message: errorMessage,
          changesReverted,
          executionTime
        },
        { status: 500 }
      );
    }
  } catch (error) {
    console.error("Error reverting optimization:", error);
    return NextResponse.json<ApiResponse>(
      {
        success: false,
        error: "Failed to revert optimization",
        message: error instanceof Error ? error.message : "Unknown error occurred"
      },
      { status: 500 }
    );
  }
}