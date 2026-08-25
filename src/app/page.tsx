'use client';

import { useState, useEffect, useCallback, useRef, useSyncExternalStore } from 'react';
import { AnimatePresence } from 'framer-motion';
import { apiFetch, captureSessionToken } from '@/lib/session-client';
import { useAppStore } from '@/store/useAppStore';
import { SplashScreen } from '@/components/ca-o/SplashScreen';
import { Header } from '@/components/ca-o/Header';
import { Dock } from '@/components/ca-o/Dock';
import { DashboardView } from '@/components/ca-o/DashboardView';
import { FullOptimizationPanel } from '@/components/ca-o/FullOptimizationPanel';
import { TroubleshootingView } from '@/components/ca-o/TroubleshootingView';
import { SettingsView } from '@/components/ca-o/SettingsView';
import { HelpModal } from '@/components/ca-o/HelpModal';
import { OnboardingWizard } from '@/components/ca-o/OnboardingWizard';
import { NotificationProvider, useNotifications } from '@/components/ca-o/NotificationSystem';
import { useKeyboardShortcuts } from '@/hooks/useKeyboardShortcuts';
import { SoundProvider } from '@/components/ca-o/SoundEffects';

// Subscribe to mount state
const emptySubscribe = () => () => {};

function AppContent() {
  const { 
    currentView, 
    settings, 
    showOnboarding,
    completeOnboarding,
    hydrateFromDB
  } = useAppStore();
  // The splash is a first-run welcome: skip it on every later launch using
  // the local flag instantly, then confirm with the persistent server flag
  // (the packaged app can change its local origin between launches).
  const [showSplash, setShowSplash] = useState<boolean>(() => {
    if (typeof window === 'undefined') return true;
    try {
      return localStorage.getItem('ca-o-splash-seen') !== 'true';
    } catch {
      return true;
    }
  });

  useEffect(() => {
    // Capture the session token handed over by the Electron launcher (once).
    captureSessionToken();
    let cancelled = false;
    apiFetch('/api/app-state')
      .then((res) => (res.ok ? res.json() : null))
      .then((json) => {
        if (cancelled || json?.data?.splashSeen !== true) return;
        try {
          localStorage.setItem('ca-o-splash-seen', 'true');
        } catch {
          // Ignore storage failures.
        }
        setShowSplash(false);
      })
      .catch(() => {});
    return () => {
      cancelled = true;
    };
  }, []);
  
  // Initialize keyboard shortcuts
  useKeyboardShortcuts();
  
  const isClient = useSyncExternalStore(emptySubscribe, () => true, () => false);
  useEffect(() => {
    // Sync persisted optimization state from SQLite on first client mount
    hydrateFromDB();
  }, [hydrateFromDB]);

  // Scroll to top when view changes
  const scrollPositionsRef = useRef<Record<string, number>>({});
  const prevViewRef = useRef(currentView);

  useEffect(() => {
    if (typeof window !== 'undefined') {
      const prevView = prevViewRef.current;
      
      // Save scroll position for the PREVIOUS view before we change to the new one
      if (prevView && prevView !== currentView) {
        scrollPositionsRef.current[prevView] = window.scrollY;
      }
      
      // Restore scroll position for the NEW view, or go to top if there isn't one
      const savedPosition = scrollPositionsRef.current[currentView];
      
      if (savedPosition !== undefined && savedPosition > 0) {
        // Use a small timeout to allow React to render the DOM elements before scrolling
        setTimeout(() => {
          window.scrollTo({ top: savedPosition, behavior: 'auto' });
        }, 10);
      } else {
        window.scrollTo({ top: 0, behavior: 'smooth' });
      }
      
      prevViewRef.current = currentView;
    }
  }, [currentView]);

  // Handle splash screen completion
  const handleSplashComplete = useCallback(() => {
    setShowSplash(false);
    try {
      localStorage.setItem('ca-o-splash-seen', 'true');
    } catch {
      // Ignore storage failures.
    }
    apiFetch('/api/app-state', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ splashSeen: true })
    }).catch(() => {});
  }, []);

  // Handle onboarding completion
  const handleOnboardingComplete = useCallback(() => {
    completeOnboarding();
  }, [completeOnboarding]);

  // Apply theme to document with transition animation
  const prevThemeRef = useRef(settings.theme);
  
  useEffect(() => {
    const prevTheme = prevThemeRef.current;
    
    // Add transition class when theme changes
    if (prevTheme !== settings.theme) {
      document.body.classList.add('theme-transitioning');
      
      // Create and add overlay for smooth transition
      const overlay = document.createElement('div');
      overlay.className = `theme-switch-overlay ${prevTheme === 'dark' ? 'dark-to-light' : 'light-to-dark'}`;
      document.body.appendChild(overlay);
      
      // Clean up after animation
      setTimeout(() => {
        document.body.classList.remove('theme-transitioning');
        overlay.remove();
      }, 500);
    }
    
    // Apply theme
    document.documentElement.classList.toggle('dark', settings.theme === 'dark');
    document.documentElement.lang = settings.language;
    
    // Update ref
    prevThemeRef.current = settings.theme;
  }, [settings.theme, settings.language]);

  // Show loading state during SSR
  if (!isClient) {
    return (
      <div className="min-h-screen bg-[#0a0a14] flex items-center justify-center">
        <div className="w-8 h-8 border-2 border-[#FF6B35] border-t-transparent rounded-full animate-spin" />
      </div>
    );
  }

  return (
    <div className={`min-h-screen flex flex-col ${settings.theme === 'dark' ? 'bg-[#0a0a14]' : 'bg-gray-50'} theme-transition`}>
      {/* Splash Screen */}
      <AnimatePresence>
        {showSplash && (
          <SplashScreen onComplete={handleSplashComplete} />
        )}
      </AnimatePresence>

      {/* Main Application */}
      {!showSplash && (
        <>
          {/* Header */}
          <Header />

          {/* Main Content Area */}
          <main className="flex-1 pb-28 pt-20 px-4 md:px-6 lg:px-8 max-w-7xl mx-auto w-full">
            <AnimatePresence mode="wait">
              {currentView === 'dashboard' && (
                <DashboardView key="dashboard" />
              )}
              {currentView === 'optimization' && (
                <FullOptimizationPanel key="optimization" />
              )}
              {currentView === 'troubleshooting' && (
                <TroubleshootingView key="troubleshooting" />
              )}
              {currentView === 'settings' && (
                <SettingsView key="settings" />
              )}
            </AnimatePresence>
          </main>

          {/* Dock Navigation */}
          <Dock />

          {/* Help Modal Overlay */}
          <HelpModal />
        </>
      )}

      {/* Onboarding Wizard - Full Screen Overlay */}
      <AnimatePresence>
        {showOnboarding && !showSplash && (
          <OnboardingWizard onComplete={handleOnboardingComplete} />
        )}
      </AnimatePresence>
    </div>
  );
}

export default function Home() {
  return (
    <NotificationProvider position="top-right">
      <SoundProvider>
        <AppContent />
      </SoundProvider>
    </NotificationProvider>
  );
}
