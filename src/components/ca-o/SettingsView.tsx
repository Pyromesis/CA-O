'use client';

import { useState } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { useAppStore } from '@/store/useAppStore';
import { t } from '@/lib/i18n';
import {
  Sun,
  Moon,
  Globe,
  Palette,
  Bell,
  ShieldCheck,
  RotateCcw,
  Info,
  Heart,
  Zap,
  ChevronRight,
  Check,
  AlertTriangle,
  XCircle,
  Trash2,
  ExternalLink,
  Clock,
  Database,
  User,
} from 'lucide-react';
import { cn } from '@/lib/utils';
import { SettingsExportImport } from './SettingsExportImport';
import { Scheduler } from './Scheduler';
import { SoundSettings } from './SoundEffects';
import { CloudSyncUI } from './CloudSyncUI';
import { AdvancedExport } from './AdvancedExport';
import { ThemeCustomization } from './ThemeCustomization';
import { UserProfile } from './UserProfile';

interface SettingsViewProps {
  className?: string;
}

// Toggle Switch Component
function ToggleSwitch({
  enabled,
  onChange,
  disabled = false,
}: {
  enabled: boolean;
  onChange: (enabled: boolean) => void;
  disabled?: boolean;
}) {
  return (
    <motion.button
      onClick={() => !disabled && onChange(!enabled)}
      disabled={disabled}
      whileTap={{ scale: 0.95 }}
      className={cn(
        'relative w-12 h-7 rounded-full transition-colors duration-200',
        enabled ? 'bg-[#FF6B35]' : (disabled ? 'bg-gray-600' : 'bg-gray-500/30'),
        disabled && 'cursor-not-allowed opacity-50'
      )}
    >
      <motion.div
        animate={{ x: enabled ? 20 : 2 }}
        transition={{ type: 'spring', stiffness: 500, damping: 30 }}
        className="absolute top-1 w-5 h-5 rounded-full bg-white shadow-md flex items-center justify-center"
      >
        {enabled && (
          <Check size={12} className="text-[#FF6B35]" strokeWidth={3} />
        )}
      </motion.div>
    </motion.button>
  );
}

// Theme Preview Card
function ThemePreviewCard({
  isDark,
  isSelected,
  onClick,
}: {
  isDark: boolean;
  isSelected: boolean;
  onClick: () => void;
}) {
  return (
    <motion.button
      whileHover={{ scale: 1.02, y: -4 }}
      whileTap={{ scale: 0.98 }}
      onClick={onClick}
      className={cn(
        'relative w-full p-4 rounded-2xl overflow-hidden transition-all',
        isSelected && 'ring-2 ring-[#FF6B35] ring-offset-2 ring-offset-background'
      )}
      style={{
        background: isDark 
          ? 'linear-gradient(135deg, #1a1a2e, #16213e)' 
          : 'linear-gradient(135deg, #f8fafc, #e2e8f0)',
        border: `1px solid ${isSelected ? '#FF6B35' : (isDark ? 'rgba(255,255,255,0.1)' : 'rgba(0,0,0,0.1)')}`,
      }}
    >
      {/* Mini UI Preview */}
      <div className="space-y-2">
        {/* Header bar */}
        <div 
          className="h-4 rounded-lg"
          style={{ background: isDark ? 'rgba(255,255,255,0.08)' : 'rgba(0,0,0,0.06)' }}
        />
        
        {/* Content area */}
        <div className="flex gap-2">
          {/* Sidebar */}
          <div 
            className="w-8 h-16 rounded-lg"
            style={{ background: isDark ? 'rgba(255,255,255,0.05)' : 'rgba(0,0,0,0.04)' }}
          />
          
          {/* Main content */}
          <div className="flex-1 space-y-1.5">
            <div 
              className="h-3 w-3/4 rounded"
              style={{ background: isDark ? 'rgba(255,255,255,0.1)' : 'rgba(0,0,0,0.08)' }}
            />
            <div 
              className="h-3 w-1/2 rounded"
              style={{ background: isDark ? 'rgba(255,255,255,0.07)' : 'rgba(0,0,0,0.05)' }}
            />
          </div>
        </div>
      </div>

      {/* Label */}
      <div className="mt-3 flex items-center justify-between">
        <span 
          className="text-sm font-medium"
          style={{ color: isDark ? '#fff' : '#1a1a2e' }}
        >
          {isDark ? 'Dark' : 'Light'}
        </span>
        
        {isSelected && (
          <motion.div
            initial={{ scale: 0 }}
            animate={{ scale: 1 }}
            className="w-5 h-5 rounded-full bg-[#FF6B35] flex items-center justify-center"
          >
            <Check size={12} className="text-white" strokeWidth={3} />
          </motion.div>
        )}
      </div>
    </motion.button>
  );
}

