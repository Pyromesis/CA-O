import { spawn } from 'child_process';
import path from 'path';

export interface PowerShellResult {
  success: boolean;
  output: string;
  error?: string;
  exitCode?: number;
}

/**
 * Resolve the Windows PowerShell executable at runtime instead of hardcoding
 * an absolute path (hardcoded paths confuse Next.js output-file tracing).
 */
function resolvePowerShellPath(): string {
  const windir = process.env.WINDIR || 'C:\\Windows';
  return path.join(windir, 'System32', 'WindowsPowerShell', 'v1.0', 'powershell.exe');
}

/**
 * Executes a PowerShell command or script
 * @param command The PowerShell command to execute
 * @param asAdmin Parameter kept for API compatibility. The packaged Electron app
 * requests elevation through its Windows application manifest.
 */
export async function runPowerShell(
  command: string,
  asAdmin: boolean = false,
  options: { timeoutMs?: number } = {},
): Promise<PowerShellResult> {
  if (process.platform !== 'win32') {
    return {
      success: false,
      output: '',
      error: 'Windows PowerShell optimizations are only supported on Windows.',
    };
  }

  const timeoutMs = options.timeoutMs ?? 120_000;

  return new Promise<PowerShellResult>((resolve) => {
    const enhancedCommand = `
$ErrorActionPreference = 'Stop';
$ProgressPreference = 'SilentlyContinue';
  $PSNativeCommandUseErrorActionPreference = $true;
${command}
`;

    const encodedCommand = Buffer.from(enhancedCommand, 'utf16le').toString('base64');
    const child = spawn(resolvePowerShellPath(), ['-NoProfile', '-NonInteractive', '-EncodedCommand', encodedCommand], {
      windowsHide: true,
    });

    let stdout = '';
    let stderr = '';
    let settled = false;
    let timedOut = false;

    const finish = (result: PowerShellResult) => {
      if (settled) return;
      settled = true;
      clearTimeout(timer);
      resolve(result);
    };

    // Matar el ARBOL de procesos: sin esto, nietos de VSS heredan los pipes
    // y la promesa nunca se resuelve aunque node mate al hijo.
    const timer = setTimeout(() => {
      timedOut = true;
      if (child.pid) {
        try {
          spawn('taskkill', ['/PID', String(child.pid), '/T', '/F'], { windowsHide: true });
        } catch { /* best-effort */ }
      }
      // margen para que el árbol muera y los pipes se cierren
      setTimeout(() => finish({
        success: false,
        output: [stdout, stderr].filter(Boolean).join('\n').trim(),
        error: timedOut ? `PowerShell execution timed out after ${Math.round(timeoutMs / 1000)}s` : 'Unknown error',
      }), 1500);
    }, timeoutMs);

    child.stdout.on('data', (d: Buffer) => {
      if (stdout.length < 1024 * 1024) stdout += d.toString();
    });
    child.stderr.on('data', (d: Buffer) => {
      if (stderr.length < 1024 * 1024) stderr += d.toString();
    });

    child.on('error', (err) => {
      finish({
        success: false,
        output: [stdout, stderr].filter(Boolean).join('\n').trim(),
        error: err.message,
      });
    });

    child.on('close', (code) => {
      const output = [stdout, stderr].filter(Boolean).join('\n').trim();
      if (code === 0) {
        finish({ success: true, output });
      } else {
        finish({
          success: false,
          output,
          error: stderr.trim() || (timedOut ? `Timed out after ${Math.round(timeoutMs / 1000)}s` : `PowerShell exited with code ${code}`),
          exitCode: code ?? undefined,
        });
      }
    });
  });
}

/**
 * Creates a system restore point if system restore is enabled.
 * Clears Windows' built-in 24h creation-frequency limit first
 * (SystemRestorePointCreationFrequency=0, documented Microsoft policy value)
 * so consecutive applications are never silently refused.
 */
export async function createSystemRestorePoint(description: string = "CA-O Backup"): Promise<PowerShellResult> {
  const safeDescription = description.replace(/'/g, "''");
  const script = `
    $ErrorActionPreference = 'Stop'
    $systemRestoreKey = 'HKLM:\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\SystemRestore'
    if (-not (Test-Path $systemRestoreKey)) { New-Item -Path $systemRestoreKey -Force | Out-Null }
    Set-ItemProperty -Path $systemRestoreKey -Name SystemRestorePointCreationFrequency -Value 0 -Type DWord -Force
    try { Enable-ComputerRestore -Drive 'C:\\' -ErrorAction SilentlyContinue } catch { }
    Checkpoint-Computer -Description '${safeDescription}' -RestorePointType 'MODIFY_SETTINGS' -ErrorAction Stop
    Write-Output 'Restore point created successfully.'
  `;
  return runPowerShell(script, false, { timeoutMs: 90_000 });
}
