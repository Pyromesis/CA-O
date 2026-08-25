'use client';

import { useState, useEffect, useMemo, useSyncExternalStore, useCallback, useRef } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { useAppStore } from '@/store/useAppStore';
import { t } from '@/lib/i18n';
import { Category, OptimizationCategory } from '@/types/optimization';
import {
  Monitor,
  Wifi,
  MousePointer2,
  Sparkles,
  Zap,
  Rocket,
  Cpu,
  MemoryStick,
  Activity,
  CheckCircle2,
  ArrowRight,
  Sun,
  Moon,
  Star,
} from 'lucide-react';
import ProfileSelector from './ProfileSelector';
import { OptimizationHistory } from './OptimizationHistory';
import { UndoQueue } from './UndoQueue';
import { cn } from '@/lib/utils';
import type { ComponentType } from 'react';
import { LucideProps } from 'lucide-react';

// Type for Lucide icons
type LucideIcon = ComponentType<LucideProps>;

interface DashboardViewProps {
  className?: string;
}

// ============================================
// Time-based Greeting Hook
// ============================================
function useTimeBasedGreeting(language: 'es' | 'en'): string {
  const isMounted = useSyncExternalStore(
    () => () => {},
    () => true,
    () => false
  );
  
  return useMemo(() => {
    if (!isMounted) return '';
    
    const hour = new Date().getHours();
    
    if (language === 'es') {
      if (hour >= 5 && hour < 12) return '¡Buenos días! ☀️';
      else if (hour >= 12 && hour < 19) return '¡Buenas tardes! 🌅';
      else return '¡Buenas noches! 🌙';
    } else {
      if (hour >= 5 && hour < 12) return 'Good Morning! ☀️';
      else if (hour >= 12 && hour < 19) return 'Good Afternoon! 🌅';
      else return 'Good Evening! 🌙';
    }
  }, [language, isMounted]);
}

// ============================================
// Animated Background Component
// ============================================
function AnimatedBackground({ isDark }: { isDark: boolean }) {
  return (
    <motion.div 
      className="fixed inset-0 -z-10 overflow-hidden"
      initial={{ opacity: 0 }}
      animate={{ opacity: 1 }}
      transition={{ duration: 1 }}
    >
      {/* Base gradient with animation */}
      <div 
        className="absolute inset-0"
        style={{
          background: isDark 
            ? `linear-gradient(135deg, #0a0a14 0%, #0f0f1f 25%, #12122a 50%, #0d0d1a 75%, #0a0a14 100%)`
            : `linear-gradient(135deg, #f8fafc 0%, #e2e8f0 25%, #f1f5f9 50%, #e2e8f0 75%, #f8fafc 100%)`,
          backgroundSize: '400% 400%',
          animation: 'bg-gradient-shift 20s ease infinite',
        }}
      />
      
      {/* Floating gradient orbs */}
      <motion.div
        animate={{ 
          x: [0, 30, -20, 10, 0],
          y: [0, -20, 30, -10, 0],
          scale: [1, 1.1, 0.95, 1.05, 1],
        }}
        transition={{ 
          duration: 15, 
          repeat: Infinity, 
          ease: 'easeInOut' 
        }}
        className="absolute top-[15%] left-[10%] w-[400px] h-[400px] rounded-full blur-[80px]"
        style={{
          background: isDark ? 'rgba(255, 107, 53, 0.08)' : 'rgba(255, 107, 53, 0.12)',
        }}
      />
      
      <motion.div
        animate={{ 
          x: [0, -25, 20, -15, 0],
          y: [0, 25, -15, 20, 0],
          scale: [1, 0.95, 1.08, 0.98, 1],
        }}
        transition={{ 
          duration: 18, 
          repeat: Infinity, 
          ease: 'easeInOut' 
        }}
        className="absolute bottom-[20%] right-[10%] w-[350px] h-[350px] rounded-full blur-[70px]"
        style={{
          background: isDark ? 'rgba(139, 92, 246, 0.06)' : 'rgba(99, 102, 241, 0.1)',
        }}
      />

      <motion.div
        animate={{ 
          x: [0, 15, -10, 20, 0],
          y: [0, -15, 20, -5, 0],
        }}
        transition={{ 
          duration: 12, 
          repeat: Infinity, 
          ease: 'easeInOut' 
        }}
        className="absolute top-[50%] right-[30%] w-[250px] h-[250px] rounded-full blur-[60px]"
        style={{
          background: isDark ? 'rgba(59, 130, 246, 0.05)' : 'rgba(59, 130, 246, 0.08)',
        }}
      />

      {/* Subtle grid pattern overlay */}
      <div 
        className="absolute inset-0 opacity-[0.03]"
        style={{
          backgroundImage: `
            linear-gradient(rgba(255,255,255,.1) 1px, transparent 1px),
            linear-gradient(90deg, rgba(255,255,255,.1) 1px, transparent 1px)
          `,
          backgroundSize: '50px 50px',
        }}
      />
    </motion.div>
  );
}

// ============================================
// Particle Effect Component for Buttons/Cards
// ============================================
function ParticleEffect({ color = '#FF6B35', isActive = false }: { color?: string; isActive?: boolean }) {
  const particles = useMemo(() => 
    Array.from({ length: 6 }, (_, i) => ({
      id: i,
      x: Math.random() * 100,
      delay: Math.random() * 2,
      size: Math.random() * 3 + 2,
    }))
  , []);

  return (
    <div className="absolute inset-0 overflow-hidden pointer-events-none rounded-2xl">
      <AnimatePresence>
        {isActive && particles.map((particle) => (
          <motion.div
            key={particle.id}
            initial={{ 
              y: '100%', 
              x: `${particle.x}%`,
              opacity: 0,
              scale: 0 
            }}
            animate={{ 
              y: '-20%',
              x: `${particle.x + (Math.random() * 40 - 20)}%`,
              opacity: [0, 1, 1, 0],
              scale: [0, particle.size / 3, particle.size / 4, 0],
            }}
            exit={{ opacity: 0 }}
            transition={{ 
              duration: 2.5, 
              delay: particle.delay,
              repeat: Infinity,
              ease: 'easeOut'
            }}
            className="absolute w-1 h-1 rounded-full"
            style={{
              backgroundColor: color,
              boxShadow: `0 0 ${particle.size * 2}px ${color}`,
              left: `${particle.x}%`,
            }}
          />
        ))}
      </AnimatePresence>
    </div>
  );
}

