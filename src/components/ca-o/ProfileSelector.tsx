'use client';

import { useState, useEffect } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { 
  Gamepad2, 
  Briefcase, 
  Leaf, 
  Shield, 
  Settings,
  Check,
  Zap,
  AlertTriangle,
  ChevronRight,
  ChevronDown,
  X,
  Loader2,
  Network,
  Mouse,
  Monitor,
  Cpu,
  Lock,
  Battery,
  Gauge,
  Info,
  Settings2,
} from 'lucide-react';
import { cn } from '@/lib/utils';
import { useAppStore } from '@/store/useAppStore';
import { t } from '@/lib/i18n';
import { getRiskLevel, isExecutableOptimizationId } from '@/lib/optimization-commands';

// ============================================
// Profile Types & Definitions
// ============================================
export interface OptimizationProfile {
  id: string;
  nameKey: string;
  descriptionKey: string;
  icon: string;
  color: string;
  gradient: string;
  glowColor: string;
  optimizationIds: string[];
  category: string;
}

// Predefined profiles with their optimization IDs
export const predefinedProfiles: OptimizationProfile[] = [
  {
    id: 'valorant',
    nameKey: 'profileValorantName',
    descriptionKey: 'profileValorantDesc',
    icon: 'Crosshair',
    color: '#ff4655',
    gradient: 'linear-gradient(135deg, #ff4655 0%, #bd3944 100%)',
    glowColor: 'rgba(255, 70, 85, 0.3)',
    optimizationIds: [
      'power-plan',
      'gaming-mode',
      'enable-hags',
      'max-system-responsiveness',
      'windowed-games-optimization',
      'disable-multiplane-overlay',
      'static-pagefile',
      'memory-compression',
      'disable-cpu-idle',
      'enable-core-parking',
      'disable-power-throttling',
      'disable-superfetch',
      'disable-automatic-maintenance',
      'disable-game-dvr',
      'disable-xbox-gamebar',
      'disable-fullscreen-optimizations',
      'disable-edge-startup-boost',
      'disable-widgets',
      'hide-copilot-button',
      'disable-recall',
      'disable-bits',
      'disable-delivery-optimization',
      'disable-network-throttling',
      'optimize-network-power',
      'disable-background-apps',
      'disable-active-probing',
    ],
    category: 'performance',
  },
  {
    id: 'fortnite',
    nameKey: 'profileFortniteName',
    descriptionKey: 'profileFortniteDesc',
    icon: 'Zap',
    color: '#8b5cf6',
    gradient: 'linear-gradient(135deg, #8b5cf6 0%, #6d28d9 100%)',
    glowColor: 'rgba(139, 92, 246, 0.3)',
    optimizationIds: [
      'power-plan',
      'gaming-mode',
      'enable-hags',
      'disable-memory-integrity',
      'max-system-responsiveness',
      'windowed-games-optimization',
      'disable-multiplane-overlay',
      'static-pagefile',
      'memory-compression',
      'disable-cpu-idle',
      'enable-core-parking',
      'disable-power-throttling',
      'disable-superfetch',
      'disable-automatic-maintenance',
      'disable-game-dvr',
      'disable-xbox-gamebar',
      'disable-fullscreen-optimizations',
      'disable-edge-startup-boost',
      'disable-widgets',
      'hide-copilot-button',
      'disable-recall',
      'disable-bits',
      'disable-delivery-optimization',
      'disable-network-throttling',
      'optimize-network-power',
      'disable-background-apps',
      'disable-hotspot-service',
      'disable-peer-name-resolution',
    ],
    category: 'performance',
  },
  {
    id: 'clean-system',
    nameKey: 'profileCleanSystemName',
    descriptionKey: 'profileCleanSystemDesc',
    icon: 'Shield',
    color: '#14b8a6',
    gradient: 'linear-gradient(135deg, #14b8a6 0%, #0f766e 100%)',
    glowColor: 'rgba(20, 184, 166, 0.3)',
    optimizationIds: [
      'disable-telemetry',
      'disable-cortana',
      'disable-search-indexing',
      'disable-superfetch',
      'disable-ceip-tasks',
      'disable-error-reporting',
      'disable-delivery-optimization',
      'disable-windows-insider',
      'disable-retail-demo',
      'disable-advertising-id',
      'disable-tailored-experiences',
      'disable-activity-history',
      'disable-windows-feedback',
      'disable-cloud-content',
      'disable-app-suggestions',
      'disable-start-tracking',
      'disable-setting-sync',
      'disable-input-personalization',
      'disable-handwriting-data',
      'disable-speech-recognition',
      'disable-find-my-device',
      'disable-welcome-experience',
      'disable-clipboard-history',
      'disable-clipboard-cloud-sync',
    ],
    category: 'privacy',
  },
  {
    id: 'gaming',
    nameKey: 'profileGamingName',
    descriptionKey: 'profileGamingDesc',
    icon: 'Gamepad2',
    color: '#ef4444',
    gradient: 'linear-gradient(135deg, #ef4444 0%, #dc2626 100%)',
    glowColor: 'rgba(239, 68, 68, 0.3)',
    optimizationIds: [
      'gaming-mode',
      'mouse-acceleration',
      'keyboard-rate',
      'disable-sticky-keys',
      'disable-filter-keys',
      'disable-controller-gamebar-chord',
      'usb-selective-suspend-off-placeholder',
    ].filter((id) => id !== 'usb-selective-suspend-off-placeholder'),
    category: 'performance',
  },
  {
    id: 'productivity',
    nameKey: 'profileProductivityName',
    descriptionKey: 'profileProductivityDesc',
    icon: 'Briefcase',
    color: '#3b82f6',
    gradient: 'linear-gradient(135deg, #3b82f6 0%, #2563eb 100%)',
    glowColor: 'rgba(59, 130, 246, 0.3)',
    optimizationIds: [
      'animations',
      'transparency',
      'speedup-shutdown',
      'no-auto-reboot-active',
      'show-file-extensions',
      'disable-start-menu-suggestions',
      'disable-taskbar-search',
      'hide-copilot-button',
    ],
    category: 'balanced',
  },
];

