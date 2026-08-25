/**
 * Known-issues engine (#39): problematic kernel drivers commonly shipped
 * with RGB/monitoring/overclocking tools, plus Windows build-specific notes.
 * Detection is read-only against Win32_SystemDriver.
 */

import { runPowerShell } from '../powershell-runner';

export interface KnownIssueMatch {
  id: string;
  severity: 'info' | 'warning' | 'critical';
  matchedSignature: string;
  titleEs: string;
  titleEn: string;
  detailEs: string;
  detailEn: string;
}

interface KnownIssueDefinition {
  id: string;
  signatures: string[];
  severity: KnownIssueMatch['severity'];
  titleEs: string;
  titleEn: string;
  detailEs: string;
  detailEn: string;
}

export const KNOWN_ISSUES: KnownIssueDefinition[] = [
  {
    id: 'inpoutx64',
    signatures: ['inpoutx64'],
    severity: 'critical',
    titleEs: 'Driver inpoutx64.sys detectado',
    titleEn: 'inpoutx64.sys driver detected',
    detailEs: 'Driver antiguo de acceso directo a puertos incluido en herramientas de RGB/monitorización; ha causado pantallazos azules y Microsoft lo bloquea en builds recientes. Desinstala la herramienta que lo instala.',
    detailEn: 'Legacy direct port-access driver shipped with RGB/monitoring tools; has caused BSODs and is blocked by Microsoft on recent builds. Uninstall the tool that installs it.',
  },
  {
    id: 'rgb-kernel-drivers',
    signatures: ['gv3', 'AsUpIO', 'AsIO', 'eneio64', 'WinRing0', 'RyzenMasterSDK'],
    severity: 'warning',
    titleEs: 'Drivers kernel de RGB/monitorización detectados',
    titleEn: 'RGB/monitoring kernel drivers detected',
    detailEs: 'Varios drivers de suites RGB/overclocking ejecutan código vulnerable en kernel (privilegio SYSTEM). Actualiza o desinstala las suites que no uses.',
    detailEn: 'Several RGB/overclocking suite drivers run vulnerable code in kernel (SYSTEM privilege). Update or uninstall the suites you do not use.',
  },
];

const DETECT_SCRIPT = `
$ErrorActionPreference = 'SilentlyContinue'
@(Get-CimInstance Win32_SystemDriver | ForEach-Object { $_.Name }) -join ';'
`;

export async function detectKnownIssues(): Promise<KnownIssueMatch[]> {
  if (process.platform !== 'win32') return [];
  const raw = await runPowerShell(DETECT_SCRIPT, false, { timeoutMs: 20_000 });
  if (!raw.success || !raw.output) return [];
  const haystack = raw.output.toLowerCase();
  const matches: KnownIssueMatch[] = [];
  for (const issue of KNOWN_ISSUES) {
    for (const sig of issue.signatures) {
      if (haystack.includes(sig.toLowerCase())) {
        matches.push({
          id: issue.id,
          severity: issue.severity,
          matchedSignature: sig,
          titleEs: issue.titleEs,
          titleEn: issue.titleEn,
          detailEs: issue.detailEs,
          detailEn: issue.detailEn,
        });
        break;
      }
    }
  }
  return matches;
}
