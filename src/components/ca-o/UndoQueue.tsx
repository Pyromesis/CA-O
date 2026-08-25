'use client';

import { useState, useCallback, useEffect } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { useAppStore } from '@/store/useAppStore';
import { t } from '@/lib/i18n';
import {
  Undo2,
  Redo2,
  Trash2,
  Clock,
  CheckCircle2,
  XCircle,
  AlertCircle,
  ChevronDown,
  ChevronUp,
  History,
  Zap,
  Shield,
} from 'lucide-react';
import { cn } from '@/lib/utils';

// Undo action entry
interface UndoAction {
  id: string;
  nameKey: string;
  type: 'apply' | 'revert';
  timestamp: number;
  optimizationId: string;
  isRedoAvailable: boolean;
}

interface UndoQueueProps {
  className?: string;
}

// Local storage key for undo queue
const UNDO_QUEUE_KEY = 'ca-o-undo-queue';
const MAX_UNDO_STEPS = 50;

export function UndoQueue({ className }: UndoQueueProps) {
  const { 
    settings, 
    applyOptimization, 
    revertOptimization,
    optimizations 
  } = useAppStore();
  
  const [queue, setQueue] = useState<UndoAction[]>(() => {
    if (typeof window !== 'undefined') {
      try {
        const stored = localStorage.getItem(UNDO_QUEUE_KEY);
        return stored ? JSON.parse(stored) : [];
      } catch {
        return [];
      }
    }
    return [];
  });
  
  const [isExpanded, setIsExpanded] = useState(false);
  const [isProcessing, setIsProcessing] = useState(false);

  // Persist queue to localStorage
  useEffect(() => {
    try {
      localStorage.setItem(UNDO_QUEUE_KEY, JSON.stringify(queue.slice(0, MAX_UNDO_STEPS)));
    } catch {
      // Storage full or unavailable
    }
  }, [queue]);

  // Add action to queue (exposed for other components)
  const addAction = useCallback((action: Omit<UndoAction, 'timestamp' | 'isRedoAvailable'>) => {
    setQueue(prev => [{
      ...action,
      timestamp: Date.now(),
      isRedoAvailable: false,
    }, ...prev.filter(a => a.id !== action.id)].slice(0, MAX_UNDO_STEPS));
  }, []);

  // Undo last action
  const handleUndo = async (action?: UndoAction) => {
    if (isProcessing) return;
    
    const actionToUndo = action || queue[0];
    if (!actionToUndo) return;

    setIsProcessing(true);
    
    try {
      if (actionToUndo.type === 'apply') {
        // Undo an apply → revert it
        await revertOptimization(actionToUndo.optimizationId);
      } else {
        // Undo a revert → apply it
        await applyOptimization(actionToUndo.optimizationId);
      }

      // Mark as redoable and move to redo stack
      setQueue(prev => prev.map(a => 
        a.id === actionToUndo.id 
          ? { ...a, isRedoAvailable: true, type: a.type === 'apply' ? 'revert' as const : 'apply' as const }
          : a
      ));
    } catch (error) {
      console.error('Undo error:', error);
    } finally {
      setIsProcessing(false);
    }
  };

  // Redo action
  const handleRedo = async (action: UndoAction) => {
    if (isProcessing || !action.isRedoAvailable) return;

    setIsProcessing(true);
    
    try {
      if (action.type === 'apply') {
        await applyOptimization(action.optimizationId);
      } else {
        await revertOptimization(action.optimizationId);
      }

      // Clear redo status
      setQueue(prev => prev.map(a => 
        a.id === action.id 
          ? { ...a, isRedoAvailable: false }
          : a
      ));
    } catch (error) {
      console.error('Redo error:', error);
    } finally {
      setIsProcessing(false);
    }
  };

  // Clear queue
  const handleClearQueue = () => {
    setQueue([]);
    localStorage.removeItem(UNDO_QUEUE_KEY);
  };

  // Remove single item
  const handleRemoveItem = (id: string) => {
    setQueue(prev => prev.filter(a => a.id !== id));
  };

  // Get undoable actions (not redo-available)
  const undoableActions = queue.filter(a => !a.isRedoAvailable);
  const redoableActions = queue.filter(a => a.isRedoAvailable);

  const formatTimeAgo = (timestamp: number) => {
    const seconds = Math.floor((Date.now() - timestamp) / 1000);
    
    if (seconds < 60) return `${settings.language === 'es' ? 'hace' : ''} ${seconds}s`;
    if (seconds < 3600) return `${settings.language === 'es' ? 'hace' : ''} ${Math.floor(seconds / 60)}m`;
    return `${settings.language === 'es' ? 'hace' : ''} ${Math.floor(seconds / 3600)}h`;
  };

  const isDark = settings.theme === 'dark';

  // Expose addAction to window for external use
  useEffect(() => {
    (window as any).__ca_o_addUndoAction = addAction;
    return () => {
      delete (window as any).__ca_o_addUndoAction;
    };
  }, [addAction]);

  return (
    <div className={cn('relative', className)}>
      {/* Queue Header / Toggle */}
      <motion.button
        onClick={() => setIsExpanded(!isExpanded)}
        whileHover={{ scale: 1.01 }}
        whileTap={{ scale: 0.99 }}
        className="w-full flex items-center justify-between p-3 rounded-xl transition-all"
        style={{
          background: isDark ? 'rgba(30, 30, 50, 0.6)' : 'rgba(255, 255, 255, 0.8)',
          border: `1px solid ${isDark ? 'rgba(255, 255, 255, 0.08)' : 'rgba(0, 0, 0, 0.08)'}`,
        }}
      >
        <div className="flex items-center gap-3">
          {/* Icon with indicator */}
          <div className="relative">
            <div 
              className="w-9 h-9 rounded-lg flex items-center justify-center"
              style={{ background: 'rgba(99, 102, 241, 0.15)' }}
            >
              <History size={18} className="text-indigo-400" />
            </div>
            
            {/* Action count badge */}
            {undoableActions.length > 0 && (
              <motion.div
                initial={{ scale: 0 }}
                animate={{ scale: 1 }}
                className="absolute -top-1 -right-1 w-5 h-5 rounded-full flex items-center justify-center text-xs font-bold text-white"
                style={{ background: 'linear-gradient(135deg, #6366F1, #4F46E5)' }}
              >
                {undoableActions.length}
              </motion.div>
            )}
          </div>
          
          <div className="text-left">
            <p 
              className="text-sm font-medium"
              style={{ color: isDark ? '#fff' : '#1a1a2e' }}
            >
              {t('undoQueue', settings.language)}
            </p>
            <p 
              className="text-xs"
              style={{ color: isDark ? 'rgba(255,255,255,0.4)' : 'rgba(0,0,0,0.4)' }}
            >
              {undoableActions.length > 0 
                ? `${undoableActions.length} ${t('actionsAvailable', settings.language).toLowerCase()}`
                : t('noActions', settings.language)
              }
            </p>
          </div>
        </div>

        <div className="flex items-center gap-2">
          {/* Quick Undo Button */}
          {undoableActions.length > 0 && (
            <motion.button
              onClick={(e) => {
                e.stopPropagation();
                handleUndo();
              }}
              disabled={isProcessing}
              whileHover={{ scale: 1.05 }}
              whileTap={{ scale: 0.95 }}
              className="flex items-center gap-1 px-3 py-1.5 rounded-lg text-xs font-medium"
              style={{
                background: 'rgba(99, 102, 241, 0.15)',
                color: '#818CF8',
                border: '1px solid rgba(99, 102, 241, 0.25)',
              }}
            >
              <Undo2 size={14} />
              <span className="hidden sm:inline">{t('undo', settings.language)}</span>
            </motion.button>
          )}
          
          <motion.div
            animate={{ rotate: isExpanded ? 180 : 0 }}
            transition={{ duration: 0.2 }}
          >
            <ChevronDown size={16} style={{ color: isDark ? 'rgba(255,255,255,0.5)' : 'rgba(0,0,0,0.5)' }} />
          </motion.div>
        </div>
      </motion.button>

      {/* Expanded Queue */}
      <AnimatePresence>
        {isExpanded && (
          <motion.div
            initial={{ height: 0, opacity: 0 }}
            animate={{ height: 'auto', opacity: 1 }}
            exit={{ height: 0, opacity: 0 }}
            transition={{ duration: 0.2 }}
            className="overflow-hidden"
          >
            <div 
              className="mt-2 rounded-xl p-4 space-y-2 max-h-64 overflow-y-auto custom-scrollbar"
              style={{
                background: isDark ? 'rgba(20, 20, 40, 0.95)' : 'rgba(255, 255, 255, 0.95)',
                border: `1px solid ${isDark ? 'rgba(255, 255, 255, 0.08)' : 'rgba(0, 0, 0, 0.08)'}`,
                boxShadow: '0 10px 40px rgba(0, 0, 0, 0.2)',
              }}
            >
              {/* Header with clear button */}
              <div className="flex items-center justify-between mb-3 pb-2" style={{ borderBottom: `1px solid ${isDark ? 'rgba(255,255,255,0.06)' : 'rgba(0,0,0,0.06)'}` }}>
                <div className="flex items-center gap-2">
                  <Clock size={14} style={{ color: isDark ? 'rgba(255,255,255,0.4)' : 'rgba(0,0,0,0.4)' }} />
                  <span className="text-xs font-medium" style={{ color: isDark ? 'rgba(255,255,255,0.5)' : 'rgba(0,0,0,0.5)' }}>
                    {t('recentActions', settings.language)}
                  </span>
                </div>
                
                {(queue.length > 0) && (
                  <button
                    onClick={handleClearQueue}
                    className="flex items-center gap-1 px-2 py-1 rounded text-xs hover:bg-red-500/10 transition-colors"
                    style={{ color: '#EF4444' }}
                  >
                    <Trash2 size={12} />
                    <span>{t('clearAll', settings.language)}</span>
                  </button>
                )}
              </div>

              {/* Actions List */}
              {queue.length > 0 ? (
                <div className="space-y-1.5">
                  {queue.map((action, index) => (
                    <motion.div
                      key={`${action.id}-${action.timestamp}`}
                      initial={{ opacity: 0, x: -20 }}
                      animate={{ opacity: 1, x: 0 }}
                      transition={{ delay: index * 0.03 }}
                      className="group flex items-center gap-3 p-2.5 rounded-lg transition-all hover:bg-white/5"
                    >
                      {/* Action Type Icon */}
                      <div 
                        className="w-7 h-7 rounded-md flex items-center justify-center shrink-0"
                        style={{
                          background: action.type === 'apply' 
                            ? 'rgba(16, 185, 129, 0.15)' 
                            : 'rgba(239, 68, 68, 0.15)',
                        }}
                      >
                        {action.type === 'apply' ? (
                          <CheckCircle2 size={14} className="text-emerald-500" />
                        ) : (
                          <XCircle size={14} className="text-red-500" />
                        )}
                      </div>

                      {/* Action Info */}
                      <div className="flex-1 min-w-0">
                        <p 
                          className="text-sm font-medium truncate"
                          style={{ color: isDark ? '#fff' : '#1a1a2e' }}
                        >
                          {t(action.nameKey, settings.language)}
                        </p>
                        <p className="text-xs" style={{ color: isDark ? 'rgba(255,255,255,0.35)' : 'rgba(0,0,0,0.35)' }}>
                          {formatTimeAgo(action.timestamp)}
                          {action.isRedoAvailable && ` • ${t('redoAvailable', settings.language).toLowerCase()}`}
                        </p>
                      </div>

                      {/* Action Buttons */}
                      <div className="flex items-center gap-1 opacity-0 group-hover:opacity-100 transition-opacity">
                        {!action.isRedoAvailable ? (
                          <motion.button
                            onClick={() => handleUndo(action)}
                            whileHover={{ scale: 1.1 }}
                            whileTap={{ scale: 0.9 }}
                            className="p-1.5 rounded-md transition-colors"
                            style={{
                              background: 'rgba(99, 102, 241, 0.15)',
                              color: '#818CF8',
                            }}
                            title={t('undo', settings.language)}
                          >
                            <Undo2 size={12} />
                          </motion.button>
                        ) : (
                          <motion.button
                            onClick={() => handleRedo(action)}
                            whileHover={{ scale: 1.1 }}
                            whileTap={{ scale: 0.9 }}
                            className="p-1.5 rounded-md transition-colors"
                            style={{
                              background: 'rgba(16, 185, 129, 0.15)',
                              color: '#10B981',
                            }}
                            title={t('redo', settings.language)}
                          >
                            <Redo2 size={12} />
                          </motion.button>
                        )}
                        
                        <button
                          onClick={() => handleRemoveItem(action.id)}
                          className="p-1.5 rounded-md hover:bg-red-500/10 transition-colors"
                          style={{ color: isDark ? 'rgba(255,255,255,0.3)' : 'rgba(0,0,0,0.3)' }}
                          title={t('remove', settings.language)}
                        >
                          <Trash2 size={12} />
                        </button>
                      </div>
                    </motion.div>
                  ))}
                </div>
              ) : (
                /* Empty State */
                <div className="text-center py-6">
                  <div 
                    className="w-12 h-12 rounded-full mx-auto mb-3 flex items-center justify-center"
                    style={{ background: isDark ? 'rgba(255, 255, 255, 0.03)' : 'rgba(0, 0, 0, 0.03)' }}
                  >
                    <History size={22} style={{ color: isDark ? 'rgba(255,255,255,0.2)' : 'rgba(0,0,0,0.2)' }} />
                  </div>
                  <p 
                    className="text-sm font-medium"
                    style={{ color: isDark ? 'rgba(255,255,255,0.4)' : 'rgba(0,0,0,0.4)' }}
                  >
                    {t('emptyQueue', settings.language)}
                  </p>
                  <p 
                    className="text-xs mt-1"
                    style={{ color: isDark ? 'rgba(255,255,255,0.25)' : 'rgba(0,0,0,0.25)' }}
                  >
                    {t('emptyQueueDesc', settings.language)}
                  </p>
                </div>
              )}

              {/* Stats Footer */}
              {queue.length > 0 && (
                <div 
                  className="mt-3 pt-3 flex items-center justify-between text-xs"
                  style={{ borderTop: `1px solid ${isDark ? 'rgba(255,255,255,0.06)' : 'rgba(0,0,0,0.06)'}`, color: isDark ? 'rgba(255,255,255,0.35)' : 'rgba(0,0,0,0.35)' }}
                >
                  <span>{queue.length} {t('totalActions', settings.language).toLowerCase()}</span>
                  <span>{undoableActions.length} {t('undoable', settings.language).toLowerCase()} / {redoableActions.length} {t('redoable', settings.language).toLowerCase()}</span>
                </div>
              )}
            </div>
          </motion.div>
        )}
      </AnimatePresence>

      {/* Custom Scrollbar Styles */}
      <style jsx global>{`
        .custom-scrollbar::-webkit-scrollbar {
          width: 4px;
        }
        .custom-scrollbar::-webkit-scrollbar-track {
          background: transparent;
        }
        .custom-scrollbar::-webkit-scrollbar-thumb {
          background: rgba(255, 255, 255, 0.1);
          border-radius: 4px;
        }
        .custom-scrollbar::-webkit-scrollbar-thumb:hover {
          background: rgba(255, 255, 255, 0.2);
        }
      `}</style>
    </div>
  );
}

// Hook for adding actions to undo queue
export function useUndoAction() {
  const addAction = useCallback((optimizationId: string, nameKey: string, type: 'apply' | 'revert') => {
    const addFn = (window as any).__ca_o_addUndoAction;
    if (addFn) {
      addFn({
        id: `${optimizationId}-${Date.now()}`,
        optimizationId,
        nameKey,
        type,
      });
    }
  }, []);
  
  return { addAction };
}

export default UndoQueue;
