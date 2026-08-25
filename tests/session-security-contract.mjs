/**
 * Session security contract (v2).
 *
 * Enforces the localhost hardening requirements:
 *  - mutating API routes validate session token / origin / host;
 *  - timing-safe token comparison and crypto-random generation;
 *  - rate limiting + audit logging exist;
 *  - no `-ExecutionPolicy Bypass` anywhere in shipped source;
 *  - timer-resolution helper has a bounded lifetime;
 *  - Electron runs sandboxed with a permission deny-all handler;
 *  - Next.js sends a CSP with frame-ancestors 'none'.
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const read = (p) => fs.readFileSync(path.join(root, p), 'utf8');

const apiSecurity = read('src/lib/api-security.ts');
const apply = read('src/app/api/optimization/apply/route.ts');
const applyAll = read('src/app/api/optimization/apply-all/route.ts');
const revert = read('src/app/api/optimization/revert/route.ts');
const revertAll = read('src/app/api/optimization/revert-all/route.ts');
const troubleshoot = read('src/app/api/troubleshoot/execute/route.ts');
const dnsBench = read('src/app/api/benchmark/dns/route.ts');
const systemBench = read('src/app/api/benchmark/system/route.ts');
const commands = read('src/lib/optimization-commands.ts');
const electronMain = read('electron-main.js');
const nextConfig = read('next.config.ts');

const checks = {
  // api-security module
  randomSecret: apiSecurity.includes('crypto.randomBytes'),
  timingSafeCompare: apiSecurity.includes('timingSafeEqual'),
  hostValidation: apiSecurity.includes("hostName !== '127.0.0.1'"),
  originValidation: apiSecurity.includes('blocked-origin'),
  rateLimit: apiSecurity.includes('checkRateLimit') && apiSecurity.includes('429'),
  auditLog: apiSecurity.includes('auditLog'),

  // every mutating route guarded
  applyGuard: apply.includes('guardOrResponse(request'),
  applyAllGuard: applyAll.includes('guardOrResponse(request'),
  revertGuard: revert.includes('guardOrResponse(request'),
  revertAllGuard: revertAll.includes('guardOrResponse(request'),
  troubleshootGuard: troubleshoot.includes('guardOrResponse(request'),
  dnsBenchmarkGuard: dnsBench.includes('guardOrResponse(request'),
  systemBenchmarkGuard: systemBench.includes('guardOrResponse(request'),

  // security confirmations in the apply route
  securityConfirmationFlag: apply.includes('confirmSecurityChange'),
  experimentalAckFlag: apply.includes('acknowledgeExperimental'),
  applicabilityGate: apply.includes('evaluateApplicability') && apply.includes('422'),

  // no ExecutionPolicy bypass in shipped source
  noBypassInCommands: !commands.includes('ExecutionPolicy'),
  noBypassInRunner: !read('src/lib/powershell-runner.ts').includes('ExecutionPolicy'),

  // timer helper bounded lifetime
  timerLifetimeCapped: commands.includes('(Get-Date).AddHours(2)'),

  // electron hardening
  electronSandbox: electronMain.includes('sandbox: true'),
  permissionDenyAll: electronMain.includes('setPermissionRequestHandler'),
  contextIsolationKept: electronMain.includes('contextIsolation: true') && electronMain.includes('nodeIntegration: false'),

  // CSP
  cspPresent: nextConfig.includes('Content-Security-Policy') && nextConfig.includes("frame-ancestors 'none'"),
};

const failed = Object.entries(checks).filter(([, ok]) => !ok).map(([name]) => name);
if (failed.length) {
  console.error(JSON.stringify({ failed }, null, 2));
  process.exit(1);
}

console.log(`Session security contract OK: ${Object.keys(checks).length} checks.`);
