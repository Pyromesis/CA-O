'use client';

import { useState } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { useAppStore } from '@/store/useAppStore';
import { t } from '@/lib/i18n';
import {
  Volume2,
  Bluetooth,
  Wifi,
  Download,
  Monitor,
  Save,
  Undo2,
  AlertTriangle,
  CheckCircle2,
  XCircle,
  Loader2,
  ChevronRight,
  ShieldAlert,
  Info,
  Wrench,
  Package,
  RefreshCw,
  Globe,
  FolderOpen,
} from 'lucide-react';
import { cn } from '@/lib/utils';

interface TroubleshootingViewProps {
  className?: string;
}

// Icon mapping
const iconMap: Record<string, React.ElementType> = {
  Volume2,
  Wrench,
  Package,
  RefreshCw,
  Globe,
  FolderOpen,
  Bluetooth,
  Wifi,
  Download,
  Monitor,
  Save,
  Undo2,
};

// Troubleshoot item configuration with colors and warnings
const troubleshootConfig = [
  {
    id: 'restore-audio',
    color: '#3B82F6',
    gradient: 'linear-gradient(135deg, #3B82F6, #1D4ED8)',
    bgColor: 'rgba(59, 130, 246, 0.15)',
    isDestructive: false,
    requiresConfirmation: false,
  },
  {
    id: 'restore-bluetooth',
    color: '#8B5CF6',
    gradient: 'linear-gradient(135deg, #8B5CF6, #7C3AED)',
    bgColor: 'rgba(139, 92, 246, 0.15)',
    isDestructive: false,
    requiresConfirmation: false,
  },
  {
    id: 'restore-network',
    color: '#10B981',
    gradient: 'linear-gradient(135deg, #10B981, #059669)',
    bgColor: 'rgba(16, 185, 129, 0.15)',
    isDestructive: false,
    requiresConfirmation: true,
  },
  {
    id: 'restore-windows-update',
    color: '#F59E0B',
    gradient: 'linear-gradient(135deg, #F59E0B, #D97706)',
    bgColor: 'rgba(245, 158, 11, 0.15)',
    isDestructive: false,
    requiresConfirmation: true,
  },
  {
    id: 'restore-display',
    color: '#EC4899',
    gradient: 'linear-gradient(135deg, #EC4899, #DB2777)',
    bgColor: 'rgba(236, 72, 153, 0.15)',
    isDestructive: false,
    requiresConfirmation: false,
  },
  {
    id: 'create-restore-point',
    color: '#14B8A6',
    gradient: 'linear-gradient(135deg, #14B8A6, #0D9488)',
    bgColor: 'rgba(20, 184, 166, 0.15)',
    isDestructive: false,
    requiresConfirmation: false,
  },
  {
    id: 'restore-all',
    color: '#EF4444',
    gradient: 'linear-gradient(135deg, #EF4444, #DC2626)',
    bgColor: 'rgba(239, 68, 68, 0.15)',
    isDestructive: true,
    requiresConfirmation: true,
  },
  {
    id: 'repair-system-files',
    color: '#F59E0B',
    gradient: 'linear-gradient(135deg, #F59E0B, #D97706)',
    bgColor: 'rgba(245, 158, 11, 0.15)',
    isDestructive: false,
    requiresConfirmation: true,
  },
  {
    id: 'reset-store-cache',
    color: '#3B82F6',
    gradient: 'linear-gradient(135deg, #3B82F6, #2563EB)',
    bgColor: 'rgba(59, 130, 246, 0.15)',
    isDestructive: false,
    requiresConfirmation: false,
  },
  {
    id: 'restart-explorer',
    color: '#10B981',
    gradient: 'linear-gradient(135deg, #10B981, #059669)',
    bgColor: 'rgba(16, 185, 129, 0.15)',
    isDestructive: false,
    requiresConfirmation: false,
  },
  {
    id: 'flush-dns-cache',
    color: '#06B6D4',
    gradient: 'linear-gradient(135deg, #06B6D4, #0891B2)',
    bgColor: 'rgba(6, 182, 212, 0.15)',
    isDestructive: false,
    requiresConfirmation: false,
  },
  {
    id: 'clean-temp-junk',
    color: '#8B5CF6',
    gradient: 'linear-gradient(135deg, #8B5CF6, #7C3AED)',
    bgColor: 'rgba(139, 92, 246, 0.15)',
    isDestructive: false,
    requiresConfirmation: false,
  },
];