// Language Option Card
function LanguageOption({
  code,
  label,
  flag,
  isSelected,
  onClick,
}: {
  code: string;
  label: string;
  flag: string;
  isSelected: boolean;
  onClick: () => void;
}) {
  return (
    <motion.button
      whileHover={{ scale: 1.02 }}
      whileTap={{ scale: 0.98 }}
      onClick={onClick}
      className={cn(
        'relative flex items-center gap-3 p-4 rounded-xl transition-all',
      )}
      style={{
        background: isSelected 
          ? 'rgba(255, 107, 53, 0.15)' 
          : 'var(--muted)',
        border: `1px solid ${isSelected ? 'rgba(255, 107, 53, 0.4)' : 'var(--border)'}`,
      }}
    >
      <span className="text-2xl">{flag}</span>
      
      <div className="text-left">
        <p 
          className="font-semibold text-sm"
          style={{ color: isSelected ? '#FF6B35' : 'var(--foreground)' }}
        >
          {label}
        </p>
        <p className="text-xs text-muted-foreground">{code.toUpperCase()}</p>
      </div>

      {isSelected && (
        <motion.div
          initial={{ scale: 0 }}
          animate={{ scale: 1 }}
          className="ml-auto w-5 h-5 rounded-full bg-[#FF6B35] flex items-center justify-center"
        >
          <Check size={12} className="text-white" strokeWidth={3} />
        </motion.div>
      )}
    </motion.button>
  );
}

// Settings Section Component
function SettingsSection({
  title,
  icon: Icon,
  children,
  isDark,
  className,
}: {
  title: string;
  icon: React.ElementType;
  children: React.ReactNode;
  isDark: boolean;
  className?: string;
}) {
  return (
    <motion.div
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      className={cn("mb-8", className)}
    >
      <div className="flex items-center gap-2 mb-4">
        <Icon size={18} className="text-[#FF6B35]" />
        <h3 
          className="text-base font-semibold"
          style={{ color: isDark ? '#fff' : '#1a1a2e' }}
        >
          {title}
        </h3>
      </div>
      
      <div 
        className="p-5 rounded-2xl space-y-5"
        style={{
          background: isDark ? 'rgba(30, 30, 50, 0.6)' : 'rgba(255, 255, 255, 0.8)',
          border: `1px solid ${isDark ? 'rgba(255, 255, 255, 0.08)' : 'rgba(0, 0, 0, 0.08)'}`,
          backdropFilter: 'blur(10px)',
        }}
      >
        {children}
      </div>
    </motion.div>
  );
}

