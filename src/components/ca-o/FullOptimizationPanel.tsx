'use client';

import { useState, useEffect, useCallback, useMemo, useRef, memo } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { useAppStore } from '@/store/useAppStore';
import { t } from '@/lib/i18n';
import { antiCheatWarnings, getRiskLevel, getRiskReason, isExecutableOptimizationId, nonExecutableOptimizationIds, realCommands, verificationCommands, revertVerificationCommands, irreversibleOptimizationIds, repeatableOptimizationIds, securityImpactById, privacyBenefitById, performanceImpactById } from '@/lib/optimization-commands';
import { optimizationTexts } from '@/lib/optimization-descriptions';
import { getOptimizationDetail } from '@/lib/optimization-details';
import {
  OptimizationItem,
  OptimizationCategory,
  CATEGORIES,
  RiskLevel,
  PerformanceImpact,
  SecurityImpact,
} from '@/types/optimizations';

const categoryForId = (id: string): OptimizationCategory => {
  if (['dns-optimization', 'disable-llmnr', 'disable-network-throttling', 'optimize-network-power', 'disable-netbios', 'disable-smb1', 'flush-arp-cache', 'disable-hotspot-service', 'require-network-level-auth', 'disable-wpad', 'disable-active-probing', 'disable-peer-name-resolution', 'restrict-point-and-print', 'disable-ssdp-discovery', 'disable-upnp-device-host', 'disable-snmp-trap'].includes(id)) return 'network';
  if (['mouse-acceleration', 'keyboard-rate', 'touchpad-latency', 'menu-delay', 'inactive-window-scroll', 'disable-sticky-keys', 'disable-usb-suspend', 'timer-resolution-0-5ms', 'enable-mouse-raw-input', 'disable-keyboard-filter', 'disable-filter-keys', 'disable-toggle-keys', 'disable-touch-keyboard-autoinvoke', 'disable-controller-gamebar-chord', 'disable-touchpad-edge-swipes', 'disable-touchpad-threefinger-slide', 'disable-windows-ink', 'numlock-on-boot', 'disable-hover-checkboxes', 'disable-tablet-input-service'].includes(id)) return 'input';
  if (['animations', 'transparency', 'shadows', 'taskbar-icons', 'notifications', 'show-file-extensions', 'disable-thumbnails', 'disable-tooltips', 'disable-wallpaper-slideshow', 'disable-system-sounds', 'show-hidden-files', 'hide-task-view', 'disable-taskbar-search', 'disable-lock-screen', 'disable-aero-peek', 'disable-startup-sound', 'disable-cast-notifications', 'disable-background-apps', 'disable-start-menu-suggestions', 'show-seconds-clock', 'hide-meet-now', 'delay-taskbar-thumbnails', 'hide-start-recommended', 'disable-drag-full-window', 'never-combine-taskbar-icons', 'disable-window-shake', 'disable-snap-layouts-flyout', 'disable-window-arrange-drag', 'disable-spotlight-wallpapers', 'hide-copilot-button'].includes(id)) return 'visual';
  if (['power-plan', 'gaming-mode', 'memory-compression', 'disable-hibernation', 'disable-fast-startup', 'enable-hags', 'disable-bits', 'disable-game-dvr', 'disable-power-throttling', 'enable-msi-gpu', 'disable-cpu-idle', 'enable-core-parking', 'disable-memory-dumps', 'enable-long-paths', 'disable-fullscreen-optimizations', 'optimize-thread-scheduling', 'disable-svchost-split-threshold', 'optimize-ntfs-memory-usage', 'disable-modern-standby', 'disable-edge-startup-boost', 'disable-automatic-maintenance', 'disable-app-readiness', 'disable-ssl-time-seeding', 'max-system-responsiveness', 'disable-memory-integrity', 'windowed-games-optimization', 'disable-multiplane-overlay', 'static-pagefile'].includes(id)) return 'advanced';
  if (['disable-advertising-id', 'disable-tailored-experiences', 'disable-activity-history', 'disable-location-tracking', 'disable-windows-feedback', 'remove-onedrive', 'disable-cloud-content', 'disable-app-suggestions', 'disable-start-tracking', 'disable-setting-sync', 'disable-input-personalization', 'disable-handwriting-data', 'disable-speech-recognition', 'disable-find-my-device', 'disable-contacts-access', 'disable-calendar-access', 'disable-camera-access', 'disable-microphone-access', 'disable-welcome-experience', 'disable-clipboard-history', 'disable-clipboard-cloud-sync', 'deny-user-account-information', 'deny-documents-library', 'deny-pictures-library', 'deny-videos-library', 'deny-email-access', 'deny-radios-access', 'deny-human-presence', 'deny-broad-filesystem', 'disable-click-to-do'].includes(id)) return 'privacy';
  return 'system';
};

const optimizationIconMap: Record<string, React.ElementType> = {
  'disable-telemetry': Shield,
  'disable-cortana': MicOff,
  'disable-search-indexing': SearchX,
  'disable-superfetch': ZapOff,
  'disable-print-spooler': Printer,
  'disable-xbox-gamebar': Gamepad2,
  'dns-optimization': Globe,
  'mouse-acceleration': MousePointer2,
  'keyboard-rate': Keyboard,
  'touchpad-latency': Touchpad,
  'animations': Sparkles,
  'transparency': Eye,
  'notifications': Bell,
  'power-plan': Battery,
  'gaming-mode': Gamepad2,
  'memory-compression': MemoryStick,
  'timer-resolution-0-5ms': Timer,
  'remove-onedrive': CloudOff,
  'disable-widgets': LayoutGrid,
  'disable-recall': EyeOff,
  'disable-netbios': Radio,
  'disable-smb1': Server,
  'show-seconds-clock': Clock,
  'hide-meet-now': Users,
  'disable-fullscreen-optimizations': Maximize2,
  'disable-welcome-experience': Rocket,
  'disable-ceip-tasks': ListChecks,
  'disable-last-access-time': FileText,
  'disable-8dot3-names': Type,
  'disable-admin-shares': FolderOpen,
  'disable-remote-assistance': HelpCircle,
  'disable-remote-desktop': MonitorSpeaker,
  'speedup-shutdown': Power,
  'no-auto-reboot-active': RefreshCw,
  'disable-driver-search': Download,
  'flush-arp-cache': Signal,
  'disable-hotspot-service': WifiOff,
  'require-network-level-auth': ShieldCheck,
  'disable-wpad': Satellite,
  'disable-active-probing': Activity,
  'disable-peer-name-resolution': Users,
  'restrict-point-and-print': Printer,
  'disable-ssdp-discovery': Radio,
  'disable-upnp-device-host': Package,
  'disable-snmp-trap': Bug,
  'disable-filter-keys': Keyboard,
  'disable-toggle-keys': ToggleLeft,
  'disable-touch-keyboard-autoinvoke': Smartphone,
  'disable-controller-gamebar-chord': Gamepad2,
  'disable-touchpad-edge-swipes': Move,
  'disable-touchpad-threefinger-slide': Layers,
  'disable-windows-ink': PenTool,
  'numlock-on-boot': Hash,
  'disable-hover-checkboxes': Square,
  'disable-tablet-input-service': Touchpad,
  'delay-taskbar-thumbnails': Timer,
  'hide-start-recommended': Star,
  'disable-drag-full-window': Move,
  'never-combine-taskbar-icons': LayoutList,
  'disable-window-shake': Wind,
  'disable-snap-layouts-flyout': Maximize2,
  'disable-window-arrange-drag': ArrowUpDown,
  'disable-spotlight-wallpapers': EyeOff,
  'hide-copilot-button': Sparkles,
  'optimize-thread-scheduling': TrendingUp,
  'disable-svchost-split-threshold': Box,
  'optimize-ntfs-memory-usage': HardDrive,
  'disable-modern-standby': Snowflake,
  'disable-edge-startup-boost': Globe,
  'disable-automatic-maintenance': Wrench,
  'disable-app-readiness': Archive,
  'disable-ssl-time-seeding': Clock,
  'max-system-responsiveness': Gauge,
  'disable-memory-integrity': ShieldAlert,
  'windowed-games-optimization': Grid3X3,
  'disable-multiplane-overlay': Layers,
  'static-pagefile': Database,
  'disable-clipboard-history': ClipboardList,
  'disable-clipboard-cloud-sync': CloudOff,
  'deny-user-account-information': Fingerprint,
  'deny-documents-library': FileText,
  'deny-pictures-library': ScanLine,
  'deny-videos-library': PlayIcon,
  'deny-email-access': ExternalLink,
  'deny-radios-access': Radio,
  'deny-human-presence': HeartPulse,
  'deny-broad-filesystem': Lock,
};

const EXECUTABLE_OPTIMIZATIONS: OptimizationItem[] = Object.keys(realCommands)
  .filter(isExecutableOptimizationId)
  .map((id) => {
    const name = id.replace(/-/g, ' ').replace(/\b\w/g, (letter) => letter.toUpperCase());
    const warning = antiCheatWarnings[id];
    const texts = optimizationTexts[id];
    const uiCategory = categoryForId(id);
    const detailCategory: 'system' | 'network' | 'input' | 'tweaks' | 'powerful' | 'privacy'
      = uiCategory === 'visual' ? 'tweaks' : uiCategory === 'advanced' ? 'powerful' : uiCategory;
    const securityImpact = securityImpactById[id] || 'none';
    const performanceImpact = performanceImpactById[id] || (irreversibleOptimizationIds.has(id) ? 'high' as const : 'medium' as const);
    const implementationSummary = realCommands[id].commands.map((command) => command.description).join('; ');
    // Specific ES/EN explanations shared with the API catalog.
    const detail = getOptimizationDetail(id, detailCategory, securityImpact, performanceImpact, implementationSummary);
    return {
      id,
      category: uiCategory,
      nameEs: name,
      nameEn: name,
      descriptionEs: texts?.descEs || `Aplica el ajuste real de Windows: ${name}.`,
      descriptionEn: texts?.descEn || `Applies the real Windows setting: ${name}.`,
      whatDoesEs: texts?.doesEs || detail.whatDoesEs,
      whatDoesEn: texts?.doesEn || detail.whatDoesEn,
      whatIsItEs: texts?.isItEs || detail.whatIsItEs,
      whatIsItEn: texts?.isItEn || detail.whatIsItEn,
      whatItAppliesEs: texts?.appliesEs || detail.whatItAppliesEs,
      whatItAppliesEn: texts?.appliesEn || detail.whatItAppliesEn,
      securityExplanationEs: detail.securityExplanationEs,
      securityExplanationEn: detail.securityExplanationEn,
      performanceExplanationEs: detail.performanceExplanationEs,
      performanceExplanationEn: detail.performanceExplanationEn,
      limitationsEs: detail.limitationsEs,
      limitationsEn: detail.limitationsEn,
      isSafe: getRiskLevel(id) === 'safe',
      riskLevel: getRiskLevel(id),
      securityImpact,
      performanceImpact,
      requiresReboot: realCommands[id].rebootRequired,
      reversible: !irreversibleOptimizationIds.has(id) && Boolean(revertVerificationCommands[id]),
      requiresExplicitConfirmation: irreversibleOptimizationIds.has(id),
      implementationEs: `Ejecuta PowerShell con privilegios de administrador: ${implementationSummary}. Después ejecuta una comprobación específica del registro, servicio o configuración modificada.`,
      implementationEn: `Runs PowerShell with administrator privileges: ${implementationSummary}. Then runs a targeted check of the modified registry, service or setting.`,
      commands: realCommands[id].commands.map((command) => command.script.trim()),
      verificationCommand: verificationCommands[id],
      revertVerificationCommand: revertVerificationCommands[id],
      ...(privacyBenefitById[id] && {
        privacyBenefitEs: privacyBenefitById[id].es,
        privacyBenefitEn: privacyBenefitById[id].en,
      }),
      warningEs: getRiskReason(id).es,
      warningEn: getRiskReason(id).en,
      ...(warning && {
        antiCheatRisk: 'possible-compatibility' as const,
        antiCheatWarningEs: warning.es,
        antiCheatWarningEn: warning.en,
      }),
    };
  });
import {
  Monitor,
  MicOff,
  SearchX,
  Printer,
  Wifi,
  MousePointer2,
  Palette,
  Zap,
  Search,
  CheckCircle2,
  XCircle,
  AlertTriangle,
  Shield,
  ChevronRight,
  RotateCcw,
  Play,
  Filter,
  ChevronDown,
  ChevronUp,
  Clock,
  Cpu,
  HardDrive,
  Globe,
  Terminal,
  Settings2,
  Lock,
  Unlock,
  AlertOctagon,
  Info,
  X,
  Check,
  Loader2,
  Download,
  CheckCheck as SelectAll,
  SquareCheck,
  Trash2,
  ArrowUpDown,
  ListFilter,
  Sparkles,
  TrendingUp,
  Activity,
  ZapOff,
  ShieldAlert,
  ShieldCheck,
  Eye,
  EyeOff,
  Keyboard,
  MousePointerClick,
  RefreshCw,
  FileCode,
  Server,
  KeyRound,
  Bug,
  Rocket,
  Target,
  Gauge,
  Timer,
  Layers,
  Grid3X3,
  LayoutGrid,
  LayoutList,
  SortAsc,
  SortDesc,
  Star,
  AlertTriangle as Warning,
  CircleDot,
  Ban,
  CheckSquare,
  Square,
  Minimize2,
  Maximize2,
  Volume2,
  VolumeX,
  Bell,
  BellRing,
  BellOff,
  ToggleLeft,
  PenTool,
  Smartphone,
  Hash,
  SkipForward,
  SkipBack,
  Pause,
  Play as PlayIcon,
  MoreVertical,
  Copy,
  ExternalLink,
  HelpCircle,
  BookOpen,
  Wrench,
  Hammer,
  Cog,
  Power,
  Battery,
  MemoryStick,
  Dna,
  Flame,
  Snowflake,
  Wind,
  Radio,
  Signal,
  WifiOff,
  Usb,
  Gamepad2,
  Monitor as MonitorSpeaker,
  ScanLine,
  Crosshair,
  Move,
  Pointer,
  Touchpad,
  Type,
  Braces,
  Code,
  Database,
  Cloud,
  CloudOff,
  Fingerprint,
  UserX,
  Users,
  UserCheck,
  FileText,
  FolderOpen,
  Archive,
  Package,
  Box,
  PackageSearch,
  ClipboardList,
  ClipboardCheck,
  ClipboardX,
  ListChecks,
  ListTodo,
  BarChart3,
  PieChart,
  LineChart,
  ActivityIcon,
  Heart,
  Activity as HeartPulse,
  Cpu as Brain,
  Search as Microscope,
  ScanEye as Binoculars,
  Disc as Radar,
  Globe2 as Satellite,
  Navigation as Compass,
  MapPin,
  Navigation,
} from 'lucide-react';
import { cn } from '@/lib/utils';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Progress } from '@/components/ui/progress';
import { Input } from '@/components/ui/input';
import { Checkbox } from '@/components/ui/checkbox';

// =============================================================================
// TYPE DEFINITIONS
// =============================================================================

interface AppliedOptimization {
  id: string;
  appliedAt: number;
  originalValue?: any;
  success: boolean;
}

interface OptimizationPanelProps {
  className?: string;
  onOptimizationApply?: (id: string) => void;
  onOptimizationRevert?: (id: string) => void;
}

