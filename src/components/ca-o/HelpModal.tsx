'use client';

import { useEffect, useCallback } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { useAppStore } from '@/store/useAppStore';
import { t } from '@/lib/i18n';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import {
  X,
  Keyboard,
  LayoutDashboard,
  Zap,
  Wrench,
  Settings2,
  HelpCircle,
  LogOut,
  Lightbulb,
  Info
} from 'lucide-react';

interface ShortcutItem {
  keys: string[];
  description: string;
  icon: React.ReactNode;
}

export function HelpModal() {
  const { showHelpModal, setShowHelpModal, setCurrentView, settings } = useAppStore();

  const handleClose = useCallback(() => {
    setShowHelpModal(false);
  }, [setShowHelpModal]);

  // Close on Escape key
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && showHelpModal) {
        handleClose();
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [showHelpModal, handleClose]);

  // Prevent body scroll when modal is open
  useEffect(() => {
    if (showHelpModal) {
      document.body.style.overflow = 'hidden';
    } else {
      document.body.style.overflow = '';
    }
    return () => {
      document.body.style.overflow = '';
    };
  }, [showHelpModal]);

  const shortcuts: ShortcutItem[] = [
    {
      keys: ['1'],
      description: t('shortcutDashboard', settings.language),
      icon: <LayoutDashboard className="h-4 w-4" />
    },
    {
      keys: ['2'],
      description: t('shortcutOptimization', settings.language),
      icon: <Zap className="h-4 w-4" />
    },
    {
      keys: ['3'],
      description: t('shortcutTroubleshooting', settings.language),
      icon: <Wrench className="h-4 w-4" />
    },
    {
      keys: ['4'],
      description: t('shortcutSettings', settings.language),
      icon: <Settings2 className="h-4 w-4" />
    },
    {
      keys: ['Esc'],
      description: t('shortcutEscape', settings.language),
      icon: <LogOut className="h-4 w-4" />
    },
    {
      keys: ['?'],
      description: t('shortcutHelp', settings.language),
      icon: <HelpCircle className="h-4 w-4" />
    }
  ];

  const tips = [
    { text: t('helpTip1', settings.language), icon: Keyboard },
    { text: t('helpTip2', settings.language), icon: Lightbulb },
    { text: t('helpTip3', settings.language), icon: Info }
  ];

  const handleShortcutClick = (keys: string[]) => {
    switch (keys[0]) {
      case '1':
        setCurrentView('dashboard');
        break;
      case '2':
        setCurrentView('optimization');
        break;
      case '3':
        setCurrentView('troubleshooting');
        break;
      case '4':
        setCurrentView('settings');
        break;
      default:
        break;
    }
  };

  return (
    <AnimatePresence>
      {showHelpModal && (
        <>
          {/* Backdrop */}
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            transition={{ duration: 0.2 }}
            className="fixed inset-0 bg-black/60 backdrop-blur-sm z-50"
            onClick={handleClose}
          />

          {/* Modal */}
          <motion.div
            initial={{ opacity: 0, scale: 0.95, y: 20 }}
            animate={{ opacity: 1, scale: 1, y: 0 }}
            exit={{ opacity: 0, scale: 0.95, y: 20 }}
            transition={{ 
              type: "spring", 
              stiffness: 300, 
              damping: 30,
              duration: 0.3 
            }}
            className="fixed inset-x-4 top-[10%] md:inset-x-auto md:left-1/2 md:-translate-x-1/2 md:w-full md:max-w-lg z-50"
          >
            <div className="bg-[#12121e] border border-white/10 rounded-2xl shadow-2xl overflow-hidden">
              {/* Header */}
              <div className="flex items-center justify-between p-5 border-b border-white/10">
                <div className="flex items-center gap-3">
                  <div className="w-10 h-10 rounded-xl bg-[#FF6B35]/20 flex items-center justify-center">
                    <Keyboard className="h-5 w-5 text-[#FF6B35]" />
                  </div>
                  <div>
                    <h2 className="text-lg font-semibold">{t('helpTitle', settings.language)}</h2>
                    <p className="text-xs text-muted-foreground">{t('keyboardShortcuts', settings.language)}</p>
                  </div>
                </div>
                <Button
                  variant="ghost"
                  size="icon"
                  onClick={handleClose}
                  className="h-8 w-8 rounded-lg hover:bg-white/10"
                >
                  <X className="h-4 w-4" />
                </Button>
              </div>

              {/* Content */}
              <div className="p-5 space-y-6">
                {/* Shortcuts List */}
                <div>
                  <h3 className="text-sm font-medium text-muted-foreground uppercase tracking-wider mb-3">
                    {t('keyboardShortcuts', settings.language)}
                  </h3>
                  <div className="space-y-2">
                    {shortcuts.map((shortcut) => (
                      <button
                        key={shortcut.keys[0]}
                        onClick={() => handleShortcutClick(shortcut.keys)}
                        className="w-full flex items-center gap-3 p-3 rounded-xl bg-white/[0.03] hover:bg-white/[0.08] transition-colors group cursor-pointer"
                      >
                        {/* Icon */}
                        <div className="w-8 h-8 rounded-lg bg-white/5 flex items-center justify-center text-muted-foreground group-hover:text-foreground transition-colors">
                          {shortcut.icon}
                        </div>

                        {/* Description */}
                        <span className="text-sm flex-1 text-left">{shortcut.description}</span>

                        {/* Key Badge */}
                        <Badge 
                          variant="outline" 
                          className="font-mono text-xs px-2 py-1 border-white/20 hover:border-[#FF6B35]/50 hover:text-[#FF6B35] transition-colors"
                        >
                          {shortcut.keys[0]}
                        </Badge>
                      </button>
                    ))}
                  </div>
                </div>

                {/* Tips Section */}
                <div className="pt-4 border-t border-white/10">
                  <h3 className="text-sm font-medium text-muted-foreground uppercase tracking-wider mb-3 flex items-center gap-2">
                    <Lightbulb className="h-4 w-4" />
                    {t('helpTips', settings.language)}
                  </h3>
                  <div className="space-y-2.5">
                    {tips.map((tip, index) => (
                      <div 
                        key={index} 
                        className="flex items-start gap-3 text-sm text-muted-foreground"
                      >
                        <tip.icon className="h-4 w-4 mt-0.5 text-[#FF6B35]/70 flex-shrink-0" />
                        <span>{tip.text}</span>
                      </div>
                    ))}
                  </div>
                </div>

                {/* Version Info */}
                <div className="pt-4 border-t border-white/10">
                  <div className="flex items-center justify-between text-xs text-muted-foreground">
                    <span>{t('helpAboutText', settings.language)}</span>
                    <Badge variant="secondary" className="text-xs">
                      {t('appName', settings.language)} v1.0.0
                    </Badge>
                  </div>
                </div>
              </div>

              {/* Footer */}
              <div className="px-5 py-4 bg-white/[0.02] border-t border-white/10">
                <Button
                  onClick={handleClose}
                  variant="ghost"
                  className="w-full h-9 text-sm font-medium hover:bg-white/10"
                >
                  {t('helpClose', settings.language)} (Esc)
                </Button>
              </div>
            </div>
          </motion.div>
        </>
      )}
    </AnimatePresence>
  );
}
