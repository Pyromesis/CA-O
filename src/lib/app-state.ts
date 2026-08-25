import fs from 'fs';
import path from 'path';

/**
 * JSON-file backed flags that must survive between launches regardless of
 * which local origin (port) the UI is served from: onboarding completion and
 * splash-screen visibility. Stored next to optimization-state.json so the
 * packaged Electron app keeps it inside its userData directory.
 */

export interface AppStateFlags {
  onboardingCompleted?: boolean;
  splashSeen?: boolean;
}

export interface ResolvedAppStateFlags {
  onboardingCompleted: boolean;
  splashSeen: boolean;
}

function getAppStatePath(): string {
  const statePath = process.env.CAO_STATE_PATH || path.join(process.cwd(), 'optimization-state.json');
  return path.join(path.dirname(statePath), 'app-state.json');
}

export function readAppState(): ResolvedAppStateFlags {
  try {
    const parsed: unknown = JSON.parse(fs.readFileSync(getAppStatePath(), 'utf-8'));
    if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) {
      return { onboardingCompleted: false, splashSeen: false };
    }
    const flags = parsed as Record<string, unknown>;
    return {
      onboardingCompleted: flags.onboardingCompleted === true,
      splashSeen: flags.splashSeen === true,
    };
  } catch {
    return { onboardingCompleted: false, splashSeen: false };
  }
}

export function updateAppState(patch: AppStateFlags): ResolvedAppStateFlags {
  const next: ResolvedAppStateFlags = { ...readAppState(), ...patch };
  const target = getAppStatePath();
  fs.mkdirSync(path.dirname(target), { recursive: true });
  const temporaryPath = `${target}.${process.pid}.${Date.now()}.tmp`;
  try {
    fs.writeFileSync(temporaryPath, JSON.stringify(next, null, 2), 'utf-8');
    fs.renameSync(temporaryPath, target);
  } catch (error) {
    try {
      if (fs.existsSync(temporaryPath)) fs.unlinkSync(temporaryPath);
    } catch {
      // Preserve the original persistence error.
    }
    throw new Error(`Could not persist app state: ${error instanceof Error ? error.message : String(error)}`);
  }
  return next;
}