// ============================================
// Floating Particles Inside Cards on Hover
// ============================================
function CardFloatingParticles({ color, isActive = false }: { color: string; isActive: boolean }) {
  const particles = useMemo(() => 
    Array.from({ length: 8 }, (_, i) => ({
      id: i,
      x: 10 + Math.random() * 80,
      y: 10 + Math.random() * 80,
      delay: Math.random() * 3,
      duration: 2 + Math.random() * 2,
      size: 2 + Math.random() * 3,
    }))
  , []);

  return (
    <div className="absolute inset-0 overflow-hidden pointer-events-none rounded-2xl">
      <AnimatePresence>
        {isActive && particles.map((particle) => (
          <motion.div
            key={particle.id}
            initial={{ 
              opacity: 0,
              scale: 0,
              x: `${particle.x}%`,
              y: `${particle.y}%`
            }}
            animate={{ 
              opacity: [0, 0.8, 0.6, 0],
              scale: [0, 1, 0.8, 0],
              y: [`${particle.y}%`, `${particle.y - 30}%`, `${particle.y - 15}%`],
              x: [`${particle.x}%`, `${particle.x + (Math.random() * 20 - 10)}%`]
            }}
            exit={{ opacity: 0 }}
            transition={{ 
              duration: particle.duration, 
              delay: particle.delay,
              repeat: Infinity,
              ease: 'easeInOut'
            }}
            className="absolute rounded-full"
            style={{
              width: particle.size,
              height: particle.size,
              backgroundColor: color,
              boxShadow: `0 0 ${particle.size * 3}px ${color}`,
              left: `${particle.x}%`,
              top: `${particle.y}%`,
            }}
          />
        ))}
      </AnimatePresence>
    </div>
  );
}

// ============================================
// Ripple Effect Component
// ============================================
function RippleEffect({ isActive, color }: { isActive: boolean; color: string }) {
  const [ripples, setRipples] = useState<Array<{ id: number; x: number; y: number }>>([]);
  
  const createRipple = useCallback((e: React.MouseEvent<HTMLDivElement>) => {
    const rect = e.currentTarget.getBoundingClientRect();
    const x = e.clientX - rect.left;
    const y = e.clientY - rect.top;
    const id = Date.now();
    
    setRipples(prev => [...prev, { id, x, y }]);
    
    setTimeout(() => {
      setRipples(prev => prev.filter(r => r.id !== id));
    }, 600);
  }, []);

  useEffect(() => {
    // Clean up old ripples on unmount
    return () => setRipples([]);
  }, []);

  return (
    <>
      <div className="absolute inset-0 overflow-hidden rounded-2xl pointer-events-none" onClick={createRipple}>
        <AnimatePresence>
          {ripples.map((ripple) => (
            <motion.span
              key={ripple.id}
              initial={{ 
                width: 0, 
                height: 0, 
                x: ripple.x, 
                y: ripple.y,
                opacity: 0.5,
                scale: 0
              }}
              animate={{ 
                width: 200, 
                height: 200, 
                x: ripple.x - 100,
                y: ripple.y - 100,
                opacity: 0,
                scale: 1
              }}
              exit={{ opacity: 0 }}
              transition={{ duration: 0.6, ease: "easeOut" as const }}
              className="absolute rounded-full"
              style={{
                background: `radial-gradient(circle, ${color}40 0%, transparent 70%)`,
              }}
            />
          ))}
        </AnimatePresence>
      </div>
      {!isActive && null}
    </>
  );
}

// ============================================
// Glass Reflection Overlay
// ============================================
function GlassReflection({ isActive }: { isActive: boolean }) {
  return (
    <motion.div
      className="absolute inset-0 rounded-2xl pointer-events-none overflow-hidden"
      initial={false}
      animate={{ opacity: isActive ? 1 : 0 }}
      transition={{ duration: 0.3 }}
      style={{
        background: `linear-gradient(
          135deg,
          rgba(255, 255, 255, 0.15) 0%,
          transparent 40%,
          transparent 60%,
          rgba(255, 255, 255, 0.05) 100%
        )`,
      }}
    >
      {/* Reflection shine line */}
      <motion.div
        className="absolute top-0 left-[-100%] w-[50%] h-full"
        animate={{ 
          left: isActive ? '150%' : '-100%'
        }}
        transition={{ 
          duration: 1.2, 
          ease: "easeInOut" as const,
          repeat: isActive ? Infinity : 0,
          repeatDelay: 2
        }}
        style={{
          background: 'linear-gradient(90deg, transparent, rgba(255,255,255,0.1), transparent)',
          transform: 'skewX(-20deg)',
        }}
      />
    </motion.div>
  );
}

// ============================================
// Animated Gradient Border
// ============================================
function GradientBorderAnimation({ isActive, colors }: { isActive: boolean; colors: string }) {
  return (
    <motion.div
      className="absolute inset-0 rounded-2xl overflow-hidden pointer-events-none"
      initial={false}
      animate={{ opacity: isActive ? 1 : 0 }}
      transition={{ duration: 0.3 }}
    >
      <div
        className="absolute inset-[-2px] rounded-[18px]"
        style={{
          background: `linear-gradient(90deg, ${colors}, ${colors.replace('FF6B35', 'ff8c42').replace('3B82F6', '60A5FA').replace('10B981', '34D399').replace('8B5CF6', 'A78BFA').replace('F59E0B', 'FBBF24').replace('EF4444', 'F87171')}, ${colors})`,
          backgroundSize: '300% 300%',
          animation: isActive ? 'gradient-border-flow 3s ease infinite' : 'none',
        }}
      />
    </motion.div>
  );
}

