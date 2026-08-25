import { spawn } from 'node:child_process';
const env = { ...process.env, CAO_STATE_PATH: process.env.APPDATA + '\\ca-o-windows-optimizer\\optimization-state.json', PORT: '3131', HOSTNAME: '127.0.0.1' };
const server = spawn('node', ['server.js'], { cwd: 'C:/Users/berna/OneDrive/Documentos/CA_O/.next/standalone', env, stdio: 'ignore' });
const wait = ms => new Promise(r => setTimeout(r, ms));
for (let i = 0; i < 30; i++) { try { await fetch('http://127.0.0.1:3131/api/app-state'); break; } catch { await wait(1000); } }

// 1) system/info: primera llamada (fría) y segunda (cacheada)
let t0 = Date.now();
const r1 = await fetch('http://127.0.0.1:3131/api/system/info');
console.log('system/info fría:', ((Date.now() - t0) / 1000).toFixed(1) + 's');
t0 = Date.now();
const r2 = await fetch('http://127.0.0.1:3131/api/system/info');
console.log('system/info cacheada:', ((Date.now() - t0) / 1000).toFixed(1) + 's');
const info = await r2.json();
console.log('  CPU:', info.data?.cpu?.name?.slice(0, 50), '| RAM:', info.data?.memory?.totalGB + 'GB', '| health:', info.data?.systemHealth?.overallStatus);

// 2) tres sondeos seguidos separados 5s (simula el dashboard)
for (let i = 0; i < 3; i++) {
  await wait(5000);
  t0 = Date.now();
  await fetch('http://127.0.0.1:3131/api/system/info');
  console.log(`sondeo ${i + 1}:`, ((Date.now() - t0) / 1000).toFixed(2) + 's');
}

// 3) optimization (con verificación cacheada del state previo si existe)
t0 = Date.now();
const opt = await fetch('http://127.0.0.1:3131/api/optimization');
console.log('optimization:', ((Date.now() - t0) / 1000).toFixed(1) + 's |', (await opt.json()).data.optimizations.length, 'opts');
t0 = Date.now();
const st = await fetch('http://127.0.0.1:3131/api/optimization/state');
const stj = await st.json();
console.log('state:', ((Date.now() - t0) / 1000).toFixed(1) + 's | true:', Object.values(stj.data).filter(v => v === true).length);

server.kill();