// Setting Row Component
function SettingRow({
  icon: Icon,
  label,
  description,
  control,
  isDark,
  warning,
}: {
  icon?: React.ElementType;
  label: string;
  description?: string;
  control: React.ReactNode;
  isDark: boolean;
  warning?: boolean;
}) {
  return (
    <div 
      className={cn(
        'flex items-start justify-between gap-4 py-3',
        warning && 'p-3 -mx-3 rounded-xl',
        warning && (isDark ? 'bg-red-500/10' : 'bg-red-500/5')
      )}>
      <div className="flex items-start gap-3 min-w-0">
        {Icon && (
          <div 
            className="w-9 h-9 rounded-xl flex items-center justify-center shrink-0 mt-0.5"
            style={{ 
              background: warning 
                ? 'rgba(239, 68, 68, 0.15)' 
                : (isDark ? 'rgba(255, 107, 53, 0.15)' : 'rgba(255, 107, 53, 0.1)'),
            }}
          >
            <Icon 
              size={17} 
              className={warning ? 'text-red-400' : 'text-[#FF6B35]'} 
            />
          </div>
        )}
        <div className="min-w-0">
          <p 
            className={cn(
              'font-medium text-sm',
              warning && 'text-red-400'
            )}
            style={!warning ? { color: isDark ? '#fff' : '#1a1a2e' } : {}}
          >
            {label}
          </p>
          {description && (
            <p 
              className="text-xs mt-0.5 leading-relaxed"
              style={{ color: isDark ? 'rgba(255,255,255,0.45)' : 'rgba(0,0,0,0.45)' }}
            >
              {description}
            </p>
          )}
        </div>
      </div>
      
      <div className="shrink-0 mt-1">
        {control}
      </div>
    </div>
  );
}