// ============================================
// Enhanced Progress Ring Component
// ============================================
function ProgressRing({ 
  progress, 
  size = 56, 
  strokeWidth = 4, 
  color = '#FF6B35',
  showLabel = true,
  delay = 0,
}: {
  progress: number;
  size?: number;
  strokeWidth?: number;
  color?: string;
  showLabel?: boolean;
  delay?: number;
}) {
  const radius = (size - strokeWidth) / 2;
  const circumference = radius * 2 * Math.PI;
  const offset = circumference - (progress / 100) * circumference;

  return (
    <div className="relative" style={{ width: size, height: size }}>
      <svg width={size} height={size} className="transform -rotate-90">
        {/* Background circle */}
        <circle
          cx={size / 2}
          cy={size / 2}
          r={radius}
          fill="none"
          stroke="currentColor"
          strokeWidth={strokeWidth}
          className="text-white/5 dark:text-white/10"
        />
        
        {/* Glow effect circle */}
        <motion.circle
          cx={size / 2}
          cy={size / 2}
          r={radius}
          fill="none"
          stroke={color}
          strokeWidth={strokeWidth + 2}
          strokeLinecap="round"
          strokeDasharray={circumference}
          initial={{ strokeDashoffset: circumference }}
          animate={{ strokeDashoffset: offset }}
          transition={{ 
            duration: 1.5, 
            delay,
            ease: "easeInOut" as const
          }}
          className="opacity-40"
          style={{ filter: `drop-shadow(0 0 ${(progress > 0 ? 8 : 0)}px ${color})` }}
        />
        
        {/* Main progress circle */}
        <motion.circle
          cx={size / 2}
          cy={size / 2}
          r={radius}
          fill="none"
          stroke={color}
          strokeWidth={strokeWidth}
          strokeLinecap="round"
          strokeDasharray={circumference}
          initial={{ strokeDashoffset: circumference }}
          animate={{ strokeDashoffset: offset }}
          transition={{ 
            duration: 1.5, 
            delay,
            ease: "easeInOut" as const
          }}
        />
      </svg>
      
      {/* Percentage label */}
      <AnimatePresence>
        {showLabel && (
          <motion.span
            key={`label-${progress}`}
            initial={{ opacity: 0, scale: 0.5 }}
            animate={{ opacity: 1, scale: 1 }}
            transition={{ duration: 0.3, delay: delay + 0.5 }}
            className="absolute inset-0 flex items-center justify-center text-xs font-bold tabular-nums"
            style={{ color }}
          >
            {Math.round(progress)}%
          </motion.span>
        )}
      </AnimatePresence>
    </div>
  );
}

// ============================================
// Enhanced Category Type with Lucide Icon
// ============================================
interface EnhancedCategory {
  id: Category;
  nameKey: string;
  descriptionKey: string;
  color: string;
  itemCount: number;
  gradient: string;
  icon: LucideIcon;
  glowColor: string;
  gradientBorderColors: string;
}

const categories: EnhancedCategory[] = [
  {
    id: Category.System,
    nameKey: 'categorySystem',
    descriptionKey: 'categorySystemDesc',
    icon: Monitor,
    color: '#3B82F6',
    itemCount: 9,
    gradient: 'linear-gradient(135deg, #3B82F6, #1D4ED8)',
    glowColor: 'rgba(59, 130, 246, 0.5)',
    gradientBorderColors: '#3B82F6, #60A5FA, #93C5FD, #3B82F6',
  },
  {
    id: Category.Network,
    nameKey: 'categoryNetwork',
    descriptionKey: 'categoryNetworkDesc',
    icon: Wifi,
    color: '#10B981',
    itemCount: 7,
    gradient: 'linear-gradient(135deg, #10B981, #059669)',
    glowColor: 'rgba(16, 185, 129, 0.5)',
    gradientBorderColors: '#10B981, #34D399, #6EE7B7, #10B981',
  },
  {
    id: Category.Input,
    nameKey: 'categoryInput',
    descriptionKey: 'categoryInputDesc',
    icon: MousePointer2,
    color: '#8B5CF6',
    itemCount: 4,
    gradient: 'linear-gradient(135deg, #8B5CF6, #7C3AED)',
    glowColor: 'rgba(139, 92, 246, 0.5)',
    gradientBorderColors: '#8B5CF6, #A78BFA, #C4B5FD, #8B5CF6',
  },
  {
    id: Category.Tweaks,
    nameKey: 'categoryTweaks',
    descriptionKey: 'categoryTweaksDesc',
    icon: Sparkles,
    color: '#F59E0B',
    itemCount: 5,
    gradient: 'linear-gradient(135deg, #F59E0B, #D97706)',
    glowColor: 'rgba(245, 158, 11, 0.5)',
    gradientBorderColors: '#F59E0B, #FBBF24, #FCD34D, #F59E0B',
  },
  {
    id: Category.Powerful,
    nameKey: 'categoryPowerful',
    descriptionKey: 'categoryPowerfulDesc',
    icon: Zap,
    color: '#EF4444',
    itemCount: 5,
    gradient: 'linear-gradient(135deg, #EF4444, #DC2626)',
    glowColor: 'rgba(239, 68, 68, 0.5)',
    gradientBorderColors: '#EF4444, #F87171, #FCA5A5, #EF4444',
  },
];

// ============================================
// System Info Hook
// ============================================
const emptySubscribe = () => () => {};

const useSystemInfo = () => {
  const isMounted = useSyncExternalStore(emptySubscribe, () => true, () => false);
  const [data, setData] = useState({
    cpuUsage: 0,
    memoryUsage: 0,
    uptime: '0:00',
    diskUsage: 0,
    cpuModel: 'No disponible',
    cpuCores: 0,
    cpuClock: 'No disponible',
    memoryTotal: 0,
    memorySpeed: 'No disponible',
    gpuName: 'No disponible',
    gpuVram: 0,
    osVersion: 'No disponible',
  });

  useEffect(() => {
    if (!isMounted) return;

    let isSubscribed = true;
    
    const fetchData = async () => {
      try {
        const res = await fetch('/api/system/info');
        const json = await res.json();
        if (json.success && json.data && isSubscribed) {
          const info = json.data;
          
          let maxDiskUsage = 0;
          if (info.disks && info.disks.length > 0) {
            maxDiskUsage = Math.max(...info.disks.map((d: any) => d.usagePercent));
          }
          
          setData({
            cpuUsage: info.cpu.usage,
            memoryUsage: info.memory.usagePercent,
            uptime: info.os.uptime.formatted,
            diskUsage: maxDiskUsage,
            cpuModel: info.cpu.name || 'No disponible',
            cpuCores: info.cpu.cores || 0,
            cpuClock: info.cpu.maxClock || info.cpu.baseClock || 'No disponible',
            memoryTotal: info.memory.totalGB || 0,
            memorySpeed: info.memory.slots?.find((slot: any) => slot.speedMHz > 0)?.speedMHz
              ? `${info.memory.slots.find((slot: any) => slot.speedMHz > 0).speedMHz} MHz`
              : 'No disponible',
            gpuName: info.gpu?.[0]?.name || 'No disponible',
            gpuVram: info.gpu?.[0]?.vramMB || 0,
            osVersion: `${info.os.name} ${info.os.version} (${info.os.build})`,
          });
        }
      } catch (err) {
        console.error('Failed to fetch system info:', err);
      }
    };

    fetchData();
    const interval = setInterval(() => {
        if (!document.hidden) fetchData();
      }, 5000); // Pausado cuando la ventana está minimizada

    return () => {
      isSubscribed = false;
      clearInterval(interval);
    };
  }, [isMounted]);
  
  return data;
};

