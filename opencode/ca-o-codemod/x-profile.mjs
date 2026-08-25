import fs from 'node:fs';
const p = 'src/store/useAppStore.ts';
let s = fs.readFileSync(p, 'utf8');

const startMarker = '      applyProfile: async (profileId) => {';
const start = s.indexOf(startMarker);
if (start < 0) throw new Error('applyProfile start not found');

// encontrar el cierre del bloque: '      },' seguido de '\n' + otro método a nivel igual
// más fiable: contar llaves desde el inicio del bloque
let depth = 0, end = -1;
for (let i = start; i < s.length; i++) {
  const c = s[i];
  if (c === '{') depth++;
  else if (c === '}') { depth--; if (depth === 0) { end = i + 1; break; } }
}
if (end < 0) throw new Error('applyProfile end not found');

const NEW = `      applyProfile: async (profileId, onProgress) => {
        const state = get();
        const profile = state.profiles.find(p => p.id === profileId);

        if (!profile) return;

        set({ isProcessing: true, selectedProfile: profileId });

        try {
          const profileIds = profile.optimizationIds.filter((optId) =>
            isExecutableOptimizationId(optId) &&
            !state.optimizations.find((opt) => opt.id === optId)?.isApplied
          );

          if (profileIds.length === 0) return;

          // 1) Punto de restauración único y acotado antes de tocar nada
          if (onProgress) onProgress(0, profileIds.length, '__backup__');
          try {
            const rp = await fetch('/api/troubleshoot/execute', {
              method: 'POST',
              headers: { 'Content-Type': 'application/json' },
              body: JSON.stringify({ action: 'create-restore-point' })
            });
            if (!rp.ok) console.warn('Restore point failed; continuing without backup');
          } catch { /* sin backup, seguimos */ }

          // 2) Aplicación una por una con progreso en vivo
          let done = 0;
          const failed: string[] = [];
          for (const optId of profileIds) {
            const optName = state.optimizations.find((opt) => opt.id === optId)?.nameKey || optId;
            if (onProgress) onProgress(done, profileIds.length, optName);
            try {
              const res = await fetch('/api/optimization/apply', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ optimizationId: optId, createBackup: false, confirmDangerous: true })
              });
              const json = await res.json().catch(() => ({}));
              if (res.ok && json.success) {
                set((cs) => ({
                  optimizations: cs.optimizations.map((o) =>
                    o.id === optId ? { ...o, isApplied: true, isEnabled: true } : o
                  ),
                  history: [
                    { id: optId, nameKey: optName, action: 'applied' as const, timestamp: Date.now() },
                    ...cs.history
                  ].slice(0, 100)
                }));
              } else if (res.status === 409) {
                // Ya aplicada en otra sesión: sincronizar bandera y seguir
                set((cs) => ({
                  optimizations: cs.optimizations.map((o) =>
                    o.id === optId ? { ...o, isApplied: true, isEnabled: true } : o
                  )
                }));
              } else {
                failed.push(optId);
                set((cs) => ({
                  optimizations: cs.optimizations.map((o) =>
                    o.id === optId ? { ...o, isApplied: false, isEnabled: false } : o
                  )
                }));
              }
            } catch {
              failed.push(optId);
            }
            done++;
            if (onProgress) onProgress(done, profileIds.length, optName);
          }

          if (failed.length > 0) {
            throw new Error(\`\${failed.length} optimizaciones no pudieron aplicarse: \${failed.join(', ')}\`);
          }
        } finally {
          set({ isProcessing: false });
        }
      }`;

s = s.slice(0, start) + NEW + s.slice(end);

// tipo de la interfaz: añadir onProgress opcional
s = s.replace(
  'applyProfile: (profileId: string) => Promise<void>;',
  'applyProfile: (profileId: string, onProgress?: (done: number, total: number, current: string) => void) => Promise<void>;'
);

fs.writeFileSync(p, s);
console.log('applyProfile rewritten. onProgress in type:', s.includes('onProgress?:'));
