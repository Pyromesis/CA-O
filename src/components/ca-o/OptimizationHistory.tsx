'use client';

import { useState } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { useAppStore } from '@/store/useAppStore';
import { t } from '@/lib/i18n';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { ScrollArea } from '@/components/ui/scroll-area';
import {
  History,
  Trash2,
  ChevronDown,
  ChevronUp,
  Clock,
  CheckCircle2,
  Undo2,
  AlertCircle
} from 'lucide-react';

interface OptimizationHistoryProps {
  className?: string;
}

export function OptimizationHistory({ className = '' }: OptimizationHistoryProps) {
  const { history, clearHistory, showHistoryPanel, setShowHistoryPanel, settings } = useAppStore();
  const [showConfirmClear, setShowConfirmClear] = useState(false);

  const formatTimestamp = (timestamp: number) => {
    const date = new Date(timestamp);
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffMins = Math.floor(diffMs / 60000);
    const diffHours = Math.floor(diffMs / 3600000);
    const diffDays = Math.floor(diffMs / 86400000);

    if (diffMins < 1) return t('historyJustNow', settings.language) || 'Just now';
    if (diffMins < 60) return `${diffMins}m ago`;
    if (diffHours < 24) return `${diffHours}h ago`;
    if (diffDays < 7) return `${diffDays}d ago`;
    
    return date.toLocaleDateString(undefined, { 
      month: 'short', 
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
  };

  const getStatusIcon = (action: string) => {
    switch (action) {
      case 'applied':
        return <CheckCircle2 className="h-4 w-4 text-emerald-400" />;
      case 'reverted':
        return <Undo2 className="h-4 w-4 text-amber-400" />;
      default:
        return <AlertCircle className="h-4 w-4 text-gray-400" />;
    }
  };

  const getStatusBadgeVariant = (action: string): "default" | "secondary" | "destructive" | "outline" => {
    switch (action) {
      case 'applied':
        return 'default';
      case 'reverted':
        return 'secondary';
      default:
        return 'outline';
    }
  };

  const handleClearHistory = () => {
    if (showConfirmClear) {
      clearHistory();
      setShowConfirmClear(false);
    } else {
      setShowConfirmClear(true);
      // Auto-hide confirmation after 3 seconds
      setTimeout(() => setShowConfirmClear(false), 3000);
    }
  };

  return (
    <motion.div
      initial={false}
      animate={{ height: showHistoryPanel ? 'auto' : 'auto' }}
      className={`w-full ${className}`}
    >
      {/* Header - Always visible */}
      <Button
        variant="ghost"
        onClick={() => setShowHistoryPanel(!showHistoryPanel)}
        className="w-full justify-between h-auto py-3 px-4 bg-white/5 hover:bg-white/10 border border-white/10 rounded-xl mb-2"
      >
        <div className="flex items-center gap-2">
          <History className="h-4 w-4 text-[#FF6B35]" />
          <span className="font-medium text-sm">{t('historyTitle', settings.language)}</span>
          {history.length > 0 && (
            <Badge variant="secondary" className="text-xs px-1.5 py-0">
              {history.length}
            </Badge>
          )}
        </div>
        {showHistoryPanel ? (
          <ChevronUp className="h-4 w-4 text-muted-foreground" />
        ) : (
          <ChevronDown className="h-4 w-4 text-muted-foreground" />
        )}
      </Button>

      {/* Collapsible Content */}
      <AnimatePresence>
        {showHistoryPanel && (
          <motion.div
            initial={{ opacity: 0, height: 0 }}
            animate={{ opacity: 1, height: 'auto' }}
            exit={{ opacity: 0, height: 0 }}
            transition={{ duration: 0.2, ease: 'easeInOut' }}
            className="overflow-hidden"
          >
            <div className="bg-white/5 border border-white/10 rounded-xl p-4">
              {/* Actions Bar */}
              <div className="flex items-center justify-between mb-3">
                <span className="text-xs text-muted-foreground flex items-center gap-1.5">
                  <Clock className="h-3 w-3" />
                  {history.length} {history.length === 1 ? 'entry' : 'entries'}
                </span>
                {history.length > 0 && (
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={handleClearHistory}
                    className={`h-7 text-xs px-2 ${
                      showConfirmClear 
                        ? 'text-red-400 hover:text-red-300 hover:bg-red-500/10' 
                        : 'text-muted-foreground hover:text-foreground'
                    }`}
                  >
                    <Trash2 className="h-3.5 w-3.5 mr-1" />
                    {showConfirmClear ? (t('confirm', settings.language) || 'Confirm?') : t('historyClear', settings.language)}
                  </Button>
                )}
              </div>

              {/* History List */}
              {history.length === 0 ? (
                <div className="flex flex-col items-center justify-center py-8 text-center">
                  <div className="w-12 h-12 rounded-full bg-white/5 flex items-center justify-center mb-3">
                    <History className="h-5 w-5 text-muted-foreground" />
                  </div>
                  <p className="text-sm font-medium text-muted-foreground mb-1">
                    {t('historyEmpty', settings.language)}
                  </p>
                  <p className="text-xs text-muted-foreground/60 max-w-[200px]">
                    {t('historyEmptyDesc', settings.language)}
                  </p>
                </div>
              ) : (
                <ScrollArea className="max-h-[300px] pr-3">
                  <div className="space-y-2">
                    {history.map((entry, index) => (
                      <motion.div
                        key={`${entry.id}-${entry.timestamp}-${index}`}
                        initial={{ opacity: 0, x: -20 }}
                        animate={{ opacity: 1, x: 0 }}
                        transition={{ delay: index * 0.05 }}
                        className="flex items-center gap-3 p-2.5 rounded-lg bg-white/[0.03] hover:bg-white/[0.06] transition-colors group"
                      >
                        {/* Status Icon */}
                        <div className="flex-shrink-0">
                          {getStatusIcon(entry.action)}
                        </div>

                        {/* Name & Time */}
                        <div className="flex-1 min-w-0">
                          <p className="text-sm font-medium truncate">
                            {t(entry.nameKey as any, settings.language) || entry.nameKey}
                          </p>
                          <p className="text-xs text-muted-foreground">
                            {formatTimestamp(entry.timestamp)}
                          </p>
                        </div>

                        {/* Status Badge */}
                        <Badge 
                          variant={getStatusBadgeVariant(entry.action)} 
                          className={`flex-shrink-0 text-xs capitalize ${
                            entry.action === 'applied' 
                              ? 'bg-emerald-500/20 text-emerald-400 border-emerald-500/30'
                              : entry.action === 'reverted'
                              ? 'bg-amber-500/20 text-amber-400 border-amber-500/30'
                              : ''
                          }`}
                        >
                          {entry.action === 'applied' && t('statusApplied', settings.language)}
                          {entry.action === 'reverted' && t('statusReverted', settings.language)}
                          {entry.action === 'pending' && t('statusPending', settings.language)}
                        </Badge>
                      </motion.div>
                    ))}
                  </div>
                </ScrollArea>
              )}
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </motion.div>
  );
}
