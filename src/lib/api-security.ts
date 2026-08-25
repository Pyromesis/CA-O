/**
 * Local API security layer (v2).
 *
 * The Next.js server binds to 127.0.0.1 only, but any local process or a
 * DNS-rebinding page could still reach it. This module enforces:
 *
 *  - random per-boot session secret (persisted 0600 next to the state file
 *    so the Electron shell can hand it to the renderer once);
 *  - Host header validation (127.0.0.1 only);
 *  - Origin validation when present;
 *  - token check on every mutating request (enforced in production /
 *    packaged builds; relaxed in `next dev` for developer ergonomics);
 *  - simple in-memory rate limiting;
 *  - append-only audit log of every mutation attempt.
 */

import crypto from 'crypto';
import fs from 'fs';
import path from 'path';
import type { NextRequest } from 'next/server';

const MUTATING_METHODS = new Set(['POST', 'PUT', 'PATCH', 'DELETE']);
const STATE_DIR = process.env.CAO_STATE_PATH
  ? path.dirname(process.env.CAO_STATE_PATH)
  : process.cwd();
const SECRET_FILE = path.join(STATE_DIR, '.cao-session-secret');
const AUDIT_FILE = path.join(STATE_DIR, 'audit.log');

let sessionSecret: Buffer | null = null;

/**
 * Resolve the session secret:
 *  1. `CAO_SESSION_SECRET` env (set by the Electron launcher — single source
 *     of truth shared with the renderer through the URL hash);
 *  2. persisted random file next to the state dir (dev restarts reuse it);
 *  3. fresh random bytes (last resort).
 */
export function getSessionSecret(): string {
  if (sessionSecret) return sessionSecret.toString('hex');

  const fromEnv = process.env.CAO_SESSION_SECRET;
  if (fromEnv && /^[a-f0-9]{64}$/.test(fromEnv)) {
    sessionSecret = Buffer.from(fromEnv, 'hex');
    return fromEnv;
  }

  try {
    if (fs.existsSync(SECRET_FILE)) {
      const hex = fs.readFileSync(SECRET_FILE, 'utf8').trim();
      if (/^[a-f0-9]{64}$/.test(hex)) {
        // Reuse across restarts only if fresh (max 12h) to survive dev reloads.
        const ageMs = Date.now() - fs.statSync(SECRET_FILE).mtimeMs;
        if (ageMs < 12 * 60 * 60 * 1000) {
          sessionSecret = Buffer.from(hex, 'hex');
          return hex;
        }
      }
    }
  } catch { /* fall through to generation */ }

  sessionSecret = crypto.randomBytes(32);
  try {
    fs.mkdirSync(path.dirname(SECRET_FILE), { recursive: true });
    fs.writeFileSync(SECRET_FILE, sessionSecret.toString('hex'), { encoding: 'utf8', mode: 0o600 });
  } catch { /* best effort */ }
  return sessionSecret.toString('hex');
}

function timingSafeEqualHex(a: string, b: string): boolean {
  if (typeof a !== 'string' || typeof b !== 'string') return false;
  const ab = Buffer.from(a);
  const bb = Buffer.from(b);
  if (ab.length !== bb.length) return false;
  return crypto.timingSafeEqual(ab, bb);
}

// ---------------- Rate limiting ----------------

interface Bucket { count: number; resetAt: number }
const buckets = new Map<string, Bucket>();
const WINDOW_MS = 60_000;
const DEFAULT_LIMIT = 90;
const HEAVY_LIMIT = 12; // apply/apply-all/revert/benchmark endpoints

export function checkRateLimit(key: string, heavy = false): boolean {
  const now = Date.now();
  const bucket = buckets.get(key);
  if (!bucket || now > bucket.resetAt) {
    buckets.set(key, { count: 1, resetAt: now + WINDOW_MS });
    return true;
  }
  bucket.count += 1;
  return bucket.count <= (heavy ? HEAVY_LIMIT : DEFAULT_LIMIT);
}

// ---------------- Audit log ----------------

export function auditLog(line: Record<string, unknown>): void {
  try {
    fs.appendFileSync(
      AUDIT_FILE,
      JSON.stringify({ ts: new Date().toISOString(), ...line }) + '\n',
      { encoding: 'utf8' },
    );
  } catch { /* auditing must never break requests */ }
}

// ---------------- Main guard ----------------

export interface GuardResult {
  ok: boolean;
  status?: number;
  error?: string;
}

/**
 * Validate an incoming request against localhost hardening rules.
 * Call at the top of every mutating route handler.
 */
export function guardMutation(request: NextRequest, options: { heavy?: boolean } = {}): GuardResult {
  const method = request.method.toUpperCase();
  if (!MUTATING_METHODS.has(method)) return { ok: true };

  const host = request.headers.get('host') ?? '';
  const hostName = host.split(':')[0];
  if (hostName !== '127.0.0.1' && hostName !== 'localhost') {
    auditLog({ event: 'blocked-host', host });
    return { ok: false, status: 403, error: 'This API only accepts connections from 127.0.0.1.' };
  }

  const origin = request.headers.get('origin');
  if (origin) {
    try {
      const parsed = new URL(origin);
      const originHost = parsed.hostname;
      if (originHost !== '127.0.0.1' && originHost !== 'localhost') {
        auditLog({ event: 'blocked-origin', origin });
        return { ok: false, status: 403, error: 'Cross-origin requests are not allowed.' };
      }
    } catch {
      return { ok: false, status: 400, error: 'Invalid Origin header.' };
    }
  }

  const isProd = process.env.NODE_ENV === 'production';
  const isPackaged = process.env.CAO_PACKAGED === '1';
  if (isProd || isPackaged) {
    const provided = request.headers.get('x-cao-token') ?? '';
    if (!provided || !timingSafeEqualHex(provided, getSessionSecret())) {
      auditLog({ event: 'blocked-token', path: new URL(request.url).pathname });
      return { ok: false, status: 401, error: 'Missing or invalid session token.' };
    }
  }

  const ip = request.headers.get('x-forwarded-for')?.split(',')[0]?.trim() || 'local';
  const key = `${ip}:${new URL(request.url).pathname}`;
  if (!checkRateLimit(key, options.heavy ?? true)) {
    auditLog({ event: 'rate-limited', path: new URL(request.url).pathname });
    return { ok: false, status: 429, error: 'Too many requests; slow down.' };
  }

  auditLog({
    event: 'mutation',
    method,
    path: new URL(request.url).pathname,
    query: new URL(request.url).search,
  });
  return { ok: true };
}

/** Helper for routes that want to short-circuit with the standard envelope. */
export function guardOrResponse(request: NextRequest, heavy = false): { json: unknown; status: number } | null {
  const result = guardMutation(request, { heavy });
  if (result.ok) return null;
  return { json: { success: false, error: result.error }, status: result.status ?? 403 };
}