// Detail info for each optimization that profiles reference
const optimizationDetails: Record<string, { nameEs: string; nameEn: string; icon: React.ComponentType<{ size?: number; className?: string }>; safe: boolean; category: string }> = {
  'dns-optimization': { nameEs: 'DNS Cloudflare 1.1.1.1', nameEn: 'Cloudflare DNS 1.1.1.1', icon: Network, safe: true, category: 'network' },
  'mouse-acceleration': { nameEs: 'Desactivar Aceleración del Ratón', nameEn: 'Disable Mouse Acceleration', icon: Mouse, safe: true, category: 'input' },
  'keyboard-rate': { nameEs: 'Tasa de Repetición del Teclado', nameEn: 'Keyboard Repeat Rate', icon: Mouse, safe: true, category: 'input' },
  'touchpad-latency': { nameEs: 'Reducir Latencia del Touchpad', nameEn: 'Reduce Touchpad Latency', icon: Mouse, safe: true, category: 'input' },
  'mouse-polling': { nameEs: 'Tasa de Muestreo del Ratón', nameEn: 'Mouse Polling Rate', icon: Mouse, safe: true, category: 'input' },
  'gaming-mode': { nameEs: 'Modo Gaming de Windows', nameEn: 'Windows Gaming Mode', icon: Cpu, safe: true, category: 'system' },
  'disable-xbox-gamebar': { nameEs: 'Desactivar Xbox Game Bar', nameEn: 'Disable Xbox Game Bar', icon: Cpu, safe: true, category: 'system' },
  'disable-telemetry': { nameEs: 'Desactivar Telemetría', nameEn: 'Disable Telemetry', icon: Shield, safe: true, category: 'system' },
  'animations': { nameEs: 'Desactivar Animaciones', nameEn: 'Disable Animations', icon: Monitor, safe: true, category: 'visual' },
  'transparency': { nameEs: 'Desactivar Transparencia', nameEn: 'Disable Transparency', icon: Monitor, safe: true, category: 'visual' },
  'shadows': { nameEs: 'Desactivar Sombras', nameEn: 'Disable Shadows', icon: Monitor, safe: true, category: 'visual' },
  'notifications': { nameEs: 'Optimizar Notificaciones', nameEn: 'Optimize Notifications', icon: Monitor, safe: true, category: 'visual' },
  'optimize-startup': { nameEs: 'Optimizar Inicio', nameEn: 'Optimize Startup', icon: Zap, safe: true, category: 'system' },
  'memory-compression': { nameEs: 'Compresión de Memoria', nameEn: 'Memory Compression', icon: Cpu, safe: true, category: 'system' },
  'clear-temp-files': { nameEs: 'Limpiar Temporales', nameEn: 'Clear Temp Files', icon: Cpu, safe: true, category: 'system' },
  'disable-search-indexing': { nameEs: 'Desactivar Indexado', nameEn: 'Disable Indexing', icon: Cpu, safe: true, category: 'system' },
  'power-plan': { nameEs: 'Alto Rendimiento', nameEn: 'High Performance', icon: Battery, safe: true, category: 'system' },
  'disable-cortana': { nameEs: 'Desactivar Cortana', nameEn: 'Disable Cortana', icon: Lock, safe: true, category: 'system' },
  'disable-superfetch': { nameEs: 'Desactivar Superfetch', nameEn: 'Disable Superfetch', icon: Cpu, safe: true, category: 'system' },
  'disable-print-spooler': { nameEs: 'Desactivar Print Spooler', nameEn: 'Disable Print Spooler', icon: Shield, safe: true, category: 'system' },
  'tweakNotificationBalloons': { nameEs: 'Silenciar Globos', nameEn: 'Silence Balloons', icon: Monitor, safe: true, category: 'visual' },
  'registry-cleanup': { nameEs: 'Limpieza del Registro', nameEn: 'Registry Cleanup', icon: Cpu, safe: false, category: 'system' },
  'disable-services': { nameEs: 'Desactivar Servicios', nameEn: 'Disable Services', icon: Cpu, safe: false, category: 'system' },
};

