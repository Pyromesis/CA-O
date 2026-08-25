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
  /** v3 state model (#7): desired vs actual bookkeeping. */
  lastAppliedAt?: string;
  lastRevertedAt?: string;
  lastVerifiedAt?: string;
  verificationStatus?: 'verified' | 'drifted' | 'error' | 'unknown';
  lastError?: string;
}

export interface StateRowV3 {
  applied: boolean;
  updatedAt: string;
  snapshot?: string;
  meta?: Record<string, unknown>;
  lastAppliedAt?: string;
  lastRevertedAt?: string;
  lastVerifiedAt?: string;
  verificationStatus?: 'verified' | 'drifted' | 'error' | 'unknown';
  lastError?: string;
}

type StateMap = Record<string, StateRowV3>;

const OPTIONAL_STRING_FIELDS = [
  'snapshot', 'lastAppliedAt', 'lastRevertedAt', 'lastVerifiedAt', 'verificationStatus', 'lastError',
] as const;

function isValidStatus(value: unknown): value is NonNullable<OptimizationEntry['verificationStatus']> {
  return value === 'verified' || value === 'drifted' || value === 'error' || value === 'unknown';
}

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
        const entry = value as Record<string, unknown>;
        if (typeof entry.applied !== 'boolean' || typeof entry.updatedAt !== 'string') return false;
        for (const field of OPTIONAL_STRING_FIELDS) {
          const v = entry[field];
          if (v !== undefined && typeof v !== 'string') return false;
        }
        if (entry.verificationStatus !== undefined && !isValidStatus(entry.verificationStatus)) return false;
        if (entry.meta !== undefined && (typeof entry.meta !== 'object' || entry.meta === null || Array.isArray(entry.meta))) return false;
        return true;
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
      return { id: where.id, ...entry, updatedAt: new Date(entry.updatedAt) };
    },

    findMany: async (): Promise<OptimizationEntry[]> => {
      const data = readDB();
      return Object.entries(data).map(([id, state]) => ({ id, ...state, updatedAt: new Date(state.updatedAt) }));
    },

    upsert: async ({ where, update, create }: {
      where: { id: string };
      update: Partial<Omit<OptimizationEntry, 'id' | 'updatedAt'>>;
      create: { id: string } & Partial<Omit<OptimizationEntry, 'id' | 'updatedAt'>>;
    }): Promise<OptimizationEntry> => {
      const data = readDB();
      const previous = data[where.id];
      const merged: StateRowV3 = {
        applied: update?.applied ?? create.applied ?? previous?.applied ?? false,
        updatedAt: new Date().toISOString(),
        snapshot: update?.snapshot ?? previous?.snapshot ?? create.snapshot,
        meta: update?.meta ?? previous?.meta ?? create.meta,
        lastAppliedAt: update?.lastAppliedAt ?? previous?.lastAppliedAt ?? create.lastAppliedAt,
        lastRevertedAt: update?.lastRevertedAt ?? previous?.lastRevertedAt ?? create.lastRevertedAt,
        lastVerifiedAt: update?.lastVerifiedAt ?? previous?.lastVerifiedAt ?? create.lastVerifiedAt,
        verificationStatus: update?.verificationStatus ?? previous?.verificationStatus ?? create.verificationStatus,
        lastError: update?.lastError ?? previous?.lastError ?? create.lastError,
      };
      // Drop undefined optional keys to keep the file clean.
      for (const key of Object.keys(merged) as Array<keyof StateRowV3>) {
        if (merged[key] === undefined) delete merged[key];
      }
      data[where.id] = merged;
      writeDB(data);
      return { id: where.id, ...data[where.id], updatedAt: new Date(data[where.id].updatedAt) };
    },

    updateMany: async ({ where, data: updateData }: {
      where: { id: string };
      data: Partial<Omit<OptimizationEntry, 'id' | 'updatedAt'>>;
    }): Promise<{ count: number }> => {
      const stateData = readDB();
      if (stateData[where.id]) {
        stateData[where.id] = { ...stateData[where.id], ...updateData, updatedAt: new Date().toISOString() };
        writeDB(stateData);
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