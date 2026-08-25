/**
 * Generates docs/OPTIMIZATION-AUDIT.md (#2/#60).
 *
 * Parses the live catalog sources (same technique as the contract tests)
 * and emits the full audit table: ID, old category, new taxonomy, evidence,
 * contextuality and final disposition (KEEP / MOVE_TO_* / CONTEXTUAL /
 * REMOVE / FIXED). Run after any catalog change:
 *
 *   node scripts/generate-audit.mjs
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const read = (p) => fs.readFileSync(path.join(root, p), 'utf8');

const apiRoute = read('src/app/api/optimization/route.ts');
const taxonomySrc = read('src/lib/catalog/taxonomy.ts');
const evidenceSrc = read('src/lib/catalog/evidence.ts');
const applicabilitySrc = read('src/lib/catalog/applicability.ts');

// --- old categories from the API map ---
const section = (text, start, end) => {
  const i = text.indexOf(start);
  const j = text.indexOf(end, i + start.length);
  return i >= 0 && j > i ? text.slice(i, j) : '';
};
const catSection = section(apiRoute, 'const categoryOptimizations', '// Catálogo cacheado');
const oldCategoryById = new Map();
for (const m of catSection.matchAll(/(\w+):\s*\[([^\]]+)\]/g)) {
  const group = m[1];
  for (const id of [...m[2].matchAll(/'([a-z0-9-]+)'/g)].map((x) => x[1])) {
    oldCategoryById.set(id, group);
  }
}

// --- taxonomy ---
const taxonomy = new Map(
  [...taxonomySrc.matchAll(/'([a-z][a-z0-9-]*)':\s*\{\s*group:\s*'([a-z-]+)',\s*subgroup:\s*'([a-z-]+)',\s*kind:\s*'([a-z-]+)'/g)]
    .map((m) => [m[1], { group: m[2], subgroup: m[3], kind: m[4] }]),
);

// --- evidence overrides (id -> {impact, confidence}) ---
const evidenceOverrides = new Map(
  [...evidenceSrc.matchAll(/\{ id:\s*'([a-z0-9-]+)',\s*expectedImpact:\s*'([a-z-]+)',\s*confidence:\s*'([a-z]+)'/g)]
    .map((m) => [m[1], { impact: m[2], confidence: m[3] }]),
);
const groupDefaultImpact = new Map(
  [...evidenceSrc.matchAll(/^\s{2}(\w+):\s*\{\s*expectedImpact:\s*'([a-z-]+)',\s*confidence:\s*'(\w+)'/gm)]
    .map((m) => [m[1], { impact: m[2], confidence: m[3] }]),
);

// --- contextual rules (requires/ack present) ---
const contextualIds = new Set();
for (const m of applicabilitySrc.matchAll(/'([a-z0-9-]+)':\s*\{([^}]*)\}/g)) {
  if (/requires:|requiresExperimentalAcknowledgement|requiresSecurityConfirmation|antiCheatSensitivity/.test(m[2])) {
    contextualIds.add(m[1]);
  }
}

// --- known one-off dispositions ---
const special = new Map([
  ['disable-keyboard-filter', 'REMOVE (executed: touched biometric WbioSrvc; guidance-only entry deleted in v3)'],
  ['enable-core-parking', 'BUG_FIX_REQUIRED -> FIXED: renamed to disable-core-parking (command sets CPMINCORES=100 = parking OFF); revert now restores true default (CPMINCORES=0)'],
]);

const OLD_TO_LABEL = { system: 'System', network: 'Network', input: 'Input', tweaks: 'Tweaks', powerful: 'Powerful', privacy: 'Privacy' };

function dispositionFor(id, entry) {
  if (special.has(id)) return special.get(id);
  const oldCat = oldCategoryById.get(id) ?? '(new)';
  const contextual = contextualIds.has(id);
  const movedGroups = {
    system: ['security', 'maintenance', 'privacy'],
    network: ['repair', 'security', 'experimental', 'performance'],
    input: ['tweaks', 'gaming', 'performance', 'experimental', 'diagnostics'],
    tweaks: ['privacy', 'tweaks', 'performance'],
    powerful: ['gaming', 'performance', 'experimental', 'diagnostics', 'maintenance', 'repair', 'security'],
    privacy: ['privacy', 'system', 'maintenance'],
  };
  const wasMoved = entry.group !== 'system' && !(movedGroups[oldCat] ?? []).includes(entry.group) && oldCat !== entry.group;
  const sameGroup = oldCat === entry.group ||
    (oldCat === 'powerful' && entry.group === 'performance') ||
    (oldCat === 'system' && entry.group === 'maintenance');

  if (!sameGroup && !wasMoved) {
    // moved to a recognized destination
    return `MOVE_TO_${entry.group.toUpperCase()}`;
  }
  if (wasMoved) return `MOVE_TO_${entry.group.toUpperCase()}`;
  return contextual ? 'KEEP_BUT_CONTEXTUAL' : 'KEEP';
}

const rows = [];
for (const [id, entry] of [...taxonomy.entries()].sort((a, b) => a[0].localeCompare(b[0]))) {
  const ev = evidenceOverrides.get(id) ?? groupDefaultImpact.get(entry.group) ?? { impact: '?', confidence: '?' };
  rows.push({
    id,
    old: OLD_TO_LABEL[oldCategoryById.get(id)] ?? oldCategoryById.get(id) ?? '-',
    group: entry.group,
    subgroup: entry.subgroup,
    kind: entry.kind,
    impact: ev.impact,
    confidence: ev.confidence,
    contextual: contextualIds.has(id) ? 'yes' : '',
    disposition: dispositionFor(id, entry),
  });
}

const counts = {};
for (const r of rows) counts[r.disposition.split(' ')[0]] = (counts[r.disposition.split(' ')[0]] ?? 0) + 1;

const md = [];
md.push('# CA-O — Auditoría individual del catálogo (2026)');
md.push('');
md.push(`Generado automáticamente por \`scripts/generate-audit.mjs\`. ${rows.length} IDs clasificados.`);
md.push('');
md.push('| Disposición | Cantidad |');
md.push('|---|---|');
for (const [k, v] of Object.entries(counts).sort((a, b) => b[1] - a[1])) md.push(`| ${k} | ${v} |`);
md.push('');
md.push('| ID | Categoría antigua | Grupo nuevo | Subgrupo | Tipo | Impacto esperado | Confianza | Contextual | Disposición |');
md.push('|---|---|---|---|---|---|---|---|---|');
for (const r of rows) {
  md.push(`| \`${r.id}\` | ${r.old} | ${r.group} | ${r.subgroup} | ${r.kind} | ${r.impact} | ${r.confidence} | ${r.contextual || '-'} | ${r.disposition} |`);
}
md.push('');

const outPath = path.join(root, 'docs', 'OPTIMIZATION-AUDIT.md');
fs.writeFileSync(outPath, md.join('\n'), 'utf8');
console.log(`Audit table written to docs/OPTIMIZATION-AUDIT.md (${rows.length} ids, dispositions: ${JSON.stringify(counts)}).`);
