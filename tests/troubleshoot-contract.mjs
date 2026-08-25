import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const source = fs.readFileSync(path.join(root, 'src/app/api/troubleshoot/execute/route.ts'), 'utf8');
const requiredActions = [
  'restore-audio',
  'restore-bluetooth',
  'restore-network',
  'restore-windows-update',
  'restore-display',
  'create-restore-point',
  'restore-all',
];

const missingActions = requiredActions.filter((action) => !source.includes(`"${action}":`));
const repairMarkers = (source.match(/fixesIssue:\s*true/g) || []).length;
const textInference = /issuesFixed\+\+[\s\S]{0,300}includes\(['"](restarted|reset|cleared|Cleaned)/.test(source);

if (missingActions.length || repairMarkers === 0 || textInference) {
  console.error(JSON.stringify({ missingActions, repairMarkers, textInference }, null, 2));
  process.exit(1);
}

console.log(`Troubleshoot contract OK: ${requiredActions.length} actions and ${repairMarkers} explicit repair markers.`);
