import fs from 'node:fs';
const p = 'tests/auxiliary-integrity.mjs';
let s = fs.readFileSync(p, 'utf8');
for (const f of ['CompactWidget', 'PerformanceMonitor', 'StatisticsDashboard']) {
  const re = new RegExp("[ \\t]*'src/components/ca-o/" + f + "\\.tsx',\\r?\\n", '');
  s = s.replace(re, '');
}
fs.writeFileSync(p, s);
const kept = (s.match(/src\/components/g) || []).length;
console.log('auditados ahora:', kept);
console.log(s.slice(s.indexOf('const files'), s.indexOf('const forbidden')));
