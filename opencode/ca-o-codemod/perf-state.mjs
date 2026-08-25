import fs from 'node:fs';
const p = 'src/app/api/optimization/state/route.ts';
let s = fs.readFileSync(p, 'utf8');
s = s.replace(
  "import { runPowerShell } from \"@/lib/powershell-runner\";",
  'import { verifyWithCache } from "@/lib/verify-cache";'
);
s = s.replace(
  `        const verificationCommand = verificationCommands[row.id];
        const verification = verificationCommand
          ? await runPowerShell(verificationCommand)
          : { success: true };
        stateMap[row.id] = verification.success;`,
  `        stateMap[row.id] = await verifyWithCache(row.id, verificationCommands[row.id]);`
);
fs.writeFileSync(p, s);
console.log('state route usa cache:', s.includes('verifyWithCache'));
