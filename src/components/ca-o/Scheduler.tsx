'use client';

import { useState, useEffect, useMemo, useCallback, useSyncExternalStore } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { useAppStore } from '@/store/useAppStore';
import { t } from '@/lib/i18n';
import {
  Clock,
  Calendar,
  Plus,
  Trash2,
  Play,
  Pause,
  Bell,
  BellOff,
  Repeat,
  Settings2,
  ChevronRight,
  AlertCircle,
  CheckCircle2,
  Zap,
  Gamepad2,
  Briefcase,
  Leaf,
  Shield,
} from 'lucide-react';

// Types
interface ScheduleItem {
  id: string;
  name: string;
  profileId: string | null;
  time: string; // HH:mm format
  days: number[]; // 0-6 (Sun-Sat)
  enabled: boolean;
  lastRun?: string;
  nextRun?: string;
  lastRunKey?: string;
}

type SchedulerView = 'list' | 'create' | 'edit';

// Profile icons map
const profileIcons: Record<string, React.ReactNode> = {
  gaming: <Gamepad2 className="w-4 h-4" />,
  productivity: <Briefcase className="w-4 h-4" />,
  'power-saver': <Leaf className="w-4 h-4" />,
  privacy: <Shield className="w-4 h-4" />,
};

const profileColors: Record<string, string> = {
  gaming: '#EF4444',
  productivity: '#3B82F6',
  'power-saver': '#10B981',
  privacy: '#A855F7',
};

const dayNamesEs = ['Dom', 'Lun', 'Mar', 'Mié', 'Jue', 'Vie', 'Sáb'];
const dayNamesEn = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];

