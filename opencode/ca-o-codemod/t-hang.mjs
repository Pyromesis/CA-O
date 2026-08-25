import fs from 'node:fs';

// ── 1) Runner a prueba de colgues: spawn + taskkill /T /F al timeout ──
{
  const p = 'src/lib/powershell-runner.ts';
  let s = fs.readFileSync(p, 'utf8');
  const start = s.indexOf('export async function runPowerShell');
  const end = s.indexOf('/**\n * Creates a system restore point');
  if (start < 0 || end < 0) throw new Error('runner anchors missing');
  const NEW = `export async function runPowerShell(
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
    const enhancedCommand = \`
$ErrorActionPreference = 'Stop';
$ProgressPreference = 'SilentlyContinue';
  $PSNativeCommandUseErrorActionPreference = $true;
\${command}
\`;

    const encodedCommand = Buffer.from(enhancedCommand, 'utf16le').toString('base64');
    const psPath = 'C:\\\\Windows\\\\System32\\\\WindowsPowerShell\\\\v1.0\\\\powershell.exe';
    const child = spawn(psPath, ['-NoProfile', '-NonInteractive', '-EncodedCommand', encodedCommand], {
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
        output: [stdout, stderr].filter(Boolean).join('\\n').trim(),
        error: timedOut ? \`PowerShell execution timed out after \${Math.round(timeoutMs / 1000)}s\` : 'Unknown error',
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
        output: [stdout, stderr].filter(Boolean).join('\\n').trim(),
        error: err.message,
      });
    });

    child.on('close', (code) => {
      const output = [stdout, stderr].filter(Boolean).join('\\n').trim();
      if (code === 0) {
        finish({ success: true, output });
      } else {
        finish({
          success: false,
          output,
          error: stderr.trim() || (timedOut ? \`Timed out after \${Math.round(timeoutMs / 1000)}s\` : \`PowerShell exited with code \${code}\`),
          exitCode: code ?? undefined,
        });
      }
    });
  });
}

`;
  s = s.slice(0, start) + NEW + s.slice(end);
  // restore point acotado a 90s: si VSS tarda más, seguimos sin backup
  s = s.replace('return runPowerShell(script, false, { timeoutMs: 300_000 });', 'return runPowerShell(script, false, { timeoutMs: 90_000 });');
  fs.writeFileSync(p, s);
  console.log('runner rewritten');
}

// ── 2) apply-all: backup no fatal (continúa con aviso) ──
{
  const p = 'src/app/api/optimization/apply-all/route.ts';
  let s = fs.readFileSync(p, 'utf8');
  const OLD = `    if (createBackup) {
      console.log(\`Creating mandatory system restore point before bulk apply of \${ids.length} optimizations...\`);
      const backupResult = await createSystemRestorePoint("CA-O Bulk Backup");
      if (!backupResult.success) {
        return NextResponse.json<ApiResponse>(
          {
            success: false,
            error: "Could not create the requested system restore point.",
            message: backupResult.error
          },
          { status: 503 }
        );
      }
    }`;
  const NEWB = `    let backupWarning: string | undefined;
    if (createBackup) {
      console.log(\`Creating system restore point before bulk apply of \${ids.length} optimizations...\`);
      const backupResult = await createSystemRestorePoint("CA-O Bulk Backup");
      if (!backupResult.success) {
        backupWarning = backupResult.error || 'Restore point could not be created';
        console.warn('Restore point failed, continuing without backup:', backupWarning);
      }
    }`;
  if (!s.includes(OLD)) throw new Error('apply-all backup block missing');
  s = s.replace(OLD, NEWB);
  // exponer el aviso en la respuesta final (éxito y fallo parcial)
  s = s.replace(
    'const appliedOptimizations: AppliedOptimization[] = [];\n    let rebootRequired = false;\n    let someFailed = false;',
    'const appliedOptimizations: AppliedOptimization[] = [];\n    let rebootRequired = false;\n    let someFailed = false;'
  );
  // añadir backupWarning al payload de éxito: buscar NextResponse.json<ApiResponse>({ success: true, data: { ... appliedOptimizations
  s = s.replace(
    /NextResponse\.json<ApiResponse>\(\{\s*success: true,\s*data: \{\s*appliedOptimizations:/,
    'NextResponse.json<ApiResponse>({\n          success: true,\n          message: backupWarning,\n          data: {\n            backupWarning,\n            appliedOptimizations:'
  );
  fs.writeFileSync(p, s);
  console.log('apply-all patched:', s.includes('backupWarning'));
}

// ── 3) apply (individual): mismo criterio ──
{
  const p = 'src/app/api/optimization/apply/route.ts';
  let s = fs.readFileSync(p, 'utf8');
  const OLD = `    if (createBackup) {
      console.log(\`Creating mandatory system restore point before applying \${id}...\`);
      const backupResult = await createSystemRestorePoint(\`CA-O Backup: \${id}\`);
      if (!backupResult.success) {
        return NextResponse.json<ApiResponse>(
          {
            success: false,
            error: "Could not create the requested system restore point.",
            message: backupResult.error
          },
          { status: 503 }
        );
      }
    }`;
  const NEWB = `    let backupWarning: string | undefined;
    if (createBackup) {
      console.log(\`Creating system restore point before applying \${id}...\`);
      const backupResult = await createSystemRestorePoint(\`CA-O Backup: \${id}\`);
      if (!backupResult.success) {
        backupWarning = backupResult.error || 'Restore point could not be created';
        console.warn('Restore point failed, continuing without backup:', backupWarning);
      }
    }`;
  if (!s.includes(OLD)) throw new Error('apply backup block missing');
  s = s.replace(OLD, NEWB);
  s = s.replace(
    /NextResponse\.json<ApiResponse>\(\{\s*success: true,\s*data: \{\s*optimization: \{/,
    'NextResponse.json<ApiResponse>({\n        success: true,\n        message: backupWarning,\n        data: {\n          backupWarning,\n          optimization: {'
  );
  fs.writeFileSync(p, s);
  console.log('apply patched:', s.includes('backupWarning'));
}
