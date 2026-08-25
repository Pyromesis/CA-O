import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { pathToFileURL, fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const tempDir = fs.mkdtempSync(path.join(os.tmpdir(), 'ca-o-state-'));
const statePath = path.join(tempDir, 'optimization-state.json');
process.env.CAO_STATE_PATH = statePath;
const { db } = await import(pathToFileURL(path.join(root, 'src/lib/db.ts')).href);

await db.optimizationState.upsert({ where: { id: 'test-state' }, update: { applied: true, snapshot: JSON.stringify({ exists: true, value: '.Current' }) }, create: { id: 'test-state', applied: true } });
await db.optimizationState.upsert({ where: { id: 'test-state-2' }, update: { applied: true }, create: { id: 'test-state-2', applied: true } });
fs.writeFileSync(statePath, '{broken');
const rows = await db.optimizationState.findMany();
const backupExists = fs.existsSync(`${statePath}.bak`);
fs.rmSync(tempDir, { recursive: true, force: true });

if (!rows.some((row) => row.id === 'test-state' && row.applied && row.snapshot?.includes('.Current')) || !backupExists) {
  console.error('Persistence recovery failed');
  process.exit(1);
}

console.log('Persistence recovery OK');
