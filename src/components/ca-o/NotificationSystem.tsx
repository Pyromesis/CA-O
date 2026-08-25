'use client';

import { useState, useEffect, useCallback, createContext, useContext, ReactNode } from 'react';
import { motion, AnimatePresence, type Variants } from 'framer-motion';
import {
  CheckCircle2,
  XCircle,
  AlertTriangle,
  Info,
  X,
} from 'lucide-react';
import { cn } from '@/lib/utils';
import type { ComponentType } from 'react';
import { LucideProps } from 'lucide-react';

// Type for Lucide icons
type LucideIcon = ComponentType<LucideProps>;

// ============================================
// Types & Interfaces
// ============================================

export type NotificationType = 'success' | 'error' | 'warning' | 'info';

export interface Notification {
  id: string;
  title: string;
  message?: string;
  type: NotificationType;
  duration?: number; // in milliseconds, default: 4000
  createdAt: Date;
}

interface NotificationContextValue {
  notifications: Notification[];
  addNotification: (notification: Omit<Notification, 'id' | 'createdAt'>) => void;
  removeNotification: (id: string) => void;
  clearAllNotifications: () => void;
  success: (title: string, message?: string) => void;
  error: (title: string, message?: string) => void;
  warning: (title: string, message?: string) => void;
  info: (title: string, message?: string) => void;
}

// ============================================
// Context
// ============================================

const NotificationContext = createContext<NotificationContextValue | null>(null);

export const useNotifications = (): NotificationContextValue => {
  const context = useContext(NotificationContext);
  if (!context) {
    throw new Error('useNotifications must be used within a NotificationProvider');
  }
  return context;
};

// ============================================
// Configuration Constants
// ============================================

const DEFAULT_DURATION = 4000;
const MAX_NOTIFICATIONS = 5;

export type NotificationPosition = 'top-right' | 'bottom-center';
const DEFAULT_POSITION: NotificationPosition = 'top-right';

// Type-specific configuration
interface TypeConfig {
  icon: LucideIcon;
  bgColor: string;
  borderColor: string;
  iconColor: string;
  progressColor: string;
}

const TYPE_CONFIG: Record<NotificationType, TypeConfig> = {
  success: {
    icon: CheckCircle2,
    bgColor: 'rgba(16, 185, 129, 0.12)',
    borderColor: 'rgba(16, 185, 129, 0.3)',
    iconColor: '#10B981',
    progressColor: '#10B981',
  },
  error: {
    icon: XCircle,
    bgColor: 'rgba(239, 68, 68, 0.12)',
    borderColor: 'rgba(239, 68, 68, 0.3)',
    iconColor: '#EF4444',
    progressColor: '#EF4444',
  },
  warning: {
    icon: AlertTriangle,
    bgColor: 'rgba(245, 158, 11, 0.12)',
    borderColor: 'rgba(245, 158, 11, 0.3)',
    iconColor: '#F59E0B',
    progressColor: '#F59E0B',
  },
  info: {
    icon: Info,
    bgColor: 'rgba(59, 130, 246, 0.12)',
    borderColor: 'rgba(59, 130, 246, 0.3)',
    iconColor: '#3B82F6',
    progressColor: '#3B82F6',
  },
};

// ============================================
// Generate Unique ID
// ============================================

let notificationCounter = 0;

const generateId = (): string => {
  notificationCounter += 1;
  return `notification-${Date.now()}-${notificationCounter}`;
};

// ============================================
// Single Toast Component
// ============================================

interface ToastProps {
  notification: Notification;
  onDismiss: (id: string) => void;
  onHoverChange: (id: string, isHovered: boolean) => void;
  index: number;
  position: NotificationPosition;
}

