import fs from 'node:fs';
const p = 'src/components/ca-o/ProfileSelector.tsx';
let s = fs.readFileSync(p, 'utf8');
if (!s.includes('const isExecutableId')) {
  s = s.replace(
    '  const humanizeId = (id: string)',
    '  const isExecutableId = (id: string) => isExecutableOptimizationId(id);\n  const humanizeId = (id: string)'
  );
}
s = s.replace(
  "import { getRiskLevel } from '@/lib/optimization-commands';",
  "import { getRiskLevel, isExecutableOptimizationId } from '@/lib/optimization-commands';"
);
fs.writeFileSync(p, s);
console.log('helpers ok:', s.includes('isExecutableId'), s.includes('isExecutableOptimizationId'));
