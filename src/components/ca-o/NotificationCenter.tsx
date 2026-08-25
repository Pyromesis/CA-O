'use client';

import { useState, useCallback, useMemo, useSyncExternalStore } from 'react';
import { createPortal } from 'react-dom';
import { motion, AnimatePresence } from 'framer-motion';
import { useAppStore } from '@/store/useAppStore';
import {
  Bell,
  BellRing,
  CheckCircle2,
  AlertTriangle,
  Info,
  X,
  Zap,
  Shield,
  Clock,
  Star,
  Trophy,
  Gift,
  Settings2,
  ExternalLink,
  ChevronRight,
  Trash2,
  CheckCheck as MarkRead,
  Filter,
  Sparkles,
} from 'lucide-react';

// Types
interface NotificationItem {
  id: string;
  type: 'success' | 'warning' | 'info' | 'achievement' | 'promotion' | 'system';
  titleEs: string;
  titleEn: string;
  messageEs: string;
  messageEn: string;
  timestamp: number;
  read: boolean;
  actionUrl?: string;
  actionLabelEs?: string;
  actionLabelEn?: string;
  icon?: React.ReactNode;
}

interface NotificationCenterProps {
  className?: string;
}

// SSR-safe
const emptySubscribe = () => () => {};
const getSnapshot = () => true;
const getServerSnapshot = () => false;

function generateInitialNotifications(): NotificationItem[] {
  return [];
  /*
  const now = Date.now();
  return [
    {
      id: 'notif-1',
      type: 'success',
      titleEs: 'Optimización Completada',
      titleEn: 'Optimization Complete',
      messageEs: '12 optimizaciones aplicadas exitosamente. Rendimiento mejorado un 15%.',
      messageEn: '12 optimizations applied successfully. Performance improved by 15%.',
      timestamp: now - 5 * 60 * 1000,
      read: false,
      icon: <CheckCircle2 className="w-5 h-5" />,
    },
    {
      id: 'notif-2',
      type: 'achievement',
      titleEs: '¡Logro Desbloqueado!',
      titleEn: 'Achievement Unlocked!',
      messageEs: 'Has completado tu primera sesión de optimización completa.',
      messageEn: 'You completed your first full optimization session.',
      timestamp: now - 15 * 60 * 1000,
      read: false,
      icon: <Trophy className="w-5 h-5" />,
    },
    {
      id: 'notif-3',
      type: 'info',
      titleEs: 'Actualización Disponible',
      titleEn: 'Update Available',
      messageEs: 'CA-O v1.2.0 está disponible con nuevas funciones y mejoras de rendimiento.',
      messageEn: 'CA-O v1.2.0 is available with new features and performance improvements.',
      timestamp: now - 30 * 60 * 1000,
      read: true,
      icon: <Info className="w-5 h-5" />,
      actionUrl: '#settings',
      actionLabelEs: 'Actualizar',
      actionLabelEn: 'Update',
    },
    {
      id: 'notif-4',
      type: 'warning',
      titleEs: 'Uso Alto de Disco',
      titleEn: 'High Disk Usage',
      messageEs: 'El disco D: tiene menos del 10% de espacio libre.',
      messageEn: 'Drive D: has less than 10% free space.',
      timestamp: now - 45 * 60 * 1000,
      read: true,
      icon: <AlertTriangle className="w-5 h-5" />,
    },
    {
      id: 'notif-5',
      type: 'system',
      titleEs: 'Mantenimiento Programado',
      titleEn: 'Scheduled Maintenance',
      messageEs: 'El mantenimiento automático se ejecutará hoy a las 3:00 AM.',
      messageEn: 'Automatic maintenance will run today at 3:00 AM.',
      timestamp: now - 120 * 60 * 1000,
      read: true,
      icon: <Settings2 className="w-5 h-5" />,
    },
    {
      id: 'notif-7',
      type: 'achievement',
      titleEs: 'Racha de 7 Días',
      titleEn: '7-Day Streak!',
      messageEs: 'Has usado CA-O durante 7 días consecutivos. ¡Increíble!',
      messageEn: "You've used CA-O for 7 consecutive days. Amazing!",
      timestamp: now - 360 * 60 * 1000,
      read: false,
      icon: <Star className="w-5 h-5" />,
    },
  ]; */
}

