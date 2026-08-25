import fs from 'node:fs';
import path from 'node:path';
const SRC = 'src';
const files = [];
function walk(d) { for (const f of fs.readdirSync(d)) { const p = path.join(d, f); const st = fs.statSync(p); if (st.isDirectory()) walk(p); else if (/\.(ts|tsx)$/.test(f)) files.push(p.replace(/\\/g, '/')); } }
walk(SRC);
console.log('button.tsx in files:', files.includes('src/components/ui/button.tsx'));
const panel = 'src/components/ca-o/FullOptimizationPanel.tsx';
const s = fs.readFileSync(panel, 'utf8');
const m = s.match(/from\s+['"](@\/components\/ui\/button)['"]/);
console.log('panel importa ui/button:', !!m);
// reconstruir resolución para ese import
const target = 'src/components/ui/button';
const cands = [target, target + '.ts', target + '.tsx'];
console.log('candidates exist:', cands.map(c => files.includes(c)));