// ============================================
// Stat Card Component with Glassmorphism
// ============================================
interface StatCardProps {
  title: string;
  value: number | string;
  unit?: string;
  icon: LucideIcon;
  iconColor: string;
  bgColor: string;
  progressValue?: number;
  progressGradient: string;
  isDark: boolean;
  index: number;
}

function StatCard({ 
  title, 
  value, 
  unit = '%',
  icon: Icon, 
  iconColor, 
  bgColor,
  progressValue,
  progressGradient,
  isDark,
  index 
}: StatCardProps) {
  return (
    <motion.div
      initial={{ opacity: 0, y: 30, scale: 0.95 }}
      animate={{ opacity: 1, y: 0, scale: 1 }}
      transition={{ 
        duration: 0.6, 
        delay: 0.2 + index * 0.1,
        ease: "easeOut" as const
      }}
      whileHover={{ 
        y: -6, 
        scale: 1.02,
        transition: { duration: 0.3, ease: "easeOut" as const }
      }}
      whileTap={{ scale: 0.98 }}
      className={cn(
        "relative p-5 rounded-2xl overflow-hidden cursor-default group",
        "card-hover-glow"
      )}
      style={{
        background: isDark 
          ? 'rgba(20, 20, 35, 0.7)' 
          : 'rgba(255, 255, 255, 0.85)',
        border: `1px solid ${isDark ? 'rgba(255, 255, 255, 0.06)' : 'rgba(0, 0, 0, 0.06)'}`,
        backdropFilter: 'blur(20px) saturate(180%)',
        WebkitBackdropFilter: 'blur(20px) saturate(180%)',
      }}
    >
      {/* Hover glow overlay */}
      <motion.div
        className="absolute inset-0 opacity-0 group-hover:opacity-100 transition-opacity duration-500"
        style={{
          background: `radial-gradient(circle at 50% 100%, ${bgColor.replace('0.15', '0.1')} 0%, transparent 60%)`,
        }}
      />
      
      {/* Shimmer effect on hover */}
      <motion.div 
        className="absolute inset-0 opacity-0 group-hover:opacity-100 pointer-events-none"
        initial={false}
        animate={{ x: ['-100%', '300%'] }}
        transition={{ duration: 1.5, ease: "easeInOut" as const }}
        style={{
          background: 'linear-gradient(90deg, transparent, rgba(255,255,255,0.08), transparent)',
        }}
      />

      <div className="relative z-10">
        {/* Header */}
        <div className="flex items-center justify-between mb-4">
          <motion.div 
            className="w-11 h-11 rounded-xl flex items-center justify-center relative"
            whileHover={{ rotate: 5, scale: 1.1 }}
            style={{ background: bgColor }}
          >
            <Icon size={22} style={{ color: iconColor }} />
            {/* Subtle pulse around icon */}
            <motion.div
              className="absolute inset-0 rounded-xl"
              animate={{ opacity: [0.3, 0.6, 0.3] }}
              transition={{ duration: 2, repeat: Infinity, delay: index * 0.3 }}
              style={{ background: bgColor, filter: 'blur(8px)' }}
            />
          </motion.div>
          
          <motion.span 
            className="text-xs font-semibold px-2.5 py-1 rounded-full backdrop-blur-sm"
            style={{ 
              background: bgColor,
              color: iconColor,
            }}
            whileHover={{ scale: 1.05 }}
          >
            {title}
          </motion.span>
        </div>

        {/* Value */}
        <p className="text-3xl font-bold mb-3 tracking-tight" 
           style={{ color: isDark ? '#fff' : '#1a1a2e' }}>
          <motion.span
            initial={{ opacity: 0, y: 10 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: 0.4 + index * 0.1 }}
          >
            {value}{unit}
          </motion.span>
        </p>

        {/* Animated Progress Bar */}
        {typeof value === 'number' && progressValue !== undefined && (
          <div className="h-2 rounded-full overflow-hidden relative" 
               style={{ background: isDark ? 'rgba(255,255,255,0.06)' : 'rgba(0,0,0,0.06)' }}>
            {/* Track glow */}
            <motion.div
              initial={{ width: 0 }}
              animate={{ width: `${progressValue}%` }}
              transition={{ duration: 1.5, delay: 0.5 + index * 0.1, ease: "easeOut" as const }}
              className="h-full rounded-full relative"
              style={{ background: progressGradient }}
            >
              {/* Shine effect */}
              <motion.div
                initial={{ x: '-100%' }}
                animate={{ x: '200%' }}
                transition={{ duration: 1.5, delay: 1 + index * 0.1, repeat: Infinity, repeatDelay: 2 }}
                className="absolute inset-0"
                style={{
                  background: 'linear-gradient(90deg, transparent, rgba(255,255,255,0.4), transparent)',
                }}
              />
            </motion.div>
            
            {/* Bar end glow */}
            {progressValue > 0 && (
              <motion.div
                className="absolute top-1/2 -translate-y-1/2 w-2 h-2 rounded-full"
                style={{ 
                  left: `calc(${progressValue}% - 4px)`,
                  boxShadow: `0 0 10px ${iconColor}, 0 0 20px ${iconColor}`,
                  background: iconColor,
                }}
              />
            )}
          </div>
        )}
      </div>
    </motion.div>
  );
}

// ============================================
// ENHANCED 3D Tilt Card Component
// ============================================
interface EnhancedCardProps {
  category: EnhancedCategory;
  progress: number;
  isDark: boolean;
  index: number;
  onClick: (id: Category) => void;
}

