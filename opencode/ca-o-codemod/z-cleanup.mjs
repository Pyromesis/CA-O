import fs from 'node:fs';

// ── 1) types/optimizations.ts compacto (solo lo usado) ──
const TYPES = `// Tipos y metadatos de categorías usados por el panel de optimización.

export type OptimizationCategory = 'system' | 'network' | 'input' | 'visual' | 'advanced' | 'privacy';

export type RiskLevel = 'safe' | 'warning' | 'dangerous';

export type PerformanceImpact = 'low' | 'medium' | 'high' | 'very-high';

export type SecurityImpact = 'none' | 'low' | 'medium' | 'high' | 'reduces-security';

export type AntiCheatRisk = 'none' | 'possible-compatibility';

export interface OptimizationItem {
  id: string;
  category: OptimizationCategory;
  nameEs: string;
  nameEn: string;
  descriptionEs: string;
  descriptionEn: string;
  whatDoesEs?: string;
  whatDoesEn?: string;
  whatIsItEs: string;
  whatIsItEn: string;
  whatItAppliesEs: string;
  whatItAppliesEn: string;
  isSafe: boolean;
  riskLevel: RiskLevel;
  securityImpact: SecurityImpact;
  antiCheatRisk?: AntiCheatRisk;
  antiCheatWarningEs?: string;
  antiCheatWarningEn?: string;
  performanceImpact: PerformanceImpact;
  registryKeys?: RegistryKey[];
  commands?: string[];
  services?: ServiceAction[];
  requiresReboot?: boolean;
  reversible?: boolean;
  requiresExplicitConfirmation?: boolean;
  warningEs?: string;
  warningEn?: string;
  implementationEs?: string;
  implementationEn?: string;
  verificationCommand?: string;
  revertVerificationCommand?: string;
  privacyBenefitEs?: string;
  privacyBenefitEn?: string;
  securityExplanationEs?: string;
  securityExplanationEn?: string;
  performanceExplanationEs?: string;
  performanceExplanationEn?: string;
  limitationsEs?: string;
  limitationsEn?: string;
}

export interface RegistryKey {
  path: string;
  name: string;
  value: number | string | boolean;
  type: 'DWORD' | 'QWORD' | 'String' | 'ExpandString' | 'MultiString' | 'Binary';
}

export interface ServiceAction {
  name: string;
  action: 'stop' | 'disable' | 'stop-and-disable';
}

export interface CategoryInfo {
  id: OptimizationCategory;
  nameEs: string;
  nameEn: string;
  descriptionEs: string;
  descriptionEn: string;
  icon: string;
  color: string;
  itemCount: number;
}

export const CATEGORIES: CategoryInfo[] = [
  {
    id: 'system',
    nameEs: 'Sistema',
    nameEn: 'System',
    descriptionEs: 'Optimizaciones del sistema operativo Windows para mejorar rendimiento general.',
    descriptionEn: 'Windows operating system optimizations for improved overall performance.',
    icon: '⚙️',
    color: '#3B82F6',
    itemCount: 0,
  },
  {
    id: 'network',
    nameEs: 'Red',
    nameEn: 'Network',
    descriptionEs: 'Optimizaciones de red para reducir latencia y mejorar conectividad.',
    descriptionEn: 'Network optimizations to reduce latency and improve connectivity.',
    icon: '🌐',
    color: '#10B981',
    itemCount: 0,
  },
  {
    id: 'input',
    nameEs: 'Entrada',
    nameEn: 'Input',
    descriptionEs: 'Optimizaciones de ratón, teclado y touchpad para mejor respuesta.',
    descriptionEn: 'Mouse, keyboard and touchpad optimizations for better response.',
    icon: '🖱️',
    color: '#8B5CF6',
    itemCount: 0,
  },
  {
    id: 'visual',
    nameEs: 'Visual',
    nameEn: 'Visual',
    descriptionEs: 'Ajustes visuales y de interfaz para aligerar la composición.',
    descriptionEn: 'Visual and interface tweaks to lighten composition.',
    icon: '✨',
    color: '#F59E0B',
    itemCount: 0,
  },
  {
    id: 'advanced',
    nameEs: 'Avanzado',
    nameEn: 'Advanced',
    descriptionEs: 'Optimizaciones avanzadas de energía, memoria y GPU.',
    descriptionEn: 'Advanced power, memory and GPU optimizations.',
    icon: '🚀',
    color: '#EF4444',
    itemCount: 0,
  },
  {
    id: 'privacy',
    nameEs: 'Privacidad',
    nameEn: 'Privacy',
    descriptionEs: 'Controles de telemetría, permisos y recopilación de datos.',
    descriptionEn: 'Telemetry, permissions and data-collection controls.',
    icon: '🔒',
    color: '#14B8A6',
    itemCount: 0,
  },
];
`;
fs.writeFileSync('src/types/optimizations.ts', TYPES);
console.log('types/optimizations.ts compactado');

