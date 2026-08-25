import { NextRequest, NextResponse } from "next/server";
import { createSystemRestorePoint, runPowerShell } from "@/lib/powershell-runner";

type TroubleshootActionType = "restore-audio" | "restore-bluetooth" | "restore-network" | "restore-windows-update" | "restore-display" | "create-restore-point" | "restore-all"
  | "repair-system-files" | "reset-store-cache" | "restart-explorer" | "flush-dns-cache" | "clean-temp-junk";
type StepStatus = "completed" | "failed";

interface TroubleshootStepDefinition {
  name: string;
  script: string;
  fixesIssue?: boolean;
  timeoutMs?: number;
}

interface TroubleshootDefinition {
  name: string;
  description: string;
  steps: TroubleshootStepDefinition[];
  rebootRequired: boolean;
  recommendations: string[];
}

interface ActionStep {
  step: number;
  name: string;
  status: StepStatus;
  message: string;
  duration: number;
}

const serviceRestart = (service: string, label: string): TroubleshootStepDefinition => ({
  name: label,
  script: `Restart-Service -Name '${service}' -Force -ErrorAction Stop; Write-Output '${label} completed'`,
  fixesIssue: true,
});

const realTroubleshootScripts: Record<TroubleshootActionType, TroubleshootDefinition> = {
  "restore-audio": {
    name: "Audio Troubleshooter",
    description: "Diagnose and fix audio playback issues",
    steps: [
      { name: "Checking audio device status", script: "Get-PnpDevice -Class AudioEndpoint -ErrorAction Stop | Select-Object -First 3 FriendlyName, Status | Format-Table -AutoSize | Out-String" },
      serviceRestart("Audiosrv", "Restarting Windows Audio service"),
      { name: "Checking audio driver status", script: "Get-CimInstance Win32_PnPSignedDriver -Filter \"DeviceClass='MEDIA'\" | Select-Object -First 2 DeviceName, DriverVersion | Format-Table -AutoSize | Out-String" },
      { name: "Checking audio enhancements", script: "Get-ItemProperty 'HKCU:\\Software\\Microsoft\\Multimedia\\Sound Mapper' -ErrorAction SilentlyContinue | Out-String" },
    ],
    rebootRequired: false,
    recommendations: ["Consider updating your audio drivers from the manufacturer's website", "Check Sound Settings and the selected output device"],
  },
  "restore-bluetooth": {
    name: "Bluetooth Troubleshooter",
    description: "Fix Bluetooth connectivity and pairing issues",
    steps: [
      { name: "Checking Bluetooth adapter status", script: "Get-PnpDevice -Class Bluetooth -ErrorAction Stop | Select-Object -First 3 FriendlyName, Status | Format-Table -AutoSize | Out-String" },
      serviceRestart("bthserv", "Restarting Bluetooth Support service"),
      { name: "Listing paired devices", script: "Get-PnpDevice -Class Bluetooth -Status OK -ErrorAction Stop | Select-Object -First 5 FriendlyName | Format-Table -AutoSize | Out-String" },
    ],
    rebootRequired: false,
    recommendations: ["Try removing and re-pairing Bluetooth devices", "Ensure Bluetooth is enabled in Windows Settings"],
  },
  "restore-network": {
    name: "Network Troubleshooter",
    description: "Diagnose and repair network connectivity problems",
    steps: [
      { name: "Checking network adapters", script: "Get-NetAdapter -ErrorAction Stop | Where-Object Status -ne 'Not Present' | Select-Object Name, Status, LinkSpeed | Format-Table -AutoSize | Out-String" },
      { name: "Flushing DNS cache", script: "ipconfig /flushdns 2>&1 | Out-String", fixesIssue: true },
      { name: "Releasing and renewing IP", script: "ipconfig /release 2>&1 | Out-Null; ipconfig /renew 2>&1 | Out-String", fixesIssue: true },
      { name: "Resetting Winsock catalog", script: "netsh winsock reset 2>&1 | Out-String", fixesIssue: true },
      { name: "Testing internet connectivity", script: "$result = Test-NetConnection -ComputerName 8.8.8.8 -Port 443 -WarningAction SilentlyContinue; if (-not $result.TcpTestSucceeded) { throw 'Internet connectivity test failed' }; Write-Output 'Internet connectivity OK'" },
      { name: "Testing DNS resolution", script: "Resolve-DnsName google.com -ErrorAction Stop | Select-Object -First 1 Name, IPAddress | Format-Table -AutoSize | Out-String" },
    ],
    rebootRequired: true,
    recommendations: ["Restart your router if connectivity issues continue", "Check network adapter drivers and firmware"],
  },
  "restore-windows-update": {
    name: "Windows Update Repair",
    description: "Fix Windows Update errors and restore update functionality",
    steps: [
      { name: "Stopping Windows Update services", script: "Stop-Service -Name wuauserv, cryptSvc, bits, msiserver -Force -ErrorAction Stop; Write-Output 'Windows Update services stopped'", fixesIssue: true },
      { name: "Clearing SoftwareDistribution cache", script: "Remove-Item -Path 'C:\\Windows\\SoftwareDistribution\\Download\\*' -Recurse -Force -ErrorAction Stop; Write-Output 'SoftwareDistribution cache cleared'", fixesIssue: true },
      { name: "Resetting catroot2", script: "if (Test-Path 'C:\\Windows\\System32\\catroot2.bak') { Remove-Item 'C:\\Windows\\System32\\catroot2.bak' -Recurse -Force }; Rename-Item 'C:\\Windows\\System32\\catroot2' catroot2.bak -Force -ErrorAction Stop; Write-Output 'catroot2 reset'", fixesIssue: true },
      { name: "Starting Windows Update services", script: "Start-Service -Name wuauserv, cryptSvc, bits, msiserver -ErrorAction Stop; Write-Output 'Windows Update services started'", fixesIssue: true },
      { name: "Verifying Windows Update services", script: "Get-Service -Name wuauserv, cryptSvc, bits, msiserver -ErrorAction Stop | Where-Object Status -ne 'Running' | ForEach-Object { throw \"Service $($_.Name) is not running\" }; Write-Output 'Windows Update services verified'" },
    ],
    rebootRequired: true,
    recommendations: ["Keep Windows Update enabled for security patches", "Restart Windows after this repair if updates remain blocked"],
  },
  "restore-display": {
    name: "Display/GPU Troubleshooter",
    description: "Fix display, graphics, and GPU-related issues",
    steps: [
      { name: "Checking display adapters", script: "Get-PnpDevice -Class Display -ErrorAction Stop | Select-Object FriendlyName, Status | Format-Table -AutoSize | Out-String" },
      { name: "Checking graphics drivers", script: "Get-CimInstance Win32_VideoController -ErrorAction Stop | Select-Object Name, DriverVersion | Format-Table -AutoSize | Out-String" },
      serviceRestart("uxsms", "Restarting Desktop Window Manager service"),
    ],
    rebootRequired: false,
    recommendations: ["Keep graphics drivers up to date", "Use the GPU vendor diagnostic tools if the issue persists"],
  },
  "create-restore-point": {
    name: "System Restore Point Creator",
    description: "Create a system restore point for recovery",
    steps: [
      { name: "Creating restore point", script: "$srKey = 'HKLM:\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\SystemRestore'; if (-not (Test-Path $srKey)) { New-Item -Path $srKey -Force | Out-Null }; Set-ItemProperty -Path $srKey -Name SystemRestorePointCreationFrequency -Value 0 -Type DWord -Force; try { Enable-ComputerRestore -Drive 'C:\\' -ErrorAction SilentlyContinue } catch { }; Checkpoint-Computer -Description 'CA-O Safety Point' -RestorePointType MODIFY_SETTINGS -ErrorAction Stop; Write-Output 'Restore point created'", fixesIssue: true },
      { name: "Verifying restore point", script: "$point = Get-ComputerRestorePoint -ErrorAction Stop | Select-Object -Last 1; if (-not $point) { throw 'Could not verify restore point' }; Write-Output \"Verified restore point: $($point.Description)\"" },
    ],
    rebootRequired: false,
    recommendations: ["Create restore points before major system changes"],
  },

  "repair-system-files": {
    name: "System File Repair",
    description: "Repair corrupted system files with DISM and SFC",
    steps: [
      { name: "Running DISM health restore (this can take several minutes)", script: "DISM /Online /Cleanup-Image /RestoreHealth /NoRestart", fixesIssue: true, timeoutMs: 900000 },
      { name: "Running System File Checker", script: "sfc /scannow", fixesIssue: true, timeoutMs: 900000 },
    ],
    rebootRequired: true,
    recommendations: ["Restart Windows afterwards to complete the repair", "Run again if issues persist"],
  },
  "reset-store-cache": {
    name: "Microsoft Store Cache Reset",
    description: "Clear the Microsoft Store cache and restart its services",
    steps: [
      { name: "Stopping Store services", script: "Stop-Service -Name InstallService, ClipSVC -Force -ErrorAction SilentlyContinue; Write-Output 'Store services stopped'", fixesIssue: true },
      { name: "Clearing Store cache", script: "Remove-Item -Path \"$env:LOCALAPPDATA\\Packages\\Microsoft.WindowsStore_8wekyb3d8bbwe\\LocalCache\\*\" -Recurse -Force -ErrorAction SilentlyContinue; Write-Output 'Store cache cleared'", fixesIssue: true },
      { name: "Starting Store services", script: "Start-Service -Name ClipSVC, InstallService -ErrorAction SilentlyContinue; Write-Output 'Store services started'" },
    ],
    rebootRequired: false,
    recommendations: ["Open the Microsoft Store to let it rebuild its data"],
  },
  "restart-explorer": {
    name: "Restart Explorer Shell",
    description: "Fix frozen taskbar/desktop by restarting explorer.exe",
    steps: [
      { name: "Restarting explorer.exe", script: "Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue; Start-Sleep -Seconds 2; if (-not (Get-Process -Name explorer -ErrorAction SilentlyContinue)) { Start-Process explorer.exe }; Write-Output 'Explorer restarted'", fixesIssue: true },
    ],
    rebootRequired: false,
    recommendations: ["Desktop and taskbar will flash briefly while restarting"],
  },
  "flush-dns-cache": {
    name: "DNS Cache Flush",
    description: "Clear the DNS resolver cache to fix name resolution issues",
    steps: [
      { name: "Flushing DNS resolver cache", script: "Clear-DnsClientCache -ErrorAction Stop; ipconfig /flushdns | Out-String", fixesIssue: true },
    ],
    rebootRequired: false,
    recommendations: ["Useful after changing DNS servers or network issues"],
  },
  "clean-temp-junk": {
    name: "Temporary Files Cleanup",
    description: "Clear user and system temporary files that cause slowdowns",
    steps: [
      { name: "Clearing user temp folder", script: "Get-ChildItem -LiteralPath \"$env:TEMP\" -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue; Write-Output 'User temp cleaned'", fixesIssue: true },
      { name: "Clearing Windows temp folder", script: "Get-ChildItem -LiteralPath 'C:\\Windows\\Temp' -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue; Write-Output 'Windows temp cleaned'", fixesIssue: true },
    ],
    rebootRequired: false,
    recommendations: ["Close other applications first so files in use are skipped"],
  },

  "restore-all": {
    name: "Complete System Repair",
    description: "Run a bounded set of system repair actions",
    steps: [
      { name: "Creating safety restore point", script: "Enable-ComputerRestore -Drive 'C:\\' -ErrorAction Stop; Checkpoint-Computer -Description ('CA-O Full Repair - ' + (Get-Date -Format 'yyyy-MM-dd HH:mm')) -RestorePointType MODIFY_SETTINGS -ErrorAction Stop; Write-Output 'Safety restore point created'", fixesIssue: true },
      serviceRestart("Audiosrv", "Restarting Windows Audio service"),
      { name: "Flushing DNS and resetting Winsock", script: "ipconfig /flushdns 2>&1 | Out-Null; netsh winsock reset 2>&1 | Out-String", fixesIssue: true },
      { name: "Checking system files", script: "sfc /scannow 2>&1 | Out-String", fixesIssue: true },
      { name: "Checking component store", script: "DISM /Online /Cleanup-Image /CheckHealth 2>&1 | Out-String" },
      serviceRestart("wuauserv", "Restarting Windows Update service"),
    ],
    rebootRequired: true,
    recommendations: ["Review SFC and DISM output if issues continue", "Keep separate backups of important files"],
  },
};

