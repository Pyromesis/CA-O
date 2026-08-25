import fs from 'node:fs';
const p = 'src/lib/powershell-runner.ts';
let s = fs.readFileSync(p, 'utf8');
s = s.replace(/import \{[^}]*\} from 'child_process';/, "import { spawn } from 'child_process';");
fs.writeFileSync(p, s);
console.log('import line now:', s.split('\n')[0]);
