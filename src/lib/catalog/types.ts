/**
 * CA-O evidence-based catalog type system (v2).
 *
 * Replaces subjective impact scales ("very-high" FPS folklore) with an
 * honest model: expected impact, confidence in that claim, where the
 * evidence comes from, applicability conditions and adverse effects.
 */

export type PerformanceImpact =
  | 'none'
  | 'small'
  | 'medium'
  | 'workload-dependent'
  | 'diagnostic-only';

export type Confidence = 'high' | 'medium' | 'low' | 'unknown';

export type EvidenceSource =
  | 'microsoft'
  | 'vendor'
  | 'benchmark'
  | 'empirical'
  | 'heuristic';

/** Top-level groups. Maintenance/repair/diagnostics/cosmetics are NEVER sold as performance. */
export type CatalogGroup =
  | 'performance'
  | 'gaming'
  | 'diagnostics'
  | 'security'
  | 'privacy'
  | 'maintenance'
  | 'repair'
  | 'tweaks'
  | 'experimental';

export type CatalogSubgroup =
  // performance
  | 'cpu' | 'gpu' | 'memory' | 'storage' | 'network' | 'input'
  // gaming
  | 'windows-gaming' | 'gpu-gaming' | 'input-gaming' | 'display-gaming' | 'per-game'
  // diagnostics
  | 'diag-cpu' | 'diag-gpu' | 'memory' | 'diag-network' | 'dpc-isr' | 'drivers' | 'thermals' | 'diag-storage'
  // security
  | 'hvci-vbs' | 'secure-boot-tpm' | 'firewall' | 'smb' | 'rdp-remote' | 'driver-security' | 'network-hardening' | 'attack-surface'
  // privacy
  | 'telemetry' | 'advertising' | 'location' | 'personalization' | 'permissions' | 'ai-features'
  // maintenance
  | 'cleanup' | 'startup' | 'services' | 'windows-features'
  // repair
  | 'network-repair' | 'system-repair' | 'update-repair' | 'troubleshooting'
  // tweaks
  | 'cosmetic' | 'explorer-ui' | 'taskbar' | 'sounds' | 'quality-of-life'
  // experimental
  | 'legacy' | 'contested' | 'advanced-power' | 'hardware-experimental';

/** What kind of action this is, independent of where it sits in the tree. */
export type ActionKind =
  | 'optimization'
  | 'maintenance'
  | 'repair-action'
  | 'security-tradeoff'
  | 'security-hardening'
  | 'privacy-control'
  | 'cosmetic'
  | 'diagnostic'
  | 'guidance';

export type CompatibilityStatus =
  | 'compatible'
  | 'no-known-conflict'
  | 'potential-conflict'
  | 'required-security-feature'
  | 'incompatible'
  | 'unknown';

/** Context predicates evaluated against a live SystemContext snapshot. */
export interface ApplicabilityCondition {
  formFactor?: Array<'desktop' | 'laptop'>;
  powerSource?: Array<'ac' | 'battery'>;
  minRamGB?: number;
  maxRamGB?: number;
  touchscreen?: boolean;
  secureBoot?: boolean;
  vbsEnabled?: boolean;
  hvciEnabled?: boolean;
  hypervisorPresent?: boolean;
}

export interface AdverseEffect {
  es: string;
  en: string;
}

/** Evidence record attached to every optimization. */
export interface EvidenceRecord {
  expectedImpact: PerformanceImpact;
  confidence: Confidence;
  sources: EvidenceSource[];
  /** Human explanation of WHY this effect is expected (bilingual). */
  rationaleEs: string;
  rationaleEn: string;
  adverseEffects: AdverseEffect[];
}

/** Per-ID classification entry. Every executable ID must have exactly one. */
export interface TaxonomyEntry {
  group: CatalogGroup;
  subgroup: CatalogSubgroup;
  kind: ActionKind;
  /** True when Windows changes the value back or it expires by itself. */
  nonPersistent?: boolean;
  /** Session-scoped action: not recorded as a persistent state change. */
  sessionAction?: boolean;
  /** Repair/maintenance action: repeatable by design. */
  repeatableAction?: boolean;
}

export interface ApplicabilityRule {
  /** Conditions that MUST hold for the item to be applicable at all. */
  requires?: ApplicabilityCondition;
  /** Extra confirmation flags required from the client beyond confirmDangerous. */
  requiresSecurityConfirmation?: boolean;
  requiresExperimentalAcknowledgement?: boolean;
  /** Anti-cheat sensitivity tags evaluated against detected anti-cheats. */
  antiCheatSensitivity?: string[];
  /** IDs that should not be combined with this one. */
  conflictsWith?: string[];
  /** Free-form prerequisites shown before Apply is enabled. */
  prerequisiteNoteEs?: string;
  prerequisiteNoteEn?: string;
}

export interface OptimizationScoreBreakdown {
  evidence: number;      // 0..25
  confidence: number;    // 0..25
  expectedBenefit: number; // 0..20 (none=0 small=8 medium=14 workload=10 diag=4)
  safety: number;        // 0..20 (hardening keeps full, tradeoff loses most)
  compatibility: number; // 0..10
  total: number;         // 0..100
}

export interface ScoredOptimization extends EvidenceRecord, ApplicabilityRule {
  score: OptimizationScoreBreakdown;
}

/** Structured environment snapshot stored with every applied change. */
export interface SnapshotMeta {
  capturedAt: string;
  windowsBuild: string;
  windowsEdition: string;
  hardwareFingerprint: string;
  elevated: boolean;
}

export type GpuVendor = 'nvidia' | 'amd' | 'intel' | 'other';

export interface GpuInfo {
  name: string;
  vendor: GpuVendor;
  driverVersion: string;
  vramMB: number;
}

/** Vendor feature notes (#17 NVIDIA / #18 AMD). Detection is best-effort. */
export interface GpuCapabilities {
  vendor: GpuVendor;
  /** Driver-level feature availability based on vendor + driver generation. */
  reflexOrAntiLag: string;      // human note
  variableRateSync: string;     // G-SYNC / FreeSync note
  hagsSupported: boolean | 'unknown';
  frameGenerationNote: string;
}

export interface SystemContext {
  collectedAt: string;
  os: {
    caption: string;
    build: string;
    displayVersion: string;
    edition: string;
    majorVersion: number;
  };
  hardware: {
    cpuName: string;
    cpuVendor: 'intel' | 'amd' | 'other';
    cores: number;
    threads: number;
    ramGB: number;
    gpuNames: string[];
    gpus: GpuInfo[];
    primaryGpuCapabilities: GpuCapabilities | null;
    formFactor: 'desktop' | 'laptop' | 'unknown';
    touchscreen: boolean;
  };
  power: {
    powerSource: 'ac' | 'battery' | 'unknown';
    batteryPresent: boolean;
    activeScheme: string;
  };
  security: {
    elevatedSession: boolean;
    secureBoot: boolean | 'unknown';
    tpmPresent: boolean | 'unknown';
    tpmVersionMajor: number | null;
    vbsEnabled: boolean | 'unknown';
    hvciEnabled: boolean | 'unknown';
    hypervisorPresent: boolean | 'unknown';
    vulnerableDriverBlocklist: boolean | 'unknown';
  };
  antiCheats: DetectedAntiCheat[];
}

export interface DetectedAntiCheat {
  id: string;
  name: string;
  status: CompatibilityStatus;
}
