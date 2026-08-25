import fs from 'node:fs';
const p = 'src/app/api/troubleshoot/execute/route.ts';
let s = fs.readFileSync(p, 'utf8');

// create-restore-point: sin límite de 24h + tolerante a fallo de Enable
const oldScript = /script: "Enable-ComputerRestore -Drive 'C:\\\\' -ErrorAction Stop; Checkpoint-Computer[^"]*"/;
const newScript = 'script: "$srKey = \'HKLM:\\\\SOFTWARE\\\\Microsoft\\\\Windows NT\\\\CurrentVersion\\\\SystemRestore\'; if (-not (Test-Path $srKey)) { New-Item -Path $srKey -Force | Out-Null }; Set-ItemProperty -Path $srKey -Name SystemRestorePointCreationFrequency -Value 0 -Type DWord -Force; try { Enable-ComputerRestore -Drive \'C:\\\\\' -ErrorAction SilentlyContinue } catch { }; Checkpoint-Computer -Description \'CA-O Safety Point\' -RestorePointType MODIFY_SETTINGS -ErrorAction Stop; Write-Output \'Restore point created\'"';
if (oldScript.test(s)) {
  s = s.replace(oldScript, newScript);
  console.log('create-restore-point script patched');
} else {
  console.log('create-restore-point pattern not found — checking...');
  const i = s.indexOf('Creating restore point');
  console.log(s.slice(i - 120, i + 260));
}
fs.writeFileSync(p, s);
