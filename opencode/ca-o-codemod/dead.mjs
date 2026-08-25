import fs from 'node:fs';
import path from 'node:path';

const SRC = 'src';
const files = [];
function walk(d) {
  for (const f of fs.readdirSync(d)) {
    const p = path.join(d, f);
    const st = fs.statSync(p);
    if (st.isDirectory()) walk(p);
    else if (/\.(ts|tsx)$/.test(f)) files.push(p.replace(/\\/g, '/'));
  }
}
walk(SRC);

const entries = new Set(
  files.filter(f => f.endsWith('/page.tsx') || f.endsWith('/layout.tsx') || f.endsWith('/route.ts') || f.includes('/api/'))
);
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

const alive = new Set(entries);
let changed = true;
while (changed) {
  changed = false;
  for (const f of files) {
    if (alive.has(f)) continue;
    for (const u of importedBy.get(f)) {
      if (alive.has(u)) { alive.add(f); changed = true; break; }
    }
  }
}

const dead = files.filter(f => !alive.has(f));
console.log('=== MUERTOS (transitivo):', dead.length, '===');
dead.forEach(d => console.log(' ', d));
fs.writeFileSync('C:/Users/berna/AppData/Local/Temp/opencode/ca-o-codemod/dead-files.json', JSON.stringify(dead, null, 2));