export function SettingsView({ className }: SettingsViewProps) {
  const { settings, updateSettings, revertAll, getAppliedCount } = useAppStore();
  
  const [showResetConfirm, setShowResetConfirm] = useState(false);
  const [toast, setToast] = useState<{ type: 'success' | 'error'; message: string } | null>(null);

  const isDark = settings.theme === 'dark';
  const appliedCount = getAppliedCount();

  const handleThemeChange = (theme: 'light' | 'dark') => {
    updateSettings({ theme });
  };

  const handleLanguageChange = (language: 'es' | 'en') => {
    updateSettings({ language });
  };

  const handleResetSettings = async () => {
    try {
      // Reset all optimizations first if any are applied
      if (appliedCount > 0) {
        await revertAll();
      }
      
      // Reset to defaults
      updateSettings({
        theme: 'dark',
        language: 'es',
        autoApplySafeTweaks: false,
        confirmBeforeApply: true,
        showNotifications: true,
      });

      setToast({ 
        type: 'success', 
        message: settings.language === 'es' 
          ? 'Configuración restablecida correctamente' 
          : 'Settings reset successfully' 
      });
    } catch (error) {
      setToast({ 
        type: 'error', 
        message: settings.language === 'es' 
          ? 'Error al restablecer la configuración' 
          : 'Error resetting settings' 
      });
    }
    
    setShowResetConfirm(false);
    setTimeout(() => setToast(null), 3000);
  };

  // Animation variants for container
  const containerVariants = {
    hidden: { opacity: 0 },
    visible: {
      opacity: 1,
      transition: {
        staggerChildren: 0.1,
        delayChildren: 0.2,
      },
    },
  };

  return (
    <div className={cn('min-h-screen pb-28', className)}>
      {/* Header */}
      <motion.div
        initial={{ opacity: 0, y: -20 }}
        animate={{ opacity: 1, y: 0 }}
        className="px-4 md:px-6 pt-6 pb-8"
      >
        <h2 
          className="text-2xl md:text-3xl font-bold mb-2"
          style={{ color: isDark ? '#fff' : '#1a1a2e' }}
        >
          {t('settingsTitle', settings.language)}
        </h2>
        <p 
          className="text-sm"
          style={{ color: isDark ? 'rgba(255,255,255,0.5)' : 'rgba(0,0,0,0.5)' }}
        >
          {settings.language === 'es' 
            ? 'Personaliza tu experiencia con CA-O'
            : 'Customize your CA-O experience'}
        </p>
      </motion.div>

      {/* Settings Sections */}
      <div className="px-4 md:px-6">
        <motion.div
          variants={containerVariants}
          initial="hidden"
          animate="visible"
        >
          {/* Appearance Section */}
          <SettingsSection 
            title={t('settingsAppearance', settings.language)} 
            icon={Palette}
            isDark={isDark}
          >
            {/* Theme Selection */}
            <div className="mb-6">
              <label 
                className="block text-xs font-semibold uppercase tracking-wider mb-3"
                style={{ color: isDark ? 'rgba(255,255,255,0.5)' : 'rgba(0,0,0,0.5)' }}
              >
                {t('settingsTheme', settings.language)}
              </label>
              
              <div className="grid grid-cols-2 gap-4">
                <ThemePreviewCard
                  isDark={false}
                  isSelected={settings.theme === 'light'}
                  onClick={() => handleThemeChange('light')}
                />
                <ThemePreviewCard
                  isDark={true}
                  isSelected={settings.theme === 'dark'}
                  onClick={() => handleThemeChange('dark')}
                />
              </div>
            </div>

            {/* Language Selection */}
            <div>
              <label 
                className="block text-xs font-semibold uppercase tracking-wider mb-3"
                style={{ color: isDark ? 'rgba(255,255,255,0.5)' : 'rgba(0,0,0,0.5)' }}
              >
                {t('settingsLanguage', settings.language)}
              </label>
              
              <div className="grid grid-cols-2 gap-3">
                <LanguageOption
                  code="es"
                  label={t('settingsSpanish', settings.language)}
                  flag="🇪🇸"
                  isSelected={settings.language === 'es'}
                  onClick={() => handleLanguageChange('es')}
                />
                <LanguageOption
                  code="en"
                  label={t('settingsEnglish', settings.language)}
                  flag="🇺🇸"
                  isSelected={settings.language === 'en'}
                  onClick={() => handleLanguageChange('en')}
                />
              </div>
            </div>
          </SettingsSection>

          {/* Behavior Section */}
          <SettingsSection 
            title={t('settingsBehavior', settings.language)} 
            icon={ShieldCheck}
            isDark={isDark}
          >
            <SettingRow
              icon={Zap}
              label={t('settingsAutoApply', settings.language)}
              description={
                settings.language === 'es'
                  ? 'Aplica automáticamente las optimizaciones marcadas como seguras'
                  : 'Automatically apply optimizations marked as safe'
              }
              control={
                <ToggleSwitch
                  enabled={settings.autoApplySafeTweaks}
                  onChange={(value) => updateSettings({ autoApplySafeTweaks: value })}
                />
              }
              isDark={isDark}
            />

            <div style={{ height: 1, background: isDark ? 'rgba(255,255,255,0.06)' : 'rgba(0,0,0,0.06)' }} />

            <SettingRow
              icon={AlertTriangle}
              label={t('settingsConfirm', settings.language)}
              description={
                settings.language === 'es'
                  ? 'Muestra un diálogo de confirmación antes de aplicar cambios'
                  : 'Show confirmation dialog before applying changes'
              }
              control={
                <ToggleSwitch
                  enabled={settings.confirmBeforeApply}
                  onChange={(value) => updateSettings({ confirmBeforeApply: value })}
                />
              }
              isDark={isDark}
            />

            <div style={{ height: 1, background: isDark ? 'rgba(255,255,255,0.06)' : 'rgba(0,0,0,0.06)' }} />

            <SettingRow
              icon={Bell}
              label={t('settingsNotifications', settings.language)}
              description={
                settings.language === 'es'
                  ? 'Muestra notificaciones cuando se completan acciones'
                  : 'Show notifications when actions complete'
              }
              control={
                <ToggleSwitch
                  enabled={settings.showNotifications}
                  onChange={(value) => updateSettings({ showNotifications: value })}
                />
              }
              isDark={isDark}
            />
          </SettingsSection>

          {/* About Section */}
          <SettingsSection 
            title={t('settingsAbout', settings.language)} 
            icon={Info}
            isDark={isDark}
          >
            {/* App Info */}
            <div className="flex items-center gap-4 p-4 rounded-xl" style={{ background: isDark ? 'rgba(255, 107, 53, 0.1)' : 'rgba(255, 107, 53, 0.08)' }}>
              <div 
                className="w-14 h-14 rounded-2xl flex items-center justify-center"
                style={{ background: 'linear-gradient(135deg, #FF6B35, #ff8c42)' }}
              >
                <Zap size={26} className="text-white" />
              </div>
              
              <div className="flex-1">
                <h4 
                  className="font-bold text-lg"
                  style={{ color: isDark ? '#fff' : '#1a1a2e' }}
                >
                  CA-O
                </h4>
                <div className="flex items-center gap-2">
                  <span 
                    className="text-xs px-2 py-0.5 rounded-full font-mono"
                    style={{ 
                      background: isDark ? 'rgba(255,255,255,0.1)' : 'rgba(0,0,0,0.08)',
                      color: isDark ? 'rgba(255,255,255,0.7)' : 'rgba(0,0,0,0.7)',
                    }}
                  >
                    v1.0.0
                  </span>
                  <span 
                    className="text-xs"
                    style={{ color: isDark ? 'rgba(255,255,255,0.4)' : 'rgba(0,0,0,0.4)' }}
                  >
                    • {t('settingsDeveloper', settings.language)}
                  </span>
                </div>
              </div>
            </div>

            {/* Links */}
            <div className="mt-4 space-y-2">
              <motion.a
                href="#"
                whileHover={{ x: 4 }}
                className="flex items-center justify-between p-3 rounded-xl group"
                style={{ 
                  background: isDark ? 'rgba(255,255,255,0.03)' : 'rgba(0,0,0,0.02)',
                  border: `1px solid transparent`,
                }}
                onMouseEnter={(e) => {
                  e.currentTarget.style.borderColor = isDark ? 'rgba(255,255,255,0.08)' : 'rgba(0,0,0,0.08)';
                }}
                onMouseLeave={(e) => {
                  e.currentTarget.style.borderColor = 'transparent';
                }}
              >
                <div className="flex items-center gap-3">
                  <ExternalLink size={16} style={{ color: isDark ? 'rgba(255,255,255,0.5)' : 'rgba(0,0,0,0.5)' }} />
                  <span 
                    className="text-sm"
                    style={{ color: isDark ? 'rgba(255,255,255,0.7)' : 'rgba(0,0,0,0.7)' }}
                  >
                    GitHub Repository
                  </span>
                </div>
                <ChevronRight size={16} className="opacity-40" />
              </motion.a>

              <motion.a
                href="#"
                whileHover={{ x: 4 }}
                className="flex items-center justify-between p-3 rounded-xl group"
                style={{ 
                  background: isDark ? 'rgba(255,255,255,0.03)' : 'rgba(0,0,0,0.02)',
                  border: `1px solid transparent`,
                }}
                onMouseEnter={(e) => {
                  e.currentTarget.style.borderColor = isDark ? 'rgba(255,255,255,0.08)' : 'rgba(0,0,0,0.08)';
                }}
                onMouseLeave={(e) => {
                  e.currentTarget.style.borderColor = 'transparent';
                }}
              >
                <div className="flex items-center gap-3">
                  <Heart size={16} className="text-red-400" />
                  <span 
                    className="text-sm"
                    style={{ color: isDark ? 'rgba(255,255,255,0.7)' : 'rgba(0,0,0,0.7)' }}
                  >
                    {settings.language === 'es' ? 'Apoyar el proyecto' : 'Support this project'}
                  </span>
                </div>
                <ChevronRight size={16} className="opacity-40" />
              </motion.a>
            </div>
          </SettingsSection>

          {/* Export/Import Section */}
          <SettingsExportImport className="mb-6" />

          {/* Scheduler Section */}
          <SettingsSection 
            title={settings.language === 'es' ? 'Programador de Tareas' : 'Task Scheduler'}
            icon={Zap}
            isDark={isDark}
            className="mb-6"
          >
            <Scheduler />
          </SettingsSection>

          {/* Sound Effects Section */}
          <SettingsSection 
            title={settings.language === 'es' ? 'Efectos de Sonido' : 'Sound Effects'}
            icon={Bell}
            isDark={isDark}
            className="mb-6"
          >
            <div className="p-4 rounded-xl" style={{
              background: isDark ? 'rgba(255,255,255,0.03)' : 'rgba(0,0,0,0.02)',
              border: `1px solid ${isDark ? 'rgba(255,255,255,0.06)' : 'rgba(0,0,0,0.06)'}`
            }}>
              <SoundSettings language={settings.language} />
            </div>
          </SettingsSection>

          {/* Cloud Sync Section */}
          <SettingsSection 
            title={settings.language === 'es' ? 'Sincronización en la Nube' : 'Cloud Sync'}
            icon={Database}
            isDark={isDark}
            className="mb-6"
          >
            <CloudSyncUI />
          </SettingsSection>

          {/* Advanced Export Section */}
          <SettingsSection 
            title={settings.language === 'es' ? 'Informes y Exportación' : 'Reports & Export'}
            icon={ExternalLink}
            isDark={isDark}
            className="mb-6"
          >
            <AdvancedExport />
          </SettingsSection>

          {/* Theme Customization Section */}
          <SettingsSection 
            title={settings.language === 'es' ? 'Personalización de Tema' : 'Theme Customization'}
            icon={Palette}
            isDark={isDark}
            className="mb-6"
          >
            <ThemeCustomization />
          </SettingsSection>

          {/* User Profile Section */}
          <SettingsSection 
            title={settings.language === 'es' ? 'Perfil de Usuario' : 'User Profile'}
            icon={User}
            isDark={isDark}
            className="mb-6"
          >
            <UserProfile />
          </SettingsSection>
          <SettingsSection 
            title={settings.language === 'es' ? 'Zona de Peligro' : 'Danger Zone'}
            icon={AlertTriangle}
            isDark={isDark}
          >
            <SettingRow
              icon={Trash2}
              label={t('reset', settings.language)}
              description={
                settings.language === 'es'
                  ? 'Restablece toda la configuración y revierte las optimizaciones aplicadas'
                  : 'Reset all settings and revert applied optimizations'
              }
              control={
                <motion.button
                  whileHover={{ scale: 1.02 }}
                  whileTap={{ scale: 0.98 }}
                  onClick={() => setShowResetConfirm(true)}
                  className="px-4 py-2 rounded-xl text-sm font-semibold text-white"
                  style={{
                    background: 'linear-gradient(135deg, #EF4444, #DC2626)',
                    boxShadow: '0 4px 15px rgba(239, 68, 68, 0.3)',
                  }}
                >
                  {t('reset', settings.language)}
                </motion.button>
              }
              isDark={isDark}
              warning
            />
          </SettingsSection>
        </motion.div>
      </div>

      {/* Reset Confirmation Dialog */}
      <AnimatePresence>
        {showResetConfirm && (
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="fixed inset-0 z-50 flex items-center justify-center p-4"
            onClick={() => setShowResetConfirm(false)}
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
                background: isDark ? 'rgba(30, 30, 50, 0.98)' : 'rgba(255, 255, 255, 0.98)',
                border: '1px solid rgba(239, 68, 68, 0.3)',
                boxShadow: '0 25px 60px rgba(0, 0, 0, 0.4)',
              }}
            >
              <div className="flex items-center gap-4 mb-4">
                <div 
                  className="w-12 h-12 rounded-xl flex items-center justify-center"
                  style={{ background: 'rgba(239, 68, 68, 0.15)' }}
                >
                  <AlertTriangle size={24} className="text-red-500" />
                </div>
                <div>
                  <h3 
                    className="text-lg font-bold"
                    style={{ color: isDark ? '#fff' : '#1a1a2e' }}
                  >
                    {t('confirm', settings.language)} {t('reset', settings.language).toLowerCase()}
                  </h3>
                  <span className="text-xs font-medium text-red-500 uppercase tracking-wider">
                    {settings.language === 'es' ? 'Acción destructiva' : 'Destructive Action'}
                  </span>
                </div>
              </div>

              <p 
                className="text-sm leading-relaxed mb-6"
                style={{ color: isDark ? 'rgba(255,255,255,0.7)' : 'rgba(0,0,0,0.7)' }}
              >
                {settings.language === 'es'
                  ? `¿Estás seguro? Esto revertirá ${appliedCount} optimizaciones aplicadas y restablecerá toda la configuración a los valores predeterminados.`
                  : `Are you sure? This will revert ${appliedCount} applied optimizations and reset all settings to default values.`}
              </p>

              <div className="flex gap-3 justify-end">
                <motion.button
                  whileHover={{ scale: 1.02 }}
                  whileTap={{ scale: 0.98 }}
                  onClick={() => setShowResetConfirm(false)}
                  className="px-5 py-2.5 rounded-xl text-sm font-medium"
                  style={{
                    background: isDark ? 'rgba(255, 255, 255, 0.08)' : 'rgba(0, 0, 0, 0.05)',
                    border: `1px solid ${isDark ? 'rgba(255, 255, 255, 0.1)' : 'rgba(0, 0, 0, 0.1)'}`,
                    color: isDark ? 'rgba(255,255,255,0.8)' : 'rgba(0,0,0,0.8)',
                  }}
                >
                  {t('cancel', settings.language)}
                </motion.button>
                
                <motion.button
                  whileHover={{ scale: 1.02 }}
                  whileTap={{ scale: 0.98 }}
                  onClick={handleResetSettings}
                  className="px-5 py-2.5 rounded-xl text-sm font-semibold text-white"
                  style={{
                    background: 'linear-gradient(135deg, #EF4444, #DC2626)',
                    boxShadow: '0 4px 15px rgba(239, 68, 68, 0.35)',
                  }}
                >
                  {t('confirm', settings.language)}
                </motion.button>
              </div>
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>

      {/* Toast Notification */}
      <AnimatePresence>
        {toast && (
          <motion.div
            initial={{ opacity: 0, y: -50, scale: 0.95 }}
            animate={{ opacity: 1, y: 0, scale: 1 }}
            exit={{ opacity: 0, y: -50, scale: 0.95 }}
            className="fixed top-20 right-4 z-50 flex items-center gap-3 px-4 py-3 rounded-xl shadow-2xl max-w-sm"
            style={{
              background: isDark ? 'rgba(30, 30, 50, 0.95)' : 'rgba(255, 255, 255, 0.95)',
              border: `1px solid ${toast.type === 'success' ? 'rgba(16, 185, 129, 0.4)' : 'rgba(239, 68, 68, 0.4)'}`,
              boxShadow: '0 10px 40px rgba(0, 0, 0, 0.3)',
            }}
          >
            <div 
              className="w-8 h-8 rounded-lg flex items-center justify-center"
              style={{ 
                background: toast.type === 'success' ? 'rgba(16, 185, 129, 0.15)' : 'rgba(239, 68, 68, 0.15)',
              }}
            >
              {toast.type === 'success' ? (
                <Check size={18} className="text-emerald-500" />
              ) : (
                <XCircle size={18} className="text-red-500" />
              )}
            </div>
            <p className="flex-1 text-sm font-medium" style={{ color: isDark ? '#fff' : '#1a1a2e' }}>
              {toast.message}
            </p>
            <button 
              onClick={() => setToast(null)} 
              className="opacity-50 hover:opacity-100 transition-opacity"
              style={{ color: isDark ? 'rgba(255,255,255,0.5)' : 'rgba(0,0,0,0.5)' }}
            >
              <XCircle size={16} />
            </button>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}

export default SettingsView;