async function executeRealAction(action: TroubleshootActionType) {
  const definition = realTroubleshootScripts[action];
  const steps: ActionStep[] = [];
  let issuesFound = 0;
  let issuesFixed = 0;

  for (const [index, stepDefinition] of definition.steps.entries()) {
    const startedAt = Date.now();
    const result = await runPowerShell(stepDefinition.script, false, { timeoutMs: stepDefinition.timeoutMs });
    const step: ActionStep = {
      step: index + 1,
      name: stepDefinition.name,
      status: result.success ? "completed" : "failed",
      message: result.success ? result.output || `${stepDefinition.name} completed` : result.error || `${stepDefinition.name} failed`,
      duration: Date.now() - startedAt,
    };
    steps.push(step);
    if (step.status === "failed") issuesFound++;
    if (step.status === "completed" && stepDefinition.fixesIssue) issuesFixed++;
  }

  return { steps, issuesFound, issuesFixed, recommendations: definition.recommendations };
}

export async function POST(request: NextRequest) {
  const startedAt = Date.now();
  try {
    const body = await request.json() as { action?: string; options?: { backupFirst?: boolean; forceRestart?: boolean } };
    const action = body.action as TroubleshootActionType;
    if (!action || !(action in realTroubleshootScripts)) {
      return NextResponse.json({ success: false, error: "Invalid or missing troubleshoot action" }, { status: 400 });
    }

    let restorePointCreated = false;
    if (body.options?.backupFirst && action !== "create-restore-point") {
      const result = await createSystemRestorePoint("CA-O Pre-Troubleshoot");
      if (!result.success) return NextResponse.json({ success: false, error: result.error || "Could not create restore point" }, { status: 503 });
      restorePointCreated = true;
    }

    const definition = realTroubleshootScripts[action];
    const result = await executeRealAction(action);
    const failedSteps = result.steps.filter((step) => step.status === "failed").length;
    const status = failedSteps === 0 ? "completed" : failedSteps < result.steps.length ? "partial" : "failed";
    restorePointCreated ||= action === "create-restore-point" && result.steps.some((step) => step.name === "Creating restore point" && step.status === "completed");

    return NextResponse.json({
      success: status !== "failed",
      data: { action, actionName: definition.name, status, steps: result.steps, issuesFound: result.issuesFound, issuesFixed: result.issuesFixed, rebootRequired: definition.rebootRequired || body.options?.forceRestart === true, restorePointCreated, executionTime: Date.now() - startedAt, recommendations: result.recommendations },
    });
  } catch (error) {
    return NextResponse.json({ success: false, error: "Failed to execute troubleshoot action", message: error instanceof Error ? error.message : "Unknown error" }, { status: 500 });
  }
}

export async function GET() {
  const actions = Object.entries(realTroubleshootScripts).map(([id, definition]) => ({ id, name: definition.name, description: definition.description, stepsCount: definition.steps.length, rebootRequired: definition.rebootRequired }));
  return NextResponse.json({ success: true, data: { actions, totalActions: actions.length } });
}
