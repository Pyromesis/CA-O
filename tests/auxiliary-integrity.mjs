import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const files = [
  'src/components/ca-o/AdvancedExport.tsx',
  'src/components/ca-o/CloudSyncUI.tsx',
  'src/components/ca-o/UserProfile.tsx',
];
const forbidden = [/simulat/i, /Math\.random\(\)/, /success\s*=\s*Math\.random/i];
const findings = [];

for (const relativePath of files) {
  const source = fs.readFileSync(path.join(root, relativePath), 'utf8');
  for (const pattern of forbidden) {
    if (pattern.test(source)) findings.push({ file: relativePath, pattern: pattern.toString() });
  }
}

if (findings.length) {
  console.error(JSON.stringify(findings, null, 2));
  process.exit(1);
}

console.log(`Auxiliary integrity OK: ${files.length} audited source files.`);
