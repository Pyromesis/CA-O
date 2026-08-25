import fs from 'node:fs';
import path from 'node:path';
const SRC = 'src';
const files = [];
function walk(d) { for (const f of fs.readdirSync(d)) { const p = path.join(d, f); const st = fs.statSync(p); if (st.isDirectory()) walk(p); else if (/\.(ts|tsx)$/.test(f)) files.push(p.replace(/\\/g, '/')); } }
walk(SRC);
const importedBy = new Map(files.map(f => [f, new Set()]));
const importRe = /from\s+['"](@\/[^'"]+|\.\.?\/[^'"]+)['"]/g;
for (const f of files) {
  const s = fs.readFileSync(f, 'utf8');
  for (const m of s.matchAll(importRe)) {
    const spec = m[1];
    let target = spec.startsWith('@/') ? 'src/' + spec.slice(2) : path.normalize(path.join(path.dirname(f), spec)).replace(/\\/g, '/');
    for (const c of [target, target + '.ts', target + '.tsx', target + '/index.ts', target + '/index.tsx']) {
      if (files.includes(c)) { importedBy.get(c).add(f); break; }
    }
  }
}
const btn = importedBy.get('src/components/ui/button.tsx');
console.log('importers de ui/button:', [...btn]);