type SortOption = 'name' | 'impact' | 'risk' | 'status';
type SortDirection = 'asc' | 'desc';
type StatusFilter = 'all' | 'applied' | 'not-applied';
type RiskFilter = 'all' | 'safe' | 'warning' | 'dangerous';

// =============================================================================
// CATEGORY CONFIGURATION
// =============================================================================

const categoryConfig = [
  {
    id: 'system' as OptimizationCategory,
    icon: Monitor,
    color: '#3B82F6',
    gradient: 'linear-gradient(135deg, #3B82F6, #1D4ED8)',
    bgColor: 'rgba(59, 130, 246, 0.15)',
    borderColor: 'rgba(59, 130, 246, 0.3)',
    emoji: '🖥️',
  },
  {
    id: 'network' as OptimizationCategory,
    icon: Wifi,
    color: '#10B981',
    gradient: 'linear-gradient(135deg, #10B981, #059669)',
    bgColor: 'rgba(16, 185, 129, 0.15)',
    borderColor: 'rgba(16, 185, 129, 0.3)',
    emoji: '🌐',
  },
  {
    id: 'input' as OptimizationCategory,
    icon: MousePointer2,
    color: '#8B5CF6',
    gradient: 'linear-gradient(135deg, #8B5CF6, #7C3AED)',
    bgColor: 'rgba(139, 92, 246, 0.15)',
    borderColor: 'rgba(139, 92, 246, 0.3)',
    emoji: '🖱️',
  },
  {
    id: 'visual' as OptimizationCategory,
    icon: Palette,
    color: '#F59E0B',
    gradient: 'linear-gradient(135deg, #F59E0B, #D97706)',
    bgColor: 'rgba(245, 158, 11, 0.15)',
    borderColor: 'rgba(245, 158, 11, 0.3)',
    emoji: '⚙️',
  },
  {
    id: 'advanced' as OptimizationCategory,
    icon: Zap,
    color: '#EF4444',
    gradient: 'linear-gradient(135deg, #EF4444, #DC2626)',
    bgColor: 'rgba(239, 68, 68, 0.15)',
    borderColor: 'rgba(239, 68, 68, 0.3)',
    emoji: '🚀',
    hasWarning: true,
  },
  {
    id: 'privacy' as OptimizationCategory,
    icon: Shield,
    color: '#14B8A6',
    gradient: 'linear-gradient(135deg, #14B8A6, #0F766E)',
    bgColor: 'rgba(20, 184, 166, 0.15)',
    borderColor: 'rgba(20, 184, 166, 0.3)',
    emoji: '🔒',
  },
];

// =============================================================================
// RISK LEVEL CONFIGURATION
// =============================================================================

const riskConfig = {
  safe: {
    labelEs: 'Seguro',
    labelEn: 'Safe',
    color: '#10B981',
    bgColor: 'rgba(16, 185, 129, 0.15)',
    borderColor: 'rgba(16, 185, 129, 0.3)',
    icon: ShieldCheck,
    descriptionEs: 'Sin riesgos conocidos',
    descriptionEn: 'No known risks',
  },
  warning: {
    labelEs: 'Advertencia',
    labelEn: 'Warning',
    color: '#F59E0B',
    bgColor: 'rgba(245, 158, 11, 0.15)',
    borderColor: 'rgba(245, 158, 11, 0.3)',
    icon: AlertTriangle,
    descriptionEs: 'Requiere precaución',
    descriptionEn: 'Requires caution',
  },
  dangerous: {
    labelEs: 'Peligroso',
    labelEn: 'Dangerous',
    color: '#EF4444',
    bgColor: 'rgba(239, 68, 68, 0.15)',
    borderColor: 'rgba(239, 68, 68, 0.3)',
    icon: ShieldAlert,
    descriptionEs: 'Puede causar inestabilidad',
    descriptionEn: 'May cause instability',
  },
};

// =============================================================================
// PERFORMANCE IMPACT CONFIGURATION
// =============================================================================

const impactConfig = {
  low: {
    labelEs: 'Bajo',
    labelEn: 'Low',
    color: '#6B7280',
    bgColor: 'rgba(107, 114, 128, 0.15)',
    icon: Minimize2,
  },
  medium: {
    labelEs: 'Medio',
    labelEn: 'Medium',
    color: '#3B82F6',
    bgColor: 'rgba(59, 130, 246, 0.15)',
    icon: Maximize2,
  },
  high: {
    labelEs: 'Alto',
    labelEn: 'High',
    color: '#F59E0B',
    bgColor: 'rgba(245, 158, 11, 0.15)',
    icon: TrendingUp,
  },
  'very-high': {
    labelEs: 'Muy Alto',
    labelEn: 'Very High',
    color: '#EF4444',
    bgColor: 'rgba(239, 68, 68, 0.15)',
    icon: Flame,
  },
};

// =============================================================================
// SECURITY IMPACT CONFIGURATION
// =============================================================================

const securityConfig = {
  none: {
    labelEs: 'Ninguno',
    labelEn: 'None',
    color: '#10B981',
    icon: ShieldCheck,
    descEs: 'No hay impacto en seguridad',
    descEn: 'No security impact',
  },
  low: {
    labelEs: 'Bajo',
    labelEn: 'Low',
    color: '#6B7280',
    icon: Lock,
    descEs: 'Impacto bajo en seguridad del sistema',
    descEn: 'Low system security impact',
  },
  medium: {
    labelEs: 'Medio',
    labelEn: 'Medium',
    color: '#F59E0B',
    icon: AlertTriangle,
    descEs: 'Impacto moderado en seguridad',
    descEn: 'Moderate security impact',
  },
  high: {
    labelEs: 'Alto',
    labelEn: 'High',
    color: '#EF4444',
    icon: ShieldAlert,
    descEs: 'Impacto alto en seguridad del sistema',
    descEn: 'High system security impact',
  },
  'reduces-security': {
    labelEs: 'Reduce Seguridad',
    labelEn: 'Reduces Security',
    color: '#DC2626',
    icon: Unlock,
    descEs: 'Esta optimización reduce la seguridad del sistema',
    descEn: 'This optimization reduces system security',
  },
};

// =============================================================================
// MAIN COMPONENT
// =============================================================================