function Toast({ notification, onDismiss, onHoverChange, index, position }: ToastProps) {
  // State declarations
  const [isExiting, setIsExiting] = useState(false);
  const [progress, setProgress] = useState(100);
  const [isPaused, setIsPaused] = useState(false);
  
  const config = TYPE_CONFIG[notification.type];
  const Icon = config.icon;
  const duration = notification.duration || DEFAULT_DURATION;

  // Define handleDismiss first (before useEffect)
  const handleDismiss = useCallback(() => {
    setIsExiting(true);
    setTimeout(() => {
      onDismiss(notification.id);
    }, 300);
  }, [onDismiss, notification.id]);

  // Progress animation - uses handleDismiss which is now defined above
  useEffect(() => {
    if (isPaused || isExiting) return;

    const startTime = Date.now();
    let animationFrameId: number;
    
    const animate = () => {
      const elapsed = Date.now() - startTime;
      const newProgress = Math.max(0, 100 - (elapsed / duration) * 100);
      setProgress(newProgress);

      if (newProgress > 0 && !isPaused && !isExiting) {
        animationFrameId = requestAnimationFrame(animate);
      } else if (newProgress <= 0) {
        handleDismiss();
      }
    };

    animationFrameId = requestAnimationFrame(animate);

    return () => {
      if (animationFrameId) {
        cancelAnimationFrame(animationFrameId);
      }
    };
  }, [duration, isPaused, isExiting, handleDismiss]);

  const handleMouseEnter = () => {
    setIsPaused(true);
    onHoverChange(notification.id, true);
  };

  const handleMouseLeave = () => {
    setIsPaused(false);
    onHoverChange(notification.id, false);
  };

  // Animation variants based on position using proper types
  const slideVariants: Variants = position === 'top-right'
    ? {
        initial: { opacity: 0, x: 300, scale: 0.95 },
        animate: { 
          opacity: 1, 
          x: 0, 
          scale: 1 
        },
        exit: { opacity: 0, x: 300, scale: 0.95 }
      }
    : {
        initial: { opacity: 0, y: 50, scale: 0.95 },
        animate: { 
          opacity: 1, 
          y: 0, 
          scale: 1 
        },
        exit: { opacity: 0, y: 30, scale: 0.95 }
      };

  return (
    <motion.div
      layout
      variants={slideVariants}
      initial="initial"
      animate="animate"
      exit="exit"
      transition={{
        type: "spring",
        stiffness: 400,
        damping: 30,
        delay: index * 0.05,
      }}
      onMouseEnter={handleMouseEnter}
      onMouseLeave={handleMouseLeave}
      className={cn(
        "notification-toast",
        `notification-toast-${notification.type}`,
        "relative overflow-hidden min-w-[320px] max-w-[380px]"
      )}
      style={{
        background: config.bgColor,
        borderColor: config.borderColor,
      }}
    >
      {/* Icon */}
      <div 
        className="notification-icon flex-shrink-0 mt-0.5"
        style={{ backgroundColor: `${config.iconColor}20` }}
      >
        <Icon size={16} style={{ color: config.iconColor }} />
      </div>

      {/* Content */}
      <div className="flex-1 min-w-0 pr-1">
        <h4 
          className="text-sm font-semibold text-white truncate"
        >
          {notification.title}
        </h4>
        
        <AnimatePresence>
          {notification.message && (
            <motion.p
              initial={{ opacity: 0, height: 0 }}
              animate={{ opacity: 1, height: 'auto' }}
              exit={{ opacity: 0, height: 0 }}
              transition={{ duration: 0.2 }}
              className="text-xs mt-1 leading-relaxed line-clamp-2"
              style={{ color: 'rgba(255,255,255,0.7)' }}
            >
              {notification.message}
            </motion.p>
          )}
        </AnimatePresence>
      </div>

      {/* Close Button */}
      <button
        onClick={(e) => {
          e.stopPropagation();
          handleDismiss();
        }}
        className="notification-close-btn"
        style={{ color: 'rgba(255,255,255,0.6)' }}
        aria-label="Dismiss notification"
      >
        <X size={14} />
      </button>

      {/* Progress Bar */}
      <motion.div
        className="notification-progress"
        style={{
          background: config.progressColor,
          boxShadow: `0 0 8px ${config.progressColor}`,
        }}
        animate={{ width: `${progress}%` }}
        transition={{ duration: 0.05, ease: "linear" as const }}
      />
    </motion.div>
  );
}

// ============================================
// Notification Container Component
// ============================================

