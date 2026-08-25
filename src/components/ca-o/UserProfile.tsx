'use client';

import { useState, useCallback, useMemo, useSyncExternalStore } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { useAppStore } from '@/store/useAppStore';
import {
  User,
  Mail,
  Calendar,
  Clock,
  Star,
  Trophy,
  Zap,
  Shield,
  Heart,
  Settings2,
  Edit3,
  Camera,
  Upload,
  Crown,
  Gem,
  Sparkles,
  CheckCircle2,
  ChevronRight,
  ExternalLink,
  LogOut,
  Lock,
  Unlock,
  Bell,
  BellOff,
  Activity,
  Target,
  Award,
  Flame,
  TrendingUp,
  Users,
  Palette,
  Globe,
  Cpu,
  HardDrive,
  Wifi,
  Battery,
  Moon,
  Sun,
  X,
  MoreHorizontal,
} from 'lucide-react';

// Types
interface UserProfileData {
  displayName: string;
  email: string;
  avatar: string | null;
  joinDate: string;
  lastLogin: string;
  level: number;
  xp: number;
  xpToNextLevel: number;
  stats: {
    optimizationsApplied: number;
    sessionsCount: number;
    streakDays: number;
    achievementsUnlocked: number;
    totalTimeUsed: string;
  };
}

interface UserProfileProps {
  className?: string;
}

// SSR-safe
const emptySubscribe = () => () => {};
const getSnapshot = () => true;
const getServerSnapshot = () => false;