// ── 2) borrar archivos muertos ──
const dead = JSON.parse(fs.readFileSync('C:/Users/berna/AppData/Local/Temp/opencode/ca-o-codemod/dead-files.json', 'utf8'));
dead.push('src/components/ca-o/OptimizationView.tsx');
let n = 0;
for (const f of dead) {
  if (fs.existsSync(f)) { fs.rmSync(f); n++; }
}
console.log('archivos eliminados:', n, 'de', dead.length);

// ── 3) tests: actualizar lista de auxiliary-integrity ──
{
  const p = 'tests/auxiliary-integrity.mjs';
  let s = fs.readFileSync(p, 'utf8');
  for (const f of ['AdvancedEffects', 'CommandPalette', 'QuickActions', 'SearchFilter', 'Tooltip', 'KeyboardShortcuts', 'MonitoringCharts', 'AchievementSystem', 'ActivityTimeline', 'NotificationCenter', 'NotificationSystem', 'OptimizationHistory', 'Scheduler', 'SettingsExportImport', 'UndoQueue']) {
    s = s.replace(new RegExp("\\s*'src/components/ca-o/" + f + "\\.tsx',"), '');
  }
  s = s.replace("'src/components/ca-o/CompactWidget.tsx',\n  'src/components/ca-o/PerformanceMonitor.tsx',", "'src/components/ca-o/PerformanceMonitor.tsx',");
  s = s.replace("'src/components/ca-o/PerformanceMonitor.tsx',\n  'src/components/ca-o/StatisticsDashboard.tsx',", "'src/components/ca-o/StatisticsDashboard.tsx',");
  fs.writeFileSync(p, s);
  const kept = (s.match(/src\/components/g) || []).length;
  console.log('auxiliary-integrity archivos auditados:', kept);
}

// ── 4) artefactos raíz ──
for (const d of ['prisma', 'db']) {
  if (fs.existsSync(d)) { fs.rmSync(d, { recursive: true, force: true }); console.log('carpeta eliminada:', d); }
}
for (const f of ['Caddyfile', 'tailwind.config.ts']) {
  if (fs.existsSync(f)) { fs.rmSync(f); console.log('archivo eliminado:', f); }
}

// ── 5) package.json: scripts prisma + deps sin uso ──
{
  const p = 'package.json';
  const pkg = JSON.parse(fs.readFileSync(p, 'utf8'));
  for (const sc of ['db:push', 'db:generate', 'db:migrate', 'db:reset']) delete pkg.scripts[sc];
  const unusedDeps = ['@dnd-kit/core', '@dnd-kit/sortable', '@dnd-kit/utilities', '@hookform/resolvers', '@mdxeditor/editor', '@reactuses/core', '@tanstack/react-query', '@tanstack/react-table', 'date-fns', 'next-auth', 'next-intl', 'react-markdown', 'react-syntax-highlighter', 'uuid', 'z-ai-web-dev-sdk', 'zod'];
  for (const d of unusedDeps) delete pkg.dependencies[d];
  fs.writeFileSync(p, JSON.stringify(pkg, null, 2) + '\n');
  console.log('package.json podado');
}