// Toast notification component
function Toast({ 
  type, 
  message, 
  onClose 
}: { 
  type: 'success' | 'error' | 'warning'; 
  message: string; 
  onClose: () => void;
}) {
  const config = {
    success: { icon: CheckCircle2, color: '#10B981', bg: 'rgba(16, 185, 129, 0.15)' },
    error: { icon: XCircle, color: '#EF4444', bg: 'rgba(239, 68, 68, 0.15)' },
    warning: { icon: AlertTriangle, color: '#F59E0B', bg: 'rgba(245, 158, 11, 0.15)' },
  };

  const { icon: Icon, color, bg } = config[type];

  return (
    <motion.div
      initial={{ opacity: 0, y: -50, scale: 0.95 }}
      animate={{ opacity: 1, y: 0, scale: 1 }}
      exit={{ opacity: 0, y: -50, scale: 0.95 }}
      className="fixed top-20 right-4 z-50 flex items-center gap-3 px-4 py-3 rounded-xl shadow-2xl max-w-sm"
      style={{
        background: 'rgba(30, 30, 50, 0.95)',
        backdropFilter: 'blur(20px)',
        border: `1px solid ${color}40`,
        boxShadow: `0 10px 40px rgba(0, 0, 0, 0.3), 0 0 20px ${color}20`,
      }}
    >
      <div className="w-8 h-8 rounded-lg flex items-center justify-center" style={{ background: bg }}>
        <Icon size={18} style={{ color }} />
      </div>
      <p className="flex-1 text-sm font-medium text-white">{message}</p>
      <button onClick={onClose} className="text-white/50 hover:text-white transition-colors">
        <XCircle size={16} />
      </button>
    </motion.div>
  );
}

// Confirmation Dialog component
function ConfirmDialog({
  isOpen,
  title,
  message,
  confirmLabel,
  cancelLabel,
  isDangerous,
  onConfirm,
  onCancel,
  language,
}: {
  isOpen: boolean;
  title: string;
  message: string;
  confirmLabel: string;
  cancelLabel: string;
  isDangerous: boolean;
  onConfirm: () => void;
  onCancel: () => void;
  language: 'es' | 'en';
}) {
  if (!isOpen) return null;

  return (
    <AnimatePresence>
      <motion.div
        initial={{ opacity: 0 }}
        animate={{ opacity: 1 }}
        exit={{ opacity: 0 }}
        className="fixed inset-0 z-50 flex items-center justify-center p-4"
        onClick={onCancel}
      >
        {/* Backdrop */}
        <div className="absolute inset-0 bg-black/60 backdrop-blur-sm" />
        
        {/* Dialog */}
        <motion.div
          initial={{ opacity: 0, scale: 0.95, y: 20 }}
          animate={{ opacity: 1, scale: 1, y: 0 }}
          exit={{ opacity: 0, scale: 0.95, y: 20 }}
          onClick={(e) => e.stopPropagation()}
          className="relative w-full max-w-md p-6 rounded-2xl"
          style={{
            background: language === 'es' ? 'rgba(30, 30, 50, 0.98)' : 'rgba(255, 255, 255, 0.98)',
            border: `1px solid ${isDangerous ? 'rgba(239, 68, 68, 0.3)' : 'rgba(255, 107, 53, 0.3)'}`,
            boxShadow: `0 25px 60px rgba(0, 0, 0, 0.4)`,
          }}
        >
          {/* Icon */}
          <div className="flex items-center gap-4 mb-4">
            <div
              className="w-12 h-12 rounded-xl flex items-center justify-center"
              style={{
                background: isDangerous ? 'rgba(239, 68, 68, 0.15)' : 'rgba(255, 107, 53, 0.15)',
              }}
            >
              {isDangerous ? (
                <ShieldAlert size={24} className="text-red-500" />
              ) : (
                <AlertTriangle size={24} className="text-orange-500" />
              )}
            </div>
            <div>
              <h3 
                className="text-lg font-bold"
                style={{ color: language === 'es' ? '#fff' : '#1a1a2e' }}
              >
                {title}
              </h3>
              {isDangerous && (
                <span className="text-xs font-medium text-red-500 uppercase tracking-wider">
                  {language === 'es' ? 'Acción destructiva' : 'Destructive Action'}
                </span>
              )}
            </div>
          </div>

          {/* Message */}
          <p 
            className="text-sm leading-relaxed mb-6"
            style={{ color: language === 'es' ? 'rgba(255,255,255,0.7)' : 'rgba(0,0,0,0.7)' }}
          >
            {message}
          </p>

          {/* Warning for destructive actions */}
          {isDangerous && (
            <div 
              className="flex items-start gap-2 p-3 rounded-xl mb-6"
              style={{
                background: 'rgba(239, 68, 68, 0.1)',
                border: '1px solid rgba(239, 68, 68, 0.2)',
              }}
            >
              <AlertTriangle size={16} className="text-red-400 mt-0.5 shrink-0" />
              <p className="text-xs text-red-300">
                {language === 'es'
                  ? 'Esta acción no se puede deshacer fácilmente. Se recomienda crear un punto de restauración primero.'
                  : 'This action cannot be easily undone. It is recommended to create a restore point first.'}
              </p>
            </div>
          )}

          {/* Actions */}
          <div className="flex gap-3 justify-end">
            <motion.button
              whileHover={{ scale: 1.02 }}
              whileTap={{ scale: 0.98 }}
              onClick={onCancel}
              className="px-5 py-2.5 rounded-xl text-sm font-medium"
              style={{
                background: language === 'es' ? 'rgba(255, 255, 255, 0.08)' : 'rgba(0, 0, 0, 0.05)',
                border: `1px solid ${language === 'es' ? 'rgba(255, 255, 255, 0.1)' : 'rgba(0, 0, 0, 0.1)'}`,
                color: language === 'es' ? 'rgba(255,255,255,0.8)' : 'rgba(0,0,0,0.8)',
              }}
            >
              {cancelLabel}
            </motion.button>
            
            <motion.button
              whileHover={{ scale: 1.02 }}
              whileTap={{ scale: 0.98 }}
              onClick={onConfirm}
              className="px-5 py-2.5 rounded-xl text-sm font-semibold text-white"
              style={{
                background: isDangerous
                  ? 'linear-gradient(135deg, #EF4444, #DC2626)'
                  : 'linear-gradient(135deg, #FF6B35, #ff8c42)',
                boxShadow: isDangerous
                  ? '0 4px 15px rgba(239, 68, 68, 0.35)'
                  : '0 4px 15px rgba(255, 107, 53, 0.35)',
              }}
            >
              {confirmLabel}
            </motion.button>
          </div>
        </motion.div>
      </motion.div>
    </AnimatePresence>
  );
}

