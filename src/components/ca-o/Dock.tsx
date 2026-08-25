'use client';

import { motion, AnimatePresence } from 'framer-motion';
import { useAppStore } from '@/store/useAppStore';
import { t } from '@/lib/i18n';
import {
  LayoutDashboard,
  SlidersHorizontal,
  Wrench,
  Settings,
  Zap,
} from 'lucide-react';
import { cn } from '@/lib/utils';

type ViewType = 'dashboard' | 'optimization' | 'troubleshooting' | 'settings';

interface DockItem {
  id: ViewType;
  icon: React.ElementType;
  labelKey: string;
  color: string;
}

const dockItems: DockItem[] = [
  {
    id: 'dashboard',
    icon: LayoutDashboard,
    labelKey: 'dashboard',
    color: '#3B82F6',
  },
  {
    id: 'optimization',
    icon: SlidersHorizontal,
    labelKey: 'optimization',
    color: '#10B981',
  },
  {
    id: 'troubleshooting',
    icon: Wrench,
    labelKey: 'troubleshooting',
    color: '#F59E0B',
  },
  {
    id: 'settings',
    icon: Settings,
    labelKey: 'settings',
    color: '#8B5CF6',
  },
];

interface DockProps {
  className?: string;
}

export function Dock({ className }: DockProps) {
  const { currentView, setCurrentView, settings, getAppliedCount } = useAppStore();
  
  const appliedCount = getAppliedCount();

  const handleItemClick = (viewId: ViewType) => {
    setCurrentView(viewId);
  };

  return (
    <motion.div
      initial={{ y: 100, opacity: 0 }}
      animate={{ y: 0, opacity: 1 }}
      transition={{ 
        delay: 0.5, 
        duration: 0.6, 
        type: 'spring', 
        stiffness: 200, 
        damping: 25 
      }}
      className={cn(
        'fixed bottom-4 left-1/2 -translate-x-1/2 z-40',
        className
      )}
    >
      {/* Glassmorphism Container */}
      <div
        className="relative flex items-center gap-1 px-3 py-2 rounded-2xl"
        style={{
          background: 'rgba(22, 22, 40, 0.72)',
          backdropFilter: 'blur(24px) saturate(1.3)',
          WebkitBackdropFilter: 'blur(24px) saturate(1.3)',
          border: '1px solid rgba(255, 255, 255, 0.06)',
          boxShadow: `
            0 2px 8px rgba(0, 0, 0, 0.15),
            0 8px 24px rgba(0, 0, 0, 0.2),
            inset 0 1px 0 rgba(255, 255, 255, 0.04)
          `,
        }}
      >
        {/* Separator */}
        <div
          className="w-px h-8 mx-1"
          style={{ background: 'linear-gradient(to bottom, transparent, rgba(255,255,255,0.15), transparent)' }}
        />

        {/* Navigation Items */}
        <div className="flex items-center gap-1">
          {dockItems.map((item) => {
            const isActive = currentView === item.id;
            const Icon = item.icon;

            return (
              <motion.button
                key={item.id}
                onClick={() => handleItemClick(item.id)}
                whileHover={{ scale: 1.15, y: -8 }}
                whileTap={{ scale: 0.92 }}
                className="relative flex flex-col items-center justify-center group"
                title={t(item.labelKey, settings.language)}
              >
                {/* Tooltip - only visible on hover via CSS */}
                <div
                  className="absolute -top-10 left-1/2 -translate-x-1/2 px-3 py-1.5 rounded-lg text-xs font-medium whitespace-nowrap pointer-events-none opacity-0 group-hover:opacity-100 transition-opacity duration-200"
                  style={{
                    background: 'rgba(20, 20, 35, 0.95)',
                    backdropFilter: 'blur(10px)',
                    border: '1px solid rgba(255, 255, 255, 0.1)',
                    color: '#fff',
                    boxShadow: '0 4px 15px rgba(0, 0, 0, 0.3)',
                  }}
                >
                  {t(item.labelKey, settings.language)}
                  {/* Arrow */}
                  <div 
                    className="absolute -bottom-1 left-1/2 -translate-x-1/2 w-2 h-2 rotate-45"
                    style={{
                      background: 'rgba(20, 20, 35, 0.95)',
                      borderBottom: '1px solid rgba(255, 255, 255, 0.1)',
                      borderRight: '1px solid rgba(255, 255, 255, 0.1)',
                      borderBottomRightRadius: 2,
                    }}
                  />
                </div>

                {/* Icon Container */}
                <div
                  className={cn(
                    'relative w-11 h-11 rounded-xl flex items-center justify-center transition-all duration-200',
                    isActive && 'ring-2 ring-offset-2 ring-offset-transparent'
                  )}
                  style={{
                    background: isActive 
                      ? `rgba(${hexToRgb(item.color)}, 0.2)` 
                      : 'transparent',
                    borderColor: isActive ? item.color : 'transparent',
                  }}
                >
                  <Icon
                    size={22}
                    style={{
                      color: isActive ? item.color : 'rgba(255, 255, 255, 0.6)',
                      transition: 'color 0.2s ease',
                    }}
                    strokeWidth={isActive ? 2.5 : 2}
                  />

                  {/* Active Indicator Dot */}
                  <AnimatePresence>
                    {isActive && (
                      <motion.div
                        initial={{ scale: 0, opacity: 0 }}
                        animate={{ scale: 1, opacity: 1 }}
                        exit={{ scale: 0, opacity: 0 }}
                        transition={{ type: 'spring', stiffness: 400, damping: 20 }}
                        className="absolute -bottom-1.5 left-1/2 -translate-x-1/2 w-1 h-1 rounded-full"
                        style={{
                          backgroundColor: item.color,
                          boxShadow: `0 0 6px ${item.color}`,
                        }}
                      />
                    )}
                  </AnimatePresence>
                </div>
              </motion.button>
            );
          })}
        </div>

        {/* Separator */}
        <div 
          className="w-px h-8 mx-1"
          style={{ background: 'linear-gradient(to bottom, transparent, rgba(255,255,255,0.15), transparent)' }}
        />

        {/* Applied Count Badge */}
        <motion.div
          whileHover={{ scale: 1.05 }}
          className="flex items-center gap-2 px-3 py-1.5 rounded-lg ml-1"
          style={{
            background: appliedCount > 0 
              ? 'rgba(16, 185, 129, 0.15)' 
              : 'rgba(255, 255, 255, 0.05)',
            border: `1px solid ${appliedCount > 0 ? 'rgba(16, 185, 129, 0.3)' : 'rgba(255, 255, 255, 0.08)'}`,
          }}
        >
          <div
            className="w-2 h-2 rounded-full"
            style={{
              backgroundColor: appliedCount > 0 ? '#10B981' : 'rgba(255, 255, 255, 0.3)',
              boxShadow: appliedCount > 0 ? '0 0 8px rgba(16, 185, 129, 0.6)' : 'none',
            }}
          />
          <span 
            className="text-xs font-mono font-medium tabular-nums"
            style={{ 
              color: appliedCount > 0 ? '#10B981' : 'rgba(255, 255, 255, 0.5)',
            }}
          >
            {appliedCount}
          </span>
        </motion.div>
      </div>

      {/* Dock Reflection Effect (Subtle) */}
      <div
        className="absolute -bottom-2 left-0 right-0 h-4 blur-sm opacity-30 rounded-b-2xl"
        style={{
          background: 'linear-gradient(to bottom, rgba(255, 107, 53, 0.3), transparent)',
          transform: 'scaleY(-1)',
          maskImage: 'linear-gradient(to top, black, transparent)',
          WebkitMaskImage: 'linear-gradient(to top, black, transparent)',
        }}
      />
    </motion.div>
  );
}

// Helper function to convert hex to RGB
function hexToRgb(hex: string): string {
  const result = /^#?([a-f\d]{2})([a-f\d]{2})([a-f\d]{2})$/i.exec(hex);
  if (!result) return '255, 255, 255';
  return `${parseInt(result[1], 16)}, ${parseInt(result[2], 16)}, ${parseInt(result[3], 16)}`;
}

export default Dock;