export function UserProfile({ className }: UserProfileProps) {
  const { settings, optimizations, history } = useAppStore();
  const isMounted = useSyncExternalStore(emptySubscribe, getSnapshot, getServerSnapshot);
  
  const [isEditing, setIsEditing] = useState(false);
  const [showSettings, setShowSettings] = useState(false);
  
  const [userData, setUserData] = useState<UserProfileData>(() => {
    if (typeof window !== 'undefined') {
      const saved = localStorage.getItem('ca-o-user-profile');
      if (saved) return JSON.parse(saved);
    }
    
    const appliedCount = optimizations.filter(o => o.isApplied).length;
    const firstAction = history.reduce<number | null>((first, entry) =>
      first === null ? entry.timestamp : Math.min(first, entry.timestamp), null);
    return {
      displayName: 'CA-O User',
      email: '',
      avatar: null,
      joinDate: firstAction ? new Date(firstAction).toISOString() : '',
      lastLogin: new Date().toISOString(),
      level: Math.floor(appliedCount / 5) + 1,
      xp: appliedCount * 100 + (appliedCount > 10 ? 500 : 0),
      xpToNextLevel: ((Math.floor(appliedCount / 5) + 2) * 500) - (appliedCount * 100 + (appliedCount > 10 ? 500 : 0)),
      stats: {
        optimizationsApplied: appliedCount,
        sessionsCount: Number(localStorage.getItem('ca-o-sessions') || '0'),
        streakDays: Number(localStorage.getItem('ca-o-streak') || '0'),
        achievementsUnlocked: Number(localStorage.getItem('ca-o-unlocked-achievements') || '0'),
        totalTimeUsed: settings.language === 'es' ? 'No disponible' : 'Unavailable',
      },
    };
  });

  // Calculate XP progress percentage
  const xpProgress = useMemo(() => {
    if (userData.xpToNextLevel === 0) return 100;
    return Math.min(100, Math.round((userData.xp / (userData.xp + userData.xpToNextLevel)) * 100));
  }, [userData]);

  // Level color based on level
  const getLevelColor = (level: number): string => {
    if (level >= 20) return '#ef4444'; // Red - Legendary
    if (level >= 15) return '#a855f7'; // Purple - Diamond
    if (level >= 10) return '#f59e0b'; // Gold
    if (level >= 5) return '#3b82f6'; // Blue
    return '#10b981'; // Green - Beginner
  };

  // Generate avatar initials if no image
  const avatarInitials = userData.displayName.split(' ').map(n => n[0]).join('').toUpperCase();

  // Save profile changes
  const saveProfile = useCallback((updates: Partial<UserProfileData>) => {
    setUserData(prev => ({ ...prev, ...updates }));
    if (typeof window !== 'undefined') {
      localStorage.setItem('ca-o-user-profile', JSON.stringify({ ...userData, ...updates }));
    }
  }, [userData]);

  // Format date relative
  const formatDate = (dateStr: string): string => {
    if (!dateStr) return settings.language === 'es' ? 'No disponible' : 'Unavailable';
    const date = new Date(dateStr);
    const diff = Date.now() - date.getTime();
    const days = Math.floor(diff / 86400000);
    
    if (days < 1) return settings.language === 'es' ? 'Hoy' : 'Today';
    if (days < 7) return `${days}d`;
    if (days < 30) return `${Math.floor(days / 7)}w`;
    return `${settings.language === 'es' ? new Date(dateStr).toLocaleDateString('es-ES', { month: 'short' }) : date.toLocaleDateString('en-US', { month: 'short', year: 'numeric' })}`;
  };

  if (!isMounted) {
    return (
      <div className={`glass-premium rounded-2xl p-6 ${className || ''}`}>
        <div className="skeleton-modern h-96 w-full rounded-xl" />
      </div>
    );
  }

  return (
    <div className={`space-y-4 ${className || ''}`}>
      {/* Main Container */}
      <motion.div
        initial={{ opacity: 0, y: -10 }}
        animate={{ opacity: 1, y: 0 }}
        className="glass-ultra rounded-2xl overflow-hidden"
      >
        {/* Header */}
        <div 
          className="relative h-40 overflow-hidden cursor-pointer group"
          onClick={() => setShowSettings(!showSettings)}
        >
          {/* Animated Background */}
          <div className="absolute inset-0 bg-gradient-to-r from-[#FF6B35]/20 via-[#a855f7]/10 to-[#3b82f6]/20" />
          
          <div className="relative h-full px-5 flex items-center justify-between">
            {/* Left: Avatar & Info */}
            <div className="flex items-center gap-4">
              {/* Avatar */}
              <motion.div
                whileHover={{ scale: 1.05 }}
                whileTap={{ scale: 0.95 }}
                className="relative"
              >
                <div 
                  className="w-16 h-16 rounded-2xl flex items-center justify-center text-lg font-bold text-white bg-gradient-to-br from-[#FF6B35] to-[#ff8c42]"
                  style={{
                    boxShadow: `0 4px 15px rgba(255, 107, 53, 0.35)`
                  }}
                >
                  {userData.avatar ? (
                    <img src={userData.avatar} alt="" className="w-full h-full object-cover rounded-2xl" />
                  ) : (
                    avatarInitials
                  )}
                </div>
                
              </motion.div>

              {/* User Info */}
              <div>
                <div className="flex items-center gap-2">
                  <h3 className="font-bold text-white text-lg">{userData.displayName}</h3>
                  
                  {isEditing ? (
                    <input
                      type="text"
                      value={userData.displayName}
                      onChange={(e) => saveProfile({ displayName: e.target.value })}
                      className="bg-white/10 border border-white/20 rounded-lg px-2 py-1 text-sm text-white outline-none focus:border-[#FF6B35]/50 w-36"
                      autoFocus
                    />
                  ) : null}
                </div>
                
                <div className="flex items-center gap-2 text-xs text-[rgba(255,255,255,0.5)]">
                  <Mail className="w-3.5 h-3.5" />
                  <span>{userData.email}</span>
                </div>
              </div>
            </div>

            {/* Right: Level & Stats */}
            <div className="flex items-center gap-4">
              {/* Level Circle */}
              <div className="relative w-14 h-14">
                <svg viewBox="0 0 56 56" className="w-full h-full -rotate-90">
                  <circle
                    cx="28"
                    cy="28"
                    r="24"
                    fill="none"
                    stroke="rgba(255,255,255,0.1)"
                    strokeWidth="4"
                  />
                  <circle
                    cx="28"
                    cy="28"
                    r="24"
                    fill="none"
                    stroke={getLevelColor(userData.level)}
                    strokeWidth="4"
                    strokeLinecap="round"
                    strokeDasharray={`${xpProgress * 1.51} 151`}
                    style={{
                      filter: `drop-shadow(0 0 8px ${getLevelColor(userData.level)}50)`
                    }}
                  />
                </svg>
                <div className="absolute inset-0 flex items-center justify-center">
                  <span className="text-xs font-bold" style={{ color: getLevelColor(userData.level) }}>
                    {userData.level}
                  </span>
                </div>
              </div>

              {/* Quick Stats */}
              <div className="hidden sm:flex gap-3">
                <div className="text-center px-3 py-1.5 rounded-lg bg-white/5">
                  <Zap className="w-3.5 h-3.5 mx-auto mb-0.5 text-[#FF6B35]" />
                  <div className="text-xs font-bold text-white">{userData.stats.optimizationsApplied}</div>
                  <div className="text-[9px] text-[rgba(255,255,255,0.4)]">Opt</div>
                </div>
                <div className="text-center px-3 py-1.5 rounded-lg bg-white/5">
                  <Trophy className="w-3.5 h-3.5 mx-auto mb-0.5 text-yellow-400" />
                  <div className="text-xs font-bold text-white">{userData.stats.achievementsUnlocked}</div>
                  <div className="text-[9px] text-[rgba(255,255,255,0.4)]">Ach</div>
                </div>
              </div>

              {/* Expand/Collapse Indicator */}
              <motion.div
                animate={{ rotate: showSettings ? 180 : 0 }}
                transition={{ duration: 0.2 }}
              >
                <ChevronRight className="w-5 h-5 text-[rgba(255,255,255,0.4)]" />
              </motion.div>
            </div>
          </div>
        </div>

        {/* Expanded Content */}
        <AnimatePresence mode="wait">
          {showSettings && (
            <motion.div
              key="expanded"
              initial={{ opacity: 0, height: 0 }}
              animate={{ opacity: 1, height: 'auto' }}
              exit={{ opacity: 0, height: 0 }}
              transition={{ duration: 0.25 }}
              className="border-t border-[rgba(255,255,255,0.06)]"
            >
              <div className="p-5 space-y-5">
                {/* XP Progress Bar */}
                <div className="space-y-2">
                  <div className="flex items-center justify-between text-xs">
                    <span className="text-[rgba(255,255,255,0.5)] uppercase tracking-wider font-medium flex items-center gap-2">
                      <Star className="w-3.5 h-3.5" />
                      {settings.language === 'es' ? 'Experiencia' : 'XP'}
                    </span>
                    <span className="text-white font-medium">{userData.xp.toLocaleString()} XP</span>
                    <span className="text-[rgba(255,255,255,0.3)]">
                      Lvl {userData.level + 1}: {(userData.xp + userData.xpToNextLevel).toLocaleString()} XP
                    </span>
                  </div>
                  
                  <div className="h-2 bg-white/5 rounded-full overflow-hidden">
                    <motion.div
                      initial={false}
                      animate={{ width: `${xpProgress}%` }}
                      transition={{ duration: 1, ease: 'easeOut' }}
                      className="h-full rounded-full bg-gradient-to-r from-[#FF6B35] via-[#ff8c42] to-[#ffa94d]"
                    />
                  </div>
                </div>

                {/* Stats Grid */}
                <div className="grid grid-cols-2 gap-3">
                  {[
                    { icon: Clock, label: settings.language === 'es' ? 'Tiempo de Uso' : 'Time Used', value: userData.stats.totalTimeUsed, color: '#3B82F6' },
                    { icon: Flame, label: settings.language === 'es' ? 'Racha Actual' : 'Current Streak', value: `${userData.stats.streakDays} días`, color: '#F59E0B' },
                    { icon: Activity, label: settings.language === 'es' ? 'Sesiones' : 'Sessions', value: userData.stats.sessionsCount.toString(), color: '#10B981' },
                    { icon: Target, label: settings.language === 'es' ? 'Efectividad' : 'Efficiency', value: `${Math.min(100, Math.round((userData.stats.optimizationsApplied / optimizations.length) * 100))}%`, color: '#A855F7' },
                  ].map((stat, i) => (
                    <motion.div
                      key={i}
                      initial={{ opacity: 0, y: 10 }}
                      animate={{ opacity: 1, y: 0 }}
                      transition={{ delay: i * 0.05 }}
                      className="glass-liquid rounded-xl p-3 flex items-center gap-3 hover-lift-glow cursor-default"
                    >
                      <div className="w-9 h-9 rounded-lg flex items-center justify-center shrink-0" style={{ background: `${stat.color}15`, border: `1px solid ${stat.color}30` }}>
                        <stat.icon className="w-4 h-4" style={{ color: stat.color }} />
                      </div>
                      <div className="flex-1 min-w-0">
                        <div className="text-[10px] text-[rgba(255,255,255,0.4)] truncate">{stat.label}</div>
                        <div className="text-sm font-bold text-white">{stat.value}</div>
                      </div>
                    </motion.div>
                  ))}
                </div>

                {/* Account Actions */}
                <div className="space-y-2 pt-2 border-t border-[rgba(255,255,255,0.06)]">
                  <button
                    onClick={() => setIsEditing(!isEditing)}
                    className="w-full py-2.5 rounded-xl font-medium text-sm flex items-center justify-center gap-2 bg-white/[0.04] hover:bg-white/[0.08] text-white transition-colors press-depth"
                  >
                    {isEditing ? <CheckCircle2 className="w-4 h-4 text-green-400" /> : <Edit3 className="w-4 h-4" />}
                    {isEditing
                      ? (settings.language === 'es' ? 'Guardar Cambios' : 'Save Changes')
                      : (settings.language === 'es' ? 'Editar Perfil' : 'Edit Profile')
                    }
                  </button>
                  
                  <button
                    className="w-full py-2 rounded-xl font-medium text-sm bg-red-500/10 hover:bg-red-500/20 text-red-400 flex items-center justify-center gap-2 transition-colors press-depth mt-2"
                  >
                    <LogOut className="w-4 h-4" />
                    {settings.language === 'es' ? 'Cerrar Sesión' : 'Sign Out'}
                  </button>
                </div>

                {/* System Info (Read-only) */}
                <div className="pt-2 space-y-2 text-xs text-[rgba(255,255,255,0.4)]">
                  <div className="flex justify-between">
                    <span>{settings.language === 'es' ? 'Miembro desde' : 'Member since'}</span>
                    <span className="text-white/70">{formatDate(userData.joinDate)}</span>
                  </div>
                  <div className="flex justify-between">
                    <span>{settings.language === 'es' ? 'Último acceso' : 'Last login'}</span>
                    <span className="text-white/70">{formatDate(userData.lastLogin)}</span>
                  </div>
                  <div className="flex justify-between">
                    <span>ID de Usuario</span>
                    <span className="font-mono text-white/70">usr_{Date.now().toString(16).slice(-8)}_ca</span>
                  </div>
                </div>
              </div>
            </motion.div>
          )}
        </AnimatePresence>
      </motion.div>
    </div>
  );
}

export default UserProfile;
