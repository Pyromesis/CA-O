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

// entrypoints que nadie importa
const entries = new Set(files.filter(f =>
  /\.(page|layout|route)\.tsx?$/.test(f) || f.endsWith('globals.css') || /app\/[^/]+\.tsx$/.test(f)
));

// construir grafo de imports
const importedBy = new Map();
for (const f of files) importedBy.set(f, new Set());
const importRe = /from\s+['"](@\/[^'"]+|\.\.?\/[^'"]+)['"]/g;

for (const f of files) {
  const s = fs.readFileSync(f, 'utf8');
  for (const m of s.matchAll(importRe)) {
    let spec = m[1];
    let target = null;
    if (spec.startsWith('@/')) target = 'src/' + spec.slice(2);
    else {
      const base = path.dirname(f);
      target = path.normalize(path.join(base, spec)).replace(/\\/g, '/');
    }
    // resolver extensión
    const candidates = [target, target + '.ts', target + '.tsx', target + '/index.ts', target + '/index.tsx'];
    for (const c of candidates) {
      if (files.includes(c)) { importedBy.get(c).add(f); break; }
    }
  }
}

// dinámicos: page.tsx importa componentes; rutas API importan lib
const unused = [];
for (const f of files) {
  if (entries.has(f)) continue;
  if (f.includes('/api/')) continue;
  const users = importedBy.get(f);
  if (!users || users.size === 0) unused.push(f);
}
console.log('=== ARCHIVOS SIN IMPORTADORES ===');
unused.forEach(u => console.log(' ', u));

// dependencias npm sin referencias
const pkg = JSON.parse(fs.readFileSync('package.json', 'utf8'));
const allSrc = files.map(f => fs.readFileSync(f, 'utf8')).join('\n') +
  ['electron-main.js', 'build-electron.js', 'next.config.ts', 'postcss.config.mjs', 'tailwind.config.ts'].filter(fs.existsSync).map(f => fs.readFileSync(f, 'utf8')).join('\n');
const unusedDeps = [];
for (const dep of Object.keys(pkg.dependencies)) {
  if (dep === 'sharp') continue; // usado por next image runtime
  const re = new RegExp("['\"]" + dep.replace(/[.*+?^${}()|[\]\\]/g, '\\$&') + "(/|['\"])",);
  if (!re.test(allSrc)) unusedDeps.push(dep);
}
console.log('=== DEPENDENCIAS SIN REFERENCIAS ===');
unusedDeps.forEach(d => console.log(' ', d));