// Detailed benefits for each profile
const profileDetailedInfo: Record<string, { benefitsEs: string[]; benefitsEn: string[]; bestForEs: string; bestForEn: string; warningEs?: string; warningEn?: string }> = {
  gaming: {
    benefitsEs: ['Reduce ping hasta 30% en juegos online', 'Elimina microstutters', 'Ratón 1:1 sin aceleración', 'CPU/GPU dedicados al juego', 'Sin interrupciones de Game Bar'],
    benefitsEn: ['Reduces ping up to 30% in online games', 'Eliminates microstutters', '1:1 mouse without acceleration', 'CPU/GPU dedicated to the game', 'No Game Bar interruptions'],
    bestForEs: 'FPS, MOBA, Battle Royale y competitivos online',
    bestForEn: 'FPS, MOBA, Battle Royale and competitive online',
    warningEs: 'Requiere reinicio. Xbox Game Bar quedará desactivado.',
    warningEn: 'Requires restart. Xbox Game Bar will be deactivated.',
  },
  productivity: {
    benefitsEs: ['Windows arranca más rápido', 'Interfaz más limpia', 'Mayor RAM disponible', 'Disco más rápido sin indexado', 'DNS más rápida'],
    benefitsEn: ['Windows starts faster', 'Cleaner interface', 'More RAM available', 'Faster disk without indexing', 'Faster DNS'],
    bestForEs: 'Desarrolladores, diseñadores, editores de video',
    bestForEn: 'Developers, designers, video editors',
  },
  powersaver: {
    benefitsEs: ['Maximiza duración de batería', 'Reduce calor del sistema', 'Menos procesos = menos RAM', 'Sin efectos visuales pesados', 'Mejor privacidad'],
    benefitsEn: ['Maximizes battery life', 'Reduces system heat', 'Fewer processes = less RAM', 'No heavy visual effects', 'Better privacy'],
    bestForEs: 'Usuarios de portátiles, equipos limitados',
    bestForEn: 'Laptop users, limited machines',
    warningEs: 'Requiere reinicio.',
    warningEn: 'Requires restart.',
  },
  privacy: {
    benefitsEs: ['Telemetría de Microsoft desactivada', 'Cortana sin recopilar datos', 'Sin indexado de archivos', 'Archivos temporales eliminados', 'Registro limpio'],
    benefitsEn: ['Microsoft telemetry disabled', 'Cortana not collecting data', 'No file indexing', 'Temp files deleted', 'Clean registry'],
    bestForEs: 'Usuarios conscientes de privacidad, profesionales',
    bestForEn: 'Privacy-conscious users, professionals',
    warningEs: 'Búsqueda de Windows será más lenta.',
    warningEn: 'Windows search will be slower.',
  },
};

const categoryColors: Record<string, string> = {
  network: '#3b82f6', input: '#f59e0b', system: '#6366f1', visual: '#ec4899', privacy: '#a855f7',
  tweaks: '#ec4899', advanced: '#ef4444', powerful: '#ef4444',
};

const humanizeId = (id: string) => id.replace(/-/g, ' ').replace(/\b\w/g, (c) => c.toUpperCase());

// Icon mapping
const iconMap: Record<string, React.ComponentType<{ size?: number; className?: string }>> = {
  Gamepad2,
  Briefcase,
  Leaf,
  Shield,
  Settings
};

// ============================================
// Optimization Row Component
// ============================================
function OptimizationRow({ optId, isApplied, language, isDark }: { optId: string; isApplied: boolean; language: 'es'|'en'; isDark: boolean }) {
  const storeCategory = useAppStore((s) => s.optimizations.find(o => o.id === optId)?.category);
  const humanName = optId.replace(/-/g, ' ').replace(/\b\w/g, (c) => c.toUpperCase());
  const detail = optimizationDetails[optId] ?? {
    nameEs: humanName,
    nameEn: humanName,
    icon: Settings2,
    safe: getRiskLevel(optId) === 'safe',
    category: (storeCategory as unknown as string) || 'system',
  };
  const Icon = detail.icon;
  const name = language === 'es' ? detail.nameEs : detail.nameEn;
  const categoryColor = categoryColors[detail.category] || '#6366f1';
  return (
    <div className="flex items-center gap-2.5 py-2 px-3 rounded-xl" style={{ background: isDark ? 'rgba(255,255,255,0.03)' : 'rgba(0,0,0,0.03)', border: `1px solid ${isDark ? 'rgba(255,255,255,0.05)' : 'rgba(0,0,0,0.05)'}` }}>
      <div className="w-6 h-6 rounded-lg flex items-center justify-center flex-shrink-0" style={{ background: `${categoryColor}20` }}>
        <Icon size={12} />
      </div>
      <span className="text-xs font-medium flex-1 min-w-0 truncate" style={{ color: isDark ? 'rgba(255,255,255,0.75)' : 'rgba(0,0,0,0.75)' }}>{name}</span>
      {isApplied && <div className="w-4 h-4 rounded-full bg-emerald-500/20 flex items-center justify-center flex-shrink-0"><Check size={9} className="text-emerald-400" /></div>}
      {!detail.safe && <div className="w-4 h-4 rounded-full bg-amber-500/20 flex items-center justify-center flex-shrink-0"><AlertTriangle size={9} className="text-amber-400" /></div>}
    </div>
  );
}

function ElapsedTicker() {
  const [seconds, setSeconds] = useState(0);
  useEffect(() => {
    const t = setInterval(() => setSeconds((s) => s + 1), 1000);
    return () => clearInterval(t);
  }, []);
  const mm = String(Math.floor(seconds / 60)).padStart(2, '0');
  const ss = String(seconds % 60).padStart(2, '0');
  return <span className="font-mono opacity-70">({mm}:{ss})</span>;
}

// ============================================
// Confirmation Dialog Component
// ============================================
interface ConfirmDialogProps {
  profile: OptimizationProfile | null;
  onConfirm: (createBackup: boolean) => void;
  onCancel: () => void;
  isProcessing: boolean;
  language: 'es' | 'en';
  isDark: boolean;
  appliedIds: Set<string>;
  progress?: { done: number; total: number; current: string } | null;
}