export function TroubleshootingView({ className }: TroubleshootingViewProps) {
  const { settings, troubleshootItems, executeTroubleshoot, isProcessing } = useAppStore();
  
  const [selectedAction, setSelectedAction] = useState<string | null>(null);
  const [showConfirmDialog, setShowConfirmDialog] = useState(false);
  const [toast, setToast] = useState<{ type: 'success' | 'error' | 'warning'; message: string } | null>(null);
  const [executingId, setExecutingId] = useState<string | null>(null);

  const isDark = settings.theme === 'dark';

  const handleActionClick = (itemId: string) => {
    const config = troubleshootConfig.find(c => c.id === itemId);
    
    if (config?.requiresConfirmation || config?.isDestructive) {
      setSelectedAction(itemId);
      setShowConfirmDialog(true);
    } else {
      executeAction(itemId);
    }
  };

  const executeAction = async (actionId: string) => {
    setExecutingId(actionId);
    
    try {
      const result = await executeTroubleshoot(actionId);
      
      // Show success toast based on action
      let message = '';
      if (result.status === 'partial') {
        message = settings.language === 'es'
          ? `${result.issuesFixed} pasos completados; ${result.issuesFound} fallaron`
          : `${result.issuesFixed} steps completed; ${result.issuesFound} failed`;
      } else if (actionId === 'create-restore-point') {
        message = t('msgRestorePointSuccess', settings.language);
      } else if (actionId === 'restore-all') {
        message = t('msgRestoreSuccess', settings.language);
      } else {
        message = settings.language === 'es'
          ? 'Acción completada correctamente'
          : 'Action completed successfully';
      }
      
      setToast({ type: 'success', message });
    } catch (error) {
      setToast({ 
        type: 'error', 
        message: settings.language === 'es' 
          ? 'Error al ejecutar la acción' 
          : 'Error executing action' 
      });
    } finally {
      setExecutingId(null);
      setShowConfirmDialog(false);
      
      // Auto-dismiss toast after 4 seconds
      setTimeout(() => setToast(null), 4000);
    }
  };

  const handleConfirm = () => {
    if (selectedAction) {
      executeAction(selectedAction);
    }
  };

  const handleCancel = () => {
    setShowConfirmDialog(false);
    setSelectedAction(null);
  };

  // Get selected item config for dialog
  const selectedItem = troubleshootItems.find(item => item.id === selectedAction);
  const selectedItemConfig = troubleshootConfig.find(c => c.id === selectedAction);

  // Animation variants
  const containerVariants = {
    hidden: { opacity: 0 },
    visible: {
      opacity: 1,
      transition: {
        staggerChildren: 0.08,
        delayChildren: 0.2,
      },
    },
  };

  const cardVariants = {
    hidden: { opacity: 0, y: 20, scale: 0.95 },
    visible: { 
      opacity: 1, 
      y: 0, 
      scale: 1,
      transition: { duration: 0.3, ease: 'easeOut' as const }
    }
  };

  return (
    <div className={cn('min-h-screen pb-28 relative', className)}>
      {/* Header Section */}
      <motion.div
        initial={{ opacity: 0, y: -20 }}
        animate={{ opacity: 1, y: 0 }}
        className="px-4 md:px-6 pt-6 pb-8"
      >
        <h2 
          className="text-2xl md:text-3xl font-bold mb-2"
          style={{ color: isDark ? '#fff' : '#1a1a2e' }}
        >
          {t('troubleshootTitle', settings.language)}
        </h2>
        <p 
          className="text-sm mb-6"
          style={{ color: isDark ? 'rgba(255,255,255,0.5)' : 'rgba(0,0,0,0.5)' }}
        >
          {t('troubleshootSubtitle', settings.language)}
        </p>

        {/* Warning Banner */}
        <motion.div
          initial={{ opacity: 0, y: -10 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.2 }}
          className="flex items-start gap-3 p-4 rounded-2xl mb-8"
          style={{
            background: 'rgba(245, 158, 11, 0.1)',
            border: '1px solid rgba(245, 158, 11, 0.2)',
          }}
        >
          <Info size={20} className="text-amber-500 mt-0.5 shrink-0" />
          <div>
            <p className="text-sm font-medium text-amber-200">
              {settings.language === 'es' ? 'Nota importante' : 'Important Note'}
            </p>
            <p className="text-xs mt-1" style={{ color: 'rgba(251, 191, 36, 0.8)' }}>
              {settings.language === 'es'
                ? 'Algunas acciones pueden requerir reinicio del sistema. Se recomienda guardar el trabajo antes de continuar.'
                : 'Some actions may require system restart. It is recommended to save your work before proceeding.'}
            </p>
          </div>
        </motion.div>
      </motion.div>

      {/* Troubleshoot Cards Grid */}
      <div className="px-4 md:px-6">
        <motion.div
          variants={containerVariants}
          initial="hidden"
          animate="visible"
          className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4"
        >
          {troubleshootItems.map((item) => {
            const config = troubleshootConfig.find(c => c.id === item.id)!;
            const IconComponent = iconMap[item.icon] || Save;
            const isExecuting = executingId === item.id;

            return (
              <motion.div
                key={item.id}
                variants={cardVariants}
                whileHover={{ y: -6, scale: 1.02 }}
                whileTap={{ scale: 0.98 }}
                onClick={() => !isExecuting && handleActionClick(item.id)}
                className={cn(
                  'relative p-5 rounded-2xl cursor-pointer overflow-hidden group transition-all',
                  isExecuting && 'pointer-events-none'
                )}
                style={{
                  background: isDark ? 'rgba(30, 30, 50, 0.6)' : 'rgba(255, 255, 255, 0.8)',
                  border: `1px solid ${config.isDestructive 
                    ? 'rgba(239, 68, 68, 0.2)' 
                    : (isDark ? 'rgba(255, 255, 255, 0.08)' : 'rgba(0, 0, 0, 0.08)')}`,
                  boxShadow: `0 4px 20px ${isDark ? 'rgba(0,0,0,0.15)' : 'rgba(0,0,0,0.05)'}`,
                }}
              >
                {/* Gradient Accent Line at Top */}
                <div 
                  className="absolute top-0 left-0 right-0 h-1"
                  style={{ background: config.gradient }}
                />

                {/* Hover Glow Effect */}
                <div 
                  className="absolute inset-0 opacity-0 group-hover:opacity-100 transition-opacity duration-300"
                  style={{
                    background: `radial-gradient(circle at center, ${config.color}12, transparent 70%)`,
                  }}
                />

                {/* Destructive Warning Badge */}
                {config.isDestructive && (
                  <div className="absolute top-3 right-3 flex items-center gap-1 px-2 py-1 rounded-full text-[10px] font-semibold uppercase tracking-wider"
                    style={{
                      background: 'rgba(239, 68, 68, 0.2)',
                      color: '#EF4444',
                      border: '1px solid rgba(239, 68, 68, 0.3)',
                    }}
                  >
                    <ShieldAlert size={10} />
                    {settings.language === 'es' ? 'Peligroso' : 'Danger'}
                  </div>
                )}

                <div className="relative z-10">
                  {/* Icon */}
                  <div 
                    className="w-14 h-14 rounded-2xl flex items-center justify-center mb-4 group-hover:scale-110 transition-transform"
                    style={{ background: config.bgColor }}
                  >
                    {isExecuting ? (
                      <motion.div
                        animate={{ rotate: 360 }}
                        transition={{ duration: 1, repeat: Infinity, ease: 'linear' }}
                      >
                        <Loader2 size={26} style={{ color: config.color }} />
                      </motion.div>
                    ) : (
                      <IconComponent size={26} style={{ color: config.color }} />
                    )}
                  </div>

                  {/* Content */}
                  <h4 
                    className="text-base font-semibold mb-2"
                    style={{ color: isDark ? '#fff' : '#1a1a2e' }}
                  >
                    {t(item.nameKey, settings.language)}
                  </h4>
                  
                  <p 
                    className="text-sm leading-relaxed mb-4 line-clamp-2"
                    style={{ color: isDark ? 'rgba(255,255,255,0.5)' : 'rgba(0,0,0,0.5)' }}
                  >
                    {t(item.descriptionKey, settings.language)}
                  </p>

                  {/* Action Button */}
                  <div className="flex items-center justify-between pt-3" style={{ borderTop: `1px solid ${isDark ? 'rgba(255,255,255,0.06)' : 'rgba(0,0,0,0.06)'}` }}>
                    <span 
                      className="text-xs font-medium"
                      style={{ color: isDark ? 'rgba(255,255,255,0.4)' : 'rgba(0,0,0,0.4)' }}
                    >
                      {config.requiresConfirmation 
                        ? (settings.language === 'es' ? 'Requiere confirmación' : 'Requires confirmation')
                        : (settings.language === 'es' ? 'Clic para ejecutar' : 'Click to execute')
                      }
                    </span>
                    
                    <motion.div
                      className="flex items-center gap-1 text-sm font-medium"
                      style={{ color: config.isDestructive ? '#EF4444' : config.color }}
                      whileHover={{ x: 4 }}
                    >
                      {isExecuting ? (
                        <>
                          <Loader2 size={14} className="animate-spin" />
                          <span>{t('processing', settings.language)}</span>
                        </>
                      ) : (
                        <>
                          <span>{t('apply', settings.language)}</span>
                          <ChevronRight size={14} />
                        </>
                      )}
                    </motion.div>
                  </div>
                </div>
              </motion.div>
            );
          })}
        </motion.div>
      </div>

      {/* Confirmation Dialog */}
      <ConfirmDialog
        isOpen={showConfirmDialog}
        title={selectedItem ? t(selectedItem.nameKey, settings.language) : ''}
        message={
          selectedItem 
            ? `${t(selectedItem.descriptionKey, settings.language)}\n\n${selectedItemConfig?.isDestructive 
                ? (settings.language === 'es' 
                  ? '\n⚠️ Esta acción revertirá TODAS las optimizaciones aplicadas.' 
                  : '\n⚠️ This action will revert ALL applied optimizations.')
                : ''}`
            : ''
        }
        confirmLabel={t('confirm', settings.language)}
        cancelLabel={t('cancel', settings.language)}
        isDangerous={selectedItemConfig?.isDestructive || false}
        onConfirm={handleConfirm}
        onCancel={handleCancel}
        language={settings.language}
      />

      {/* Toast Notification */}
      <AnimatePresence>
        {toast && (
          <Toast
            type={toast.type}
            message={toast.message}
            onClose={() => setToast(null)}
          />
        )}
      </AnimatePresence>
    </div>
  );
}

export default TroubleshootingView;