export function NotificationCenter({ className }: NotificationCenterProps) {
  const { settings } = useAppStore();
  const isMounted = useSyncExternalStore(emptySubscribe, getSnapshot, getServerSnapshot);
  
  const [isOpen, setIsOpen] = useState(false);
  const [filter, setFilter] = useState<'all' | NotificationItem['type']>('all');
  const [notifications, setNotifications] = useState<NotificationItem[]>(() => generateInitialNotifications());

  // Filtered notifications
  const filteredNotifications = useMemo(() => {
    if (filter === 'all') return notifications;
    return notifications.filter(n => n.type === filter);
  }, [notifications, filter]);

  // Unread count
  const unreadCount = useMemo(() => 
    notifications.filter(n => !n.read).length,
    [notifications]
  );

  // Group notifications by date
  const groupedNotifications = useMemo(() => {
    const groups: { [key: string]: NotificationItem[] } = {};
    
    filteredNotifications.forEach(notif => {
      const date = new Date(notif.timestamp);
      const now = new Date();
      const diffDays = Math.floor((now.getTime() - date.getTime()) / (1000 * 60 * 60 * 24));
      
      let groupKey: string;
      if (diffDays === 0) groupKey = 'today';
      else if (diffDays === 1) groupKey = 'yesterday';
      else if (diffDays < 7) groupKey = 'thisWeek';
      else groupKey = 'older';
      
      if (!groups[groupKey]) groups[groupKey] = [];
      groups[groupKey].push(notif);
    });
    
    return groups;
  }, [filteredNotifications]);

  // Mark notification as read
  const markAsRead = useCallback((id: string) => {
    setNotifications(prev =>
      prev.map(n => n.id === id ? { ...n, read: true } : n)
    );
  }, []);

  // Mark all as read
  const markAllAsRead = useCallback(() => {
    setNotifications(prev => prev.map(n => ({ ...n, read: true })));
  }, []);

  // Delete notification
  const deleteNotification = useCallback((id: string) => {
    setNotifications(prev => prev.filter(n => n.id !== id));
  }, []);

  // Clear all
  const clearAll = useCallback(() => {
    setNotifications([]);
  }, []);

  // Get notification type styling
  const getTypeStyle = (type: NotificationItem['type']) => {
    const styles: Record<NotificationItem['type'], { bg: string; iconBg: string; iconColor: string }> = {
      success: { bg: 'bg-green-500/5 border-green-500/10', iconBg: 'bg-green-500/20', iconColor: 'text-green-400' },
      warning: { bg: 'bg-yellow-500/5 border-yellow-500/10', iconBg: 'bg-yellow-500/20', iconColor: 'text-yellow-400' },
      info: { bg: 'bg-blue-500/5 border-blue-500/10', iconBg: 'bg-blue-500/20', iconColor: 'text-blue-400' },
      achievement: { bg: 'bg-purple-500/5 border-purple-500/10', iconBg: 'bg-purple-500/20', iconColor: 'text-purple-400' },
      promotion: { bg: 'bg-[#FF6B35]/5 border-[#FF6B35]/10', iconBg: 'bg-[#FF6B35]/20', iconColor: 'text-[#FF6B35]' },
      system: { bg: 'bg-gray-500/5 border-gray-500/10', iconBg: 'bg-gray-500/20', iconColor: 'text-gray-400' },
    };
    return styles[type];
  };

  // Format relative time
  const formatRelativeTime = (timestamp: number) => {
    const diff = Date.now() - timestamp;
    const minutes = Math.floor(diff / (1000 * 60));
    const hours = Math.floor(diff / (1000 * 60 * 60));
    const days = Math.floor(diff / (1000 * 60 * 60 * 24));

    if (minutes < 1) return settings.language === 'es' ? 'Ahora' : 'Just now';
    if (minutes < 60) return `${minutes}m`;
    if (hours < 24) return `${hours}h`;
    if (days < 7) return `${days}d`;
    return new Date(timestamp).toLocaleDateString(settings.language === 'es' ? 'es-ES' : 'en-US', { month: 'short', day: 'numeric' });
  };

  // Get group label
  const getGroupLabel = (groupKey: string) => {
    const labels: Record<string, { es: string; en: string }> = {
      today: { es: 'Hoy', en: 'Today' },
      yesterday: { es: 'Ayer', en: 'Yesterday' },
      thisWeek: { es: 'Esta Semana', en: 'This Week' },
      older: { es: 'Anteriores', en: 'Older' },
    };
    return labels[groupKey]?.[settings.language] || groupKey;
  };

  // Default icons per type
  const getDefaultIcon = (type: NotificationItem['type']) => {
    const icons: Record<NotificationItem['type'], React.ReactNode> = {
      success: <CheckCircle2 className="w-4 h-4" />,
      warning: <AlertTriangle className="w-4 h-4" />,
      info: <Info className="w-4 h-4" />,
      achievement: <Trophy className="w-4 h-4" />,
      promotion: <Gift className="w-4 h-4" />,
      system: <Settings2 className="w-4 h-4" />,
    };
    return icons[type];
  };

  if (!isMounted) {
    return (
      <div className={`glass-premium rounded-xl p-4 ${className || ''}`}>
        <div className="loader-skeleton-premium h-8 w-full rounded-lg mb-3" />
        <div className="loader-skeleton-premium h-16 w-full rounded-lg" />
      </div>
    );
  }

  return (
    <div className={`relative z-[100] ${className || ''}`}>
      {/* Bell Button */}
      <button
        onClick={() => setIsOpen(!isOpen)}
        aria-label={settings.language === 'es' ? 'Abrir notificaciones' : 'Open notifications'}
        aria-expanded={isOpen}
        aria-haspopup="dialog"
        className={`relative p-2.5 rounded-xl transition-all hover:bg-white/[0.05] ${
          isOpen ? 'bg-white/[0.08]' : ''
        } ${unreadCount > 0 ? 'notification-dot' : ''}`}
      >
        {unreadCount > 0 ? (
          <BellRing className="w-5 h-5 text-[#FF6B35]" />
        ) : (
          <Bell className="w-5 h-5 text-white/60" />
        )}
        
        {unreadCount > 0 && (
          <span className="absolute -top-0.5 -right-0.5 min-w-[18px] h-[18px] px-1 rounded-full bg-gradient-to-r from-[#FF6B35] to-[#ff8c42] flex items-center justify-center text-[10px] font-bold text-white">
            {unreadCount > 9 ? '9+' : unreadCount}
          </span>
        )}
      </button>

      {createPortal(
        <>
          {/* Dropdown Panel */}
          <AnimatePresence>
        {isOpen && (
          <motion.div
            initial={{ opacity: 0, y: 10, scale: 0.95 }}
            animate={{ opacity: 1, y: 0, scale: 1 }}
            exit={{ opacity: 0, y: 10, scale: 0.95 }}
            transition={{ duration: 0.2 }}
            className="fixed top-20 right-4 w-[380px] max-w-[calc(100vw-32px)] glass-diamond rounded-2xl shadow-2xl z-[200] overflow-hidden"
          >
            {/* Header */}
            <div className="p-4 border-b border-white/[0.06]">
              <div className="flex items-center justify-between mb-3">
                <h3 className="font-semibold text-white flex items-center gap-2">
                  <Sparkles className="w-4 h-4 text-[#FF6B35]" />
                  {settings.language === 'es' ? 'Notificaciones' : 'Notifications'}
                </h3>
                <button
                  onClick={markAllAsRead}
                  disabled={unreadCount === 0}
                  className="text-xs text-[#A855F7] hover:text-[#A855F7]/80 disabled:text-white/30 transition-colors font-medium"
                >
                  {settings.language === 'es' ? 'Marcar todo leído' : 'Mark all read'}
                </button>
              </div>

              {/* Filter Tabs */}
              <div className="flex gap-1.5 overflow-x-auto scrollbar-none pb-1">
                {[
                  { key: 'all', label: { es: 'Todo', en: 'All' } },
                  { key: 'success', label: { es: 'Éxito', en: 'Success' } },
                  { key: 'warning', label: { es: 'Advertencia', en: 'Warning' } },
                  { key: 'achievement', label: { es: 'Logros', en: 'Achievements' } },
                ].map(tab => (
                  <button
                    key={tab.key}
                    onClick={() => setFilter(tab.key as typeof filter)}
                    className={`px-3 py-1 rounded-lg text-xs font-medium whitespace-nowrap transition-all ${
                      filter === tab.key
                        ? 'bg-[#FF6B35]/15 text-[#FF6B35]'
                        : 'text-white/50 hover:text-white/70 hover:bg-white/[0.04]'
                    }`}
                  >
                    {settings.language === 'es' ? tab.label.es : tab.label.en}
                  </button>
                ))}
              </div>
            </div>

            {/* Notifications List */}
            <div className="max-h-[350px] overflow-y-auto scrollbar-luxury">
              {filteredNotifications.length === 0 ? (
                /* Empty State */
                <motion.div
                  initial={{ opacity: 0 }}
                  animate={{ opacity: 1 }}
                  className="p-8 text-center"
                >
                  <div className="w-14 h-14 mx-auto mb-3 rounded-2xl bg-white/[0.03] flex items-center justify-center">
                    <Bell className="w-6 h-6 text-white/20" />
                  </div>
                  <p className="text-sm text-white/40">
                    {settings.language === 'es'
                      ? 'No hay notificaciones para mostrar'
                      : 'No notifications to display'}
                  </p>
                </motion.div>
              ) : (
                /* Grouped Notifications */
                Object.entries(groupedNotifications).map(([groupKey, items]) => (
                  <div key={groupKey}>
                    {/* Group Header */}
                    <div className="px-4 py-2 bg-black/10 sticky top-0 z-10">
                      <span className="text-[10px] font-semibold text-white/40 uppercase tracking-wider">
                        {getGroupLabel(groupKey)} · {items.length}
                      </span>
                    </div>

                    {/* Items */}
                    <div className="divide-y divide-white/[0.04]">
                      {items.map(notif => {
                        const style = getTypeStyle(notif.type);
                        
                        return (
                          <motion.div
                            key={notif.id}
                            layout
                            initial={{ opacity: 0, x: -10 }}
                            animate={{ opacity: 1, x: 0 }}
                            className={`p-3 hover:bg-white/[0.02] cursor-pointer transition-colors ${
                              !notif.read ? style.bg : ''
                            } ${!notif.read ? '' : 'opacity-70'}`}
                            onClick={() => !notif.read && markAsRead(notif.id)}
                          >
                            <div className="flex gap-3">
                              {/* Icon */}
                              <div className={`w-9 h-9 rounded-lg flex items-center justify-center shrink-0 ${style.iconBg}`}>
                                {notif.icon || getDefaultIcon(notif.type)}
                                <span className={`${style.iconColor}`}>{/* Force color */}</span>
                              </div>

                              {/* Content */}
                              <div className="flex-1 min-w-0">
                                <div className="flex items-start justify-between gap-2 mb-0.5">
                                  <h4 className={`text-sm truncate ${!notif.read ? 'font-semibold text-white' : 'text-white/80'}`}>
                                    {settings.language === 'es' ? notif.titleEs : notif.titleEn}
                                  </h4>
                                  <div className="flex items-center gap-1 shrink-0">
                                    {!notif.read && (
                                      <span className="w-2 h-2 rounded-full bg-[#FF6B35]" />
                                    )}
                                    <span className="text-[10px] text-white/30">
                                      {formatRelativeTime(notif.timestamp)}
                                    </span>
                                    <button
                                      onClick={(e) => {
                                        e.stopPropagation();
                                        deleteNotification(notif.id);
                                      }}
                                      className="p-1 rounded hover:bg-red-500/20 text-white/30 hover:text-red-400 transition-colors"
                                    >
                                      <X className="w-3 h-3" />
                                    </button>
                                  </div>
                                </div>
                                <p className="text-xs text-white/50 line-clamp-2 mb-1.5">
                                  {settings.language === 'es' ? notif.messageEs : notif.messageEn}
                                </p>
                                
                                {notif.actionLabelEs && (
                                  <button
                                    className="inline-flex items-center gap-1 text-xs font-medium text-[#A855F7] hover:text-[#A855F7]/80 transition-colors"
                                    onClick={(e) => e.stopPropagation()}
                                  >
                                    {settings.language === 'es' ? notif.actionLabelEs : notif.actionLabelEn}
                                    <ExternalLink className="w-3 h-3" />
                                  </button>
                                )}
                              </div>
                            </div>
                          </motion.div>
                        );
                      })}
                    </div>
                  </div>
                ))
              )}
            </div>

            {/* Footer */}
            {notifications.length > 0 && (
              <div className="p-3 border-t border-white/[0.06] flex justify-between items-center">
                <span className="text-[10px] text-white/30">
                  {notifications.length} {settings.language === 'es' ? 'notificaciones totales' : 'total notifications'}
                </span>
                <button
                  onClick={clearAll}
                  className="flex items-center gap-1 text-xs text-red-400/70 hover:text-red-400 transition-colors"
                >
                  <Trash2 className="w-3.5 h-3.5" />
                  {settings.language === 'es' ? 'Limpiar todo' : 'Clear all'}
                </button>
              </div>
            )}
          </motion.div>
        )}
          </AnimatePresence>

          {/* Backdrop */}
          {isOpen && (
            <div
              className="fixed inset-0 z-[190]"
              onClick={() => setIsOpen(false)}
            />
          )}
        </>
        , document.body
      )}
    </div>
  );
}

export default NotificationCenter;