export function FullOptimizationPanel({ className, onOptimizationApply, onOptimizationRevert }: OptimizationPanelProps) {
  // Store state
  const {
    settings,
    optimizations: storeOptimizations,
    applyOptimization,
    revertOptimization,
    applyAllInCategory,
    revertAll,
    isProcessing,
    addToHistory,
  } = useAppStore();

  // Local state
  const [activeCategory, setActiveCategory] = useState<OptimizationCategory>('system');
  const [searchQuery, setSearchQuery] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');

  useEffect(() => {
    const timer = setTimeout(() => setDebouncedSearch(searchQuery), 200);
    return () => clearTimeout(timer);
  }, [searchQuery]);
  const [selectedItems, setSelectedItems] = useState<Set<string>>(new Set());
  const [expandedItems, setExpandedItems] = useState<Set<string>>(new Set());
  const [riskFilter, setRiskFilter] = useState<RiskFilter>('all');
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('all');
  const [sortBy, setSortBy] = useState<SortOption>('name');
  const [sortDir, setSortDir] = useState<SortDirection>('asc');
  const [showFilters, setShowFilters] = useState(false);
  const [showBulkActions, setShowBulkActions] = useState(false);
  const [confirmModal, setConfirmModal] = useState<{
    isOpen: boolean;
    items: OptimizationItem[];
    action: 'apply' | 'revert';
  }>({ isOpen: false, items: [], action: 'apply' });
  const [understoodRisks, setUnderstoodRisks] = useState(false);
  const [isApplyingBatch, setIsApplyingBatch] = useState(false);
  const [batchProgress, setBatchProgress] = useState({
    current: 0,
    total: 0,
    success: 0,
    failed: 0,
    currentItem: '',
  });
  const [toastMessage, setToastMessage] = useState<{ message: string; type: 'success' | 'error' | 'info' } | null>(null);
  const [viewMode, setViewMode] = useState<'grid' | 'list'>('list');

  // Refs
  const containerRef = useRef<HTMLDivElement>(null);

  // Derived state
  const isDark = settings.theme === 'dark';
  const lang = settings.language;

  // Get optimizations for active category with filters and sorting
  const filteredOptimizations = useMemo(() => {
    let items = EXECUTABLE_OPTIMIZATIONS.filter((opt) => opt.category === activeCategory);

    // Apply search filter
    if (debouncedSearch.trim()) {
      const query = debouncedSearch.toLowerCase();
      items = items.filter(
        (opt) =>
          opt.nameEs.toLowerCase().includes(query) ||
          opt.nameEn.toLowerCase().includes(query) ||
          opt.descriptionEs.toLowerCase().includes(query) ||
          opt.descriptionEn.toLowerCase().includes(query) ||
          opt.id.toLowerCase().includes(query)
      );
    }

    // Apply risk filter
    if (riskFilter !== 'all') {
      items = items.filter((opt) => opt.riskLevel === riskFilter);
    }

    // Apply status filter
    if (statusFilter !== 'all') {
      const isApplied = (id: string) =>
        storeOptimizations.find((o) => o.id === id)?.isApplied ?? false;
      if (statusFilter === 'applied') {
        items = items.filter((opt) => isApplied(opt.id));
      } else if (statusFilter === 'not-applied') {
        items = items.filter((opt) => !isApplied(opt.id));
      }
    }

    // Apply sorting
    const isApplied = (id: string) =>
      storeOptimizations.find((o) => o.id === id)?.isApplied ?? false;
    
    items.sort((a, b) => {
      let comparison = 0;
      switch (sortBy) {
        case 'name':
          comparison = (lang === 'es' ? a.nameEs : a.nameEn).localeCompare(
            lang === 'es' ? b.nameEs : b.nameEn
          );
          break;
        case 'impact':
          const impactOrder = { low: 1, medium: 2, high: 3, 'very-high': 4 };
          comparison = impactOrder[a.performanceImpact] - impactOrder[b.performanceImpact];
          break;
        case 'risk':
          const riskOrder = { safe: 1, warning: 2, dangerous: 3 };
          comparison = riskOrder[a.riskLevel] - riskOrder[b.riskLevel];
          break;
        case 'status':
          comparison = Number(isApplied(b.id)) - Number(isApplied(a.id));
          break;
      }
      return sortDir === 'asc' ? comparison : -comparison;
    });

    return items;
  }, [
    activeCategory,
    searchQuery,
    riskFilter,
    statusFilter,
    sortBy,
    sortDir,
    storeOptimizations,
    lang,
  ]);

  // Category statistics
  const categoryStats = useMemo(() => {
    return categoryConfig.map((cat) => {
      const catOptimizations = EXECUTABLE_OPTIMIZATIONS.filter((opt) => opt.category === cat.id);
      const appliedCount = catOptimizations.filter(
        (opt) => storeOptimizations.find((o) => o.id === opt.id)?.isApplied
      ).length;
      return {
        ...cat,
        total: catOptimizations.length,
        applied: appliedCount,
        pending: catOptimizations.length - appliedCount,
      };
    });
  }, [storeOptimizations]);

  // Overall statistics
  const overallStats = useMemo(() => {
    const executableTotal = EXECUTABLE_OPTIMIZATIONS.length;
    const total = executableTotal + nonExecutableOptimizationIds.size;
    const applied = storeOptimizations.filter((o) => o.isApplied).length;
    const pending = executableTotal - applied;
    const progress = executableTotal > 0 ? Math.round((applied / executableTotal) * 100) : 0;
    return { total, executableTotal, guidanceTotal: nonExecutableOptimizationIds.size, applied, pending, progress };
  }, [storeOptimizations]);

  // Current category config
  const currentCategoryConfig = categoryConfig.find((c) => c.id === activeCategory)!;
  const currentCategoryMeta = CATEGORIES.find((c) => c.id === activeCategory);

  // Toast notification handler
  const showToast = useCallback((message: string, type: 'success' | 'error' | 'info' = 'info') => {
    setToastMessage({ message, type });
    setTimeout(() => setToastMessage(null), 3000);
  }, []);

  // Selection handlers
  const toggleSelection = useCallback((id: string) => {
    setSelectedItems((prev) => {
      const newSet = new Set(prev);
      if (newSet.has(id)) {
        newSet.delete(id);
      } else {
        newSet.add(id);
      }
      return newSet;
    });
  }, []);

  const selectAllVisible = useCallback(() => {
    setSelectedItems(new Set(filteredOptimizations.map((opt) => opt.id)));
  }, [filteredOptimizations]);

  const deselectAll = useCallback(() => {
    setSelectedItems(new Set());
  }, []);

  const selectAllSafe = useCallback(() => {
    const safeIds = filteredOptimizations
      .filter((opt) => opt.riskLevel === 'safe')
      .map((opt) => opt.id);
    setSelectedItems(new Set(safeIds));
  }, [filteredOptimizations]);

  // Expand/collapse handlers
  const toggleExpand = useCallback((id: string) => {
    setExpandedItems((prev) => {
      const newSet = new Set(prev);
      if (newSet.has(id)) {
        newSet.delete(id);
      } else {
        newSet.add(id);
      }
      return newSet;
    });
  }, []);

  const expandAll = useCallback(() => {
    setExpandedItems(new Set(filteredOptimizations.map((opt) => opt.id)));
  }, [filteredOptimizations]);

  const collapseAll = useCallback(() => {
    setExpandedItems(new Set());
  }, []);

  // Single optimization apply/revert
  const handleApplySingle = async (optimization: OptimizationItem) => {
    try {
      if (storeOptimizations.find((opt) => opt.id === optimization.id)?.isApplied) {
        await revertOptimization(optimization.id);
      } else {
        // Irreversible optimizations require the explicit risk confirmation.
        if (optimization.riskLevel === 'dangerous') {
          setConfirmModal({ isOpen: true, items: [optimization], action: 'apply' });
          return;
        }
        await applyOptimization(optimization.id, true);
      }
      showToast(
        lang === 'es'
          ? `"${optimization.nameEs}" aplicada correctamente`
          : `"${optimization.nameEn}" applied successfully`,
        'success'
      );
      onOptimizationApply?.(optimization.id);
      addToHistory(optimization.id, optimization.nameEs, 'applied');
    } catch (error) {
      showToast(
        lang === 'es'
          ? `Error al aplicar "${optimization.nameEs}"`
          : `Error applying "${optimization.nameEn}"`,
        'error'
      );
    }
  };

  // Re-run a repeatable maintenance action (temp cleanup, DNS flush, timer
  // resolution). Clicking "Run again" is itself the explicit confirmation.
  const handleRunAgain = async (optimization: OptimizationItem) => {
    try {
      await applyOptimization(optimization.id, false, true);
      showToast(
        lang === 'es'
          ? `"${optimization.nameEs}" ejecutada de nuevo correctamente`
          : `"${optimization.nameEn}" ran again successfully`,
        'success'
      );
      onOptimizationApply?.(optimization.id);
      addToHistory(optimization.id, optimization.nameEs, 'applied');
    } catch {
      showToast(
        lang === 'es'
          ? `Error al ejecutar de nuevo "${optimization.nameEs}"`
          : `Error running "${optimization.nameEn}" again`,
        'error'
      );
    }
  };

  const handleRevertSingle = async (optimization: OptimizationItem) => {
    try {
      await revertOptimization(optimization.id);
      showToast(
        lang === 'es'
          ? `"${optimization.nameEs}" revertida correctamente`
          : `"${optimization.nameEn}" reverted successfully`,
        'success'
      );
      onOptimizationRevert?.(optimization.id);
      addToHistory(optimization.id, optimization.nameEs, 'reverted');
    } catch (error) {
      showToast(
        lang === 'es'
          ? `Error al revertir "${optimization.nameEs}"`
          : `Error reverting "${optimization.nameEn}"`,
        'error'
      );
    }
  };

  // Batch operations
  const handleApplySelected = async () => {
    const selectedOpts = filteredOptimizations.filter((opt) => selectedItems.has(opt.id));
    const dangerousItems = selectedOpts.filter((opt) => opt.riskLevel === 'dangerous');

    if (dangerousItems.length > 0) {
      setConfirmModal({ isOpen: true, items: dangerousItems, action: 'apply' });
      return;
    }

    await executeBatchApply(selectedOpts);
  };

  const handleApplyAllSafe = async () => {
    const safeOpts = filteredOptimizations.filter(
      (opt) => opt.riskLevel !== 'dangerous' && !storeOptimizations.find((o) => o.id === opt.id)?.isApplied
    );
    await executeBatchApply(safeOpts);
  };

  const handleApplyCategory = async () => {
    const catOpts = filteredOptimizations.filter(
      (opt) => !storeOptimizations.find((o) => o.id === opt.id)?.isApplied
    );
    const dangerousItems = catOpts.filter((opt) => opt.riskLevel === 'dangerous');

    if (dangerousItems.length > 0) {
      setConfirmModal({ isOpen: true, items: dangerousItems, action: 'apply' });
      return;
    }

    await executeBatchApply(catOpts);
  };

  const handleUndoAll = async () => {
    const appliedOpts = filteredOptimizations.filter((opt) =>
      storeOptimizations.find((o) => o.id === opt.id)?.isApplied
    );

    if (appliedOpts.length === 0) {
      showToast(lang === 'es' ? 'No hay optimizaciones para revertir' : 'No optimizations to revert', 'info');
      return;
    }

    setConfirmModal({ isOpen: true, items: appliedOpts, action: 'revert' });
  };

  const executeBatchApply = async (items: OptimizationItem[]) => {
    setIsApplyingBatch(true);
    setBatchProgress({ current: 0, total: items.length, success: 0, failed: 0, currentItem: '' });

    let successCount = 0;
    let failedCount = 0;

    for (let i = 0; i < items.length; i++) {
      const opt = items[i];
      setBatchProgress((prev) => ({ ...prev, current: i + 1, currentItem: lang === 'es' ? opt.nameEs : opt.nameEn }));

      try {
        await applyOptimization(opt.id, false, understoodRisks);
        successCount++;
        setBatchProgress((prev) => ({ ...prev, success: successCount }));
        addToHistory(opt.id, opt.nameEs, 'applied');
      } catch (error) {
        failedCount++;
        setBatchProgress((prev) => ({ ...prev, failed: failedCount }));
      }

      // Small delay between operations
      await new Promise((resolve) => setTimeout(resolve, 100));
    }

    setIsApplyingBatch(false);
    setShowBulkActions(false);
    setSelectedItems(new Set());

    showToast(
      lang === 'es'
        ? `${successCount} optimizaciones aplicadas`
        : `${successCount} optimizations applied`,
      failedCount > 0 ? 'error' : 'success'
    );
  };

  const confirmAndExecute = async () => {
    if (!understoodRisks && confirmModal.items.some((i) => i.riskLevel === 'dangerous')) {
      showToast(lang === 'es' ? 'Debes aceptar los riesgos para continuar' : 'You must accept the risks to continue', 'error');
      return;
    }

    if (confirmModal.action === 'apply') {
      await executeBatchApply(confirmModal.items);
    } else {
      // Revert all selected
      setIsApplyingBatch(true);
      setBatchProgress({
        current: 0,
        total: confirmModal.items.length,
        success: 0,
        failed: 0,
        currentItem: '',
      });

      for (let i = 0; i < confirmModal.items.length; i++) {
        const opt = confirmModal.items[i];
        setBatchProgress((prev) => ({
          ...prev,
          current: i + 1,
          currentItem: lang === 'es' ? opt.nameEs : opt.nameEn,
        }));

        try {
          await revertOptimization(opt.id);
          setBatchProgress((prev) => ({ ...prev, success: prev.success + 1 }));
          addToHistory(opt.id, opt.nameEs, 'reverted');
        } catch (error) {
          setBatchProgress((prev) => ({ ...prev, failed: prev.failed + 1 }));
        }

        await new Promise((resolve) => setTimeout(resolve, 100));
      }

      setIsApplyingBatch(false);
      showToast(
        lang === 'es'
          ? `${batchProgress.success} optimizaciones revertidas`
          : `${batchProgress.success} optimizations reverted`,
        batchProgress.failed > 0 ? 'error' : 'success'
      );
    }

    setConfirmModal({ isOpen: false, items: [], action: 'apply' });
    setUnderstoodRisks(false);
  };

  // Export functionality
  const handleExportScript = () => {
    const selectedOpts = filteredOptimizations.filter((opt) => selectedItems.has(opt.id));
    if (selectedOpts.length === 0) {
      showToast(lang === 'es' ? 'Selecciona optimizaciones para exportar' : 'Select optimizations to export', 'info');
      return;
    }

    // Generate batch script content
    let scriptContent = '@echo off\n';
    scriptContent += ':: CA-O Windows Optimization Script\n';
    scriptContent += `:: Generated: ${new Date().toISOString()}\n`;
    scriptContent += ':: ========================================\n\n';

    selectedOpts.forEach((opt) => {
      scriptContent += `:: ${lang === 'es' ? opt.nameEs : opt.nameEn}\n`;
      scriptContent += `:: ${lang === 'es' ? opt.descriptionEs : opt.descriptionEn}\n`;

      // Add registry modifications
      if (opt.registryKeys) {
        opt.registryKeys.forEach((reg) => {
          scriptContent += `reg add "${reg.path}" /v ${reg.name} /t ${reg.type} /d ${reg.value} /f\n`;
        });
      }

      // Add service commands
      if (opt.services) {
        opt.services.forEach((svc) => {
          if (svc.action === 'stop-and-disable') {
            scriptContent += `net stop ${svc.name}\nsc config ${svc.name} start= disabled\n`;
          } else if (svc.action === 'stop') {
            scriptContent += `net stop ${svc.name}\n`;
          } else if (svc.action === 'disable') {
            scriptContent += `sc config ${svc.name} start= disabled\n`;
          }
        });
      }

      // Add shell commands
      if (opt.commands) {
        opt.commands.forEach((cmd) => {
          scriptContent += `${cmd}\n`;
        });
      }

      scriptContent += '\n';
    });

    scriptContent += 'echo Optimizations complete!\npause\n';

    // Create download
    const blob = new Blob([scriptContent], { type: 'text/plain' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'ca-o-optimizations.bat';
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);

    showToast(lang === 'es' ? 'Script descargado correctamente' : 'Script downloaded successfully', 'success');
  };

  // Keyboard navigation
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.target instanceof HTMLInputElement || e.target instanceof HTMLTextAreaElement) return;

      switch (e.key) {
        case '1':
          setActiveCategory('system');
          break;
        case '2':
          setActiveCategory('network');
          break;
        case '3':
          setActiveCategory('input');
          break;
        case '4':
          setActiveCategory('visual');
          break;
        case '5':
          setActiveCategory('advanced');
          break;
        case '6':
          setActiveCategory('privacy');
          break;
        case 'a':
          if (e.ctrlKey || e.metaKey) {
            e.preventDefault();
            selectAllVisible();
          }
          break;
        case 'f':
          if (e.ctrlKey || e.metaKey) {
            e.preventDefault();
            // Focus search input would go here
          }
          break;
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [selectAllVisible]);

  // Helper to check if optimization is applied
  const isOptimizationApplied = useCallback(
    (id: string) => storeOptimizations.find((o) => o.id === id)?.isApplied ?? false,
    [storeOptimizations]
  );

  // Render risk badge
  const renderRiskBadge = (risk: RiskLevel) => {
    const config = riskConfig[risk];
    const Icon = config.icon;
    return (
      <div
        className="flex items-center gap-1 px-2 py-1 rounded-md text-xs font-medium"
        style={{ background: config.bgColor, color: config.color, border: `1px solid ${config.borderColor}` }}
      >
        <Icon size={12} />
        <span>{lang === 'es' ? config.labelEs : config.labelEn}</span>
      </div>
    );
  };

  // Render impact badge
  const renderImpactBadge = (impact: PerformanceImpact) => {
    const config = impactConfig[impact];
    const Icon = config.icon;
    return (
      <div
        className="flex items-center gap-1 px-2 py-1 rounded-md text-xs font-medium"
        style={{ background: config.bgColor, color: config.color }}
      >
        <Icon size={12} />
        <span>{lang === 'es' ? config.labelEs : config.labelEn}</span>
      </div>
    );
  };

  // Render security indicator
  const renderSecurityIndicator = (security: SecurityImpact) => {
    const config = securityConfig[security];
    const Icon = config.icon;
    return (
      <div className="flex items-center gap-1.5" style={{ color: config.color }}>
        <Icon size={14} />
        <span className="text-xs">{lang === 'es' ? config.labelEs : config.labelEn}</span>
      </div>
    );
  };

  // ===========================================================================
  // RENDER
  // ===========================================================================

  return (
    <div
      ref={containerRef}
      className={cn(
        'min-h-screen pb-32 relative overflow-hidden',
        isDark ? 'bg-[#0b0b18]' : 'bg-gray-50',
        className
      )}
    >
      {/* Background Effects */}
      <div className="fixed inset-0 pointer-events-none overflow-hidden">
        <div
          className="absolute top-0 right-0 w-96 h-96 rounded-full opacity-10 blur-3xl"
          style={{
            background: currentCategoryConfig.gradient,
            transform: 'translate(30%, -30%)',
          }}
        />
        <div
          className="absolute bottom-0 left-0 w-80 h-80 rounded-full opacity-5 blur-3xl"
          style={{
            background: currentCategoryConfig.gradient,
            transform: 'translate(-30%, 30%)',
          }}
        />
      </div>

      {/* Toast Notification */}
      <AnimatePresence>
        {toastMessage && (
          <motion.div
            initial={{ opacity: 0, y: -50, x: '-50%' }}
            animate={{ opacity: 1, y: 0, x: '-50%' }}
            exit={{ opacity: 0, y: -50, x: '-50%' }}
            role="status"
            aria-live="polite"
            className="fixed top-4 left-1/2 z-[100] w-[calc(100vw-2rem)] max-w-xl px-4 sm:px-6 py-3 rounded-xl shadow-2xl flex items-start gap-3"
            style={{
              background:
                toastMessage.type === 'success'
                  ? 'rgba(16, 185, 129, 0.95)'
                  : toastMessage.type === 'error'
                  ? 'rgba(239, 68, 68, 0.95)'
                  : 'rgba(59, 130, 246, 0.95)',
              color: '#fff',
              backdropFilter: 'blur(10px)',
              border: `1px solid ${
                toastMessage.type === 'success'
                  ? 'rgba(16, 185, 129, 0.5)'
                  : toastMessage.type === 'error'
                  ? 'rgba(239, 68, 68, 0.5)'
                  : 'rgba(59, 130, 246, 0.5)'
              }`,
            }}
          >
            {toastMessage.type === 'success' ? (
              <CheckCircle2 size={20} />
            ) : toastMessage.type === 'error' ? (
              <XCircle size={20} />
            ) : (
              <Info size={20} />
            )}
            <span className="font-medium text-sm leading-5 break-words">{toastMessage.message}</span>
            <button onClick={() => setToastMessage(null)} className="ml-auto shrink-0 hover:opacity-70" aria-label={lang === 'es' ? 'Cerrar notificación' : 'Close notification'}>
              <X size={16} />
            </button>
          </motion.div>
        )}
      </AnimatePresence>

      {/* ================================================================== */}
      {/* HEADER SECTION */}
      {/* ================================================================== */}
      <motion.div
        initial={{ opacity: 0, y: -20 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.5 }}
        className="relative z-10 px-4 md:px-8 pt-6 pb-4"
      >
        {/* Title & Stats Row */}
        <div className="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-4 mb-6">
          {/* Title */}
          <div className="flex items-center gap-4">
            <motion.div
              whileHover={{ scale: 1.05, rotate: 5 }}
              className="w-14 h-14 rounded-2xl flex items-center justify-center text-2xl shadow-lg"
              style={{
                background: currentCategoryConfig.gradient,
                boxShadow: `0 8px 25px ${currentCategoryConfig.color}40`,
              }}
            >
              {currentCategoryConfig.emoji}
            </motion.div>
            <div>
              <h1
                className="text-2xl md:text-3xl font-bold"
                style={{ color: isDark ? '#fff' : '#1a1a2e' }}
              >
                {lang === 'es' ? 'Centro de Optimización' : 'Optimization Center'}
              </h1>
              <p
                className="text-sm"
                style={{ color: isDark ? 'rgba(255,255,255,0.5)' : 'rgba(0,0,0,0.5)' }}
              >
                {currentCategoryMeta
                  ? (lang === 'es' ? currentCategoryMeta.descriptionEs : currentCategoryMeta.descriptionEn)
                  : ''}
              </p>
            </div>
          </div>

          {/* Stats Cards */}
          <div className="flex flex-wrap gap-3">
            <StatCard
              icon={<Database size={18} />}
              value={overallStats.total}
              label={lang === 'es' ? 'Catálogo' : 'Catalog'}
              color="#6B7280"
              isDark={isDark}
            />
            <StatCard
              icon={<CheckCircle2 size={18} />}
              value={overallStats.applied}
              label={lang === 'es' ? 'Aplicadas' : 'Applied'}
              color="#10B981"
              isDark={isDark}
            />
            <StatCard
              icon={<Clock size={18} />}
              value={overallStats.pending}
              label={lang === 'es' ? 'Pendientes' : 'Pending'}
              color="#F59E0B"
              isDark={isDark}
            />
            <StatCard
              icon={<TrendingUp size={18} />}
              value={`${overallStats.progress}%`}
              label={lang === 'es' ? 'Progreso' : 'Progress'}
              color="#FF6B35"
              isDark={isDark}
            />
          </div>
          <p className="mt-2 text-xs" style={{ color: isDark ? 'rgba(255,255,255,0.5)' : 'rgba(0,0,0,0.5)' }}>
            {lang === 'es'
              ? `${overallStats.executableTotal} aplicables y ${overallStats.guidanceTotal} guías no automatizables`
              : `${overallStats.executableTotal} applicable and ${overallStats.guidanceTotal} non-automatable guides`}
          </p>
        </div>

        {/* Progress Bar */}
        <div className="mb-6 p-4 rounded-2xl" style={{
          background: isDark ? 'rgba(30, 30, 50, 0.8)' : 'rgba(255, 255, 255, 0.9)',
          border: `1px solid ${isDark ? 'rgba(255, 255, 255, 0.08)' : 'rgba(0, 0, 0, 0.08)'}`,
          backdropFilter: 'blur(10px)',
        }}>
          <div className="flex items-center justify-between mb-2">
            <span className="text-sm font-medium" style={{ color: isDark ? 'rgba(255,255,255,0.7)' : 'rgba(0,0,0,0.7)' }}>
              {lang === 'es' ? 'Progreso General de Optimización' : 'Overall Optimization Progress'}
            </span>
            <span className="text-sm font-bold" style={{ color: '#FF6B35' }}>
              {overallStats.applied}/{overallStats.total} ({overallStats.progress}%)
            </span>
          </div>
          <div className="relative h-3 bg-gray-200 dark:bg-gray-700 rounded-full overflow-hidden">
            <motion.div
              initial={{ width: 0 }}
              animate={{ width: `${overallStats.progress}%` }}
              transition={{ duration: 1, ease: 'easeOut' }}
              className="absolute left-0 top-0 h-full rounded-full"
              style={{
                background: `linear-gradient(90deg, #FF6B35, #ff8c42, #FF6B35)`,
                backgroundSize: '200% 100%',
                animation: overallStats.progress > 0 ? 'shimmer 2s infinite linear' : 'none',
              }}
            />
          </div>
        </div>

        {/* Quick Actions */}
        <div className="flex flex-wrap gap-2 mb-6">
          <QuickActionButton
            icon={<Play size={16} />}
            label={lang === 'es' ? 'Aplicar Todas Seguras' : 'Apply All Safe'}
            onClick={handleApplyAllSafe}
            variant="primary"
            disabled={isProcessing || isApplyingBatch}
            isDark={isDark}
          />
          <QuickActionButton
            icon={<Layers size={16} />}
            label={lang === 'es' ? 'Aplicar Categoría' : 'Apply Category'}
            onClick={handleApplyCategory}
            variant="secondary"
            disabled={isProcessing || isApplyingBatch}
            isDark={isDark}
          />
          <QuickActionButton
            icon={<RotateCcw size={16} />}
            label={lang === 'es' ? 'Deshacer Todo' : 'Undo All'}
            onClick={handleUndoAll}
            variant="danger"
            disabled={isProcessing || isApplyingBatch || overallStats.applied === 0}
            isDark={isDark}
          />
        </div>
      </motion.div>

      {/* ================================================================== */}
      {/* CATEGORY TABS */}
      {/* ================================================================== */}
      <motion.div
        initial={{ opacity: 0, y: 10 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ delay: 0.2, duration: 0.5 }}
        className="relative z-10 px-4 md:px-8 mb-6"
      >
        <div className="flex gap-2 overflow-x-auto pb-2 scrollbar-hide">
          {categoryStats.map((cat) => {
            const CatIcon = cat.icon;
            const isActive = activeCategory === cat.id;
            const progressPercent = cat.total > 0 ? Math.round((cat.applied / cat.total) * 100) : 0;

            return (
              <motion.button
                key={cat.id}
                onClick={() => {
                  setActiveCategory(cat.id);
                  setSelectedItems(new Set());
                  setExpandedItems(new Set());
                }}
                whileHover={{ scale: 1.02 }}
                whileTap={{ scale: 0.98 }}
                className={cn(
                  'relative flex items-center gap-3 px-5 py-3 rounded-xl font-medium text-sm whitespace-nowrap transition-all min-w-fit',
                )}
                style={{
                  background: isActive
                    ? cat.bgColor
                    : (isDark ? 'rgba(255, 255, 255, 0.04)' : 'rgba(0, 0, 0, 0.03)'),
                  border: `2px solid ${isActive ? cat.borderColor : (isDark ? 'rgba(255, 255, 255, 0.06)' : 'rgba(0, 0, 0, 0.06)')}`,
                  color: isActive ? cat.color : (isDark ? 'rgba(255,255,255,0.6)' : 'rgba(0,0,0,0.6)'),
                }}
              >
                <CatIcon size={18} />
                <div className="flex flex-col items-start">
                  <span className="flex items-center gap-1.5">
                    <span>{cat.emoji}</span>
                    <span>{lang === 'es' ? CATEGORIES.find(c => c.id === cat.id)?.nameEn || '' : CATEGORIES.find(c => c.id === cat.id)?.nameEn || ''}</span>
                    {(cat as typeof categoryConfig[number]).hasWarning && (
                      <Warning size={14} className="text-red-500" />
                    )}
                  </span>
                  <span className="text-xs opacity-70">
                    {cat.applied}/{cat.total}
                  </span>
                </div>

                {/* Progress indicator */}
                {isActive && (
                  <motion.div
                    layoutId="activeTabIndicator"
                    className="absolute bottom-0 left-2 right-2 h-0.5 rounded-full overflow-hidden"
                    style={{ backgroundColor: cat.color }}
                  >
                    <motion.div
                      initial={{ width: '0%' }}
                      animate={{ width: `${progressPercent}%` }}
                      className="h-full bg-white/50 rounded-full"
                    />
                  </motion.div>
                )}

                {/* Applied count badge */}
                {cat.applied > 0 && (
                  <motion.div
                    initial={{ scale: 0 }}
                    animate={{ scale: 1 }}
                    className="absolute -top-1 -right-1 w-5 h-5 rounded-full flex items-center justify-center text-[10px] font-bold text-white"
                    style={{ background: cat.color }}
                  >
                    {cat.applied}
                  </motion.div>
                )}
              </motion.button>
            );
          })}
        </div>
      </motion.div>

      {/* ================================================================== */}
      {/* SEARCH & FILTERS BAR */}
      {/* ================================================================== */}
      <motion.div
        initial={{ opacity: 0, y: 10 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ delay: 0.3, duration: 0.5 }}
        className="relative z-10 px-4 md:px-8 mb-6"
      >
        <div className="flex flex-col lg:flex-row gap-3">
          {/* Search Input */}
          <div className="relative flex-1">
            <Search
              size={18}
              className="absolute left-4 top-1/2 -translate-y-1/2"
              style={{ color: isDark ? 'rgba(255,255,255,0.4)' : 'rgba(0,0,0,0.4)' }}
            />
            <Input
              type="text"
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              placeholder={
                lang === 'es'
                  ? 'Buscar optimizaciones por nombre, descripción...'
                  : 'Search optimizations by name, description...'
              }
              className="w-full pl-11 pr-4 py-3 rounded-xl text-sm outline-none transition-all focus:ring-2"
              style={{
                background: isDark ? 'rgba(30, 30, 50, 0.8)' : 'rgba(255, 255, 255, 0.95)',
                border: `1px solid ${isDark ? 'rgba(255, 255, 255, 0.1)' : 'rgba(0, 0, 0, 0.1)'}`,
                color: isDark ? '#fff' : '#1a1a2e',
              }}
            />
            {searchQuery && (
              <button
                onClick={() => setSearchQuery('')}
                className="absolute right-4 top-1/2 -translate-y-1/2"
                style={{ color: isDark ? 'rgba(255,255,255,0.4)' : 'rgba(0,0,0,0.4)' }}
              >
                <X size={16} />
              </button>
            )}
          </div>

          {/* Filter Toggle */}
          <Button
            variant="outline"
            onClick={() => setShowFilters(!showFilters)}
            className={cn(
              'flex items-center gap-2 px-4 py-3 rounded-xl text-sm font-medium transition-all',
            )}
            style={{
              background: showFilters ? 'rgba(255, 107, 53, 0.15)' : (isDark ? 'rgba(255, 255, 255, 0.06)' : 'rgba(0, 0, 0, 0.04)'),
              border: `1px solid ${showFilters ? 'rgba(255, 107, 53, 0.4)' : (isDark ? 'rgba(255, 255, 255, 0.1)' : 'rgba(0, 0, 0, 0.1)')}`,
              color: showFilters ? '#FF6B35' : (isDark ? 'rgba(255,255,255,0.7)' : 'rgba(0,0,0,0.7)'),
            }}
          >
            <Filter size={16} />
            <span className="hidden sm:inline">{lang === 'es' ? 'Filtros' : 'Filters'}</span>
            <ChevronDown size={14} className={cn(showFilters && 'rotate-180 transition-transform')} />
          </Button>

          {/* View Mode Toggle */}
          <div className="flex rounded-xl overflow-hidden" style={{ border: `1px solid ${isDark ? 'rgba(255, 255, 255, 0.1)' : 'rgba(0, 0, 0, 0.1)'}` }}>
            <button
              onClick={() => setViewMode('list')}
              className="p-3 transition-colors"
              style={{
                background: viewMode === 'list' ? (isDark ? 'rgba(255, 107, 53, 0.2)' : 'rgba(255, 107, 53, 0.1)') : 'transparent',
                color: viewMode === 'list' ? '#FF6B35' : (isDark ? 'rgba(255,255,255,0.5)' : 'rgba(0,0,0,0.5)'),
              }}
            >
              <LayoutList size={18} />
            </button>
            <button
              onClick={() => setViewMode('grid')}
              className="p-3 transition-colors"
              style={{
                background: viewMode === 'grid' ? (isDark ? 'rgba(255, 107, 53, 0.2)' : 'rgba(255, 107, 53, 0.1)') : 'transparent',
                color: viewMode === 'grid' ? '#FF6B35' : (isDark ? 'rgba(255,255,255,0.5)' : 'rgba(0,0,0,0.5)'),
              }}
            >
              <Grid3X3 size={18} />
            </button>
          </div>
        </div>

        {/* Expanded Filters */}
        <AnimatePresence>
          {showFilters && (
            <motion.div
              initial={{ height: 0, opacity: 0 }}
              animate={{ height: 'auto', opacity: 1 }}
              exit={{ height: 0, opacity: 0 }}
              transition={{ duration: 0.3 }}
              className="overflow-hidden mt-4"
            >
              <div
                className="p-4 rounded-xl space-y-4"
                style={{
                  background: isDark ? 'rgba(30, 30, 50, 0.8)' : 'rgba(255, 255, 255, 0.95)',
                  border: `1px solid ${isDark ? 'rgba(255, 255, 255, 0.08)' : 'rgba(0, 0, 0, 0.08)'}`,
                }}
              >
                {/* Risk Level Filters */}
                <div>
                  <label className="text-xs font-medium uppercase tracking-wider mb-2 block" style={{ color: isDark ? 'rgba(255,255,255,0.5)' : 'rgba(0,0,0,0.5)' }}>
                    {lang === 'es' ? 'Nivel de Riesgo' : 'Risk Level'}
                  </label>
                  <div className="flex flex-wrap gap-2">
                    {[
                      { value: 'all' as RiskFilter, label: lang === 'es' ? 'Todos' : 'All', color: '#6B7280' },
                      { value: 'safe' as RiskFilter, label: riskConfig.safe.labelEs, color: riskConfig.safe.color },
                      { value: 'warning' as RiskFilter, label: riskConfig.warning.labelEs, color: riskConfig.warning.color },
                      { value: 'dangerous' as RiskFilter, label: riskConfig.dangerous.labelEs, color: riskConfig.dangerous.color },
                    ].map((filter) => (
                      <button
                        key={filter.value}
                        onClick={() => setRiskFilter(filter.value)}
                        className={cn(
                          'px-3 py-1.5 rounded-lg text-xs font-medium transition-all',
                        )}
                        style={{
                          background: riskFilter === filter.value ? `${filter.color}20` : (isDark ? 'rgba(255, 255, 255, 0.05)' : 'rgba(0, 0, 0, 0.03)'),
                          border: `1px solid ${riskFilter === filter.value ? `${filter.color}40` : (isDark ? 'rgba(255, 255, 255, 0.08)' : 'rgba(0, 0, 0, 0.08)')}`,
                          color: riskFilter === filter.value ? filter.color : (isDark ? 'rgba(255,255,255,0.6)' : 'rgba(0,0,0,0.6)'),
                        }}
                      >
                        {filter.label}
                      </button>
                    ))}
                  </div>
                </div>

                {/* Status Filter */}
                <div>
                  <label className="text-xs font-medium uppercase tracking-wider mb-2 block" style={{ color: isDark ? 'rgba(255,255,255,0.5)' : 'rgba(0,0,0,0.5)' }}>
                    {lang === 'es' ? 'Estado' : 'Status'}
                  </label>
                  <div className="flex flex-wrap gap-2">
                    {[
                      { value: 'all' as StatusFilter, label: lang === 'es' ? 'Todos' : 'All' },
                      { value: 'applied' as StatusFilter, label: lang === 'es' ? 'Aplicadas' : 'Applied' },
                      { value: 'not-applied' as StatusFilter, label: lang === 'es' ? 'No Aplicadas' : 'Not Applied' },
                    ].map((filter) => (
                      <button
                        key={filter.value}
                        onClick={() => setStatusFilter(filter.value)}
                        className={cn(
                          'px-3 py-1.5 rounded-lg text-xs font-medium transition-all',
                        )}
                        style={{
                          background: statusFilter === filter.value ? 'rgba(255, 107, 53, 0.15)' : (isDark ? 'rgba(255, 255, 255, 0.05)' : 'rgba(0, 0, 0, 0.03)'),
                          border: `1px solid ${statusFilter === filter.value ? 'rgba(255, 107, 53, 0.3)' : (isDark ? 'rgba(255, 255, 255, 0.08)' : 'rgba(0, 0, 0, 0.08)')}`,
                          color: statusFilter === filter.value ? '#FF6B35' : (isDark ? 'rgba(255,255,255,0.6)' : 'rgba(0,0,0,0.6)'),
                        }}
                      >
                        {filter.label}
                      </button>
                    ))}
                  </div>
                </div>

                {/* Sort Options */}
                <div>
                  <label className="text-xs font-medium uppercase tracking-wider mb-2 block" style={{ color: isDark ? 'rgba(255,255,255,0.5)' : 'rgba(0,0,0,0.5)' }}>
                    {lang === 'es' ? 'Ordenar por' : 'Sort by'}
                  </label>
                  <div className="flex flex-wrap gap-2">
                    {([
                      { value: 'name' as SortOption, label: lang === 'es' ? 'Nombre' : 'Name' },
                      { value: 'impact' as SortOption, label: lang === 'es' ? 'Impacto' : 'Impact' },
                      { value: 'risk' as SortOption, label: lang === 'es' ? 'Riesgo' : 'Risk' },
                      { value: 'status' as SortOption, label: lang === 'es' ? 'Estado' : 'Status' },
                    ] as const).map((sort) => (
                      <button
                        key={sort.value}
                        onClick={() => {
                          if (sortBy === sort.value) {
                            setSortDir(sortDir === 'asc' ? 'desc' : 'asc');
                          } else {
                            setSortBy(sort.value);
                            setSortDir('asc');
                          }
                        }}
                        className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium transition-all"
                        style={{
                          background: sortBy === sort.value ? 'rgba(255, 107, 53, 0.15)' : (isDark ? 'rgba(255, 255, 255, 0.05)' : 'rgba(0, 0, 0, 0.03)'),
                          border: `1px solid ${sortBy === sort.value ? 'rgba(255, 107, 53, 0.3)' : (isDark ? 'rgba(255, 255, 255, 0.08)' : 'rgba(0, 0, 0, 0.08)')}`,
                          color: sortBy === sort.value ? '#FF6B35' : (isDark ? 'rgba(255,255,255,0.6)' : 'rgba(0,0,0,0.6)'),
                        }}
                      >
                        {sortBy === sort.value ? (
                          sortDir === 'asc' ? <SortAsc size={12} /> : <SortDesc size={12} />
                        ) : (
                          <ArrowUpDown size={12} />
                        )}
                        {sort.label}
                      </button>
                    ))}
                  </div>
                </div>
              </div>
            </motion.div>
          )}
        </AnimatePresence>
      </motion.div>

      {/* ================================================================== */}
      {/* OPTIMIZATION LIST/CARDS SECTION */}
      {/* ================================================================== */}
      <div className="relative z-10 px-4 md:px-8">
        {/* Results Count & Actions */}
        <div className="flex items-center justify-between mb-4">
          <div className="flex items-center gap-2">
            <span className="text-sm" style={{ color: isDark ? 'rgba(255,255,255,0.5)' : 'rgba(0,0,0,0.5)' }}>
              {filteredOptimizations.length} {lang === 'es' ? 'optimizaciones encontradas' : 'optimizations found'}
            </span>
            {selectedItems.size > 0 && (
              <motion.span
                initial={{ scale: 0 }}
                animate={{ scale: 1 }}
                className="px-2 py-0.5 rounded-full text-xs font-bold text-white"
                style={{ background: '#FF6B35' }}
              >
                {selectedItems.size} {lang === 'es' ? 'seleccionado(s)' : 'selected'}
              </motion.span>
            )}
          </div>

          <div className="flex items-center gap-2">
            <Button
              variant="ghost"
              size="sm"
              onClick={expandAll}
              style={{ color: isDark ? 'rgba(255,255,255,0.5)' : 'rgba(0,0,0,0.5)' }}
            >
              {lang === 'es' ? 'Expandir Todo' : 'Expand All'}
            </Button>
            <Button
              variant="ghost"
              size="sm"
              onClick={collapseAll}
              style={{ color: isDark ? 'rgba(255,255,255,0.5)' : 'rgba(0,0,0,0.5)' }}
            >
              {lang === 'es' ? 'Contraer Todo' : 'Collapse All'}
            </Button>
          </div>
        </div>

        {/* Cards Container */}
        <AnimatePresence mode="wait">
          {viewMode === 'list' ? (
            <motion.div
              key={`list-${activeCategory}-${debouncedSearch}-${riskFilter}-${statusFilter}`}
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              exit={{ opacity: 0 }}
              className="space-y-3"
            >
              {filteredOptimizations.map((optimization, index) => (
                <OptimizationCard
                  key={optimization.id}
                  optimization={optimization}
                  index={index}
                  isApplied={isOptimizationApplied(optimization.id)}
                  isSelected={selectedItems.has(optimization.id)}
                  isExpanded={expandedItems.has(optimization.id)}
                  isDark={isDark}
                  lang={lang}
                  isProcessing={isProcessing || isApplyingBatch}
                  onSelect={() => toggleSelection(optimization.id)}
                  onToggleExpand={() => toggleExpand(optimization.id)}
                  onApply={() => handleApplySingle(optimization)}
                  onRevert={() => handleRevertSingle(optimization)}
                  onRunAgain={() => handleRunAgain(optimization)}
                  categoryColor={currentCategoryConfig.color}
                />
              ))}
            </motion.div>
          ) : (
            <motion.div
              key={`grid-${activeCategory}-${debouncedSearch}-${riskFilter}-${statusFilter}`}
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              exit={{ opacity: 0 }}
              className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4"
            >
              {filteredOptimizations.map((optimization, index) => (
                <OptimizationCardGrid
                  key={optimization.id}
                  optimization={optimization}
                  index={index}
                  isApplied={isOptimizationApplied(optimization.id)}
                  isSelected={selectedItems.has(optimization.id)}
                  isExpanded={expandedItems.has(optimization.id)}
                  isDark={isDark}
                  lang={lang}
                  isProcessing={isProcessing || isApplyingBatch}
                  onSelect={() => toggleSelection(optimization.id)}
                  onToggleExpand={() => toggleExpand(optimization.id)}
                  onApply={() => handleApplySingle(optimization)}
                  onRevert={() => handleRevertSingle(optimization)}
                  onRunAgain={() => handleRunAgain(optimization)}
                  categoryColor={currentCategoryConfig.color}
                />
              ))}
            </motion.div>
          )}

          {/* Empty State */}
          {filteredOptimizations.length === 0 && (
            <motion.div
              initial={{ opacity: 0, scale: 0.95 }}
              animate={{ opacity: 1, scale: 1 }}
              className="text-center py-20"
            >
              <div
                className="w-24 h-24 mx-auto mb-6 rounded-full flex items-center justify-center"
                style={{
                  background: isDark ? 'rgba(255, 255, 255, 0.05)' : 'rgba(0, 0, 0, 0.03)',
                  border: `1px dashed ${isDark ? 'rgba(255, 255, 255, 0.1)' : 'rgba(0, 0, 0, 0.1)'}`,
                }}
              >
                <Search size={40} style={{ color: isDark ? 'rgba(255,255,255,0.2)' : 'rgba(0,0,0,0.2)' }} />
              </div>
              <h3
                className="text-xl font-semibold mb-2"
                style={{ color: isDark ? 'rgba(255,255,255,0.7)' : 'rgba(0,0,0,0.7)' }}
              >
                {lang === 'es' ? 'No se encontraron optimizaciones' : 'No optimizations found'}
              </h3>
              <p
                className="text-sm max-w-md mx-auto"
                style={{ color: isDark ? 'rgba(255,255,255,0.4)' : 'rgba(0,0,0,0.4)' }}
              >
                {lang === 'es'
                  ? 'Intenta ajustar los filtros de búsqueda o selecciona otra categoría.'
                  : 'Try adjusting your search filters or select another category.'}
              </p>
              <Button
                variant="outline"
                onClick={() => {
                  setSearchQuery('');
                  setRiskFilter('all');
                  setStatusFilter('all');
                }}
                className="mt-4"
                style={{ color: '#FF6B35', borderColor: 'rgba(255, 107, 53, 0.3)' }}
              >
                {lang === 'es' ? 'Limpiar Filtros' : 'Clear Filters'}
              </Button>
            </motion.div>
          )}
        </AnimatePresence>
      </div>

      {/* ================================================================== */}
      {/* BULK ACTIONS BAR (Sticky at bottom) */}
      {/* ================================================================== */}
      <AnimatePresence>
        {(selectedItems.size > 0 || showBulkActions) && (
          <motion.div
            initial={{ y: 100, opacity: 0 }}
            animate={{ y: 0, opacity: 1 }}
            exit={{ y: 100, opacity: 0 }}
            transition={{ type: 'spring', damping: 25, stiffness: 300 }}
            className="fixed bottom-0 left-0 right-0 z-50"
          >
            <div
              className="mx-4 mb-4 p-4 rounded-2xl shadow-2xl"
              style={{
                background: isDark ? 'rgba(20, 20, 40, 0.98)' : 'rgba(255, 255, 255, 0.98)',
                border: `1px solid ${isDark ? 'rgba(255, 255, 255, 0.1)' : 'rgba(0, 0, 0, 0.1)'}`,
                backdropFilter: 'blur(20px)',
              }}
            >
              <div className="flex flex-col sm:flex-row items-center justify-between gap-4">
                {/* Selection Info */}
                <div className="flex items-center gap-3">
                  <div
                    className="w-10 h-10 rounded-xl flex items-center justify-center font-bold text-white"
                    style={{ background: '#FF6B35' }}
                  >
                    {selectedItems.size}
                  </div>
                  <div>
                    <p className="text-sm font-semibold" style={{ color: isDark ? '#fff' : '#1a1a2e' }}>
                      {lang === 'es' ? 'optimizaciones seleccionadas' : 'optimizations selected'}
                    </p>
                    <p className="text-xs" style={{ color: isDark ? 'rgba(255,255,255,0.5)' : 'rgba(0,0,0,0.5)' }}>
                      {lang === 'es' ? 'Usa las acciones masivas abajo' : 'Use bulk actions below'}
                    </p>
                  </div>
                </div>

                {/* Action Buttons */}
                <div className="flex flex-wrap items-center gap-2">
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={selectAllVisible}
                    style={{ borderColor: isDark ? 'rgba(255,255,255,0.2)' : 'rgba(0,0,0,0.2)', color: isDark ? 'rgba(255,255,255,0.7)' : 'rgba(0,0,0,0.7)' }}
                  >
                    <SelectAll size={14} className="mr-1" />
                    {lang === 'es' ? 'Seleccionar Todo' : 'Select All'}
                  </Button>
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={deselectAll}
                    style={{ borderColor: isDark ? 'rgba(255,255,255,0.2)' : 'rgba(0,0,0,0.2)', color: isDark ? 'rgba(255,255,255,0.7)' : 'rgba(0,0,0,0.7)' }}
                  >
                    <Square size={14} className="mr-1" />
                    {lang === 'es' ? 'Deseleccionar' : 'Deselect'}
                  </Button>
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={selectAllSafe}
                    style={{ borderColor: 'rgba(16, 185, 129, 0.3)', color: '#10B981' }}
                  >
                    <ShieldCheck size={14} className="mr-1" />
                    {lang === 'es' ? 'Solo Seguras' : 'Safe Only'}
                  </Button>
                  <Button
                    size="sm"
                    onClick={handleApplySelected}
                    disabled={selectedItems.size === 0 || isApplyingBatch}
                    className="text-white"
                    style={{ background: 'linear-gradient(135deg, #FF6B35, #ff8c42)' }}
                  >
                    <Play size={14} className="mr-1" />
                    {lang === 'es' ? 'Aplicar Seleccionadas' : 'Apply Selected'}
                  </Button>
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={handleExportScript}
                    disabled={selectedItems.size === 0}
                    style={{ borderColor: 'rgba(59, 130, 246, 0.3)', color: '#3B82F6' }}
                  >
                    <Download size={14} className="mr-1" />
                    .bat
                  </Button>
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => {
                      setShowBulkActions(false);
                      setSelectedItems(new Set());
                    }}
                  >
                    <X size={14} />
                  </Button>
                </div>
              </div>
            </div>
          </motion.div>
        )}
      </AnimatePresence>

      {/* ================================================================== */}
      {/* PROGRESS TRACKER MODAL */}
      {/* ================================================================== */}
      <AnimatePresence>
        {isApplyingBatch && (
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="fixed inset-0 z-[60] flex items-center justify-center p-4"
            style={{ background: 'rgba(0, 0, 0, 0.7)', backdropFilter: 'blur(8px)' }}
          >
            <motion.div
              initial={{ scale: 0.9, opacity: 0 }}
              animate={{ scale: 1, opacity: 1 }}
              exit={{ scale: 0.9, opacity: 0 }}
              className="w-full max-w-md p-6 rounded-2xl shadow-2xl"
              style={{
                background: isDark ? 'rgba(30, 30, 50, 0.98)' : 'rgba(255, 255, 255, 0.98)',
                border: `1px solid ${isDark ? 'rgba(255, 255, 255, 0.1)' : 'rgba(0, 0, 0, 0.1)'}`,
              }}
            >
              {/* Header */}
              <div className="flex items-center gap-3 mb-6">
                <div
                  className="w-12 h-12 rounded-xl flex items-center justify-center"
                  style={{ background: 'rgba(255, 107, 53, 0.15)' }}
                >
                  <Loader2 size={24} className="animate-spin" style={{ color: '#FF6B35' }} />
                </div>
                <div>
                  <h3
                    className="text-lg font-semibold"
                    style={{ color: isDark ? '#fff' : '#1a1a2e' }}
                  >
                    {lang === 'es' ? 'Aplicando Optimizaciones...' : 'Applying Optimizations...'}
                  </h3>
                  <p className="text-sm" style={{ color: isDark ? 'rgba(255,255,255,0.5)' : 'rgba(0,0,0,0.5)' }}>
                    {batchProgress.current} / {batchProgress.total}
                  </p>
                </div>
              </div>

              {/* Current Item */}
              {batchProgress.currentItem && (
                <div
                  className="mb-4 p-3 rounded-xl truncate"
                  style={{
                    background: isDark ? 'rgba(255, 255, 255, 0.05)' : 'rgba(0, 0, 0, 0.03)',
                    color: isDark ? 'rgba(255,255,255,0.7)' : 'rgba(0,0,0,0.7)',
                  }}
                >
                  <span className="text-xs uppercase tracking-wider mr-2" style={{ color: '#FF6B35' }}>
                    {lang === 'es' ? 'Actual' : 'Current'}:
                  </span>
                  {batchProgress.currentItem}
                </div>
              )}

              {/* Progress Bar */}
              <div className="mb-4">
                <Progress
                  value={batchProgress.total > 0 ? (batchProgress.current / batchProgress.total) * 100 : 0}
                  className="h-3"
                />
              </div>

              {/* Stats */}
              <div className="grid grid-cols-3 gap-3 mb-4">
                <div
                  className="text-center p-3 rounded-xl"
                  style={{ background: 'rgba(16, 185, 129, 0.1)' }}
                >
                  <p className="text-2xl font-bold text-emerald-500">{batchProgress.success}</p>
                  <p className="text-xs" style={{ color: isDark ? 'rgba(255,255,255,0.5)' : 'rgba(0,0,0,0.5)' }}>
                    {lang === 'es' ? 'Éxito' : 'Success'}
                  </p>
                </div>
                <div
                  className="text-center p-3 rounded-xl"
                  style={{ background: 'rgba(239, 68, 68, 0.1)' }}
                >
                  <p className="text-2xl font-bold text-red-500">{batchProgress.failed}</p>
                  <p className="text-xs" style={{ color: isDark ? 'rgba(255,255,255,0.5)' : 'rgba(0,0,0,0.5)' }}>
                    {lang === 'es' ? 'Fallido' : 'Failed'}
                  </p>
                </div>
                <div
                  className="text-center p-3 rounded-xl"
                  style={{ background: 'rgba(255, 107, 53, 0.1)' }}
                >
                  <p className="text-2xl font-bold" style={{ color: '#FF6B35' }}>
                    {Math.round(batchProgress.total > 0 ? ((batchProgress.current / batchProgress.total) * 100) : 0)}%
                  </p>
                  <p className="text-xs" style={{ color: isDark ? 'rgba(255,255,255,0.5)' : 'rgba(0,0,0,0.5)' }}>
                    {lang === 'es' ? 'Progreso' : 'Progress'}
                  </p>
                </div>
              </div>

              {/* Cancel Button */}
              <Button
                variant="outline"
                className="w-full"
                onClick={() => setIsApplyingBatch(false)}
                disabled
                style={{ opacity: 0.5 }}
              >
                {lang === 'es' ? 'Por favor espere...' : 'Please wait...'}
              </Button>
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>

      {/* ================================================================== */}
      {/* CONFIRMATION MODAL FOR DANGEROUS OPTIMIZATIONS */}
      {/* ================================================================== */}
      <AnimatePresence>
        {confirmModal.isOpen && (
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="fixed inset-0 z-[70] flex items-center justify-center p-4"
            onClick={() => setConfirmModal({ isOpen: false, items: [], action: 'apply' })}
            style={{ background: 'rgba(0, 0, 0, 0.7)', backdropFilter: 'blur(8px)' }}
          >
            <motion.div
              initial={{ scale: 0.9, opacity: 0 }}
              animate={{ scale: 1, opacity: 1 }}
              exit={{ scale: 0.9, opacity: 0 }}
              onClick={(e) => e.stopPropagation()}
              className="w-full max-w-lg p-6 rounded-2xl shadow-2xl max-h-[85vh] overflow-y-auto"
              style={{
                background: isDark ? 'rgba(30, 30, 50, 0.98)' : 'rgba(255, 255, 255, 0.98)',
                border: `1px solid ${isDark ? 'rgba(255, 255, 255, 0.1)' : 'rgba(0, 0, 0, 0.1)'}`,
              }}
            >
              {/* Header */}
              <div className="flex items-start gap-4 mb-6">
                <div
                  className="w-14 h-14 rounded-2xl flex items-center justify-center shrink-0"
                  style={{ background: 'rgba(239, 68, 68, 0.15)' }}
                >
                  <ShieldAlert size={28} className="text-red-500" />
                </div>
                <div>
                  <h3
                    className="text-xl font-bold mb-1"
                    style={{ color: isDark ? '#fff' : '#1a1a2e' }}
                  >
                    {confirmModal.action === 'apply'
                      ? (lang === 'es' ? 'Advertencia: Optimizaciones Peligrosas' : 'Warning: Dangerous Optimizations')
                      : (lang === 'es' ? 'Confirmar Reversión' : 'Confirm Reversion')}
                  </h3>
                  <p
                    className="text-sm"
                    style={{ color: isDark ? 'rgba(255,255,255,0.6)' : 'rgba(0,0,0,0.6)' }}
                  >
                    {confirmModal.action === 'apply'
                      ? (lang === 'es'
                        ? 'Las siguientes optimizaciones pueden afectar la estabilidad del sistema.'
                        : 'The following optimizations may affect system stability.')
                      : (lang === 'es'
                        ? 'Estás a punto de revertir las siguientes optimizaciones.'
                        : 'You are about to revert the following optimizations.')}
                  </p>
                </div>
              </div>

              {/* Danger Items List */}
              <div className="space-y-2 mb-6 max-h-48 overflow-y-auto">
                {confirmModal.items.map((item) => (
                  <div
                    key={item.id}
                    className="flex items-center gap-3 p-3 rounded-xl"
                    style={{
                      background: isDark ? 'rgba(239, 68, 68, 0.08)' : 'rgba(239, 68, 68, 0.05)',
                      border: `1px solid ${isDark ? 'rgba(239, 68, 68, 0.2)' : 'rgba(239, 68, 68, 0.15)'}`,
                    }}
                  >
                    <AlertTriangle size={16} className="shrink-0 text-red-500" />
                    <span className="text-sm font-medium truncate" style={{ color: isDark ? '#fff' : '#1a1a2e' }}>
                      {lang === 'es' ? item.nameEs : item.nameEn}
                    </span>
                    <Badge
                      variant="destructive"
                      className="ml-auto shrink-0 text-[10px]"
                    >
                      {lang === 'es' ? 'Peligroso' : 'Dangerous'}
                    </Badge>
                  </div>
                ))}
              </div>

              {/* Risks Information */}
              {confirmModal.action === 'apply' && confirmModal.items.some((i) => i.riskLevel === 'dangerous') && (
                <div
                  className="mb-6 p-4 rounded-xl"
                  style={{
                    background: isDark ? 'rgba(245, 158, 11, 0.1)' : 'rgba(245, 158, 11, 0.05)',
                    border: `1px solid ${isDark ? 'rgba(245, 158, 11, 0.2)' : 'rgba(245, 158, 11, 0.15)'}`,
                  }}
                >
                  <h4 className="flex items-center gap-2 font-semibold text-sm mb-2" style={{ color: '#F59E0B' }}>
                    <Info size={16} />
                    {lang === 'es' ? 'Riesgos Potenciales:' : 'Potential Risks:'}
                  </h4>
                  <ul className="space-y-1.5 text-xs" style={{ color: isDark ? 'rgba(255,255,255,0.7)' : 'rgba(0,0,0,0.7)' }}>
                    <li className="flex items-start gap-2">
                      <Ban size={12} className="mt-0.5 shrink-0 text-red-400" />
                      {lang === 'es'
                        ? 'Algunas optimizaciones pueden desactivar funciones esenciales del sistema'
                        : 'Some optimizations may disable essential system functions'}
                    </li>
                    <li className="flex items-start gap-2">
                      <Ban size={12} className="mt-0.5 shrink-0 text-red-400" />
                      {lang === 'es'
                        ? 'Podría requerir reinstalación de controladores o software'
                        : 'May require reinstallation of drivers or software'}
                    </li>
                    <li className="flex items-start gap-2">
                      <Ban size={12} className="mt-0.5 shrink-0 text-red-400" />
                      {lang === 'es'
                        ? 'Se recomienda crear un punto de restauración antes de continuar'
                        : 'Creating a restore point before continuing is recommended'}
                    </li>
                  </ul>
                </div>
              )}

              {/* Accept Risks Checkbox */}
              {confirmModal.action === 'apply' && confirmModal.items.some((i) => i.riskLevel === 'dangerous') && (
                <label className="flex items-start gap-3 mb-6 cursor-pointer group">
                  <div
                    className="w-5 h-5 rounded mt-0.5 flex items-center justify-center shrink-0 transition-all"
                    style={{
                      background: understoodRisks ? '#EF4444' : (isDark ? 'rgba(255, 255, 255, 0.1)' : 'rgba(0, 0, 0, 0.1)'),
                      border: understoodRisks ? '2px solid #EF4444' : (isDark ? '2px solid rgba(255, 255, 255, 0.2)' : '2px solid rgba(0, 0, 0, 0.2)'),
                    }}
                    onClick={() => setUnderstoodRisks(!understoodRisks)}
                  >
                    {understoodRisks && <Check size={12} className="text-white" />}
                  </div>
                  <span className="text-sm" style={{ color: isDark ? 'rgba(255,255,255,0.8)' : 'rgba(0,0,0,0.8)' }}>
                    {lang === 'es'
                      ? 'Entiendo los riesgos y deseo continuar con mi responsabilidad'
                      : 'I understand the risks and wish to proceed at my own responsibility'}
                  </span>
                </label>
              )}

              {/* Action Buttons */}
              <div className="flex gap-3">
                <Button
                  variant="outline"
                  className="flex-1"
                  onClick={() => {
                    setConfirmModal({ isOpen: false, items: [], action: 'apply' });
                    setUnderstoodRisks(false);
                  }}
                  style={{
                    borderColor: isDark ? 'rgba(255,255,255,0.2)' : 'rgba(0,0,0,0.2)',
                    color: isDark ? 'rgba(255,255,255,0.8)' : 'rgba(0,0,0,0.8)',
                  }}
                >
                  {lang === 'es' ? 'Cancelar' : 'Cancel'}
                </Button>
                <Button
                  className="flex-1 text-white"
                  onClick={confirmAndExecute}
                  disabled={confirmModal.action === 'apply' && !understoodRisks}
                  style={{
                    background:
                      confirmModal.action === 'revert'
                        ? 'linear-gradient(135deg, #6B7280, #9CA3AF)'
                        : 'linear-gradient(135deg, #EF4444, #F87171)',
                    opacity: confirmModal.action === 'apply' && !understoodRisks ? 0.5 : 1,
                  }}
                >
                  {confirmModal.action === 'apply' ? (
                    <>
                      <Zap size={16} className="mr-1" />
                      {lang === 'es' ? 'Aplicar de Todos Modos' : 'Apply Anyway'}
                    </>
                  ) : (
                    <>
                      <RotateCcw size={16} className="mr-1" />
                      {lang === 'es' ? 'Confirmar Reversión' : 'Confirm Revert'}
                    </>
                  )}
                </Button>
              </div>
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>

      {/* CSS for shimmer animation */}
      <style jsx global>{`
        @keyframes shimmer {
          0% { background-position: 200% 0; }
          100% { background-position: -200% 0; }
        }
        .scrollbar-hide::-webkit-scrollbar {
          display: none;
        }
        .scrollbar-hide {
          -ms-overflow-style: none;
          scrollbar-width: none;
        }
        .line-clamp-2 {
          display: -webkit-box;
          -webkit-line-clamp: 2;
          -webkit-box-orient: vertical;
          overflow: hidden;
        }
        .line-clamp-3 {
          display: -webkit-box;
          -webkit-line-clamp: 3;
          -webkit-box-orient: vertical;
          overflow: hidden;
        }
      `}</style>
    </div>
  );
}

// =============================================================================
// SUB-COMPONENTS
// =============================================================================

// Stat Card Component
function StatCard({
  icon,
  value,
  label,
  color,
  isDark,
}: {
  icon: React.ReactNode;
  value: number | string;
  label: string;
  color: string;
  isDark: boolean;
}) {
  return (
    <motion.div
      whileHover={{ scale: 1.02 }}
      className="flex items-center gap-3 px-4 py-2.5 rounded-xl"
      style={{
        background: isDark ? 'rgba(255, 255, 255, 0.06)' : 'rgba(255, 255, 255, 0.9)',
        border: `1px solid ${isDark ? 'rgba(255, 255, 255, 0.08)' : 'rgba(0, 0, 0, 0.08)'}`,
      }}
    >
      <div style={{ color }}>{icon}</div>
      <div>
        <p className="text-lg font-bold" style={{ color: isDark ? '#fff' : '#1a1a2e' }}>
          {value}
        </p>
        <p className="text-[10px] uppercase tracking-wider" style={{ color: isDark ? 'rgba(255,255,255,0.5)' : 'rgba(0,0,0,0.5)' }}>
          {label}
        </p>
      </div>
    </motion.div>
  );
}

// Quick Action Button Component
function QuickActionButton({
  icon,
  label,
  onClick,
  variant,
  disabled,
  isDark,
}: {
  icon: React.ReactNode;
  label: string;
  onClick: () => void;
  variant: 'primary' | 'secondary' | 'danger';
  disabled?: boolean;
  isDark: boolean;
}) {
  const styles = {
    primary: {
      background: 'linear-gradient(135deg, #FF6B35, #ff8c42)',
      color: '#fff',
      boxShadow: '0 4px 15px rgba(255, 107, 53, 0.3)',
    },
    secondary: {
      background: isDark ? 'rgba(255, 255, 255, 0.08)' : 'rgba(0, 0, 0, 0.05)',
      color: isDark ? 'rgba(255,255,255,0.8)' : 'rgba(0,0,0,0.8)',
      border: `1px solid ${isDark ? 'rgba(255, 255, 255, 0.1)' : 'rgba(0, 0, 0, 0.1)'}`,
    },
    danger: {
      background: isDark ? 'rgba(239, 68, 68, 0.1)' : 'rgba(239, 68, 68, 0.05)',
      color: '#EF4444',
      border: `1px solid rgba(239, 68, 68, 0.3)`,
    },
  };

  return (
    <motion.button
      whileHover={{ scale: disabled ? 1 : 1.02 }}
      whileTap={{ scale: disabled ? 1 : 0.98 }}
      onClick={onClick}
      disabled={disabled}
      className="flex items-center gap-2 px-4 py-2.5 rounded-xl text-sm font-medium transition-all disabled:opacity-50 disabled:cursor-not-allowed"
      style={styles[variant]}
    >
      {icon}
      <span className="hidden sm:inline">{label}</span>
    </motion.button>
  );
}

// Optimization Card Component (List View)
const OptimizationCard = memo(function OptimizationCard({
  optimization,
  index,
  isApplied,
  isSelected,
  isExpanded,
  isDark,
  lang,
  isProcessing,
  onSelect,
  onToggleExpand,
  onApply,
  onRevert,
  onRunAgain,
  categoryColor,
}: {
  optimization: OptimizationItem;
  index: number;
  isApplied: boolean;
  isSelected: boolean;
  isExpanded: boolean;
  isDark: boolean;
  lang: 'es' | 'en';
  isProcessing: boolean;
  onSelect: () => void;
  onToggleExpand: () => void;
  onApply: () => void;
  onRevert: () => void;
  onRunAgain: () => void;
  categoryColor: string;
}) {
  const risk = riskConfig[optimization.riskLevel];
  const impact = impactConfig[optimization.performanceImpact];
  const RiskIcon = risk.icon;
  const ImpactIcon = impact.icon;

  return (
    <motion.div
      initial={{ opacity: 0, x: -20 }}
      animate={{ opacity: 1, x: 0 }}
      transition={{ duration: 0.2 }}
      whileHover={{ x: 4 }}
      className="relative overflow-hidden rounded-2xl transition-all"
      style={{
        background: isDark ? 'rgba(30, 30, 50, 0.8)' : 'rgba(255, 255, 255, 0.95)',
        border: `2px solid ${
          isSelected
            ? categoryColor
            : isApplied
            ? 'rgba(16, 185, 129, 0.3)'
            : isDark
            ? 'rgba(255, 255, 255, 0.06)'
            : 'rgba(0, 0, 0, 0.06)'
        }`,
        boxShadow: isSelected
          ? `0 0 20px ${categoryColor}30`
          : isApplied
          ? '0 4px 20px rgba(16, 185, 129, 0.1)'
          : undefined,
      }}
    >
      {/* Applied Indicator Line */}
      {isApplied && (
        <motion.div
          layoutId={`applied-${optimization.id}`}
          className="absolute left-0 top-0 bottom-0 w-1.5"
          style={{ background: 'linear-gradient(to bottom, #10B981, #34D399)' }}
        />
      )}

      {/* Selected Indicator Line */}
      {isSelected && !isApplied && (
        <motion.div
          layoutId={`selected-${optimization.id}`}
          className="absolute left-0 top-0 bottom-0 w-1.5"
          style={{ background: categoryColor }}
        />
      )}

      {/* Main Content */}
      <div className="p-4">
        <div className="flex items-start gap-4">
          {/* Checkbox */}
          <button
            onClick={(e) => {
              e.stopPropagation();
              onSelect();
            }}
            className="mt-1 shrink-0"
          >
            <div
              className="w-5 h-5 rounded-md flex items-center justify-center transition-all"
              style={{
                background: isSelected ? categoryColor : (isDark ? 'rgba(255, 255, 255, 0.1)' : 'rgba(0, 0, 0, 0.1)'),
                border: isSelected ? `2px solid ${categoryColor}` : (isDark ? '2px solid rgba(255, 255, 255, 0.2)' : '2px solid rgba(0, 0, 0, 0.2)'),
              }}
            >
              {isSelected && <Check size={12} className="text-white" />}
            </div>
          </button>

          {/* Icon */}
          <div
            className="w-12 h-12 rounded-xl flex items-center justify-center shrink-0"
            style={{
              background: isApplied
                ? 'rgba(16, 185, 129, 0.15)'
                : `${categoryColor}15`,
            }}
          >
            {(() => { const OptimizationIcon = optimizationIconMap[optimization.id] || Settings2; return <OptimizationIcon
              size={22}
              style={{
                color: isApplied ? '#10B981' : categoryColor,
              }}
            />; })()}
          </div>

          {/* Content */}
          <div className="flex-1 min-w-0">
            {/* Header Row */}
            <div className="flex items-start justify-between gap-2 mb-2">
              <div className="min-w-0">
                <h4
                  className="font-semibold text-base truncate"
                  style={{ color: isDark ? '#fff' : '#1a1a2e' }}
                >
                  {lang === 'es' ? optimization.nameEs : optimization.nameEn}
                </h4>
                <p
                  className="text-sm line-clamp-2"
                  style={{ color: isDark ? 'rgba(255,255,255,0.5)' : 'rgba(0,0,0,0.5)' }}
                >
                  {lang === 'es' ? optimization.descriptionEs : optimization.descriptionEn}
                </p>
              </div>

              {/* Status Badge */}
              <div className="shrink-0">
                {isApplied ? (
                  <div
                    className="flex items-center gap-1 px-2 py-1 rounded-full"
                    style={{ background: 'rgba(16, 185, 129, 0.15)', color: '#10B981' }}
                  >
                    <CheckCircle2 size={14} />
                    <span className="text-xs font-medium">
                      {lang === 'es' ? 'Aplicado' : 'Applied'}
                    </span>
                  </div>
                ) : (
                  <div
                    className="flex items-center gap-1 px-2 py-1 rounded-full"
                    style={{
                      background: isDark ? 'rgba(255, 255, 255, 0.08)' : 'rgba(0, 0, 0, 0.08)',
                      color: isDark ? 'rgba(255,255,255,0.5)' : 'rgba(0,0,0,0.5)',
                    }}
                  >
                    <Clock size={14} />
                    <span className="text-xs font-medium">
                      {lang === 'es' ? 'Pendiente' : 'Pending'}
                    </span>
                  </div>
                )}
              </div>
            </div>

            {/* Badges Row */}
            <div className="flex items-center gap-2 flex-wrap mb-3">
              {/* Risk Badge */}
              <div
                className="flex items-center gap-1 px-2 py-1 rounded-md text-xs font-medium"
                style={{ background: risk.bgColor, color: risk.color, border: `1px solid ${risk.borderColor}` }}
              >
                <RiskIcon size={12} />
                <span>{lang === 'es' ? `Riesgo: ${risk.labelEs}` : `Risk: ${risk.labelEn}`}</span>
              </div>

              {/* Impact Badge */}
              <div
                className="flex items-center gap-1 px-2 py-1 rounded-md text-xs font-medium"
                style={{ background: impact.bgColor, color: impact.color }}
              >
                <ImpactIcon size={12} />
                <span>{lang === 'es' ? `Impacto de rendimiento: ${impact.labelEs}` : `Performance impact: ${impact.labelEn}`}</span>
              </div>

              {/* Reversibility */}
              <div
                className="flex items-center gap-1 px-2 py-1 rounded-md text-xs font-medium"
                style={{
                  background: optimization.reversible !== false ? 'rgba(16, 185, 129, 0.15)' : 'rgba(239, 68, 68, 0.15)',
                  color: optimization.reversible !== false ? '#10B981' : '#EF4444',
                  border: `1px solid ${optimization.reversible !== false ? 'rgba(16, 185, 129, 0.3)' : 'rgba(239, 68, 68, 0.3)'}`,
                }}
              >
                {optimization.reversible !== false ? <RotateCcw size={12} /> : <Ban size={12} />}
                <span>{optimization.reversible !== false
                  ? (lang === 'es' ? 'Reversible' : 'Reversible')
                  : (lang === 'es' ? 'No reversible' : 'Irreversible')}</span>
              </div>

              {/* Restart Required */}
              {optimization.requiresReboot && (
                <div
                  className="flex items-center gap-1 px-2 py-1 rounded-md text-xs font-medium"
                  style={{
                    background: isDark ? 'rgba(245, 158, 11, 0.15)' : 'rgba(245, 158, 11, 0.1)',
                    color: '#F59E0B',
                  }}
                >
                  <RotateCcw size={12} />
                  <span>{lang === 'es' ? 'Reinicio Req.' : 'Restart Req.'}</span>
                </div>
              )}

              {/* Anti-cheat relevance */}
              {optimization.antiCheatRisk === 'possible-compatibility' && (
                <div
                  className="flex items-center gap-1 px-2 py-1 rounded-md text-xs font-medium cursor-help"
                  style={{
                    background: 'rgba(245, 158, 11, 0.15)',
                    color: '#F59E0B',
                    border: '1px solid rgba(245, 158, 11, 0.3)',
                  }}
                  title={lang === 'es' ? optimization.antiCheatWarningEs : optimization.antiCheatWarningEn}
                >
                  <Crosshair size={12} />
                  <span>{lang === 'es' ? 'Nota anti-cheat' : 'Anti-cheat note'}</span>
                </div>
              )}

              {/* Security Impact */}
              {optimization.securityImpact !== 'none' && (
                <div 
                  className="flex items-center gap-1 text-xs cursor-help" 
                  style={{ color: securityConfig[optimization.securityImpact].color }}
                  title={lang === 'es' ? securityConfig[optimization.securityImpact].descEs : securityConfig[optimization.securityImpact].descEn}
                >
                  {(() => {
                    const SecIcon = securityConfig[optimization.securityImpact].icon;
                    return <SecIcon size={12} />;
                  })()}
                  <span>{lang === 'es' ? securityConfig[optimization.securityImpact].labelEs : securityConfig[optimization.securityImpact].labelEn}</span>
                </div>
              )}

              {optimization.antiCheatRisk === 'possible-compatibility' && (
                <div className="basis-full flex items-start gap-1.5 text-xs text-amber-400">
                  <ShieldAlert size={13} className="mt-0.5 shrink-0" />
                  <span>{lang === 'es' ? optimization.antiCheatWarningEs : optimization.antiCheatWarningEn}</span>
                </div>
              )}
            </div>

            {/* Action Buttons Row */}
            <div className="flex items-center gap-2">
              <motion.button
                whileHover={{ scale: 1.02 }}
                whileTap={{ scale: 0.98 }}
                onClick={onToggleExpand}
                className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium transition-colors"
                style={{
                  background: isDark ? 'rgba(255, 255, 255, 0.06)' : 'rgba(0, 0, 0, 0.04)',
                  color: isDark ? 'rgba(255,255,255,0.6)' : 'rgba(0,0,0,0.6)',
                }}
              >
                {isExpanded ? <ChevronUp size={14} /> : <ChevronDown size={14} />}
                {isExpanded
                  ? (lang === 'es' ? 'Menos información' : 'Less info')
                  : (lang === 'es' ? 'Más información' : 'More info')}
              </motion.button>

              {isApplied && !irreversibleOptimizationIds.has(optimization.id) && (
                <motion.button
                  whileHover={{ scale: 1.02 }}
                  whileTap={{ scale: 0.98 }}
                  onClick={(e) => {
                    e.stopPropagation();
                    onRevert();
                  }}
                  disabled={isProcessing}
                  className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium text-white disabled:opacity-50"
                  style={{
                    background: 'linear-gradient(135deg, #EF4444, #F87171)',
                  }}
                >
                  <RotateCcw size={14} />
                  {lang === 'es' ? 'Revertir' : 'Revert'}
                </motion.button>
              )}

              {isApplied && irreversibleOptimizationIds.has(optimization.id) && repeatableOptimizationIds.has(optimization.id) && (
                <motion.button
                  whileHover={{ scale: 1.02 }}
                  whileTap={{ scale: 0.98 }}
                  onClick={(e) => {
                    e.stopPropagation();
                    onRevert();
                  }}
                  disabled={isProcessing}
                  className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium text-white disabled:opacity-50"
                  style={{
                    background: isDark ? 'rgba(255, 255, 255, 0.06)' : 'rgba(0, 0, 0, 0.04)',
                    color: isDark ? 'rgba(255,255,255,0.6)' : 'rgba(0,0,0,0.6)',
                  }}
                >
                  <CheckCircle2 size={14} />
                  {lang === 'es' ? 'Marcar como pendiente' : 'Mark as pending'}
                </motion.button>
              )}

              {isApplied && repeatableOptimizationIds.has(optimization.id) && (
                <motion.button
                  whileHover={{ scale: 1.02 }}
                  whileTap={{ scale: 0.98 }}
                  onClick={(e) => {
                    e.stopPropagation();
                    onRunAgain();
                  }}
                  disabled={isProcessing}
                  className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-semibold text-white disabled:opacity-50"
                  style={{
                    background: 'linear-gradient(135deg, #FF6B35, #ff8c42)',
                  }}
                >
                  <RefreshCw size={14} />
                  {lang === 'es' ? 'Ejecutar de nuevo' : 'Run again'}
                </motion.button>
              )}
            </div>
          </div>
        </div>
      </div>

      {/* Expandable Details Section */}
      <AnimatePresence>
        {isExpanded && (
          <motion.div
            initial={{ height: 0, opacity: 0 }}
            animate={{ height: 'auto', opacity: 1 }}
            exit={{ height: 0, opacity: 0 }}
            transition={{ duration: 0.3 }}
            className="overflow-hidden"
          >
            <div
              className="px-4 pb-4 pt-2 border-t"
              style={{
                borderColor: isDark ? 'rgba(255, 255, 255, 0.06)' : 'rgba(0, 0, 0, 0.06)',
              }}
            >
              {/* What Is It Section */}
              <div className="mb-4">
                <h5
                  className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider mb-2"
                  style={{ color: categoryColor }}
                >
                  <Activity size={14} />
                  {lang === 'es' ? 'Qué hace' : 'What it does'}
                </h5>
                <p
                  className="text-sm leading-relaxed pl-6"
                  style={{ color: isDark ? 'rgba(255,255,255,0.6)' : 'rgba(0,0,0,0.6)' }}
                >
                  {lang === 'es' ? (optimization.whatDoesEs || optimization.descriptionEs) : (optimization.whatDoesEn || optimization.descriptionEn)}
                </p>
              </div>

              {/* What Is It Section */}
              <div className="mb-4">
                <h5
                  className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider mb-2"
                  style={{ color: categoryColor }}
                >
                  <BookOpen size={14} />
                  {lang === 'es' ? '¿Qué es esto?' : 'What is this?'}
                </h5>
                <p
                  className="text-sm leading-relaxed pl-6"
                  style={{ color: isDark ? 'rgba(255,255,255,0.6)' : 'rgba(0,0,0,0.6)' }}
                >
                  {lang === 'es' ? optimization.whatIsItEs : optimization.whatIsItEn}
                </p>
              </div>

              {/* What It Applies To Section */}
              <div className="mb-4">
                <h5
                  className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider mb-2"
                  style={{ color: categoryColor }}
                >
                  <Target size={14} />
                  {lang === 'es' ? 'Qué Afecta' : 'What It Affects'}
                </h5>
                <p
                  className="text-sm leading-relaxed pl-6"
                  style={{ color: isDark ? 'rgba(255,255,255,0.6)' : 'rgba(0,0,0,0.6)' }}
                >
                  {lang === 'es' ? optimization.whatItAppliesEs : optimization.whatItAppliesEn}
                </p>
              </div>

              {/* Registry Keys Section */}
              {optimization.registryKeys && optimization.registryKeys.length > 0 && (
                <div className="mb-4">
                  <h5
                    className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider mb-2"
                    style={{ color: categoryColor }}
                  >
                    <KeyRound size={14} />
                    {lang === 'es' ? 'Claves del Registro' : 'Registry Keys'}
                  </h5>
                  <div className="space-y-1.5 pl-6">
                    {optimization.registryKeys.map((reg, idx) => (
                      <div
                        key={idx}
                        className="flex items-start gap-2 p-2 rounded-lg font-mono text-xs"
                        style={{
                          background: isDark ? 'rgba(255, 255, 255, 0.04)' : 'rgba(0, 0, 0, 0.03)',
                          color: isDark ? 'rgba(255,255,255,0.7)' : 'rgba(0,0,0,0.7)',
                        }}
                      >
                        <code className="truncate">{reg.path}</code>
                        <span style={{ color: categoryColor }}>→</span>
                        <code>{reg.name} = {String(reg.value)}</code>
                        <Badge
                          variant="outline"
                          className="ml-auto shrink-0 text-[10px]"
                          style={{ borderColor: categoryColor, color: categoryColor }}
                        >
                          {reg.type}
                        </Badge>
                      </div>
                    ))}
                  </div>
                </div>
              )}

              {/* Services Section */}
              {optimization.services && optimization.services.length > 0 && (
                <div className="mb-4">
                  <h5
                    className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider mb-2"
                    style={{ color: categoryColor }}
                  >
                    <Server size={14} />
                    {lang === 'es' ? 'Servicios Afectados' : 'Affected Services'}
                  </h5>
                  <div className="flex flex-wrap gap-2 pl-6">
                    {optimization.services.map((svc, idx) => (
                      <div
                        key={idx}
                        className="flex items-center gap-1.5 px-2.5 py-1.5 rounded-lg text-xs font-mono"
                        style={{
                          background:
                            svc.action === 'stop-and-disable'
                              ? 'rgba(239, 68, 68, 0.1)'
                              : 'rgba(245, 158, 11, 0.1)',
                          color:
                            svc.action === 'stop-and-disable'
                              ? '#EF4444'
                              : '#F59E0B',
                          border: `1px solid ${
                            svc.action === 'stop-and-disable'
                              ? 'rgba(239, 68, 68, 0.2)'
                              : 'rgba(245, 158, 11, 0.2)'
                          }`,
                        }}
                      >
                        <Settings2 size={12} />
                        {svc.name}
                        <span className="opacity-70">({svc.action})</span>
                      </div>
                    ))}
                  </div>
                </div>
              )}

              {/* Commands Section */}
              {optimization.commands && optimization.commands.length > 0 && (
                <div className="mb-4">
                  <h5
                    className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider mb-2"
                    style={{ color: categoryColor }}
                  >
                    <Terminal size={14} />
                    {lang === 'es' ? 'Comandos a Ejecutar' : 'Commands to Execute'}
                  </h5>
                  <div className="space-y-1.5 pl-6">
                    {optimization.commands.map((cmd, idx) => (
                      <div
                        key={idx}
                        className="flex items-center gap-2 p-2 rounded-lg font-mono text-xs"
                        style={{
                          background: isDark ? 'rgba(139, 92, 246, 0.1)' : 'rgba(139, 92, 246, 0.05)',
                          color: isDark ? 'rgba(255,255,255,0.7)' : 'rgba(0,0,0,0.7)',
                        }}
                      >
                        <Terminal size={12} style={{ color: '#8B5CF6' }} />
                        <code className="truncate">{cmd}</code>
                        <button
                          onClick={() => navigator.clipboard.writeText(cmd)}
                          className="ml-auto shrink-0 opacity-50 hover:opacity-100 transition-opacity"
                          style={{ color: isDark ? 'rgba(255,255,255,0.5)' : 'rgba(0,0,0,0.5)' }}
                        >
                          <Copy size={12} />
                        </button>
                      </div>
                    ))}
                  </div>
                </div>
              )}

              {/* Additional Info */}
              <div className="grid gap-3 sm:grid-cols-2 mb-4">
                <div className="rounded-lg p-3" style={{ background: isDark ? 'rgba(255,255,255,0.04)' : 'rgba(0,0,0,0.03)' }}>
                  <p className="text-xs font-semibold mb-1" style={{ color: categoryColor }}>{lang === 'es' ? 'Seguridad' : 'Safety'}</p>
                  <p className="text-xs" style={{ color: isDark ? 'rgba(255,255,255,0.65)' : 'rgba(0,0,0,0.65)' }}>
                    {optimization.isSafe ? (lang === 'es' ? 'Sí, con punto de restauración obligatorio.' : 'Yes, with a mandatory restore point.') : (lang === 'es' ? 'No. Puede afectar funciones del sistema.' : 'No. System functions may be affected.')}
                  </p>
                </div>
                <div className="rounded-lg p-3" style={{ background: isDark ? 'rgba(255,255,255,0.04)' : 'rgba(0,0,0,0.03)' }}>
                  <p className="text-xs font-semibold mb-1" style={{ color: categoryColor }}>{lang === 'es' ? 'Cómo lo hace' : 'How it works'}</p>
                  <p className="text-xs" style={{ color: isDark ? 'rgba(255,255,255,0.65)' : 'rgba(0,0,0,0.65)' }}>
                    {lang === 'es' ? optimization.implementationEs : optimization.implementationEn}
                  </p>
                </div>
              </div>
              {optimization.privacyBenefitEs && (
                <div className="mb-4 rounded-lg border border-teal-500/25 bg-teal-500/10 p-3">
                  <p className="text-xs font-semibold text-teal-300 mb-1">{lang === 'es' ? 'Beneficio de privacidad' : 'Privacy benefit'}</p>
                  <p className="text-xs text-teal-100/70">{lang === 'es' ? optimization.privacyBenefitEs : optimization.privacyBenefitEn}</p>
                </div>
              )}
              <div className="grid gap-3 sm:grid-cols-2 mb-4">
                <div className="rounded-lg p-3" style={{ background: isDark ? 'rgba(239,68,68,0.08)' : 'rgba(239,68,68,0.05)' }}>
                  <p className="text-xs font-semibold mb-1" style={{ color: '#EF4444' }}>{lang === 'es' ? 'Impacto en seguridad' : 'Security impact'}</p>
                  <p className="text-xs" style={{ color: isDark ? 'rgba(255,255,255,0.7)' : 'rgba(0,0,0,0.7)' }}>
                    {lang === 'es' ? optimization.securityExplanationEs : optimization.securityExplanationEn}
                  </p>
                </div>
                <div className="rounded-lg p-3" style={{ background: isDark ? 'rgba(16,185,129,0.08)' : 'rgba(16,185,129,0.05)' }}>
                  <p className="text-xs font-semibold mb-1" style={{ color: '#10B981' }}>{lang === 'es' ? 'Rendimiento esperado' : 'Expected performance'}</p>
                  <p className="text-xs" style={{ color: isDark ? 'rgba(255,255,255,0.7)' : 'rgba(0,0,0,0.7)' }}>
                    {lang === 'es' ? optimization.performanceExplanationEs : optimization.performanceExplanationEn}
                  </p>
                </div>
              </div>
              <div className="mb-4 rounded-lg border border-amber-500/25 bg-amber-500/10 p-3">
                <p className="text-xs font-semibold mb-1 text-amber-300">{lang === 'es' ? 'Limitaciones y efectos secundarios' : 'Limitations and side effects'}</p>
                <p className="text-xs text-amber-100/75">
                  {lang === 'es' ? optimization.limitationsEs : optimization.limitationsEn}
                </p>
              </div>
              {optimization.warningEs && (
                <div className="mb-4 rounded-lg border border-red-500/30 bg-red-500/10 p-3 text-xs text-red-300">
                  {lang === 'es' ? optimization.warningEs : optimization.warningEn}
                </div>
              )}
              <div className="mb-4">
                <h5 className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider mb-2" style={{ color: categoryColor }}>
                  <ShieldCheck size={14} />
                  {lang === 'es' ? 'Verificación real' : 'Real verification'}
                </h5>
                <code className="block max-h-24 overflow-auto rounded-lg p-3 pl-6 text-[11px] whitespace-pre-wrap" style={{ background: isDark ? 'rgba(0,0,0,0.25)' : 'rgba(0,0,0,0.05)', color: isDark ? 'rgba(255,255,255,0.65)' : 'rgba(0,0,0,0.65)' }}>
                  {optimization.verificationCommand || (lang === 'es' ? 'No disponible' : 'Not available')}
                </code>
              </div>
              <div className="flex items-center gap-4 pt-3 border-t" style={{ borderColor: isDark ? 'rgba(255, 255, 255, 0.06)' : 'rgba(0, 0, 0, 0.06)' }}>
                <div className="flex items-center gap-1.5 text-xs" style={{ color: optimization.requiresReboot ? '#F59E0B' : '#10B981' }}>
                  <RotateCcw size={12} />
                  {lang === 'es' ? `Reinicio: ${optimization.requiresReboot ? 'Sí' : 'No'}` : `Restart: ${optimization.requiresReboot ? 'Yes' : 'No'}`}
                </div>
                {/* Reversible */}
                <div className="flex items-center gap-1.5 text-xs" style={{ color: optimization.reversible !== false ? '#10B981' : '#EF4444' }}>
                  {optimization.reversible !== false ? <RotateCcw size={12} /> : <Ban size={12} />}
                  {optimization.reversible !== false
                    ? (lang === 'es' ? 'Reversible' : 'Reversible')
                    : (lang === 'es' ? 'Irreversible' : 'Irreversible')}
                </div>

                {/* ID */}
                <div className="flex items-center gap-1.5 text-xs font-mono" style={{ color: isDark ? 'rgba(255,255,255,0.4)' : 'rgba(0,0,0,0.4)' }}>
                  <HashIcon size={12} />
                  {optimization.id}
                </div>
              </div>
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </motion.div>
  );
  });

// Hash icon component
function HashIcon({ size }: { size: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <line x1="4" y1="9" x2="20" y2="9"></line>
      <line x1="4" y1="15" x2="20" y2="15"></line>
      <line x1="10" y1="3" x2="8" y2="21"></line>
      <line x1="16" y1="3" x2="14" y2="21"></line>
    </svg>
  );
}

// Optimization Card Component (Grid View)
const OptimizationCardGrid = memo(function OptimizationCardGrid({
  optimization,
  index,
  isApplied,
  isSelected,
  isExpanded,
  isDark,
  lang,
  isProcessing,
  onSelect,
  onToggleExpand,
  onApply,
  onRevert,
  onRunAgain,
  categoryColor,
}: {
  optimization: OptimizationItem;
  index: number;
  isApplied: boolean;
  isSelected: boolean;
  isExpanded: boolean;
  isDark: boolean;
  lang: 'es' | 'en';
  isProcessing: boolean;
  onSelect: () => void;
  onToggleExpand: () => void;
  onApply: () => void;
  onRevert: () => void;
  onRunAgain: () => void;
  categoryColor: string;
}) {
  const risk = riskConfig[optimization.riskLevel];
  const impact = impactConfig[optimization.performanceImpact];
  const RiskIcon = risk.icon;

  return (
    <motion.div
      initial={{ opacity: 0, scale: 0.95 }}
      animate={{ opacity: 1, scale: 1 }}
      transition={{ delay: index * 0.03 }}
      whileHover={{ y: -4 }}
      className="relative overflow-hidden rounded-2xl transition-all"
      style={{
        background: isDark ? 'rgba(30, 30, 50, 0.8)' : 'rgba(255, 255, 255, 0.95)',
        border: `2px solid ${
          isSelected
            ? categoryColor
            : isApplied
            ? 'rgba(16, 185, 129, 0.3)'
            : isDark
            ? 'rgba(255, 255, 255, 0.06)'
            : 'rgba(0, 0, 0, 0.06)'
        }`,
      }}
    >
      {/* Applied Indicator */}
      {isApplied && (
        <div
          className="absolute top-3 right-3 w-8 h-8 rounded-full flex items-center justify-center"
          style={{ background: 'rgba(16, 185, 129, 0.15)' }}
        >
          <CheckCircle2 size={18} className="text-emerald-500" />
        </div>
      )}

      {/* Card Content */}
      <div className="p-4">
        {/* Header */}
        <div className="flex items-start gap-3 mb-3">
          <button
            onClick={(e) => {
              e.stopPropagation();
              onSelect();
            }}
            className="mt-0.5 shrink-0"
          >
            <div
              className="w-5 h-5 rounded-md flex items-center justify-center transition-all"
              style={{
                background: isSelected ? categoryColor : (isDark ? 'rgba(255, 255, 255, 0.1)' : 'rgba(0, 0, 0, 0.1)'),
                border: isSelected ? `2px solid ${categoryColor}` : (isDark ? '2px solid rgba(255, 255, 255, 0.2)' : '2px solid rgba(0, 0, 0, 0.2)'),
              }}
            >
              {isSelected && <Check size={12} className="text-white" />}
            </div>
          </button>

          <div
            className="w-10 h-10 rounded-xl flex items-center justify-center shrink-0"
            style={{
              background: isApplied ? 'rgba(16, 185, 129, 0.15)' : `${categoryColor}15`,
            }}
          >
            {(() => { const OptimizationIcon = optimizationIconMap[optimization.id] || Settings2; return <OptimizationIcon
              size={18}
              style={{ color: isApplied ? '#10B981' : categoryColor }}
            />; })()}
          </div>

          <div className="flex-1 min-w-0">
            <h4
              className="font-semibold text-sm truncate"
              style={{ color: isDark ? '#fff' : '#1a1a2e' }}
            >
              {lang === 'es' ? optimization.nameEs : optimization.nameEn}
            </h4>
            <p
              className="text-xs line-clamp-2 mt-1"
              style={{ color: isDark ? 'rgba(255,255,255,0.5)' : 'rgba(0,0,0,0.5)' }}
            >
              {lang === 'es' ? optimization.descriptionEs : optimization.descriptionEn}
            </p>
          </div>
        </div>

        {/* Badges */}
        <div className="flex items-center gap-1.5 flex-wrap mb-3">
          <div
            className="flex items-center gap-1 px-1.5 py-0.5 rounded text-[10px] font-medium"
            style={{ background: risk.bgColor, color: risk.color }}
          >
            <RiskIcon size={10} />
            {lang === 'es' ? `Riesgo: ${risk.labelEs}` : `Risk: ${risk.labelEn}`}
          </div>

          <div
            className="flex items-center gap-1 px-1.5 py-0.5 rounded text-[10px] font-medium"
            style={{
              background: optimization.reversible !== false ? 'rgba(16, 185, 129, 0.15)' : 'rgba(239, 68, 68, 0.15)',
              color: optimization.reversible !== false ? '#10B981' : '#EF4444',
            }}
          >
            {optimization.reversible !== false ? <RotateCcw size={10} /> : <Ban size={10} />}
            {optimization.reversible !== false
              ? (lang === 'es' ? 'Reversible' : 'Reversible')
              : (lang === 'es' ? 'No reversible' : 'Irreversible')}
          </div>

          {optimization.requiresReboot && (
            <div
              className="flex items-center gap-1 px-1.5 py-0.5 rounded text-[10px]"
              style={{
                background: isDark ? 'rgba(245, 158, 11, 0.15)' : 'rgba(245, 158, 11, 0.1)',
                color: '#F59E0B',
              }}
            >
              <RotateCcw size={10} />
              {lang === 'es' ? 'Reinicio' : 'Restart'}
            </div>
          )}

          {optimization.antiCheatRisk === 'possible-compatibility' && (
            <div
              className="flex items-center gap-1 px-1.5 py-0.5 rounded text-[10px] font-medium cursor-help"
              style={{
                background: 'rgba(245, 158, 11, 0.15)',
                color: '#F59E0B',
                border: '1px solid rgba(245, 158, 11, 0.3)',
              }}
              title={lang === 'es' ? optimization.antiCheatWarningEs : optimization.antiCheatWarningEn}
            >
              <Crosshair size={10} />
              {lang === 'es' ? 'Anti-cheat' : 'Anti-cheat'}
            </div>
          )}
        </div>

        {/* Actions */}
        <div className="flex items-center gap-2">
          <button
            onClick={onToggleExpand}
            className="flex-1 flex items-center justify-center gap-1 py-2 rounded-lg text-xs font-medium transition-colors"
            style={{
              background: isDark ? 'rgba(255, 255, 255, 0.06)' : 'rgba(0, 0, 0, 0.04)',
              color: isDark ? 'rgba(255,255,255,0.6)' : 'rgba(0,0,0,0.6)',
            }}
          >
            {isExpanded ? <ChevronUp size={14} /> : <ChevronDown size={14} />}
            {lang === 'es' ? 'Detalles' : 'Details'}
          </button>

          {isApplied ? (
            <button
              onClick={(e) => {
                e.stopPropagation();
                onRevert();
              }}
              disabled={isProcessing}
              className="flex-1 flex items-center justify-center gap-1 py-2 rounded-lg text-xs font-medium text-white disabled:opacity-50"
              style={{ background: 'linear-gradient(135deg, #EF4444, #F87171)' }}
            >
              <RotateCcw size={12} />
              {repeatableOptimizationIds.has(optimization.id)
                ? (lang === 'es' ? 'Pendiente' : 'Reset')
                : (lang === 'es' ? 'Revertir' : 'Revert')}
            </button>
          ) : (
            <button
              onClick={(e) => {
                e.stopPropagation();
                onApply();
              }}
              disabled={isProcessing}
              className="flex-1 flex items-center justify-center gap-1 py-2 rounded-lg text-xs font-semibold text-white disabled:opacity-50"
              style={{ background: 'linear-gradient(135deg, #10B981, #34D399)' }}
            >
              <Play size={12} />
              {lang === 'es' ? 'Aplicar' : 'Apply'}
            </button>
          )}

          {isApplied && repeatableOptimizationIds.has(optimization.id) && (
            <button
              onClick={(e) => {
                e.stopPropagation();
                onRunAgain();
              }}
              disabled={isProcessing}
              className="flex-1 flex items-center justify-center gap-1 py-2 rounded-lg text-xs font-semibold text-white disabled:opacity-50"
              style={{ background: 'linear-gradient(135deg, #FF6B35, #ff8c42)' }}
            >
              <RefreshCw size={12} />
              {lang === 'es' ? 'Ejecutar de nuevo' : 'Run again'}
            </button>
          )}
        </div>
      </div>

      {/* Expanded Details (Grid Mode) */}
      <AnimatePresence>
        {isExpanded && (
          <motion.div
            initial={{ height: 0, opacity: 0 }}
            animate={{ height: 'auto', opacity: 1 }}
            exit={{ height: 0, opacity: 0 }}
            transition={{ duration: 0.3 }}
            className="overflow-hidden"
          >
            <div
              className="px-4 pb-4 pt-0 border-t"
              style={{ borderColor: isDark ? 'rgba(255, 255, 255, 0.06)' : 'rgba(0, 0, 0, 0.06)' }}
            >
              <div className="pt-3 space-y-3">
                {/* Anti-cheat verdict */}
                {optimization.antiCheatRisk === 'possible-compatibility' && (
                  <div className="flex items-start gap-2 p-2 rounded-lg text-xs leading-relaxed" style={{ background: 'rgba(245, 158, 11, 0.1)', color: '#F59E0B' }}>
                    <ShieldAlert size={14} className="mt-0.5 shrink-0" />
                    <span>{lang === 'es' ? optimization.antiCheatWarningEs : optimization.antiCheatWarningEn}</span>
                  </div>
                )}

                {/* What Is It */}
                <div>
                  <p className="text-xs leading-relaxed" style={{ color: isDark ? 'rgba(255,255,255,0.6)' : 'rgba(0,0,0,0.6)' }}>
                    {lang === 'es' ? optimization.whatIsItEs : optimization.whatIsItEn}
                  </p>
                </div>

                {/* Registry Keys */}
                {optimization.registryKeys && optimization.registryKeys.length > 0 && (
                  <div className="space-y-1">
                    {optimization.registryKeys.slice(0, 2).map((reg, idx) => (
                      <div
                        key={idx}
                        className="p-1.5 rounded text-[10px] font-mono truncate"
                        style={{
                          background: isDark ? 'rgba(255, 255, 255, 0.04)' : 'rgba(0, 0, 0, 0.03)',
                          color: isDark ? 'rgba(255,255,255,0.6)' : 'rgba(0,0,0,0.6)',
                        }}
                      >
                        {reg.path} → {reg.name}
                      </div>
                    ))}
                    {optimization.registryKeys.length > 2 && (
                      <p className="text-[10px]" style={{ color: isDark ? 'rgba(255,255,255,0.4)' : 'rgba(0,0,0,0.4)' }}>
                        +{optimization.registryKeys.length - 2} more keys...
                      </p>
                    )}
                  </div>
                )}
              </div>
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </motion.div>
  );
});

// Default Export
export default FullOptimizationPanel;