export function Scheduler({ className }: { className?: string }) {
  const { settings, profiles, applyProfile } = useAppStore();
  
  const [schedules, setSchedules] = useState<ScheduleItem[]>(() => {
    if (typeof window !== 'undefined') {
      const saved = localStorage.getItem('ca-o-schedules');
      return saved ? JSON.parse(saved) : getDefaultSchedules();
    }
    return getDefaultSchedules();
  });
  
  const [currentView, setCurrentView] = useState<SchedulerView>('list');
  const [editingId, setEditingId] = useState<string | null>(null);
  const [isCheckingSchedule, setIsCheckingSchedule] = useState(false);

  // SSR-safe mount
  const mounted = useSyncExternalStore(
    () => () => {},
    () => true,
    () => false
  );

  // Save schedules to localStorage
  useEffect(() => {
    if (mounted) {
      localStorage.setItem('ca-o-schedules', JSON.stringify(schedules));
    }
  }, [schedules, mounted]);

  // Check for due schedules every minute
  useEffect(() => {
    if (!mounted) return;

    const checkSchedules = async () => {
      const now = new Date();
      const currentTime = `${String(now.getHours()).padStart(2, '0')}:${String(now.getMinutes()).padStart(2, '0')}`;
      const currentDay = now.getDay();
      const currentRunKey = `${now.toISOString().slice(0, 10)}T${currentTime}`;

      const dueSchedules = schedules.filter((schedule) => {
        if (!schedule.enabled || !schedule.profileId || schedule.nextRun !== currentTime || schedule.lastRunKey === currentRunKey) return false;
        return schedule.days.includes(currentDay) && profiles.some((profile) => profile.id === schedule.profileId);
      });

      for (const schedule of dueSchedules) {
        if (schedule.profileId) {
          try {
            await applyProfile(schedule.profileId);
          } catch {
            // Record the attempt and allow the next scheduled run to proceed.
          }
        }
      }

      if (dueSchedules.length > 0) setSchedules(prev => prev.map(schedule => {
        if (!dueSchedules.some((dueSchedule) => dueSchedule.id === schedule.id)) return schedule;

        return {
          ...schedule,
          lastRun: currentTime,
          lastRunKey: currentRunKey,
          nextRun: calculateNextRun(schedule.time, schedule.days),
        };
      }));
    };

    // Initial check
    checkSchedules();

    // Set up interval to check every minute
    const interval = setInterval(checkSchedules, 60000);

    return () => clearInterval(interval);
  }, [mounted, applyProfile, profiles, schedules]);

  // Calculate next run time
  function calculateNextRun(time: string, days: number[]): string {
    const now = new Date();
    const [hours, minutes] = time.split(':').map(Number);
    
    for (let i = 0; i < 7; i++) {
      const checkDate = new Date(now);
      checkDate.setDate(checkDate.getDate() + i);
      
      if (days.includes(checkDate.getDay())) {
        if (i === 0) {
          // Today - check if time has passed
          const scheduledTime = new Date(checkDate.setHours(hours, minutes, 0, 0));
          if (scheduledTime > now) {
            return time;
          }
        } else {
          return time;
        }
      }
    }
    
    return time; // Fallback
  }

  // Get default schedules
  function getDefaultSchedules(): ScheduleItem[] {
    return [
      {
        id: 'default-1',
        name: settings.language === 'en' ? 'Morning Optimization' : 'Optimización Matutina',
        profileId: null,
        time: '09:00',
        days: [1, 2, 3, 4, 5], // Weekdays
        enabled: false,
      },
      {
        id: 'default-2',
        name: settings.language === 'en' ? 'Gaming Mode Evening' : 'Modo Gaming Noche',
        profileId: null,
        time: '20:00',
        days: [0, 5, 6], // Fri, Sat, Sun
        enabled: false,
      },
    ];
  }

  // Add new schedule
  const addSchedule = useCallback((schedule: Omit<ScheduleItem, 'id' | 'nextRun'>) => {
    const newSchedule: ScheduleItem = {
      ...schedule,
      id: `schedule-${Date.now()}`,
      nextRun: calculateNextRun(schedule.time, schedule.days),
    };
    
    setSchedules(prev => [...prev, newSchedule]);
    setCurrentView('list');
  }, []);

  // Update schedule
  const updateSchedule = useCallback((id: string, updates: Partial<ScheduleItem>) => {
    setSchedules(prev => prev.map(s => 
      s.id === id 
        ? { ...s, ...updates, nextRun: updates.time || updates.days ? calculateNextRun(updates.time || s.time, updates.days || s.days) : s.nextRun }
        : s
    ));
  }, []);

  // Delete schedule
  const deleteSchedule = useCallback((id: string) => {
    setSchedules(prev => prev.filter(s => s.id !== id));
  }, []);

  // Toggle schedule
  const toggleSchedule = useCallback((id: string) => {
    setSchedules(prev => prev.map(s =>
      s.id === id ? { ...s, enabled: !s.enabled } : s
    ));
  }, []);

  // Run schedule now
  const runScheduleNow = useCallback(async (schedule: ScheduleItem) => {
    if (!schedule.profileId) return;
    
    setIsCheckingSchedule(true);
    try {
      await applyProfile(schedule.profileId);
      
      updateSchedule(schedule.id, {
        lastRun: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
      });
    } finally {
      setIsCheckingSchedule(false);
    }
  }, [applyProfile, updateSchedule]);

  const dayNames = settings.language === 'es' ? dayNamesEs : dayNamesEn;

  if (!mounted) {
    return (
      <div className="glass-premium rounded-2xl p-6">
        <div className="skeleton h-48 w-full rounded-xl" />
      </div>
    );
  }

  return (
    <div className={`space-y-6 ${className || ''}`}>
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <div className="w-12 h-12 rounded-xl bg-gradient-to-br from-[#FF6B35] to-[#ff8c42] flex items-center justify-center">
            <Clock className="w-6 h-6 text-white" />
          </div>
          <div>
            <h2 className="text-xl font-bold text-white">
              {settings.language === 'en' ? 'Scheduler' : 'Programador'}
            </h2>
            <p className="text-sm text-[rgba(255,255,255,0.5)]">
              {settings.language === 'en' ? 'Automate your optimizations' : 'Automatiza tus optimizaciones'}
            </p>
          </div>
        </div>

        {currentView === 'list' && (
          <motion.button
            onClick={() => setCurrentView('create')}
            className="btn-premium"
            whileHover={{ scale: 1.05 }}
            whileTap={{ scale: 0.95 }}
          >
            <Plus className="w-4 h-4" />
            <span>{settings.language === 'en' ? 'New Schedule' : 'Nueva Programación'}</span>
          </motion.button>
        )}

        {(currentView === 'create' || currentView === 'edit') && (
          <motion.button
            onClick={() => setCurrentView('list')}
            className="btn-ghost"
            whileHover={{ scale: 1.05 }}
            whileTap={{ scale: 0.95 }}
          >
            <ChevronRight className="w-4 h-4 rotate-180" />
            <span>{settings.language === 'en' ? 'Back' : 'Volver'}</span>
          </motion.button>
        )}
      </div>

      <AnimatePresence mode="wait">
        {currentView === 'list' ? (
          <motion.div
            key="list"
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0, y: -20 }}
            transition={{ duration: 0.2 }}
            className="space-y-4"
          >
            {schedules.length > 0 ? (
              schedules.map((schedule, index) => {
                const profile = profiles.find(p => p.id === schedule.profileId);
                
                return (
                  <motion.div
                    key={schedule.id}
                    initial={{ opacity: 0, x: -20 }}
                    animate={{ opacity: 1, x: 0 }}
                    transition={{ delay: index * 0.1 }}
                    className={`glass-premium rounded-xl p-4 transition-all ${!schedule.enabled ? 'opacity-50' : ''}`}
                  >
                    <div className="flex items-center gap-4">
                      {/* Toggle */}
                      <button
                        onClick={() => toggleSchedule(schedule.id)}
                        className={`toggle-premium ${schedule.enabled ? 'active' : ''}`}
                      >
                        <span className="toggle-premium-knob" />
                      </button>

                      {/* Info */}
                      <div className="flex-1 min-w-0">
                        <div className="flex items-center gap-2 mb-1">
                          <h4 className="font-medium text-white truncate">{schedule.name}</h4>
                          {schedule.enabled && (
                            <span className="status-dot online" />
                          )}
                        </div>

                        <div className="flex items-center gap-3 text-xs text-[rgba(255,255,255,0.5)]">
                          <span className="flex items-center gap-1">
                            <Clock className="w-3 h-3" />
                            {schedule.time}
                          </span>
                          
                          <span className="flex items-center gap-1">
                            <Repeat className="w-3 h-3" />
                            {schedule.days.map(d => dayNames[d]).join(', ')}
                          </span>

                          {profile && (
                            <span 
                              className="tag"
                              style={{
                                backgroundColor: `${profileColors[profile.id] || '#FF6B35'}15`,
                                color: profileColors[profile.id] || '#FF6B35',
                                borderColor: `${profileColors[profile.id] || '#FF6B35'}30`,
                              }}
                            >
                              {profileIcons[profile.id]}
                              {t(profile.nameKey, settings.language)}
                            </span>
                          )}
                        </div>
                      </div>

                      {/* Actions */}
                      <div className="flex items-center gap-2">
                        {schedule.profileId && schedule.enabled && (
                          <motion.button
                            onClick={() => runScheduleNow(schedule)}
                            disabled={isCheckingSchedule}
                            className="w-8 h-8 rounded-lg bg-green-500/10 flex items-center justify-center text-green-400 hover:bg-green-500/20 transition-colors"
                            whileHover={{ scale: 1.1 }}
                            whileTap={{ scale: 0.9 }}
                            title={settings.language === 'en' ? 'Run now' : 'Ejecutar ahora'}
                          >
                            {isCheckingSchedule ? (
                              <div className="spinner-sm" />
                            ) : (
                              <Play className="w-4 h-4" />
                            )}
                          </motion.button>
                        )}

                        <motion.button
                          onClick={() => {
                            setEditingId(schedule.id);
                            setCurrentView('edit');
                          }}
                          className="w-8 h-8 rounded-lg bg-blue-500/10 flex items-center justify-center text-blue-400 hover:bg-blue-500/20 transition-colors"
                          whileHover={{ scale: 1.1 }}
                          whileTap={{ scale: 0.9 }}
                          title={settings.language === 'en' ? 'Edit' : 'Editar'}
                        >
                          <Settings2 className="w-4 h-4" />
                        </motion.button>

                        <motion.button
                          onClick={() => deleteSchedule(schedule.id)}
                          className="w-8 h-8 rounded-lg bg-red-500/10 flex items-center justify-center text-red-400 hover:bg-red-500/20 transition-colors"
                          whileHover={{ scale: 1.1 }}
                          whileTap={{ scale: 0.9 }}
                          title={settings.language === 'en' ? 'Delete' : 'Eliminar'}
                        >
                          <Trash2 className="w-4 h-4" />
                        </motion.button>
                      </div>
                    </div>

                    {/* Status Bar */}
                    {schedule.lastRun && (
                      <div className="mt-3 pt-3 border-t border-[rgba(255,255,255,0.06)] flex items-center justify-between text-xs">
                        <span className="text-[rgba(255,255,255,0.4)] flex items-center gap-1">
                          <CheckCircle2 className="w-3 h-3 text-green-400" />
                          {settings.language === 'en' ? 'Last run:' : 'Última ejecución:'} {schedule.lastRun}
                        </span>
                        
                        {schedule.nextRun && schedule.enabled && (
                          <span className="text-[#FF6B35] flex items-center gap-1">
                            <Clock className="w-3 h-3" />
                            {settings.language === 'en' ? 'Next run:' : 'Próxima:'} {schedule.nextRun}
                          </span>
                        )}
                      </div>
                    )}
                  </motion.div>
                );
              })
            ) : (
              /* Empty State */
              <div className="empty-state py-12">
                <Calendar className="empty-state-icon" />
                <h3 className="empty-state-title">
                  {settings.language === 'en' ? 'No Schedules Yet' : 'Sin Programaciones Aún'}
                </h3>
                <p className="empty-state-description">
                  {settings.language === 'en' 
                    ? 'Create a schedule to automatically apply optimization profiles'
                    : 'Crea una programación para aplicar perfiles automáticamente'}
                </p>
                <motion.button
                  onClick={() => setCurrentView('create')}
                  className="btn-premium mt-4"
                  whileHover={{ scale: 1.05 }}
                  whileTap={{ scale: 0.95 }}
                >
                  <Plus className="w-4 h-4" />
                  {settings.language === 'en' ? 'Create First Schedule' : 'Crear Primera Programación'}
                </motion.button>
              </div>
            )}

            {/* Info Banner */}
            <motion.div
              initial={{ opacity: 0, y: 10 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ delay: 0.3 }}
              className="flex items-start gap-3 p-4 rounded-xl bg-[rgba(59,130,246,0.08)] border border-[rgba(59,130,246,0.15)]"
            >
              <AlertCircle className="w-5 h-5 text-blue-400 mt-0.5 shrink-0" />
              <div>
                <p className="text-sm font-medium text-blue-300 mb-1">
                  {settings.language === 'en' ? 'How it works' : 'Cómo funciona'}
                </p>
                <ul className="text-xs text-blue-200/70 space-y-1">
                  <li>• {settings.language === 'en' ? 'Schedules are checked every minute' : 'Las programaciones se revisan cada minuto'}</li>
                  <li>• {settings.language === 'en' ? 'The app must be open in browser for schedules to run' : 'La aplicación debe estar abierta para ejecutar'}</li>
                  <li>• {settings.language === 'en' ? 'Enable notifications to get alerts when profiles are applied' : 'Activa las notificaciones para recibir alertas'}</li>
                </ul>
              </div>
            </motion.div>
          </motion.div>
        ) : (
          <motion.div
            key="form"
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0, y: -20 }}
            transition={{ duration: 0.2 }}
          >
            <ScheduleForm
              currentView={currentView}
              editingId={editingId}
              schedules={schedules}
              profiles={profiles}
              language={settings.language}
              onSubmit={(data) => {
                if (currentView === 'edit' && editingId) {
                  updateSchedule(editingId, data);
                } else {
                  addSchedule(data as Omit<ScheduleItem, 'id' | 'nextRun'>);
                }
              }}
              onCancel={() => setCurrentView('list')}
            />
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}

