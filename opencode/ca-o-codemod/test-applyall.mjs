import { spawn } from 'node:child_process';

const ud = process.env.APPDATA + '\\ca-o-windows-optimizer';
const env = {
  ...process.env,
  CAO_STATE_PATH: ud + '\\optimization-state.json',
  PORT: '3130',
  HOSTNAME: '127.0.0.1',
};
const server = spawn('node', ['server.js'], {
  cwd: 'C:/Users/berna/OneDrive/Documentos/CA_O/.next/standalone',
  env, stdio: 'ignore',
});
const wait = ms => new Promise(r => setTimeout(r, ms));
for (let i = 0; i < 30; i++) { try { await fetch('http://127.0.0.1:3130/api/app-state'); break; } catch { await wait(1000); } }

console.log('apply-all con createBackup=true + show-seconds-clock (cosmético, reversible)...');
const t0 = Date.now();
const res = await fetch('http://127.0.0.1:3130/api/optimization/apply-all', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ ids: ['show-seconds-clock'], createBackup: true, confirmDangerous: true }),
});
const dt = ((Date.now() - t0) / 1000).toFixed(1);
const json = await res.json();
console.log(`HTTP ${res.status} en ${dt}s | success=${json.success}`);
if (json.message) console.log('message:', json.message.slice(0, 160));
if (json.data?.backupWarning) console.log('backupWarning:', json.data.backupWarning.slice(0, 160));
const item = json.data?.appliedOptimizations?.[0];
console.log('item:', item?.id, '-> success:', item?.success);

console.log('\nrevert del mismo id para dejar el sistema como estaba...');
const t1 = Date.now();
const r = await fetch('http://127.0.0.1:3130/api/optimization/revert', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ optimizationId: 'show-seconds-clock' }),
});
const rj = await r.json();
console.log(`revert ${((Date.now() - t1) / 1000).toFixed(1)}s | success=${rj.success}`);

server.kill();
