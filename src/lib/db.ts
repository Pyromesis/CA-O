import fs from 'fs';
import path from 'path';

/**
 * Simple JSON-file backed storage for optimization states.
 * Replaces Prisma/SQLite to avoid table-creation and EPERM issues.
 * The file is stored next to the running process so it works both
 * in development (`next dev`) and inside the packaged Electron app.
 */

interface OptimizationEntry {
  id: string;
  applied: boolean;
  updatedAt: Date;
  snapshot?: string;
  /** v2: structured provenance for the snapshot (build, fingerprint, etc.). */
  meta?: Record<string, unknown>;
}

type StateMap = Record<string, { applied: boolean; updatedAt: string; snapshot?: string; meta?: Record<string, unknown> }>;

function getDbPath(): string {
  return process.env.CAO_STATE_PATH || path.join(process.cwd(), 'optimization-state.json');
}

function readDB(): StateMap {
  const dbPath = getDbPath();
  const backupPath = `${dbPath}.bak`;

  const readStateFile = (filePath: string): StateMap => {
    const parsed: unknown = JSON.parse(fs.readFileSync(/*turbopackIgnore: true*/ filePath, 'utf-8'));
    if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) return {};

    return Object.fromEntries(
      Object.entries(parsed).filter(([, value]) => {
        if (!value || typeof value !== 'object' || Array.isArray(value)) return false;
        const entry = value as { applied?: unknown; updatedAt?: unknown; snapshot?: unknown; meta?: unknown };
        return typeof entry.applied === 'boolean' && typeof entry.updatedAt === 'string' &&
          (entry.snapshot === undefined || typeof entry.snapshot === 'string') &&
          (entry.meta === undefined || (typeof entry.meta === 'object' && entry.meta !== null && !Array.isArray(entry.meta)));
      })
    ) as StateMap;
  };

  try {
    if (!fs.existsSync(/*turbopackIgnore: true*/ dbPath)) return {};
    return readStateFile(dbPath);
  } catch (err) {
    try {
      if (fs.existsSync(/*turbopackIgnore: true*/ backupPath)) {
        console.warn('[db] State file was invalid; recovered the last valid backup.');
        return readStateFile(backupPath);
      }
    } catch (backupError) {
      console.warn('[db] Could not read state backup:', backupError);
    }
    console.warn('[db] Could not read state file, starting fresh:', err);
  }
  return {};
}

function writeDB(data: StateMap): void {
  const dbPath = getDbPath();
  const backupPath = `${dbPath}.bak`;
  const temporaryPath = `${dbPath}.${process.pid}.${Date.now()}.tmp`;

  try {
    fs.mkdirSync(path.dirname(dbPath), { recursive: true });
    if (fs.existsSync(/*turbopackIgnore: true*/ dbPath)) {
      fs.copyFileSync(dbPath, backupPath);
    }
    fs.writeFileSync(temporaryPath, JSON.stringify(data, null, 2), {
      encoding: 'utf-8',
      mode: 0o600,
    });
    fs.renameSync(temporaryPath, dbPath);
  } catch (err) {
    try {
      if (fs.existsSync(temporaryPath)) fs.unlinkSync(temporaryPath);
    } catch {
      // Preserve the original persistence error.
    }
    throw new Error(`Could not persist optimization state: ${err instanceof Error ? err.message : String(err)}`);
  }
}

/**
 * Drop-in replacement for Prisma's `db.optimizationState` that the
 * API routes already call. Every method signature matches what the
 * existing route handlers expect.
 */
export const db = {
  optimizationState: {
    findUnique: async ({ where }: { where: { id: string } }): Promise<OptimizationEntry | null> => {
      const data = readDB();
      const entry = data[where.id];
      if (!entry) return null;
      return { id: where.id, applied: entry.applied, updatedAt: new Date(entry.updatedAt), snapshot: entry.snapshot, meta: entry.meta };
    },

    findMany: async (): Promise<OptimizationEntry[]> => {
      const data = readDB();
      return Object.entries(data).map(([id, state]) => ({
        id,
        applied: state.applied,
        updatedAt: new Date(state.updatedAt),
        snapshot: state.snapshot,
        meta: state.meta,
      }));
    },

    upsert: async ({ where, update, create }: {
      where: { id: string };
      update: { applied: boolean; snapshot?: string; meta?: Record<string, unknown> };
      create: { id: string; applied: boolean; snapshot?: string; meta?: Record<string, unknown> };
    }): Promise<OptimizationEntry> => {
      const data = readDB();
      const applied = update?.applied ?? create.applied;
      const now = new Date().toISOString();
      const snapshot = update?.snapshot ?? data[where.id]?.snapshot ?? create.snapshot;
      const meta = update?.meta ?? create?.meta;
      data[where.id] = { applied, updatedAt: now, ...(snapshot !== undefined ? { snapshot } : {}), ...(meta !== undefined ? { meta } : {}) };
      writeDB(data);
      return { id: where.id, applied, updatedAt: new Date(now), snapshot: data[where.id].snapshot, meta: data[where.id].meta };
    },

    updateMany: async ({ where, data: updateData }: {
      where: { id: string };
      data: { applied: boolean };
    }): Promise<{ count: number }> => {
      const data = readDB();
      if (data[where.id]) {
        data[where.id] = { ...data[where.id], applied: updateData.applied, updatedAt: new Date().toISOString() };
        writeDB(data);
        return { count: 1 };
      }
      return { count: 0 };
    },

    deleteMany: async (): Promise<{ count: number }> => {
      const data = readDB();
      const count = Object.keys(data).length;
      writeDB({});
      return { count };
    },
  },
};