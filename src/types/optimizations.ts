// Tipos y metadatos de categorías usados por el panel de optimización.

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