function ConfirmDialog({ profile, onConfirm, onCancel, isProcessing, language, isDark, appliedIds, progress }: ConfirmDialogProps) {
  const [createBackup, setCreateBackup] = useState(true);

  if (!profile) return null;

  const progressLabel = progress
    ? progress.current === '__backup__'
      ? (language === 'es' ? 'Creando punto de restauración…' : 'Creating restore point…')
      : `${progress.done}/${progress.total} — ${humanizeId(progress.current)}`
    : null;

  const IconComponent = iconMap[profile.icon] || Settings;
  const detailedInfo = profileDetailedInfo[profile.id];
  const benefits = language === 'es' ? detailedInfo?.benefitsEs : detailedInfo?.benefitsEn;
  const bestFor = language === 'es' ? detailedInfo?.bestForEs : detailedInfo?.bestForEn;
  const warning = language === 'es' ? detailedInfo?.warningEs : detailedInfo?.warningEn;
  const pendingCount = profile.optimizationIds.filter(id => !appliedIds.has(id)).length;

  return (
    <motion.div
      initial={{ opacity: 0 }}
      animate={{ opacity: 1 }}
      exit={{ opacity: 0 }}
      className="fixed inset-0 z-50 flex items-center justify-center p-4"
      onClick={onCancel}
    >
      {/* Backdrop */}
      <div className="absolute inset-0 bg-black/70 backdrop-blur-sm" />
      
      {/* Dialog */}
      <motion.div
        initial={{ scale: 0.95, opacity: 0, y: 20 }}
        animate={{ scale: 1, opacity: 1, y: 0 }}
        exit={{ scale: 0.95, opacity: 0, y: 20 }}
        transition={{ type: 'spring', duration: 0.4 }}
        onClick={(e) => e.stopPropagation()}
        className="relative w-full max-w-lg rounded-2xl overflow-hidden max-h-[90vh] flex flex-col"
        style={{
          background: isDark ? 'rgba(15, 15, 28, 0.98)' : 'rgba(255, 255, 255, 0.98)',
          border: `1px solid ${isDark ? 'rgba(255,255,255,0.08)' : 'rgba(0,0,0,0.08)'}`,
          boxShadow: `0 30px 60px -12px rgba(0,0,0,0.6), 0 0 60px ${profile.glowColor}`,
        }}
      >
        {/* Gradient header bar */}
        <div className="h-1" style={{ background: profile.gradient }} />
        
        <div className="p-6 overflow-y-auto flex-1">
          {/* Header */}
          <div className="flex items-start gap-4 mb-5">
            <motion.div 
              className="w-14 h-14 rounded-2xl flex items-center justify-center flex-shrink-0"
              style={{ background: `${profile.color}18`, border: `1px solid ${profile.color}30` }}
              whileHover={{ rotate: 5, scale: 1.05 }}
            >
              <span style={{ color: profile.color }}><IconComponent size={28} className="text-current" /></span>
            </motion.div>
            
            <div className="flex-1 min-w-0">
              <h3 className="text-xl font-bold mb-1" style={{ color: isDark ? '#fff' : '#1a1a2e' }}>
                {t(profile.nameKey, language)}
              </h3>
              <p className="text-sm leading-relaxed" style={{ color: isDark ? 'rgba(255,255,255,0.5)' : 'rgba(0,0,0,0.5)' }}>
                {t(profile.descriptionKey, language)}
              </p>
            </div>
            
            <button
              onClick={onCancel}
              className="w-8 h-8 rounded-lg flex items-center justify-center hover:bg-white/[0.05] transition-colors flex-shrink-0"
              disabled={isProcessing}
            >
              <X size={16} style={{ color: isDark ? 'rgba(255,255,255,0.5)' : 'rgba(0,0,0,0.5)' }} />
            </button>
          </div>

          {/* Best for */}
          {bestFor && (
            <div className="flex items-start gap-2 p-3 rounded-xl mb-4" style={{ background: `${profile.color}10`, border: `1px solid ${profile.color}20` }}>
              <Gauge size={14} className="flex-shrink-0 mt-0.5" style={{ color: profile.color } as React.CSSProperties} />
              <span className="text-xs leading-relaxed" style={{ color: profile.color }}>
                <strong>{language === 'es' ? 'Ideal para: ' : 'Best for: '}</strong>{bestFor}
              </span>
            </div>
          )}

          {/* Benefits */}
          {benefits && (
            <div className="mb-4">
              <p className="text-xs font-semibold uppercase tracking-wider mb-2" style={{ color: isDark ? 'rgba(255,255,255,0.4)' : 'rgba(0,0,0,0.4)' }}>
                {language === 'es' ? '✨ Beneficios' : '✨ Benefits'}
              </p>
              <div className="space-y-1.5">
                {benefits.map((b, i) => (
                  <div key={i} className="flex items-start gap-2">
                    <Check size={12} className="flex-shrink-0 mt-0.5" style={{ color: profile.color } as React.CSSProperties} />
                    <span className="text-xs" style={{ color: isDark ? 'rgba(255,255,255,0.65)' : 'rgba(0,0,0,0.65)' }}>{b}</span>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Optimizations list */}
          <div className="mb-4">
            <div className="flex items-center justify-between mb-2">
              <p className="text-xs font-semibold uppercase tracking-wider" style={{ color: isDark ? 'rgba(255,255,255,0.4)' : 'rgba(0,0,0,0.4)' }}>
                {language === 'es' ? '⚡ Optimizaciones incluidas' : '⚡ Included optimizations'}
              </p>
              <span className="text-xs px-2 py-0.5 rounded-full" style={{ background: `${profile.color}15`, color: profile.color }}>
                {pendingCount} {language === 'es' ? 'pendientes' : 'pending'}
              </span>
            </div>
            <div className="space-y-1.5 max-h-44 overflow-y-auto pr-1">
              {profile.optimizationIds.map((optId) => (
                <OptimizationRow
                  key={optId}
                  optId={optId}
                  isApplied={appliedIds.has(optId)}
                  language={language}
                  isDark={isDark}
                />
              ))}
            </div>
          </div>
          
          {/* Warning note */}
          {warning && (
            <div className="flex items-start gap-2 p-3 rounded-xl mb-4 bg-amber-500/10 border border-amber-500/20">
              <AlertTriangle size={14} className="text-amber-400 mt-0.5 flex-shrink-0" />
              <span className="text-xs text-amber-400/80 leading-relaxed">{warning}</span>
            </div>
          )}

          {/* Info note */}
          <div className="flex items-start gap-2 p-3 rounded-xl mb-4" style={{ background: isDark ? 'rgba(255,255,255,0.03)' : 'rgba(0,0,0,0.03)' }}>
            <Info size={13} className="flex-shrink-0 mt-0.5" style={{ color: isDark ? 'rgba(255,255,255,0.35)' : 'rgba(0,0,0,0.35)' } as React.CSSProperties} />
            <span className="text-xs leading-relaxed" style={{ color: isDark ? 'rgba(255,255,255,0.4)' : 'rgba(0,0,0,0.4)' }}>
              {language === 'es'
                ? 'Las optimizaciones ya aplicadas serán omitidas. Todas se guardan en la base de datos y persisten entre reinicios.'
                : 'Already applied optimizations will be skipped. All are saved in the database and persist across restarts.'}
            </span>
          </div>

          {/* Backup Checkbox */}
          <div 
            className="flex items-start gap-3 p-4 rounded-xl mb-6 cursor-pointer border transition-colors"
            style={{ 
              background: createBackup ? (isDark ? 'rgba(255, 107, 53, 0.1)' : 'rgba(255, 107, 53, 0.05)') : 'transparent',
              borderColor: createBackup ? '#FF6B35' : (isDark ? 'rgba(255,255,255,0.1)' : 'rgba(0,0,0,0.1)')
            }}
            onClick={() => setCreateBackup(!createBackup)}
          >
            <div className={cn(
              "w-5 h-5 rounded flex items-center justify-center border shrink-0 mt-0.5",
              createBackup ? "bg-[#FF6B35] border-[#FF6B35]" : "border-gray-500"
            )}>
              {createBackup && <Check size={14} className="text-white" />}
            </div>
            <div>
              <p className="text-sm font-semibold" style={{ color: isDark ? '#fff' : '#1a1a2e' }}>
                {language === 'es' ? 'Crear punto de restauración (Obligatorio)' : 'Create restore point (Required)'}
              </p>
              <p className="text-xs mt-1" style={{ color: isDark ? 'rgba(255,255,255,0.5)' : 'rgba(0,0,0,0.5)' }}>
                {language === 'es' ? 'Recomendado para poder revertir cambios fácilmente si algo sale mal.' : 'Recommended to easily revert changes if something goes wrong.'}
              </p>
            </div>
          </div>
          
          {/* Action buttons */}
            {/* Progress en vivo */}
            {isProcessing && progressLabel && (
              <div className="mb-4 px-4 py-2.5 rounded-xl text-xs font-medium flex items-center justify-between" style={{ background: 'rgba(255,107,53,0.12)', color: '#FF6B35' }}>
                <span className="truncate">{progressLabel}</span>
                {progress && progress.current !== '__backup__' && (
                  <span className="font-mono ml-3 shrink-0">{Math.round((progress.done / Math.max(progress.total, 1)) * 100)}%</span>
                )}
              </div>
            )}

            <div className="flex gap-3">
              <button
                onClick={onCancel}
              disabled={isProcessing}
              className={cn(
                'flex-1 py-3 px-4 rounded-xl text-sm font-semibold transition-all',
                'border',
                isDark 
                  ? 'border-white/10 text-white/70 hover:bg-white/5' 
                  : 'border-gray-200 text-gray-600 hover:bg-gray-50'
              )}
            >
              {t('cancel', language)}
            </button>
            
            <motion.button
              onClick={() => onConfirm(createBackup)}
              disabled={isProcessing || !createBackup}
              whileHover={{ scale: (isProcessing || !createBackup) ? 1 : 1.02 }}
              whileTap={{ scale: (isProcessing || !createBackup) ? 1 : 0.98 }}
              className={cn(
                'flex-1 py-3 px-4 rounded-xl text-sm font-semibold transition-all',
                'text-white flex items-center justify-center gap-2',
                !createBackup && 'opacity-50 cursor-not-allowed grayscale'
              )}
              style={{ background: profile.gradient }}
            >
              {isProcessing ? (
                <>
                  <Loader2 size={16} className="animate-spin" />
                  {t('processing', language)}...
                  {progress && <span className="font-mono opacity-90">{progress.done}/{progress.total}</span>}
                  <ElapsedTicker />
                </>
              ) : (
                <>
                  <Zap size={16} />
                  {language === 'es' ? `Aplicar ${pendingCount} optimizaciones` : `Apply ${pendingCount} optimizations`}
                </>
              )}
            </motion.button>
          </div>
        </div>
      </motion.div>
    </motion.div>
  );
}

// ============================================
// Profile Card Component
// ============================================
interface ProfileCardProps {
  profile: OptimizationProfile;
  isSelected: boolean;
  appliedCount: number;
  totalCount: number;
  onSelect: () => void;
  onApply: () => void;
  index: number;
  language: 'es' | 'en';
  isDark: boolean;
  isProcessing: boolean;
  appliedIds: Set<string>;
}

function ProfileCard({
  profile,
  isSelected,
  appliedCount,
  totalCount,
  onSelect,
  onApply,
  index,
  language,
  isDark,
  isProcessing,
  appliedIds,
}: ProfileCardProps) {
  const [showOptList, setShowOptList] = useState(false);
  const IconComponent = iconMap[profile.icon] || Settings;
  const progressPercent = Math.round((appliedCount / totalCount) * 100);
  const detailedInfo = profileDetailedInfo[profile.id];
  const bestFor = language === 'es' ? detailedInfo?.bestForEs : detailedInfo?.bestForEn;
  
  return (
    <motion.div
      initial={{ opacity: 0, y: 30 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ delay: index * 0.1, duration: 0.5, ease: [0.4, 0, 0.2, 1] }}
      whileHover={{ y: -8, transition: { duration: 0.2 } }}
      onClick={onSelect}
      className={cn(
        "relative p-5 rounded-2xl cursor-pointer overflow-hidden group transition-all duration-300",
        isSelected && "ring-2 ring-offset-2 ring-offset-transparent"
      )}
      style={{
        background: isDark ? 'rgba(20, 20, 35, 0.6)' : 'rgba(255, 255, 255, 0.85)',
        borderColor: isSelected ? profile.color : (isDark ? 'rgba(255,255,255,0.06)' : 'rgba(0,0,0,0.06)'),
        borderWidth: '1px',
        backdropFilter: 'blur(20px)',
        boxShadow: isSelected ? `0 0 30px ${profile.glowColor}` : undefined,
        ...(isSelected ? { ringOffsetColor: isDark ? '#0f0f1a' : '#ffffff' } : {})
      }}
    >
      {/* Gradient accent at top */}
      <div 
        className="absolute top-0 left-0 right-0 h-1 origin-left"
        style={{ background: profile.gradient }}
      />
      
      {/* Hover glow effect */}
      <motion.div
        className="absolute inset-0 opacity-0 group-hover:opacity-100 transition-opacity duration-500 pointer-events-none"
        style={{
          background: `radial-gradient(circle at 50% 0%, ${profile.glowColor}, transparent 70%)`,
        }}
      />
      
      {/* Content */}
      <div className="relative z-10">
        {/* Header row */}
        <div className="flex items-start justify-between mb-4">
          {/* Icon */}
          <motion.div
            className={cn(
              "w-12 h-12 rounded-xl flex items-center justify-center relative"
            )}
            style={{ background: `${profile.color}18` }}
            whileHover={{ rotate: 8, scale: 1.1 }}
          >
            <span style={{ color: profile.color }}><IconComponent size={24} className="text-current" /></span>
            
            {/* Pulse animation when selected */}
            {isSelected && (
              <motion.div
                className="absolute inset-0 rounded-xl"
                animate={{ 
                  opacity: [0.2, 0.5, 0.2],
                  scale: [1, 1.05, 1]
                }}
                transition={{ 
                  duration: 2, 
                  repeat: Infinity 
                }}
                style={{
                  background: `${profile.color}25`,
                  filter: 'blur(8px)'
                }}
              />
            )}
          </motion.div>
          
          {/* Selection indicator */}
          <motion.div
            initial={false}
            animate={isSelected ? { scale: [0, 1.2, 1] } : {}}
            className={cn(
              "w-6 h-6 rounded-full flex items-center justify-center border-2 transition-colors",
              isSelected ? "bg-[#FF6B35] border-[#FF6B35]" : "border-gray-400/30"
            )}
          >
            {isSelected && (
              <motion.svg
                width="12"
                height="12"
                viewBox="0 0 24 24"
                fill="none"
                stroke="white"
                strokeWidth="3"
                strokeLinecap="round"
                strokeLinejoin="round"
                initial={{ pathLength: 0 }}
                animate={{ pathLength: 1 }}
                transition={{ duration: 0.2 }}
              >
                <polyline points="20 6 9 17 4 12"></polyline>
              </motion.svg>
            )}
          </motion.div>
        </div>
        
        {/* Title and Description */}
        <div onClick={onSelect} className="cursor-pointer flex-1">
          <h4 className="text-base font-bold mb-1 tracking-tight" style={{ color: isDark ? '#fff' : '#1a1a2e' }}>
            {t(profile.nameKey, language)}
          </h4>
          
          <p className="text-xs leading-relaxed mb-2 line-clamp-2" style={{ color: isDark ? 'rgba(255,255,255,0.45)' : 'rgba(0,0,0,0.45)' }}>
            {t(profile.descriptionKey, language)}
          </p>

          {bestFor && (
            <p className="text-xs mb-3 line-clamp-1 font-medium" style={{ color: profile.color }}>
              {language === 'es' ? 'Para: ' : 'For: '}{bestFor}
            </p>
          )}
        </div>
        
        <div className="space-y-2 mb-3">
          <div className="flex items-center justify-between text-xs">
            <span style={{ color: isDark ? 'rgba(255,255,255,0.4)' : 'rgba(0,0,0,0.4)' }}>
              {language === 'es' ? 'Progreso' : 'Progress'}
            </span>
            <span className="font-medium" style={{ color: profile.color }}>
              {appliedCount}/{totalCount}
            </span>
          </div>
          <div className="h-1.5 bg-white/[0.05] rounded-full overflow-hidden">
            <motion.div
              className="h-full rounded-full"
              style={{ background: profile.gradient }}
              initial={{ width: 0 }}
              animate={{ width: `${progressPercent}%` }}
              transition={{ duration: 0.8, ease: [0.4, 0, 0.2, 1], delay: 0.2 + index * 0.1 }}
            />
          </div>
        </div>

        {/* Expand optimizations list */}
        <button
          onClick={(e) => {
            e.stopPropagation();
            setShowOptList(!showOptList);
          }}
          className="flex items-center gap-1.5 text-xs mb-3 transition-colors"
          style={{ color: isDark ? 'rgba(255,255,255,0.4)' : 'rgba(0,0,0,0.4)' }}
        >
          {showOptList ? <ChevronDown size={12} /> : <ChevronRight size={12} />}
          {totalCount} {language === 'es' ? 'optimizaciones' : 'optimizations'}
        </button>

        <AnimatePresence>
          {showOptList && (
            <motion.div
              initial={{ height: 0, opacity: 0 }}
              animate={{ height: 'auto', opacity: 1 }}
              exit={{ height: 0, opacity: 0 }}
              transition={{ duration: 0.25 }}
              className="overflow-hidden mb-3"
            >
              <div className="space-y-1 max-h-48 overflow-y-auto pr-1">
                {profile.optimizationIds.map((optId) => (
                  <OptimizationRow
                    key={optId}
                    optId={optId}
                    isApplied={appliedIds.has(optId)}
                    language={language}
                    isDark={isDark}
                  />
                ))}
              </div>
            </motion.div>
          )}
        </AnimatePresence>
        
        {/* Apply button */}
        <motion.button
          onClick={(e) => {
            e.stopPropagation();
            onApply();
          }}
          disabled={isProcessing}
          whileHover={{ scale: isProcessing ? 1 : 1.02 }}
          whileTap={{ scale: isProcessing ? 1 : 0.98 }}
          className={cn(
            "w-full py-2.5 px-4 rounded-xl text-sm font-semibold transition-all",
            "flex items-center justify-center gap-2 text-white"
          )}
          style={{ background: profile.gradient }}
        >
          {isProcessing ? (
            <>
              <Loader2 size={14} className="animate-spin" />
              {t('processing', language)}...
            </>
          ) : (
            <>
              <Zap size={14} />
              {progressPercent >= 100 
                ? (language === 'es' ? 'Reaplicar' : 'Reapply')
                : t('profileApplyButton', language)
              }
              <ChevronRight size={14} className="ml-auto" />
            </>
          )}
        </motion.button>
      </div>
      
      {/* Corner decoration on hover */}
      <motion.div
        className="absolute bottom-0 right-0 w-20 h-20 opacity-0 group-hover:opacity-100 transition-opacity duration-300"
        style={{
          background: `radial-gradient(circle at 100% 100%, ${profile.glowColor}, transparent 70%)`,
        }}
      />
    </motion.div>
  );
}

// ============================================
// Main ProfileSelector Component
// ============================================
interface ProfileSelectorProps {
  className?: string;
}

export default function ProfileSelector({ className }: ProfileSelectorProps) {
  const { settings, optimizations, applyOptimization, isProcessing } = useAppStore();
  const isDark = settings.theme === 'dark';
  const language = settings.language as 'es' | 'en';
  
  const [selectedProfileId, setSelectedProfileId] = useState<string | null>(null);
  const [showConfirmDialog, setShowConfirmDialog] = useState(false);
  const [pendingProfileId, setPendingProfileId] = useState<string | null>(null);
  const [applyingProfile, setApplyingProfile] = useState(false);
  const [applyProgress, setApplyProgress] = useState<{ done: number; total: number; current: string } | null>(null);
  const [applyNotice, setApplyNotice] = useState<{ profileName: string; ok: number; total: number; failed: { id: string; reason: string }[] } | null>(null);

  const selectedProfile = predefinedProfiles.find(p => p.id === pendingProfileId) || null;
  
  const appliedOptIds = new Set(
    optimizations.filter(opt => opt.isApplied).map(opt => opt.id)
  );

  // Calculate applied counts for each profile
  const getAppliedCount = (profile: OptimizationProfile): number => {
    return profile.optimizationIds.filter(id => appliedOptIds.has(id)).length;
  };

  // Handle profile selection
  const handleSelectProfile = (profileId: string) => {
    setSelectedProfileId(selectedProfileId === profileId ? null : profileId);
  };

  // Handle apply button click
  const handleApplyClick = (profileId: string) => {
    setPendingProfileId(profileId);
    setShowConfirmDialog(true);
  };

  // Confirm and apply profile
  const isExecutableId = (id: string) => isExecutableOptimizationId(id);

  const handleConfirmApply = async (createBackup: boolean) => {
    if (!pendingProfileId) return;

    setApplyingProfile(true);
    const profile = predefinedProfiles.find(p => p.id === pendingProfileId);
    const fresh = useAppStore.getState().optimizations;

    try {
      const pending = profile
        ? profile.optimizationIds.filter(id =>
            isExecutableId(id) && !fresh.find(o => o.id === id)?.isApplied
          )
        : [];
      const total = pending.length;

      // UN solo punto de restauración antes de tocar nada
      if (createBackup && total > 0) {
        setApplyProgress({ done: 0, total, current: '__backup__' });
        try {
          await fetch('/api/troubleshoot/execute', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ action: 'create-restore-point' })
          });
        } catch { /* sin backup, seguimos */ }
      }

      const failed: { id: string; reason: string }[] = [];
      for (let i = 0; i < pending.length; i++) {
        const optId = pending[i];
        const nm = fresh.find(o => o.id === optId)?.nameKey || optId;
        setApplyProgress({ done: i, total, current: nm });
        try {
          await applyOptimization(optId, false);
        } catch (err: unknown) {
          const msg = String((err as Error)?.message || 'Error desconocido');
          if (!msg.toLowerCase().includes('already applied')) failed.push({ id: optId, reason: msg });
          // sincronizar bandera local aunque haya fallado
          useAppStore.setState((cs) => ({
            optimizations: cs.optimizations.map((o) =>
              o.id === optId ? { ...o, isApplied: false, isEnabled: false } : o
            )
          }));
        }
      }

      setApplyProgress({ done: total, total, current: '' });

      if (failed.length > 0) {
        setApplyNotice({
          profileName: profile?.nameKey === 'profileValorantName' ? 'Valorant' : (profile ? humanizeId(profile.id) : ''),
          ok: total - failed.length,
          total,
          failed,
        });
      }
    } finally {
      setApplyingProfile(false);
      setShowConfirmDialog(false);
      setPendingProfileId(null);
      setApplyProgress(null);
    }
  };

  return (
    <div className={cn("space-y-4", className)}>
      {applyNotice && (
        <motion.div
          initial={{ opacity: 0, y: -8 }}
          animate={{ opacity: 1, y: 0 }}
          className="rounded-2xl p-4 text-sm"
          style={{
            background: applyNotice.failed.length ? 'rgba(239, 68, 68, 0.12)' : 'rgba(16, 185, 129, 0.12)',
            border: `1px solid ${applyNotice.failed.length ? 'rgba(239, 68, 68, 0.35)' : 'rgba(16, 185, 129, 0.35)'}`,
          }}
        >
          <div className="font-semibold mb-1" style={{ color: applyNotice.failed.length ? '#EF4444' : '#10B981' }}>
            {applyNotice.failed.length
              ? `Perfil ${applyNotice.profileName}: ${applyNotice.ok}/${applyNotice.total} aplicadas — ${applyNotice.failed.length} fallaron`
              : `Perfil ${applyNotice.profileName}: ${applyNotice.total}/${applyNotice.total} aplicadas correctamente`}
          </div>
          {applyNotice.failed.length > 0 && (
            <ul className="mt-2 space-y-1" style={{ color: applyNotice.failed.length ? '#fca5a5' : undefined }}>
              {applyNotice.failed.map((f) => (
                <li key={f.id} className="truncate">
                  • {humanizeId(f.id)} — <span className="opacity-80">{f.reason}</span>
                </li>
              ))}
            </ul>
          )}
          <button
            onClick={() => setApplyNotice(null)}
            className="mt-2 text-xs underline opacity-70 hover:opacity-100"
          >
            Cerrar
          </button>
        </motion.div>
      )}
      {/* Section Header */}
      <motion.div 
        className="flex items-center justify-between"
        initial={{ opacity: 0, y: -10 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.5 }}
      >
        <div className="flex items-center gap-2">
          <Settings size={18} className="text-[#FF6B35]" />
          <h3 className="text-base font-semibold" style={{ color: isDark ? '#fff' : '#1a1a2e' }}>
            {t('profileTitle', language)}
          </h3>
        </div>
        <span className="text-xs px-2.5 py-1 rounded-full bg-[#FF6B35]/10 text-[#FF6B35] border border-[#FF6B35]/20">
          {predefinedProfiles.length} {language === 'es' ? 'perfiles' : 'profiles'}
        </span>
      </motion.div>

      {/* Subtitle */}
      <motion.p 
        className="text-sm max-w-xl"
        style={{ color: isDark ? 'rgba(255,255,255,0.4)' : 'rgba(0,0,0,0.4)' }}
        initial={{ opacity: 0 }}
        animate={{ opacity: 1 }}
        transition={{ delay: 0.1, duration: 0.5 }}
      >
        {t('profileSubtitle', language)}
      </motion.p>

      {/* Profiles Grid */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        {predefinedProfiles.map((profile, index) => (
          <ProfileCard
            key={profile.id}
            profile={profile}
            isSelected={selectedProfileId === profile.id}
            appliedCount={getAppliedCount(profile)}
            totalCount={profile.optimizationIds.length}
            onSelect={() => handleSelectProfile(profile.id)}
            onApply={() => handleApplyClick(profile.id)}
            index={index}
            language={language}
            isDark={isDark}
            isProcessing={applyingProfile}
            appliedIds={appliedOptIds}
          />
        ))}
      </div>

      {/* Custom Profile Hint */}
      <motion.div
        initial={{ opacity: 0 }}
        animate={{ opacity: 1 }}
        transition={{ delay: 0.5, duration: 0.5 }}
        className="flex items-center gap-3 p-4 rounded-xl"
        style={{
          background: isDark ? 'rgba(255,255,255,0.02)' : 'rgba(0,0,0,0.02)',
          border: `1px dashed ${isDark ? 'rgba(255,255,255,0.08)' : 'rgba(0,0,0,0.08)'}`,
        }}
      >
        <div className="w-10 h-10 rounded-xl flex items-center justify-center bg-white/[0.03]">
          <Settings size={18} className="opacity-40" />
        </div>
        <div className="flex-1 min-w-0">
          <h4 className="text-sm font-medium mb-0.5" style={{ color: isDark ? 'rgba(255,255,255,0.7)' : 'rgba(0,0,0,0.7)' }}>
            {t('profileCustomName', language)}
          </h4>
          <p className="text-xs" style={{ color: isDark ? 'rgba(255,255,255,0.35)' : 'rgba(0,0,0,0.35)' }}>
            {t('profileCustomDesc', language)}
          </p>
        </div>
        <motion.button
          whileHover={{ x: 3 }}
          onClick={() => useAppStore.getState().setCurrentView('optimization')}
          className="flex items-center gap-1 text-sm font-medium text-[#FF6B35]"
        >
          {t('profileCustomAction', language)}
          <ChevronRight size={14} />
        </motion.button>
      </motion.div>

      {/* Confirmation Dialog */}
      <AnimatePresence>
        {showConfirmDialog && selectedProfile && (
          <ConfirmDialog
            profile={selectedProfile}
            onConfirm={handleConfirmApply}
            onCancel={() => {
              setShowConfirmDialog(false);
              setPendingProfileId(null);
            }}
            isProcessing={applyingProfile}
            language={language}
            isDark={isDark}
            appliedIds={appliedOptIds}
            progress={applyProgress}
          />
        )}
      </AnimatePresence>
    </div>
  );
}


