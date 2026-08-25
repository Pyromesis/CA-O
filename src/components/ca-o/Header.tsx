'use client';

import { motion } from 'framer-motion';
import { useAppStore } from '@/store/useAppStore';
import { t } from '@/lib/i18n';
import {
  Sun,
  Moon,
  Globe,
  Gauge,
  Heart,
  Activity,
  HelpCircle,
} from 'lucide-react';
import { cn } from '@/lib/utils';
import { useMemo } from 'react';
import NotificationCenter from './NotificationCenter';

interface HeaderProps {
  className?: string;
}

export function Header({ className }: HeaderProps) {
  const { 
    settings, 
    updateSettings, 
    getAppliedCount, 
    getTotalCount,
    setShowHelpModal
  } = useAppStore();

  const appliedCount = getAppliedCount();
  const totalCount = getTotalCount();
  const isDark = settings.theme === 'dark';

  // Compute health score as derived value (base 60 to avoid showing "error" at start)
  const healthScore = useMemo(() => {
    // Base score of 60 + bonus for applied optimizations (up to 40)
    return 60 + Math.round((appliedCount / totalCount) * 40);
  }, [appliedCount, totalCount]);

  const toggleTheme = () => {
    updateSettings({ theme: isDark ? 'light' : 'dark' });
  };

  const toggleLanguage = () => {
    updateSettings({ language: settings.language === 'es' ? 'en' : 'es' });
  };

  const getHealthColor = (score: number): string => {
    if (score >= 85) return '#10B981'; // Green - Excellent
    if (score >= 70) return '#F59E0B'; // Yellow - Good
    return '#3B82F6'; // Blue - Normal (not red/error)
  };

  const getHealthLabel = (score: number): string => {
    if (score >= 85) return t('success', settings.language);
    if (score >= 70) return t('warning', settings.language);
    return t('info', settings.language); // Use 'info' instead of 'error'
  };

  return (
    <motion.header
      initial={{ y: -20, opacity: 0 }}
      animate={{ y: 0, opacity: 1 }}
      transition={{ duration: 0.5, ease: 'easeOut' }}
      className={cn(
        'sticky top-0 z-30 w-full',
        className
      )}
    >
      {/* Glassmorphism Background */}
      <div
        className="flex items-center justify-between px-4 md:px-6 py-3 mx-4 mt-4 rounded-2xl"
        style={{
          background: isDark
            ? 'rgba(18, 18, 35, 0.72)'
            : 'rgba(255, 255, 255, 0.82)',
          backdropFilter: 'blur(24px) saturate(1.3)',
          WebkitBackdropFilter: 'blur(24px) saturate(1.3)',
          border: `1px solid ${isDark ? 'rgba(255, 255, 255, 0.06)' : 'rgba(0, 0, 0, 0.06)'}`,
          boxShadow: isDark
            ? '0 1px 2px rgba(0,0,0,0.15), 0 4px 16px rgba(0,0,0,0.12), inset 0 1px 0 rgba(255,255,255,0.04)'
            : '0 1px 2px rgba(0,0,0,0.05), 0 4px 16px rgba(0,0,0,0.06), inset 0 1px 0 rgba(255,255,255,0.9)',
        }}
      >
        {/* Left Section - Title */}
        <div className="flex items-center gap-3">
          {/* Title & Subtitle */}
          <div className="hidden sm:block">
            <h1 
              className="text-base font-semibold tracking-tight"
              style={{ color: isDark ? '#fff' : '#1a1a2e' }}
            >
              CA-O
            </h1>
            <p 
              className="text-xs -mt-0.5"
              style={{ color: isDark ? 'rgba(255,255,255,0.5)' : 'rgba(0,0,0,0.5)' }}
            >
              {t('appTagline', settings.language)}
            </p>
          </div>
        </div>

        {/* Center Section - Health Score (Desktop) */}
        <div className="hidden md:flex items-center gap-4">
          <motion.div
            className="flex items-center gap-3 px-4 py-2 rounded-xl"
            style={{
              background: isDark ? 'rgba(255, 255, 255, 0.05)' : 'rgba(0, 0, 0, 0.03)',
              border: `1px solid ${isDark ? 'rgba(255, 255, 255, 0.06)' : 'rgba(0, 0, 0, 0.06)'}`,
            }}
          >
            <Activity size={16} style={{ color: getHealthColor(healthScore) }} />
            <div className="flex flex-col">
              <span 
                className="text-[10px] uppercase tracking-wider font-medium"
                style={{ color: isDark ? 'rgba(255,255,255,0.5)' : 'rgba(0,0,0,0.5)' }}
              >
                {t('healthScore', settings.language)}
              </span>
              <div className="flex items-center gap-1.5">
                <motion.span
                  key={healthScore}
                  initial={{ opacity: 0, y: -5 }}
                  animate={{ opacity: 1, y: 0 }}
                  className="text-lg font-bold tabular-nums"
                  style={{ color: getHealthColor(healthScore) }}
                >
                  {healthScore}
                </motion.span>
                <span 
                  className="text-[10px] font-medium px-1.5 py-0.5 rounded-full"
                  style={{
                    backgroundColor: `${getHealthColor(healthScore)}20`,
                    color: getHealthColor(healthScore),
                  }}
                >
                  {getHealthLabel(healthScore)}
                </span>
              </div>
            </div>

            {/* Mini Progress Ring */}
            <svg width="36" height="36" viewBox="0 0 36 36" className="-mr-1">
              <circle
                cx="18"
                cy="18"
                r="15"
                fill="none"
                stroke={isDark ? 'rgba(255,255,255,0.08)' : 'rgba(0,0,0,0.08)'}
                strokeWidth="3"
              />
              <motion.circle
                cx="18"
                cy="18"
                r="15"
                fill="none"
                stroke={getHealthColor(healthScore)}
                strokeWidth="3"
                strokeLinecap="round"
                strokeDasharray={`${healthScore * 0.94} 94`}
                initial={{ strokeDasharray: '0 94' }}
                animate={{ strokeDasharray: `${healthScore * 0.94} 94` }}
                transition={{ duration: 0.8, ease: 'easeOut' }}
                transform="rotate(-90 18 18)"
              />
              <text
                x="18"
                y="19"
                textAnchor="middle"
                fontSize="8"
                fontWeight="600"
                fill={getHealthColor(healthScore)}
                dy="0"
              >
                {healthScore}%
              </text>
            </svg>
          </motion.div>

          {/* Applied Optimizations Counter */}
          <div 
            className="flex items-center gap-2 px-3 py-2 rounded-xl"
            style={{
              background: isDark ? 'rgba(16, 185, 129, 0.1)' : 'rgba(16, 185, 129, 0.1)',
              border: `1px solid ${isDark ? 'rgba(16, 185, 129, 0.2)' : 'rgba(16, 185, 129, 0.2)'}`,
            }}
          >
            <Heart size={14} className="text-emerald-500" fill="currentColor" />
            <span className="text-sm font-medium text-emerald-500">
              {appliedCount}/{totalCount}
            </span>
          </div>
        </div>

        {/* Right Section - Controls */}
        <div className="flex items-center gap-2">
          <NotificationCenter />
          {/* Theme Toggle */}
          <motion.button
            onClick={toggleTheme}
            whileHover={{ scale: 1.05 }}
            whileTap={{ scale: 0.92 }}
            className="relative w-10 h-10 rounded-xl flex items-center justify-center transition-colors"
            style={{
              background: isDark ? 'rgba(255, 255, 255, 0.08)' : 'rgba(0, 0, 0, 0.05)',
              border: `1px solid ${isDark ? 'rgba(255, 255, 255, 0.1)' : 'rgba(0, 0, 0, 0.08)'}`,
            }}
            title={isDark ? t('settingsLight', settings.language) : t('settingsDark', settings.language)}
          >
            <AnimateIcon condition={isDark}>
              <Moon size={18} className="text-yellow-400" />
            </AnimateIcon>
            <AnimateIcon condition={!isDark}>
              <Sun size={18} className="text-orange-400" />
            </AnimateIcon>
          </motion.button>

          {/* Language Toggle */}
          <motion.button
            onClick={toggleLanguage}
            whileHover={{ scale: 1.05 }}
            whileTap={{ scale: 0.92 }}
            className="relative h-10 px-3 rounded-xl flex items-center justify-center gap-1.5 font-semibold text-sm transition-colors"
            style={{
              background: isDark ? 'rgba(255, 255, 255, 0.08)' : 'rgba(0, 0, 0, 0.05)',
              border: `1px solid ${isDark ? 'rgba(255, 255, 255, 0.1)' : 'rgba(0, 0, 0, 0.08)'}`,
              color: isDark ? '#fff' : '#1a1a2e',
            }}
            title={settings.language === 'es' ? t('settingsEnglish', settings.language) : t('settingsSpanish', settings.language)}
          >
            <Globe size={16} className={cn(
              settings.language === 'en' ? 'text-blue-400' : 'text-red-400'
            )} />
            <span>{settings.language.toUpperCase()}</span>
          </motion.button>

          {/* Help Button */}
          <motion.button
            onClick={() => setShowHelpModal(true)}
            whileHover={{ scale: 1.05 }}
            whileTap={{ scale: 0.92 }}
            className="relative w-10 h-10 rounded-xl flex items-center justify-center transition-colors"
            style={{
              background: isDark ? 'rgba(255, 107, 53, 0.1)' : 'rgba(255, 107, 53, 0.08)',
              border: `1px solid ${isDark ? 'rgba(255, 107, 53, 0.2)' : 'rgba(255, 107, 53, 0.15)'}`,
            }}
            title={t('helpTitle', settings.language) + ' (?)'}
          >
            <HelpCircle size={18} className="text-[#FF6B35]" />
            {/* Keyboard shortcut hint */}
            <span 
              className="absolute -top-1 -right-1 w-4 h-4 rounded-full text-[9px] font-bold flex items-center justify-center"
              style={{
                background: '#FF6B35',
                color: '#fff',
              }}
            >
              ?
            </span>
          </motion.button>
        </div>
      </div>
    </motion.header>
  );
}

// Helper component for animated icon switching
function AnimateIcon({ 
  condition, 
  children 
}: { 
  condition: boolean; 
  children: React.ReactNode;
}) {
  return (
    <motion.div
      initial={false}
      animate={{
        opacity: condition ? 1 : 0,
        scale: condition ? 1 : 0.8,
        rotate: condition ? 0 : -90,
      }}
      transition={{ duration: 0.2, ease: 'easeInOut' }}
      className="absolute inset-0 flex items-center justify-center"
    >
      {children}
    </motion.div>
  );
}

export default Header;
