/**
 * Catalog taxonomy contract (v2).
 *
 * Verifies the evidence-based reclassification:
 *  - every executable catalog ID is classified exactly once;
 *  - groups/subgroups/kinds are valid;
 *  - contested folklore lives in `experimental`;
 *  - flush/reset/cleanup actions live under `repair`;
 *  - HVCI/memory-integrity lives under `security` with security-tradeoff kind;
 *  - maintenance/repair items are NOT inside performance/gaming groups;
 *  - profiles never include security trade-offs.
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const read = (p) => fs.readFileSync(path.join(root, p), 'utf8');

const taxonomySrc = read('src/lib/catalog/taxonomy.ts');
const apiRoute = read('src/app/api/optimization/route.ts');
const profilesSrc = read('src/lib/profiles.ts');

// --- collect IDs from the API category map (source of truth for the UI) ---
const section = (text, start, end) => {
  const i = text.indexOf(start);
  const j = text.indexOf(end, i + start.length);
  return i >= 0 && j > i ? text.slice(i, j) : '';
};
const categorySection = section(apiRoute, 'const categoryOptimizations', '// Catálogo cacheado');
const catalogIds = [...categorySection.matchAll(/'([a-z][a-z0-9-]*)'/g)].map((m) => m[1]);

if (catalogIds.includes('disable-keyboard-filter')) {
  console.error('catalog-taxonomy: disable-keyboard-filter was removed but is still referenced in the catalog');
  process.exit(1);
}
if (catalogIds.includes('enable-core-parking')) {
  console.error('catalog-taxonomy: enable-core-parking was renamed to disable-core-parking but still appears');
  process.exit(1);
}
if (catalogIds.length < 150) {
  console.error(`catalog-taxonomy: expected >=150 catalog ids, found ${catalogIds.length}`);
  process.exit(1);
}

// --- parse taxonomy entries from source ---
const entryMatches = [...taxonomySrc.matchAll(/'([a-z][a-z0-9-]*)':\s*\{\s*group:\s*'([a-z-]+)',\s*subgroup:\s*'([a-z-]+)',\s*kind:\s*'([a-z-]+)'/g)];
const taxonomy = new Map(entryMatches.map((m) => [m[1], { group: m[2], subgroup: m[3], kind: m[4] }]));

const VALID_GROUPS = new Set(['performance', 'gaming', 'diagnostics', 'security', 'privacy', 'maintenance', 'repair', 'tweaks', 'experimental']);
const VALID_KINDS = new Set(['optimization', 'maintenance', 'repair-action', 'security-tradeoff', 'security-hardening', 'privacy-control', 'cosmetic', 'diagnostic', 'guidance']);

const problems = {
  unclassified: [],
  invalidGroup: [],
  invalidKind: [],
};

for (const id of catalogIds) {
  const entry = taxonomy.get(id);
  if (!entry) { problems.unclassified.push(id); continue; }
  if (!VALID_GROUPS.has(entry.group)) problems.invalidGroup.push(`${id}:${entry.group}`);
  if (!VALID_KINDS.has(entry.kind)) problems.invalidKind.push(`${id}:${entry.kind}`);
}

// --- semantic assertions ---
const mustBeIn = (id, group, kind) => {
  const entry = taxonomy.get(id);
  if (!entry) return `${id}: not classified`;
  if (entry.group !== group) return `${id}: expected group '${group}', got '${entry.group}'`;
  if (kind && entry.kind !== kind) return `${id}: expected kind '${kind}', got '${entry.kind}'`;
  return null;
};
const mustNotBeIn = (id, group) => {
  const entry = taxonomy.get(id);
  if (!entry) return null;
  return entry.group === group ? `${id}: must not be in '${group}'` : null;
};

const semantics = [
  mustBeIn('memory-compression', 'diagnostics', 'diagnostic'),
  mustBeIn('disable-superfetch', 'experimental'),
  mustBeIn('timer-resolution-0-5ms', 'experimental'),
  mustBeIn('disable-network-throttling', 'experimental'),
  mustBeIn('dns-optimization', 'experimental'),
  mustBeIn('static-pagefile', 'experimental'),
  mustBeIn('disable-svchost-split-threshold', 'experimental'),
  mustBeIn('disable-modern-standby', 'experimental'),
  mustBeIn('disable-memory-integrity', 'security', 'security-tradeoff'),
  mustBeIn('winsock-reset', 'repair', 'repair-action'),
  mustBeIn('flush-dns', 'repair', 'repair-action'),
  mustBeIn('reset-network', 'repair', 'repair-action'),
  mustBeIn('flush-arp-cache', 'repair', 'repair-action'),
  mustBeIn('clear-temp-files', 'maintenance', 'maintenance'),
  mustBeIn('registry-cleanup', 'maintenance', 'maintenance'),
  mustBeIn('disable-bits', 'maintenance', 'maintenance'),
  mustBeIn('disable-delivery-optimization', 'maintenance', 'maintenance'),
  mustBeIn('disable-automatic-maintenance', 'maintenance', 'maintenance'),
  mustBeIn('disable-core-parking', 'performance'),
  mustBeIn('disable-fullscreen-optimizations', 'repair'),
  mustBeIn('disable-multiplane-overlay', 'repair'),
  mustBeIn('disable-smb1', 'security', 'security-hardening'),
  mustBeIn('disable-admin-shares', 'security'),
  mustBeIn('disable-remote-desktop', 'security'),
  mustNotBeIn('memory-compression', 'gaming'),
  mustNotBeIn('disable-core-parking', 'experimental'),
  mustNotBeIn('static-pagefile', 'performance'),
  mustNotBeIn('winsock-reset', 'performance'),
].filter(Boolean);

// --- no repair/maintenance items inside performance or gaming groups ---
const misplaced = [...taxonomy.entries()]
  .filter(([id, e]) => catalogIds.includes(id))
  .filter(([id, e]) => (e.group === 'performance' || e.group === 'gaming') && (e.kind === 'repair-action' || e.kind === 'maintenance'))
  .map(([id, e]) => `${id} (${e.group}/${e.kind})`);

// --- profiles: no security trade-offs ---
const profileIdSection = profilesSrc;
const forbiddenInProfiles = ['disable-memory-integrity'];
const profileViolations = forbiddenInProfiles.filter((id) => {
  const pattern = new RegExp(`optimizationIds:\\s*\\[[^\\]]*'${id}'`, 's');
  return pattern.test(profileIdSection);
});

// --- evidence module sanity ---
const evidenceSrc = read('src/lib/catalog/evidence.ts');
const evidenceChecks = {
  hasImpactTypes: /'diagnostic-only'/.test(evidenceSrc),
  hasConfidence: /confidenceScore/.test(evidenceSrc),
  hasSources: /sourceScore/.test(evidenceSrc),
  scoringPresent: /scoreOptimization/.test(evidenceSrc),
};

const failed = {
  ...problems,
  semantics,
  misplaced,
  profileViolations,
  evidenceChecks: Object.entries(evidenceChecks).filter(([, ok]) => !ok).map(([k]) => k),
};

const totalFailures = problems.unclassified.length + problems.invalidGroup.length + problems.invalidKind.length +
  semantics.length + misplaced.length + profileViolations.length + failed.evidenceChecks.length;

if (totalFailures > 0) {
  console.error(JSON.stringify(failed, null, 2));
  process.exit(1);
}

console.log(`Catalog taxonomy OK: ${taxonomy.size} entries classified, ${catalogIds.length} catalog ids covered.`);