interface NotificationContainerProps {
  notifications: Notification[];
  onRemove: (id: string) => void;
  position: NotificationPosition;
}

function NotificationContainer({ notifications, onRemove, position }: NotificationContainerProps) {
  const [, setHoveredIds] = useState<Set<string>>(new Set());

  const handleHoverChange = (id: string, isHovered: boolean) => {
    setHoveredIds(prev => {
      const next = new Set(prev);
      if (isHovered) {
        next.add(id);
      } else {
        next.delete(id);
      }
      return next;
    });
  };

  // Only show the latest MAX_NOTIFICATIONS
  const visibleNotifications = notifications.slice(-MAX_NOTIFICATIONS);

  if (visibleNotifications.length === 0) return null;

  return (
    <AnimatePresence mode="popLayout">
      <motion.div
        initial={{ opacity: 0 }}
        animate={{ opacity: 1 }}
        exit={{ opacity: 0 }}
        className={cn(
          "notification-container",
          position === 'top-right' ? 'notification-container-top-right' : 'notification-container-bottom-center'
        )}
      >
        {visibleNotifications.map((notification, index) => (
          <Toast
            key={notification.id}
            notification={notification}
            onDismiss={onRemove}
            onHoverChange={handleHoverChange}
            index={index}
            position={position}
          />
        ))}
      </motion.div>
    </AnimatePresence>
  );
}

// ============================================
// Provider Component
// ============================================

interface NotificationProviderProps {
  children: ReactNode;
  position?: NotificationPosition;
  maxNotifications?: number;
}

export function NotificationProvider({ 
  children, 
  position = DEFAULT_POSITION,
  maxNotifications = MAX_NOTIFICATIONS 
}: NotificationProviderProps) {
  const [notifications, setNotifications] = useState<Notification[]>([]);

  const removeNotification = useCallback((id: string) => {
    setNotifications(prev => prev.filter(n => n.id !== id));
  }, []);

  const addNotification = useCallback((notification: Omit<Notification, 'id' | 'createdAt'>) => {
    const newNotification: Notification = {
      ...notification,
      id: generateId(),
      createdAt: new Date(),
    };

    setNotifications(prev => {
      const updated = [...prev, newNotification];
      // Keep only the latest maxNotifications
      return updated.slice(-maxNotifications);
    });

    // Auto-dismiss after duration (fallback)
    const dur = notification.duration || DEFAULT_DURATION;
    setTimeout(() => {
      removeNotification(newNotification.id);
    }, dur + 300); // Add buffer for exit animation
  }, [maxNotifications, removeNotification]);

  const clearAllNotifications = useCallback(() => {
    setNotifications([]);
  }, []);

  // Convenience methods
  const success = useCallback((title: string, message?: string) => {
    addNotification({ title, message, type: 'success' });
  }, [addNotification]);

  const error = useCallback((title: string, message?: string) => {
    addNotification({ title, message, type: 'error', duration: 5000 }); // Errors stay longer
  }, [addNotification]);

  const warning = useCallback((title: string, message?: string) => {
    addNotification({ title, message, type: 'warning', duration: 4500 });
  }, [addNotification]);

  const info = useCallback((title: string, message?: string) => {
    addNotification({ title, message, type: 'info' });
  }, [addNotification]);

  const value: NotificationContextValue = {
    notifications,
    addNotification,
    removeNotification,
    clearAllNotifications,
    success,
    error,
    warning,
    info,
  };

  return (
    <NotificationContext.Provider value={value}>
      {children}
      <NotificationContainer 
        notifications={notifications} 
        onRemove={removeNotification}
        position={position}
      />
    </NotificationContext.Provider>
  );
}

// ============================================
// Hook for External Use
// ============================================

/**
 * Custom hook to show toast notifications from any component.
 * Must be used within a NotificationProvider.
 * 
 * @example
 * ```tsx
 * const { success, error, warning, info } = useToast();
 * 
 * success('Operation completed!');
 * error('Something went wrong', 'Please try again');
 * ```
 */
export function useToast() {
  return useNotifications();
}

// Default export
export default NotificationSystem;

// Export as named component for consistency
function NotificationSystem() {
  return null; // This is just a container for types/exports
}
