import { NextRequest, NextResponse } from "next/server";
import { runPowerShell } from "@/lib/powershell-runner";
import { guardOrResponse } from "@/lib/api-security";

/**
 * Maintenance endpoint (v2, requirement #10).
 *
 * Separates cleanup into explicit targets and ALWAYS estimates recoverable
 * space before deleting anything. Prefetch is deliberately NOT touched as
 * an "optimization" — clearing prefetch is a troubleshooting action at most.
 */

type CleanupTarget =
  | 'user-temp'
  | 'windows-temp'
  | 'thumbnails'
  | 'shader-cache'
  | 'recycle-bin'
  | 'delivery-optimization-cache'
  | 'windows-update-cache';

const TARGET_PATHS: Record<Exclude<CleanupTarget, 'recycle-bin' | 'delivery-optimization-cache' | 'windows-update-cache'>, string> = {
  'user-temp': '$env:TEMP',
  'windows-temp': '(Join-Path $env:WINDIR "Temp")',
  'thumbnails': '(Join-Path $env:LOCALAPPDATA "Microsoft\\Windows\\Explorer")',
  'shader-cache': '(Join-Path $env:LOCALAPPDATA "D3DSCache")',
};

function buildEstimateScript(target: CleanupTarget): string {
  if (target === 'recycle-bin') {
    return `
$ErrorActionPreference = 'SilentlyContinue'
$shell = New-Object -ComObject Shell.Application
$items = @($shell.NameSpace(0xA).Items())
$total = 0
foreach ($item in $items) { $total += $item.Size }
"$total"
`;
  }
  if (target === 'delivery-optimization-cache') {
    // No simple public size API; report unknown (-1).
    return `"-1"`;
  }
  if (target === 'windows-update-cache') {
    return `
$ErrorActionPreference = 'SilentlyContinue'
$p = Join-Path $env:WINDIR "SoftwareDistribution\\Download"
$total = 0
if (Test-Path $p) {
  Get-ChildItem -LiteralPath $p -Recurse -Force -File | ForEach-Object { $total += $_.Length }
}
"$total"
`;
  }
  const pathExpr = TARGET_PATHS[target];
  return `
$ErrorActionPreference = 'SilentlyContinue'
$p = ${pathExpr}
$total = 0
if (Test-Path $p) {
  Get-ChildItem -LiteralPath $p -Recurse -Force -File -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notlike '*\\Explorer\\thumbcache_*.db' -or '${target}' -ne 'thumbnails' } |
    ForEach-Object { $total += $_.Length }
  if ('${target}' -eq 'thumbnails') {
    Get-ChildItem -LiteralPath $p -Filter 'thumbcache_*.db' -File -ErrorAction SilentlyContinue | ForEach-Object { $total += $_.Length }
  }
}
"$total"
`;
}

function buildCleanScript(target: CleanupTarget): string {
  switch (target) {
    case 'user-temp':
      return `Get-ChildItem -LiteralPath $env:TEMP -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue; Write-Output 'user temp cleared'`;
    case 'windows-temp':
      return `Get-ChildItem -LiteralPath (Join-Path $env:WINDIR "Temp") -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue; Write-Output 'windows temp cleared'`;
    case 'thumbnails':
      return `Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue; Get-ChildItem -LiteralPath (Join-Path $env:LOCALAPPDATA "Microsoft\\Windows\\Explorer") -Filter 'thumbcache_*.db' -File -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue; Start-Process explorer.exe; Write-Output 'thumbnail cache cleared'`;
    case 'shader-cache':
      return `Get-ChildItem -LiteralPath (Join-Path $env:LOCALAPPDATA "D3DSCache") -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue; Write-Output 'D3D shader cache cleared (will rebuild automatically)'`;
    case 'recycle-bin':
      return `Clear-RecycleBin -Force -ErrorAction SilentlyContinue; Write-Output 'recycle bin emptied'`;
    case 'delivery-optimization-cache':
      return `try { Delete-DeliveryOptimizationCache -Force -ErrorAction Stop; Write-Output 'delivery optimization cache cleared' } catch { Write-Output 'delivery optimization cache not available or already empty' }`;
    case 'windows-update-cache':
      return `Stop-Service wuauserv -Force -ErrorAction SilentlyContinue; Get-ChildItem -LiteralPath (Join-Path $env:WINDIR "SoftwareDistribution\\Download") -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue; Start-Service wuauserv -ErrorAction SilentlyContinue; Write-Output 'update download cache cleared'`;
    default:
      throw new Error(`unknown target`);
  }
}

const VALID_TARGETS: CleanupTarget[] = [
  'user-temp', 'windows-temp', 'thumbnails', 'shader-cache',
  'recycle-bin', 'delivery-optimization-cache', 'windows-update-cache',
];

export async function POST(request: NextRequest) {
  const guard = guardOrResponse(request, true);
  if (guard) return NextResponse.json(guard.json, { status: guard.status });

  try {
    const body: unknown = await request.json();
    if (!body || typeof body !== 'object' || Array.isArray(body)) {
      return NextResponse.json({ success: false, error: "Request body must be an object" }, { status: 400 });
    }
    const { action, targets } = body as { action?: string; targets?: string[] };

    const requested = Array.isArray(targets) && targets.length > 0
      ? (targets.filter((t): t is CleanupTarget => (VALID_TARGETS as string[]).includes(t)))
      : VALID_TARGETS;
    if (requested.length === 0) {
      return NextResponse.json({ success: false, error: 'No valid targets provided.' }, { status: 400 });
    }

    if (action === 'estimate') {
      const results: Array<{ target: CleanupTarget; estimatedBytes: number | null; known: boolean }> = [];
      for (const target of requested) {
        const raw = await runPowerShell(buildEstimateScript(target), false, { timeoutMs: 60_000 });
        const bytes = raw.success && raw.output ? parseInt(raw.output.trim(), 10) : NaN;
        results.push({
          target,
          estimatedBytes: Number.isFinite(bytes) ? bytes : null,
          known: Number.isFinite(bytes) && bytes >= 0,
        });
      }
      return NextResponse.json({
        success: true,
        data: { mode: 'estimate', results },
        note: {
          es: 'Nada se ha borrado en este paso. Prefetch no se limpia como optimización: es un mito de rendimiento.',
          en: 'Nothing was deleted in this step. Prefetch is not cleaned as an optimization: that is a performance myth.',
        },
      });
    }

    if (action === 'clean') {
      const results: Array<{ target: CleanupTarget; success: boolean; output?: string; error?: string }> = [];
      for (const target of requested) {
        const raw = await runPowerShell(buildCleanScript(target), false, { timeoutMs: 120_000 });
        results.push({ target, success: raw.success, output: raw.output || undefined, error: raw.error });
      }
      return NextResponse.json({
        success: results.some((r) => r.success),
        data: { mode: 'clean', results },
      });
    }

    return NextResponse.json(
      { success: false, error: "action must be 'estimate' or 'clean'" },
      { status: 400 }
    );
  } catch (error) {
    console.error('Maintenance temp failed:', error);
    return NextResponse.json(
      { success: false, error: error instanceof Error ? error.message : 'Maintenance action failed.' },
      { status: 500 }
    );
  }
}
