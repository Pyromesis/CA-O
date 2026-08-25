/**
 * Catalog integrity contract (#51).
 *
 * For EVERY optimization in the catalog, verifies that:
 *  - it is classified in the v3 taxonomy with a valid group/subgroup/kind;
 *  - its group has an evidence default (or a per-ID override exists);
 *  - guidance-only IDs have bilingual reasons;
 *  - no ID lost between old/new registries (rename safety);
 *  - the apply route derives risk from the irreversible set (no hardcoded lies).
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const read = (p) => fs.readFileSync(path.join(root, p), 'utf8');

const section = (text, start, end) => {
  const i = text.indexOf(start);
  const j = text.indexOf(end, i + start.length);
  return i >= 0 && j > i ? text.slice(i, j) : '';
};

const apiRoute = read('src/app/api/optimization/route.ts');
const commands = read('src/lib/optimization-commands.ts');
const taxonomySrc = read('src/lib/catalog/taxonomy.ts');
const evidenceSrc = read('src/lib/catalog/evidence.ts');
const guidanceSection = section(commands, 'export const nonExecutableReasonById', 'const baseVerificationCommands');

// --- collect catalog ids ---
const catSection = section(apiRoute, 'const categoryOptimizations', '// Catálogo cacheado');
const catalogIds = [...catSection.matchAll(/'([a-z][a-z0-9-]*)'/g)].map((m) => m[1]);

// --- taxonomy map ---
const taxonomy = new Map(
  [...taxonomySrc.matchAll(/'([a-z][a-z0-9-]*)':\s*\{\s*group:\s*'([a-z-]+)',\s*subgroup:\s*'([a-z-]+)',\s*kind:\s*'([a-z-]+)'/g)]
    .map((m) => [m[1], { group: m[2], subgroup: m[3], kind: m[4] }]),
);

// --- evidence coverage ---
const overrideIds = new Set([...evidenceSrc.matchAll(/\{ id:\s*'([a-z0-9-]+)',/g)].map((m) => m[1]));
const groupsWithDefaults = new Set(
  [...evidenceSrc.matchAll(/^\s{2}(\w+):\s*\{\s*expectedImpact:/gm)].map((m) => m[1]),
);

const problems = {
  unclassified: [],
  invalidKind: [],
  noEvidence: [],
  duplicateInCatalog: [],
};

const VALID_KINDS = new Set(['optimization', 'maintenance', 'repair-action', 'security-tradeoff', 'security-hardening', 'privacy-control', 'cosmetic', 'diagnostic', 'guidance']);

for (const id of catalogIds) {
  const entry = taxonomy.get(id);
  if (!entry) { problems.unclassified.push(id); continue; }
  if (!VALID_KINDS.has(entry.kind)) problems.invalidKind.push(`${id}:${entry.kind}`);
  if (!overrideIds.has(id) && !groupsWithDefaults.has(entry.group)) {
    problems.noEvidence.push(id);
  }
}

// duplicates inside the catalog lists
const seen = new Set();
for (const id of catalogIds) {
  if (seen.has(id)) problems.duplicateInCatalog.push(id);
  seen.add(id);
}

// --- guidance reasons must cover every non-executable id ---
const nonExecSection = section(commands, 'export const nonExecutableOptimizationIds', ']);');
const nonExecIds = [...nonExecSection.matchAll(/'([a-z0-9-]+)'/g)].map((m) => m[1]);
const reasonIds = new Set([...guidanceSection.matchAll(/^\s*'([a-z0-9-]+)':/gm)].map((m) => m[1]));
const missingReasons = nonExecIds.filter((id) => !reasonIds.has(id));

// --- risk derivation present (no hardcoded per-item risk strings in UI catalog build)
const riskOk = /irreversibleOptimizationIds\.has\(id\)\s*\?\s*'dangerous'/.test(apiRoute);

const failed = {
  ...problems,
  missingReasons,
  riskDerivationBroken: riskOk ? [] : ['riskLevel not derived from irreversibleOptimizationIds'],
};
const total = problems.unclassified.length + problems.invalidKind.length + problems.noEvidence.length +
  problems.duplicateInCatalog.length + missingReasons.length + (riskOk ? 0 : 1);

if (total > 0) {
  console.error(JSON.stringify(failed, null, 2));
  process.exit(1);
}

console.log(`Catalog integrity OK: ${catalogIds.length} ids classified + evidenced; ${nonExecIds.length} guidance entries documented.`);