// Schedule Form Component
function ScheduleForm({
  currentView,
  editingId,
  schedules,
  profiles,
  language,
  onSubmit,
  onCancel,
}: {
  currentView: SchedulerView;
  editingId: string | null;
  schedules: ScheduleItem[];
  profiles: { id: string; nameKey: string }[];
  language: 'es' | 'en';
  onSubmit: (data: Partial<ScheduleItem>) => void;
  onCancel: () => void;
}) {
  const existingSchedule = currentView === 'edit' && editingId
    ? schedules.find(s => s.id === editingId)
    : null;

  const [name, setName] = useState(existingSchedule?.name || '');
  const [time, setTime] = useState(existingSchedule?.time || '09:00');
  const [selectedDays, setSelectedDays] = useState<number[]>(
    existingSchedule?.days || [1, 2, 3, 4, 5]
  );
  const [selectedProfile, setSelectedProfile] = useState<string | null>(
    existingSchedule?.profileId || null
  );

  const dayNames = language === 'es' ? dayNamesEs : dayNamesEn;

  const toggleDay = (dayIndex: number) => {
    setSelectedDays(prev =>
      prev.includes(dayIndex)
        ? prev.filter(d => d !== dayIndex)
        : [...prev, dayIndex].sort()
    );
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    
    if (!name.trim()) return;
    
    onSubmit({
      name: name.trim(),
      time,
      days: selectedDays,
      profileId: selectedProfile,
      enabled: existingSchedule?.enabled ?? true,
    });
  };

  return (
    <form onSubmit={handleSubmit} className="glass-premium rounded-2xl p-6 space-y-6">
      {/* Name Input */}
      <div className="space-y-2">
        <label className="text-sm font-medium text-[rgba(255,255,255,0.8)] block">
          {language === 'en' ? 'Schedule Name' : 'Nombre de la Programación'} *
        </label>
        <input
          type="text"
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder={language === 'en' ? 'e.g., Morning Boost' : 'ej., Impulso Matutino'}
          className="input-glass w-full"
          required
        />
      </div>

      {/* Time Picker */}
      <div className="space-y-2">
        <label className="text-sm font-medium text-[rgba(255,255,255,0.8)] block">
          <Clock className="w-4 h-4 inline mr-2" />
          {language === 'en' ? 'Time' : 'Hora'}
        </label>
        <input
          type="time"
          value={time}
          onChange={(e) => setTime(e.target.value)}
          className="input-glass w-full"
        />
      </div>

      {/* Day Selector */}
      <div className="space-y-2">
        <label className="text-sm font-medium text-[rgba(255,255,255,0.8)] block">
          <Calendar className="w-4 h-4 inline mr-2" />
          {language === 'en' ? 'Repeat on' : 'Repetir en'}
        </label>
        
        <div className="grid grid-cols-7 gap-2">
          {dayNames.map((day, index) => (
            <button
              key={index}
              type="button"
              onClick={() => toggleDay(index)}
              className={`py-2 px-1 text-xs font-medium rounded-lg transition-all ${
                selectedDays.includes(index)
                  ? 'bg-[#FF6B35] text-white'
                  : 'bg-[rgba(255,255,255,0.05)] text-[rgba(255,255,255,0.6)] hover:bg-[rgba(255,255,255,0.1)]'
              }`}
            >
              {day}
            </button>
          ))}
        </div>
        
        <div className="flex gap-2 mt-2">
          <button
            type="button"
            onClick={() => setSelectedDays([0, 1, 2, 3, 4, 5, 6])}
            className="text-xs text-[#FF6B35] hover:text-[#ff8c42]"
          >
            {language === 'en' ? 'Select All' : 'Seleccionar Todos'}
          </button>
          <span className="text-[rgba(255,255,255,0.2)]">|</span>
          <button
            type="button"
            onClick={() => setSelectedDays([1, 2, 3, 4, 5])}
            className="text-xs text-[#FF6B35] hover:text-[#ff8c42]"
          >
            {language === 'en' ? 'Weekdays' : 'Días Laborables'}
          </button>
          <span className="text-[rgba(255,255,255,0.2)]">|</span>
          <button
            type="button"
            onClick={() => setSelectedDays([0, 6])}
            className="text-xs text-[#FF6B35] hover:text-[#ff8c42]"
          >
            {language === 'en' ? 'Weekends' : 'Fines de Semana'}
          </button>
        </div>
      </div>

      {/* Profile Selector */}
      <div className="space-y-2">
        <label className="text-sm font-medium text-[rgba(255,255,255,0.8)] block">
          <Zap className="w-4 h-4 inline mr-2" />
          {language === 'en' ? 'Profile to Apply' : 'Perfil a Aplicar'}
        </label>
        
        <div className="space-y-2 max-h-[160px] overflow-y-auto scrollbar-hide">
          {profiles.map(profile => (
            <button
              key={profile.id}
              type="button"
              onClick={() => setSelectedProfile(selectedProfile === profile.id ? null : profile.id)}
              className={`w-full flex items-center gap-3 p-3 rounded-xl transition-all ${
                selectedProfile === profile.id
                  ? 'bg-[rgba(255,107,53,0.15)] border-[#FF6B35]'
                  : 'bg-[rgba(255,255,255,0.03)] border-transparent hover:bg-[rgba(255,255,255,0.06)]'
              } border`}
            >
              <div
                className="w-10 h-10 rounded-lg flex items-center justify-center"
                style={{
                  backgroundColor: `${profileColors[profile.id] || '#FF6B35'}20`,
                  color: profileColors[profile.id] || '#FF6B35',
                }}
              >
                {profileIcons[profile.id]}
              </div>
              
              <span className="text-sm font-medium text-white">
                {t(profile.nameKey, language)}
              </span>
              
              {selectedProfile === profile.id && (
                <CheckCircle2 className="w-5 h-5 text-[#FF6B35] ml-auto" />
              )}
            </button>
          ))}
          
          <button
            type="button"
            onClick={() => setSelectedProfile(null)}
            className={`w-full flex items-center gap-3 p-3 rounded-xl transition-all ${
              selectedProfile === null
                ? 'bg-[rgba(100,116,139,0.2)] border-gray-500'
                : 'bg-[rgba(255,255,255,0.03)] border-transparent hover:bg-[rgba(255,255,255,0.06)]'
            } border`}
          >
            <div className="w-10 h-10 rounded-lg bg-[rgba(100,116,139,0.2)] flex items-center justify-center text-gray-400">
              <BellOff className="w-5 h-5" />
            </div>
            
            <span className="text-sm font-medium text-gray-400">
              {language === 'en' ? 'No Profile (Notification Only)' : 'Sin Perfil (Solo Notificación)'}
            </span>
            
            {selectedProfile === null && (
              <CheckCircle2 className="w-5 h-5 text-gray-400 ml-auto" />
            )}
          </button>
        </div>
      </div>

      {/* Actions */}
      <div className="flex gap-3 pt-4 border-t border-[rgba(255,255,255,0.06)]">
        <motion.button
          type="submit"
          className="btn-premium flex-1"
          whileHover={{ scale: 1.02 }}
          whileTap={{ scale: 0.98 }}
        >
          <CheckCircle2 className="w-4 h-4" />
          {language === 'en' ? 'Save Schedule' : 'Guardar Programación'}
        </motion.button>
        
        <motion.button
          type="button"
          onClick={onCancel}
          className="btn-ghost"
          whileHover={{ scale: 1.02 }}
          whileTap={{ scale: 0.98 }}
        >
          {language === 'en' ? 'Cancel' : 'Cancelar'}
        </motion.button>
      </div>
    </form>
  );
}

export default Scheduler;