function EnhancedCategoryCard({ category, progress, isDark, index, onClick }: EnhancedCardProps) {
  const cardRef = useRef<HTMLDivElement>(null);
  const [tiltStyle, setTiltStyle] = useState<{ rotateX: number; rotateY: number; shinePos: { x: number; y: number } }>({
    rotateX: 0,
    rotateY: 0,
    shinePos: { x: 50, y: 50 }
  });
  const [isHovered, setIsHovered] = useState(false);

  const handleMouseMove = useCallback((e: React.MouseEvent<HTMLDivElement>) => {
    if (!cardRef.current) return;
    
    const rect = cardRef.current.getBoundingClientRect();
    const centerX = rect.left + rect.width / 2;
    const centerY = rect.top + rect.height / 2;
    
    const mouseX = e.clientX - centerX;
    const mouseY = e.clientY - centerY;
    
    // Calculate rotation (max 12 degrees)
    const rotateY = (mouseX / (rect.width / 2)) * 12;
    const rotateX = -(mouseY / (rect.height / 2)) * 12;
    
    // Calculate shine position percentage
    const shineX = ((e.clientX - rect.left) / rect.width) * 100;
    const shineY = ((e.clientY - rect.top) / rect.height) * 100;
    
    setTiltStyle({
      rotateX,
      rotateY,
      shinePos: { x: shineX, y: shineY }
    });
  }, []);

  const handleMouseEnter = useCallback(() => {
    setIsHovered(true);
  }, []);

  const handleMouseLeave = useCallback(() => {
    setTiltStyle({ rotateX: 0, rotateY: 0, shinePos: { x: 50, y: 50 } });
    setIsHovered(false);
  }, []);

  const handleClick = useCallback(() => {
    onClick(category.id);
  }, [category.id, onClick]);

  const Icon = category.icon;

  return (
    <motion.div
      ref={cardRef}
      variants={{
        hidden: { opacity: 0, y: 40, scale: 0.95 },
        visible: {
          opacity: 1,
          y: 0,
          scale: 1,
          transition: { duration: 0.6, ease: "easeOut" as const },
        },
      }}
      whileTap={{ scale: 0.97 }}
      onMouseMove={handleMouseMove}
      onMouseEnter={handleMouseEnter}
      onMouseLeave={handleMouseLeave}
      onClick={handleClick}
      className="relative p-5 rounded-2xl cursor-pointer overflow-hidden group"
      style={{
        background: isDark 
          ? 'rgba(20, 20, 35, 0.8)' 
          : 'rgba(255, 255, 255, 0.9)',
        border: `1px solid ${isDark ? 'rgba(255, 255, 255, 0.08)' : 'rgba(0, 0, 0, 0.08)'}`,
        backdropFilter: 'blur(20px) saturate(180%)',
        WebkitBackdropFilter: 'blur(20px) saturate(180%)',
        boxShadow: isHovered
          ? `
            ${tiltStyle.rotateY > 0 ? '-' : ''}${Math.abs(tiltStyle.rotateY) * 1.5}px ${Math.max(tiltStyle.rotateX * 1.5, 0)}px 30px rgba(0, 0, 0, 0.25),
            0 0 40px ${category.glowColor},
            inset 0 1px 0 rgba(255, 255, 255, 0.1)
          `
          : `0 4px 24px ${isDark ? 'rgba(0,0,0,0.2)' : 'rgba(0,0,0,0.08)'}`,
        transform: `perspective(1000px) rotateX(${tiltStyle.rotateX}deg) rotateY(${tiltStyle.rotateY}deg)`,
        transition: 'box-shadow 0.3s ease, transform 0.1s ease-out, background 0.3s ease',
      }}
    >
      {/* Gradient Border Animation */}
      <GradientBorderAnimation isActive={isHovered} colors={category.gradientBorderColors} />

      {/* Inner Glow that intensifies on hover */}
      <motion.div
        className="absolute inset-0 rounded-2xl pointer-events-none"
        animate={{
          background: isHovered
            ? `radial-gradient(circle at ${tiltStyle.shinePos.x}% ${tiltStyle.shinePos.y}%, ${category.glowColor} 0%, transparent 60%)`
            : 'transparent'
        }}
        transition={{ duration: 0.2 }}
      />

      {/* Floating Particles on Hover */}
      <CardFloatingParticles color={category.color} isActive={isHovered} />

      {/* Ripple Effect */}
      <RippleEffect isActive={true} color={category.color} />

      {/* Glass Reflection Overlay */}
      <GlassReflection isActive={isHovered} />

      {/* Dynamic Shine based on mouse position */}
      <div
        className="absolute inset-0 rounded-2xl pointer-events-none opacity-0 group-hover:opacity-100 transition-opacity duration-300"
        style={{
          background: `radial-gradient(circle at ${tiltStyle.shinePos.x}% ${tiltStyle.shinePos.y}%, rgba(255,255,255,${isHovered ? 0.15 : 0}) 0%, transparent 50%)`,
        }}
      />

      {/* Gradient Accent Line at Top */}
      <motion.div 
        className="absolute top-0 left-0 right-0 h-1 origin-left"
        style={{ background: category.gradient }}
        initial={{ scaleX: 0 }}
        animate={{ scaleX: 1 }}
        transition={{ delay: 0.5, duration: 0.6, ease: "easeOut" as const }}
      />

      <div className="relative z-10">
        {/* Header */}
        <div className="flex items-start justify-between mb-4">
          {/* Icon with bounce animation on hover */}
          <motion.div
            className="w-13 h-13 rounded-xl flex items-center justify-center relative"
            style={{ background: `${category.color}18` }}
            animate={
              isHovered
                ? {
                    scale: [1, 1.15, 1.05],
                    rotate: [0, 5, -5, 0],
                    y: [0, -4, 0],
                  }
                : {}
            }
            transition={{
              duration: 0.5,
              ease: "easeOut" as const,
              repeat: isHovered ? 1 : 0,
            }}
          >
            <Icon size={26} style={{ color: category.color }} />
            
            {/* Icon pulse glow intensifies on hover */}
            <motion.div
              className="absolute inset-0 rounded-xl"
              animate={{ 
                opacity: isHovered ? [0.4, 0.7, 0.4] : [0.2, 0.5, 0.2],
                scale: isHovered ? [1, 1.2, 1] : [1, 1.05, 1],
              }}
              transition={{ 
                duration: isHovered ? 1 : 2.5, 
                repeat: Infinity,
                delay: categories.indexOf(category) * 0.2 
              }}
              style={{
                background: `${category.color}${isHovered ? '40' : '20'}`,
                filter: `blur(${isHovered ? 12 : 8}px)`,
              }}
            />
          </motion.div>

          {/* Enhanced Progress Ring */}
          <ProgressRing 
            progress={progress} 
            size={56} 
            strokeWidth={4} 
            color={category.color}
            showLabel={true}
            delay={0.3 + categories.indexOf(category) * 0.1}
          />
        </div>

        {/* Content */}
        <h4 
          className="text-lg font-bold mb-2 tracking-tight"
          style={{ color: isDark ? '#fff' : '#1a1a2e' }}
        >
          {t(category.nameKey, useAppStore.getState().settings.language)}
        </h4>
        
        <p 
          className="text-sm leading-relaxed mb-4 line-clamp-2"
          style={{ color: isDark ? 'rgba(255,255,255,0.4)' : 'rgba(0,0,0,0.4)' }}
        >
          {t(category.descriptionKey, useAppStore.getState().settings.language)}
        </p>

        {/* Footer */}
        <div 
          className="flex items-center justify-between pt-4"
          style={{ borderTop: `1px solid ${isDark ? 'rgba(255,255,255,0.05)' : 'rgba(0,0,0,0.05)'}` }}
        >
          <span 
            className="text-xs font-medium flex items-center gap-1.5"
            style={{ color: isDark ? 'rgba(255,255,255,0.35)' : 'rgba(0,0,0,0.35)' }}
          >
            <Sparkles size={12} />
            {useAppStore.getState().getOptimizationsByCategory(category.id).length}{' '}
            {useAppStore.getState().settings.language === 'es' ? 'optimizaciones' : 'optimizations'}
          </span>
          
          <motion.div
            className="flex items-center gap-1.5 text-sm font-semibold px-2 py-1 rounded-lg"
            style={{ color: category.color }}
            whileHover={{ x: 5, gap: 3 }}
            animate={isHovered ? { x: [0, 4], gap: [2, 4] } : {}}
            transition={{ duration: 0.2 }}
          >
            <span>{t('apply', useAppStore.getState().settings.language)}</span>
            <ArrowRight size={14} />
          </motion.div>
        </div>
      </div>

      {/* Corner decoration on hover */}
      <motion.div
        className="absolute bottom-0 right-0 w-16 h-16 opacity-0 group-hover:opacity-100 transition-opacity duration-300"
        style={{
          background: `radial-gradient(circle at 100% 100%, ${category.color}15 0%, transparent 70%)`,
        }}
      />
    </motion.div>
  );
}

