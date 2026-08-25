/**
 * Renderer-side session token handling (v2).
 *
 * The Electron launcher hands the random session secret to the renderer
 * exactly once through the URL hash (`/#tk=<hex>`), which never reaches
 * the server. This module captures it and attaches it as `X-CAO-Token`
 * to every mutating API request. In dev mode the server skips the check.
 */

let cachedToken: string | null = null;

export function captureSessionToken(): void {
  if (typeof window === 'undefined') return;
  try {
    const hash = window.location.hash;
    const match = hash.match(/^#tk=([a-f0-9]{64})$/);
    if (match) {
      sessionStorage.setItem('cao-session-token', match[1]);
      // Strip the token from the URL immediately (history.replaceState keeps
      // it out of the address bar and out of shared links).
      window.history.replaceState(null, '', window.location.pathname + window.location.search);
    }
  } catch { /* storage unavailable */ }
}

function getToken(): string | null {
  if (cachedToken) return cachedToken;
  if (typeof window === 'undefined') return null;
  try {
    cachedToken = sessionStorage.getItem('cao-session-token');
  } catch { cachedToken = null; }
  return cachedToken;
}

const MUTATING = new Set(['POST', 'PUT', 'PATCH', 'DELETE']);

/**
 * Drop-in fetch wrapper for CA-O API calls. Adds the session token to
 * mutating requests when available.
 */
export async function apiFetch(input: string, init: RequestInit = {}): Promise<Response> {
  const method = (init.method ?? 'GET').toUpperCase();
  const headers = new Headers(init.headers ?? {});
  if (MUTATING.has(method)) {
    const token = getToken();
    if (token) headers.set('x-cao-token', token);
  }
  return fetch(input, { ...init, headers });
}
