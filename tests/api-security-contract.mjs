import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const read = (relativePath) => fs.readFileSync(path.join(root, relativePath), 'utf8');
const apply = read('src/app/api/optimization/apply/route.ts');
const applyAll = read('src/app/api/optimization/apply-all/route.ts');
const revert = read('src/app/api/optimization/revert/route.ts');
const revertAll = read('src/app/api/optimization/revert-all/route.ts');
const registry = read('src/app/api/registry/route.ts');

const checks = {
  applyBodyValidation: apply.includes('Request body must be an object') && apply.includes('createBackup must be a boolean'),
  revertBodyValidation: revert.includes('Request body must be an object'),
  applyAllowlist: apply.includes('isExecutableOptimizationId(id)'),
  revertAllowlist: revert.includes('isExecutableOptimizationId(optimizationId)'),
  bulkLimit: applyAll.includes('requestBody.ids.length > 100') && revertAll.includes('requestBody.ids.length > 100'),
  irreversibleWarning: apply.includes('isExecutableOptimizationId(id)') && applyAll.includes('isExecutableOptimizationId(id)') && read('src/lib/optimization-commands.ts').includes("return 'dangerous'") && read('src/components/ca-o/FullOptimizationPanel.tsx').includes('getRiskLevel(id)'),
  registryNotSimulated: registry.includes('status: 501') && registry.includes('previous implementation was simulated'),
};

const failed = Object.entries(checks).filter(([, passed]) => !passed).map(([name]) => name);
if (failed.length) {
  console.error(JSON.stringify({ failed }, null, 2));
  process.exit(1);
}

console.log(`API security contract OK: ${Object.keys(checks).length} checks.`);