// ============================================
// Main DashboardView Component
// ============================================
export function DashboardView({ className }: DashboardViewProps) {
  const {
    settings,
    setCurrentView,
    setSelectedCategory,
    getOptimizationsByCategory,
    getAppliedCount,
    applyAllInCategory,
    isProcessing,
    loadOptimizations
  } = useAppStore();

  // Load optimizations from API on mount
  useEffect(() => {
    loadOptimizations();
  }, []);

  const systemInfo = useSystemInfo();
  const appliedCount = getAppliedCount();
  const isDark = settings.theme === 'dark';
  const greeting = useTimeBasedGreeting(settings.language);
  
  // Button interaction states
  const [isButtonHovered, setIsButtonHovered] = useState(false);
  const [isButtonPressed, setIsButtonPressed] = useState(false);

  // Calculate progress for each category
  const getCategoryProgress = useCallback((categoryId: Category): number => {
    const items = getOptimizationsByCategory(categoryId);
    if (items.length === 0) return 0;
    const applied = items.filter(item => item.isApplied).length;
    return Math.round((applied / items.length) * 100);
  }, [getOptimizationsByCategory]);

  const handleCategoryClick = (category: Category) => {
    setSelectedCategory(category);
    setCurrentView('optimization');
  };

  const handleOptimizeAll = async () => {
    // Apply all safe optimizations across all categories
    for (const category of Object.values(Category)) {
      await applyAllInCategory(category);
    }
  };

  // Animation variants
  const containerVariants = {
    hidden: { opacity: 0 },
    visible: {
      opacity: 1,
      transition: {
        staggerChildren: 0.1,
        delayChildren: 0.3,
      },
    },
  };

  return (
    <div className={cn('min-h-screen pb-28 relative', className)}>
      {/* Animated Background */}
      <AnimatedBackground isDark={isDark} />

      {/* Hero Section */}
      <motion.div
        initial={{ opacity: 0 }}
        animate={{ opacity: 1 }}
        transition={{ duration: 0.5 }}
        className="px-4 md:px-6 pt-6 pb-8 relative"
      >
        {/* Welcome Message Section */}
        <motion.div 
          className="mb-8"
          initial={{ opacity: 0, y: -20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.6, ease: "easeOut" as const }}
        >
          {/* Greeting Badge */}
          <motion.div
            initial={{ opacity: 0, x: -20, scale: 0.9 }}
            animate={{ opacity: 1, x: 0, scale: 1 }}
            transition={{ delay: 0.1, type: "spring" as const, stiffness: 200, damping: 20 }}
            className="inline-flex items-center gap-2 px-3 py-1.5 rounded-full mb-4"
            style={{
              background: isDark ? 'rgba(255, 107, 53, 0.12)' : 'rgba(255, 107, 53, 0.1)',
              border: `1px solid ${isDark ? 'rgba(255, 107, 53, 0.2)' : 'rgba(255, 107, 53, 0.15)'}`,
            }}
          >
            <motion.span
              animate={{ rotate: [0, 15, -15, 0] }}
              transition={{ duration: 1, delay: 0.5, repeat: Infinity, repeatDelay: 3 }}
            >
              {greeting.includes('Morning') || greeting.includes('días') ? 
                <Sun size={14} className="text-orange-400" /> :
                greeting.includes('Evening') || greeting.includes('noches') ?
                <Moon size={14} className="text-blue-400" /> :
                <Star size={14} className="text-yellow-400" />
              }
            </motion.span>
            <span className="text-sm font-medium" style={{ color: isDark ? 'rgba(255,255,255,0.8)' : 'rgba(0,0,0,0.7)' }}>
              {greeting.split(/[☀️🌅🌙]/)[0].trim()}
            </span>
          </motion.div>

          <motion.h2
            initial={{ opacity: 0, x: -20 }}
            animate={{ opacity: 1, x: 0 }}
            transition={{ delay: 0.2, type: "spring" as const, stiffness: 200, damping: 20 }}
            className="text-3xl md:text-4xl font-bold mb-3 tracking-tight"
            style={{ color: isDark ? '#fff' : '#1a1a2e' }}
          >
            {t('dashboard', settings.language)}
            <span className={cn("ml-2", isDark && "gradient-text")}>CA-O</span>
          </motion.h2>
          
          <motion.p
            initial={{ opacity: 0, x: -20 }}
            animate={{ opacity: 1, x: 0 }}
            transition={{ delay: 0.3 }}
            className="text-base max-w-lg leading-relaxed"
            style={{ color: isDark ? 'rgba(255,255,255,0.45)' : 'rgba(0,0,0,0.45)' }}
          >
            {t('appTagline', settings.language)}
          </motion.p>
        </motion.div>

        {/* System Info Cards */}
        <motion.div
          variants={containerVariants}
          initial="hidden"
          animate="visible"
          className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 mb-8"
        >
          {/* CPU Usage Card */}
          <StatCard
            title={t('cpuUsage', settings.language)}
            value={systemInfo.cpuUsage}
            icon={Cpu}
            iconColor="#3B82F6"
            bgColor="rgba(59, 130, 246, 0.15)"
            progressValue={systemInfo.cpuUsage}
            progressGradient="linear-gradient(90deg, #3B82F6, #60A5FA, #93C5FD)"
            isDark={isDark}
            index={0}
          />

          {/* Memory Usage Card */}
          <StatCard
            title={t('memoryUsage', settings.language)}
            value={systemInfo.memoryUsage}
            icon={MemoryStick}
            iconColor="#8B5CF6"
            bgColor="rgba(139, 92, 246, 0.15)"
            progressValue={systemInfo.memoryUsage}
            progressGradient="linear-gradient(90deg, #8B5CF6, #A78BFA, #C4B5FD)"
            isDark={isDark}
            index={1}
          />

          {/* Optimizations Applied Card */}
          <StatCard
            title={t('optimizationsApplied', settings.language)}
            value={appliedCount}
            unit=""
            icon={CheckCircle2}
            iconColor="#10B981"
            bgColor="rgba(16, 185, 129, 0.15)"
            progressValue={Math.min(appliedCount * 2.5, 100)}
            progressGradient="linear-gradient(90deg, #10B981, #34D399, #6EE7B7)"
            isDark={isDark}
            index={2}
          />

          {/* Health Score Card */}
          <StatCard
            title={settings.language === 'es' ? 'Salud del Sistema' : 'Health Score'}
            value={100 - Math.round(systemInfo.cpuUsage * 0.3 + systemInfo.memoryUsage * 0.4)}
            icon={Activity}
            iconColor="#FF6B35"
            bgColor="rgba(255, 107, 53, 0.15)"
            progressValue={100 - Math.round(systemInfo.cpuUsage * 0.3 + systemInfo.memoryUsage * 0.4)}
            progressGradient="linear-gradient(90deg, #FF6B35, #ff8c42, #ffa94d)"
            isDark={isDark}
            index={3}
          />
        </motion.div>

        {/* Detected hardware */}
        <motion.section
          initial={{ opacity: 0, y: 16 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.55 }}
          className="mb-8 rounded-2xl p-5"
          style={{
            background: isDark ? 'rgba(30, 30, 50, 0.6)' : 'rgba(255, 255, 255, 0.8)',
            border: `1px solid ${isDark ? 'rgba(255,255,255,0.08)' : 'rgba(0,0,0,0.08)'}`,
          }}
        >
          <div className="flex items-center justify-between gap-3 mb-4">
            <div>
              <h3 className="text-base font-semibold" style={{ color: isDark ? '#fff' : '#1a1a2e' }}>
                {settings.language === 'es' ? 'Hardware detectado' : 'Detected hardware'}
              </h3>
              <p className="text-xs text-muted-foreground">
                {settings.language === 'es' ? 'Datos leídos directamente de Windows' : 'Data read directly from Windows'}
              </p>
            </div>
            <span className="text-xs text-emerald-400">{settings.language === 'es' ? 'Real' : 'Live'}</span>
          </div>
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3 text-sm">
            <div><span className="text-muted-foreground">CPU</span><p className="font-medium truncate" title={systemInfo.cpuModel}>{systemInfo.cpuModel}</p><p className="text-xs text-muted-foreground">{systemInfo.cpuCores} cores · {systemInfo.cpuClock}</p></div>
            <div><span className="text-muted-foreground">RAM</span><p className="font-medium">{systemInfo.memoryTotal ? `${systemInfo.memoryTotal} GB` : 'No disponible'}</p><p className="text-xs text-muted-foreground">{systemInfo.memorySpeed}</p></div>
            <div><span className="text-muted-foreground">GPU</span><p className="font-medium truncate" title={systemInfo.gpuName}>{systemInfo.gpuName}</p><p className="text-xs text-muted-foreground">{systemInfo.gpuVram ? `${systemInfo.gpuVram} MB VRAM` : 'VRAM no disponible'}</p></div>
            <div><span className="text-muted-foreground">Windows</span><p className="font-medium truncate" title={systemInfo.osVersion}>{systemInfo.osVersion}</p><p className="text-xs text-muted-foreground">Uptime: {systemInfo.uptime}</p></div>
          </div>
        </motion.section>

        {/* Real-Time Monitoring Charts Section */}
        <div className="px-4 md:px-6 mb-8">
        </div>

        {/* Optimize Now Button with Particles & Glow */}
        <motion.div
          initial={{ opacity: 0, y: 20, scale: 0.95 }}
          animate={{ opacity: 1, y: 0, scale: 1 }}
          transition={{ delay: 0.7, type: "spring" as const, stiffness: 200, damping: 20 }}
          className="mb-10 flex justify-center sm:justify-start"
        >
          <motion.button
            onClick={handleOptimizeAll}
            disabled={isProcessing}
            onHoverStart={() => setIsButtonHovered(true)}
            onHoverEnd={() => setIsButtonHovered(false)}
            whileHover={{ 
              scale: 1.03, 
              y: -4,
              transition: { type: "spring" as const, stiffness: 400, damping: 15 }
            }}
            whileTap={{ scale: 0.97 }}
            onMouseDown={() => setIsButtonPressed(true)}
            onMouseUp={() => setIsButtonPressed(false)}
            onMouseLeave={() => setIsButtonPressed(false)}
            className={cn(
              "relative group px-10 py-5 rounded-2xl font-bold text-white text-lg",
              "overflow-hidden transition-all duration-300",
              "disabled:opacity-50 disabled:cursor-not-allowed disabled:hover:scale-100 disabled:hover:translate-y-0"
            )}
            style={{
              background: 'linear-gradient(135deg, #FF6B35 0%, #ff8c42 50%, #ffa94d 100%)',
              boxShadow: isButtonHovered
                ? '0 15px 50px rgba(255, 107, 53, 0.5), 0 0 100px rgba(255, 107, 53, 0.2)'
                : '0 10px 40px rgba(255, 107, 53, 0.35), 0 0 60px rgba(255, 107, 53, 0.1)',
            }}
          >
            {/* Particle Effects */}
            <ParticleEffect color="#ffffff" isActive={isButtonHovered || isButtonPressed} />
            
            {/* Animated Border Glow */}
            <motion.div
              className="absolute inset-0 rounded-2xl overflow-hidden"
              animate={{
                background: isButtonHovered
                  ? 'linear-gradient(135deg, rgba(255,255,255,0.2), transparent 50%, rgba(255,255,255,0.1))'
                  : 'transparent',
              }}
              transition={{ duration: 0.3 }}
            />
            
            {/* Shimmer Effect */}
            <motion.div 
              className="absolute inset-0 opacity-0 group-hover:opacity-100 transition-opacity duration-300"
              initial={false}
            >
              <motion.div
                animate={{ x: ['-100%', '300%'] }}
                transition={{ duration: 1.8, repeat: Infinity, ease: "easeInOut" as const }}
                className="w-full h-full"
                style={{
                  background: 'linear-gradient(90deg, transparent, rgba(255,255,255,0.25), transparent)',
                }}
              />
            </motion.div>

            {/* Pulse Ring Animation */}
            {!isProcessing && (isButtonHovered || isButtonPressed) && (
              <>
                <motion.span
                  className="absolute -inset-1 rounded-2xl border-2"
                  style={{ borderColor: 'rgba(255, 107, 53, 0.5)', filter: 'blur(4px)' }}
                  animate={{ scale: [1, 1.1, 1], opacity: [0.5, 0, 0.5] }}
                  transition={{ duration: 2, repeat: Infinity }}
                />
                <motion.span
                  className="absolute -inset-2 rounded-2xl border"
                  style={{ borderColor: 'rgba(255, 107, 53, 0.3)', filter: 'blur(8px)' }}
                  animate={{ scale: [1, 1.15, 1], opacity: [0.3, 0, 0.3] }}
                  transition={{ duration: 2, repeat: Infinity, delay: 0.3 }}
                />
              </>
            )}

            {/* Button Content */}
            <div className="relative flex items-center justify-center gap-3">
              <motion.div
                animate={{ rotate: isProcessing ? 360 : 0 }}
                transition={{ duration: 1, repeat: isProcessing ? Infinity : 0, ease: "linear" as const }}
              >
                <Rocket size={22} />
              </motion.div>
              
              <span>{t('optimizeNow', settings.language)}</span>
              
              <motion.div
                animate={{ x: isButtonHovered ? 4 : 0 }}
                transition={{ type: "spring" as const, stiffness: 400, damping: 20 }}
                className="flex items-center"
              >
                <ArrowRight size={18} />
              </motion.div>
            </div>

            {/* Processing Overlay */}
            <AnimatePresence>
              {isProcessing && (
                <motion.div
                  initial={{ opacity: 0 }}
                  animate={{ opacity: 1 }}
                  exit={{ opacity: 0 }}
                  className="absolute inset-0 flex items-center justify-center bg-black/20 rounded-2xl"
                >
                  <motion.div
                    animate={{ rotate: 360 }}
                    transition={{ duration: 1, repeat: Infinity, ease: "linear" as const }}
                    className="w-8 h-8 border-3 border-white border-t-transparent rounded-full"
                    style={{ borderTopColor: 'white', borderRightColor: 'transparent', borderBottomColor: 'transparent', borderLeftColor: 'white', borderWidth: '3px', borderStyle: 'solid', borderRadius: '50%' }}
                  />
                </motion.div>
              )}
            </AnimatePresence>
          </motion.button>
        </motion.div>
      </motion.div>

      {/* Category Cards Section */}
      <div className="px-4 md:px-6">
        {/* Section Header */}
        <motion.div
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.8 }}
          className="flex items-center gap-3 mb-6"
        >
          <motion.div
            animate={{ rotate: [0, 10, -10, 0] }}
            transition={{ duration: 2, repeat: Infinity, repeatDelay: 3 }}
          >
            <Sparkles size={20} className="text-orange-400" />
          </motion.div>
          <h3 
            className="text-xl font-bold tracking-tight"
            style={{ color: isDark ? '#fff' : '#1a1a2e' }}
          >
            {settings.language === 'es' ? 'Categorías de Optimización' : 'Optimization Categories'}
          </h3>
        </motion.div>

        {/* Enhanced Category Cards Grid with 3D Tilt */}
        <motion.div
          variants={containerVariants}
          initial="hidden"
          animate="visible"
          className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-5 gap-5"
        >
          {categories.map((category, idx) => {
            const progress = getCategoryProgress(category.id);

            return (
              <EnhancedCategoryCard
                key={category.id}
                category={category}
                progress={progress}
                isDark={isDark}
                index={idx}
                onClick={handleCategoryClick}
              />
            );
          })}
        </motion.div>
      </div>

      {/* Profiles Section */}
      <div className="px-4 md:px-6 mt-10">
        <ProfileSelector />
      </div>

      <div className="px-4 md:px-6 mt-10 space-y-4">
        <UndoQueue />
        <OptimizationHistory />
      </div>

    </div>
  );
}

export default DashboardView;
