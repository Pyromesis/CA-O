'use client';

import { useState, useEffect, useCallback, useRef, useMemo } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { useAppStore } from '@/store/useAppStore';
import {
  Rocket,
  Zap,
  Shield,
  Globe,
  Cpu,
  Sparkles,
  Check,
  X,
  ChevronLeft,
  ChevronRight,
  Sun,
  Moon,
} from 'lucide-react';

// Step types
type OnboardingStep = 'welcome' | 'features' | 'language' | 'quickstart' | 'ready';

interface OnboardingWizardProps {
  onComplete: () => void;
}

// Feature data for step 2
const features = [
  {
    icon: Zap,
    color: '#FF6B35',
    bgColor: 'rgba(255, 107, 53, 0.15)',
    titleKey: 'onboardFeature1Title',
    descKey: 'onboardFeature1Desc',
  },
  {
    icon: Shield,
    color: '#10B981',
    bgColor: 'rgba(16, 185, 129, 0.15)',
    titleKey: 'onboardFeature2Title',
    descKey: 'onboardFeature2Desc',
  },
  {
    icon: Globe,
    color: '#3B82F6',
    bgColor: 'rgba(59, 130, 246, 0.15)',
    titleKey: 'onboardFeature3Title',
    descKey: 'onboardFeature3Desc',
  },
  {
    icon: Cpu,
    color: '#8B5CF6',
    bgColor: 'rgba(139, 92, 246, 0.15)',
    titleKey: 'onboardFeature4Title',
    descKey: 'onboardFeature4Desc',
  },
];

// Confetti colors
const confettiColors = ['#FF6B35', '#10B981', '#3B82F6', '#8B5CF6', '#F59E0B', '#EF4444'];

export function OnboardingWizard({ onComplete }: OnboardingWizardProps) {
  const [currentStep, setCurrentStep] = useState<OnboardingStep>('welcome');
  const [direction, setDirection] = useState(1);
  const [isExiting, setIsExiting] = useState(false);
  const [selectedLanguage, setSelectedLanguage] = useState<'es' | 'en'>('es');
  const [enableQuickStart, setEnableQuickStart] = useState(true);
  
  const { updateSettings } = useAppStore();
  
  // Steps order for navigation
  const steps: OnboardingStep[] = ['welcome', 'features', 'language', 'quickstart', 'ready'];
  const currentStepIndex = steps.indexOf(currentStep);

  // Animation variants
  const slideVariants = {
    enter: (direction: number) => ({
      x: direction > 0 ? 100 : -100,
      opacity: 0,
      scale: 0.95,
    }),
    center: {
      x: 0,
      opacity: 1,
      scale: 1,
    },
    exit: (direction: number) => ({
      x: direction > 0 ? -100 : 100,
      opacity: 0,
      scale: 0.95,
    }),
  };

  const navigateToStep = useCallback((step: OnboardingStep) => {
    const newIdx = steps.indexOf(step);
    const dir = newIdx > currentStepIndex ? 1 : -1;
    setDirection(dir);
    setCurrentStep(step);
  }, [currentStepIndex]);

  const handleNext = useCallback(() => {
    if (currentStepIndex < steps.length - 1) {
      setDirection(1);
      setCurrentStep(steps[currentStepIndex + 1]);
    }
  }, [currentStepIndex]);

  const handleBack = useCallback(() => {
    if (currentStepIndex > 0) {
      setDirection(-1);
      setCurrentStep(steps[currentStepIndex - 1]);
    }
  }, [currentStepIndex]);

  const handleSkip = useCallback(async () => {
    setIsExiting(true);
    // Save language preference
    updateSettings({ language: selectedLanguage });
    
    // Small delay for exit animation
    setTimeout(() => {
      onComplete();
    }, 400);
  }, [selectedLanguage, updateSettings, onComplete]);

  const handleComplete = useCallback(async () => {
    setIsExiting(true);
    // Save preferences
    updateSettings({ 
      language: selectedLanguage,
      autoApplySafeTweaks: enableQuickStart 
    });
    
    // Small delay for exit animation
    setTimeout(() => {
      onComplete();
    }, 500);
  }, [selectedLanguage, enableQuickStart, updateSettings, onComplete]);

  // Generate confetti particles for final step
  const confettiParticles = useMemo(() => 
    Array.from({ length: 20 }, (_, i) => ({
      id: i,
      x: Math.random() * 100,
      color: confettiColors[Math.floor(Math.random() * confettiColors.length)],
      delay: Math.random() * 1.5,
      duration: 1.5 + Math.random() * 1,
      size: 6 + Math.random() * 8,
    }))
  , []);

  return (
    <motion.div
      className="onboarding-overlay"
      initial={{ opacity: 0 }}
      animate={{ opacity: isExiting ? 0 : 1 }}
      transition={{ duration: isExiting ? 0.4 : 0.6 }}
      style={{
        animation: isExiting ? 'none' : undefined,
      }}
    >
      {/* Background Orbs */}
      <div className="onboarding-bg-orb orb-1" />
      <div className="onboarding-bg-orb orb-2" />
      <div className="onboarding-bg-orb orb-3" />

      {/* Main Card */}
      <motion.div
        className="onboarding-card"
        initial={{ scale: 0.9, y: 30 }}
        animate={{ 
          scale: isExiting ? 1.1 : 1, 
          y: isExiting ? -50 : 0,
          opacity: isExiting ? 0 : 1,
        }}
        transition={{ 
          scale: { duration: isExiting ? 0.4 : 0.5, ease: "easeOut" as const },
          y: { duration: isExiting ? 0.4 : 0.5, ease: "easeOut" as const },
          opacity: { duration: 0.3 }
        }}
      >
        {/* Skip Button */}
        {currentStep !== 'ready' && (
          <button className="btn-skip" onClick={handleSkip}>
            {selectedLanguage === 'es' ? 'Omitir' : 'Skip'}
          </button>
        )}

        {/* Content Area */}
        <AnimatePresence mode="wait" custom={direction}>
          <motion.div
            key={currentStep}
            custom={direction}
            variants={slideVariants}
            initial="enter"
            animate="center"
            exit="exit"
            transition={{ duration: 0.35, ease: "easeInOut" as const }}
            className="onboarding-content"
          >
            {/* STEP 1: Welcome */}
            {currentStep === 'welcome' && (
              <>
                <div className="logo-container">
                  <div className="logo-pulse-ring" />
                  <motion.div
                    className="logo-icon"
                    initial={{ rotate: -10 }}
                    animate={{ rotate: 0 }}
                    transition={{ type: "spring", stiffness: 200, damping: 15, delay: 0.2 }}
                  >
                    <Rocket size={44} color="white" strokeWidth={2.5} />
                  </motion.div>
                </div>

                <motion.h2
                  initial={{ opacity: 0, y: 20 }}
                  animate={{ opacity: 1, y: 0 }}
                  transition={{ delay: 0.3 }}
                  className="text-3xl font-bold mb-4 text-white"
                >
                  {selectedLanguage === 'es' 
                    ? '¡Bienvenido a CA-O!' 
                    : 'Welcome to CA-O!'}
                </motion.h2>

                <motion.p
                  initial={{ opacity: 0, y: 20 }}
                  animate={{ opacity: 1, y: 0 }}
                  transition={{ delay: 0.4 }}
                  className="text-base leading-relaxed text-white/60 max-w-sm mx-auto"
                >
                  {selectedLanguage === 'es'
                    ? 'Tu asistente de optimización de sistema para Windows. Descubre cómo mejorar el rendimiento de tu PC.'
                    : 'Your Windows system optimization assistant. Discover how to boost your PC performance.'}
                </motion.p>

                <motion.div
                  initial={{ opacity: 0 }}
                  animate={{ opacity: 1 }}
                  transition={{ delay: 0.6 }}
                  className="flex items-center gap-2 mt-6 px-4 py-2 rounded-full bg-white/5 border border-white/10"
                >
                  <Sparkles size={16} className="text-orange-400" />
                  <span className="text-sm text-white/70">
                    {selectedLanguage === 'es' ? 'Solo unos pasos para comenzar' : 'Just a few steps to get started'}
                  </span>
                </motion.div>

                <div className="onboarding-nav">
                  <button onClick={handleNext} className="btn-onboarding-primary">
                    {selectedLanguage === 'es' ? 'Comenzar' : 'Get Started'}
                    <ChevronRight size={18} className="inline ml-1" />
                  </button>
                </div>
              </>
            )}

            {/* STEP 2: Features Overview */}
            {currentStep === 'features' && (
              <>
                <h2 className="text-2xl font-bold mb-2 text-white">
                  {selectedLanguage === 'es' ? 'Características' : 'Features'}
                </h2>
                <p className="text-sm text-white/50 mb-6">
                  {selectedLanguage === 'es' 
                    ? 'Lo que puedes hacer con CA-O'
                    : 'What you can do with CA-O'}
                </p>

                <div className="feature-grid">
                  {features.map((feature, idx) => {
                    const Icon = feature.icon;
                    return (
                      <motion.div
                        key={idx}
                        className="feature-item"
                        initial={{ opacity: 0, y: 20 }}
                        animate={{ opacity: 1, y: 0 }}
                        transition={{ delay: 0.1 * idx }}
                      >
                        <div 
                          className="feature-icon-wrapper"
                          style={{ background: feature.bgColor }}
                        >
                          <Icon size={26} style={{ color: feature.color }} />
                        </div>
                        <h4 className="font-semibold text-white text-sm mb-1">
                          {selectedLanguage === 'es' 
                            ? (feature.titleKey === 'onboardFeature1Title' && 'Optimización Rápida') ||
                              (feature.titleKey === 'onboardFeature2Title' && 'Modo Seguro') ||
                              (feature.titleKey === 'onboardFeature3Title' && 'Red Mejorada') ||
                              (feature.titleKey === 'onboardFeature4Title' && 'Rendimiento Total')
                            : (feature.titleKey === 'onboardFeature1Title' && 'Fast Optimization') ||
                              (feature.titleKey === 'onboardFeature2Title' && 'Safe Mode') ||
                              (feature.titleKey === 'onboardFeature3Title' && 'Network Boost') ||
                              (feature.titleKey === 'onboardFeature4Title' && 'Full Performance')}
                        </h4>
                        <p className="text-xs text-white/40">
                          {selectedLanguage === 'es'
                            ? (feature.descKey === 'onboardFeature1Desc' && 'Aplica ajustes en un clic') ||
                              (feature.descKey === 'onboardFeature2Desc' && 'Cambios reversibles seguros') ||
                              (feature.descKey === 'onboardFeature3Desc' && 'Reduce latencia de red') ||
                              (feature.descKey === 'onboardFeature4Desc' && 'Máximo poder del sistema')
                            : (feature.descKey === 'onboardFeature1Desc' && 'Apply tweaks in one click') ||
                              (feature.descKey === 'onboardFeature2Desc' && 'Reversible safe changes') ||
                              (feature.descKey === 'onboardFeature3Desc' && 'Reduce network latency') ||
                              (feature.descKey === 'onboardFeature4Desc' && 'Maximum system power')}
                        </p>
                      </motion.div>
                    );
                  })}
                </div>

                <div className="onboarding-nav">
                  <button onClick={handleBack} className="btn-onboarding-secondary">
                    <ChevronLeft size={18} className="inline mr-1" />
                    {selectedLanguage === 'es' ? 'Atrás' : 'Back'}
                  </button>
                  <button onClick={handleNext} className="btn-onboarding-primary">
                    {selectedLanguage === 'es' ? 'Siguiente' : 'Next'}
                    <ChevronRight size={18} className="inline ml-1" />
                  </button>
                </div>
              </>
            )}

            {/* STEP 3: Choose Language */}
            {currentStep === 'language' && (
              <>
                <motion.div
                  initial={{ scale: 0 }}
                  animate={{ scale: 1 }}
                  transition={{ type: "spring", stiffness: 300, damping: 20 }}
                  className="w-20 h-20 rounded-2xl bg-blue-500/15 flex items-center justify-center mb-6"
                >
                  <Globe size={36} className="text-blue-400" />
                </motion.div>

                <h2 className="text-2xl font-bold mb-2 text-white">
                  {selectedLanguage === 'es' ? 'Idioma' : 'Language'}
                </h2>
                <p className="text-sm text-white/50 mb-6">
                  {selectedLanguage === 'es' 
                    ? 'Selecciona tu idioma preferido'
                    : 'Select your preferred language'}
                </p>

                <div className="language-selector">
                  <motion.button
                    className={`language-option ${selectedLanguage === 'es' ? 'selected' : ''}`}
                    whileTap={{ scale: 0.95 }}
                    onClick={() => setSelectedLanguage('es')}
                  >
                    <span className="language-flag">🇪🇸</span>
                    <span className="language-name">Español</span>
                  </motion.button>

                  <motion.button
                    className={`language-option ${selectedLanguage === 'en' ? 'selected' : ''}`}
                    whileTap={{ scale: 0.95 }}
                    onClick={() => setSelectedLanguage('en')}
                  >
                    <span className="language-flag">🇬🇧</span>
                    <span className="language-name">English</span>
                  </motion.button>
                </div>

                <motion.p
                  initial={{ opacity: 0 }}
                  animate={{ opacity: 1 }}
                  transition={{ delay: 0.3 }}
                  className="text-xs text-white/40 mt-4 flex items-center gap-1"
                >
                  <Sparkles size={12} />
                  {selectedLanguage === 'es' 
                    ? 'Puedes cambiarlo después en Configuración'
                    : 'You can change it later in Settings'}
                </motion.p>

                <div className="onboarding-nav">
                  <button onClick={handleBack} className="btn-onboarding-secondary">
                    <ChevronLeft size={18} className="inline mr-1" />
                    {selectedLanguage === 'es' ? 'Atrás' : 'Back'}
                  </button>
                  <button onClick={handleNext} className="btn-onboarding-primary">
                    {selectedLanguage === 'es' ? 'Siguiente' : 'Next'}
                    <ChevronRight size={18} className="inline ml-1" />
                  </button>
                </div>
              </>
            )}

            {/* STEP 4: Quick Start */}
            {currentStep === 'quickstart' && (
              <>
                <motion.div
                  initial={{ scale: 0, rotate: -180 }}
                  animate={{ scale: 1, rotate: 0 }}
                  transition={{ type: "spring", stiffness: 200, damping: 15 }}
                  className="w-20 h-20 rounded-2xl bg-green-500/15 flex items-center justify-center mb-6"
                >
                  <Zap size={36} className="text-green-400" />
                </motion.div>

                <h2 className="text-2xl font-bold mb-2 text-white">
                  {selectedLanguage === 'es' ? 'Inicio Rápido' : 'Quick Start'}
                </h2>
                <p className="text-sm text-white/50 mb-6 max-w-xs mx-auto">
                  {selectedLanguage === 'es'
                    ? '¿Deseas aplicar automáticamente las optimizaciones seguras?'
                    : 'Would you like to automatically apply safe optimizations?'}
                </p>

                <motion.div
                  className="quick-start-toggle"
                  whileTap={{ scale: 0.98 }}
                  onClick={() => setEnableQuickStart(!enableQuickStart)}
                >
                  <span className={`text-white font-medium ${!enableQuickStart ? 'opacity-50' : ''}`}>
                    {selectedLanguage === 'es' ? 'Aplicar optimizaciones seguras' : 'Apply safe optimizations'}
                  </span>
                  <div className={`toggle-switch ${enableQuickStart ? 'active' : ''}`}>
                    <div className="toggle-knob" />
                  </div>
                </motion.div>

                <motion.div
                  initial={{ opacity: 0 }}
                  animate={{ opacity: 1 }}
                  transition={{ delay: 0.3 }}
                  className="px-4 py-3 rounded-xl bg-white/5 border border-white/10 mt-4 max-w-xs"
                >
                  <p className="text-xs text-white/50 leading-relaxed">
                    {selectedLanguage === 'es'
                      ? 'Las optimizaciones seguras son ajustes probados que no afectan la estabilidad del sistema.'
                      : 'Safe optimizations are tested tweaks that don\'t affect system stability.'}
                  </p>
                </motion.div>

                <div className="onboarding-nav">
                  <button onClick={handleBack} className="btn-onboarding-secondary">
                    <ChevronLeft size={18} className="inline mr-1" />
                    {selectedLanguage === 'es' ? 'Atrás' : 'Back'}
                  </button>
                  <button onClick={handleNext} className="btn-onboarding-primary">
                    {selectedLanguage === 'es' ? 'Último paso' : 'Final Step'}
                    <ChevronRight size={18} className="inline ml-1" />
                  </button>
                </div>
              </>
            )}

            {/* STEP 5: Ready to Go */}
            {currentStep === 'ready' && (
              <>
                {/* Success Checkmark */}
                <div className="success-checkmark">
                  <Check size={40} className="text-white" strokeWidth={3} />
                </div>

                <motion.h2
                  initial={{ opacity: 0, y: 20 }}
                  animate={{ opacity: 1, y: 0 }}
                  transition={{ delay: 0.3 }}
                  className="text-3xl font-bold mb-3 text-white"
                >
                  {selectedLanguage === 'es' ? '¡Listo para Optimizar!' : 'Ready to Optimize!'}
                </motion.h2>

                <motion.p
                  initial={{ opacity: 0, y: 20 }}
                  animate={{ opacity: 1, y: 0 }}
                  transition={{ delay: 0.4 }}
                  className="text-base leading-relaxed text-white/60 max-w-sm mx-auto"
                >
                  {selectedLanguage === 'es'
                    ? 'Tu sistema está configurado. Explora las categorías de optimización o aplica todo con un solo click.'
                    : 'Your system is configured. Explore optimization categories or apply everything with one click.'}
                </motion.p>

                {/* Summary */}
                <motion.div
                  initial={{ opacity: 0, y: 20 }}
                  animate={{ opacity: 1, y: 0 }}
                  transition={{ delay: 0.5 }}
                  className="flex gap-4 mt-6 w-full max-w-sm"
                >
                  <div className="flex-1 p-4 rounded-xl bg-white/5 text-center">
                    <Globe size={20} className="mx-auto mb-2 text-blue-400" />
                    <p className="text-xs text-white/50">{selectedLanguage === 'es' ? 'Idioma' : 'Language'}</p>
                    <p className="text-sm font-semibold text-white">
                      {selectedLanguage === 'es' ? 'Español' : 'English'}
                    </p>
                  </div>
                  <div className="flex-1 p-4 rounded-xl bg-white/5 text-center">
                    {enableQuickStart ? (
                      <Zap size={20} className="mx-auto mb-2 text-green-400" />
                    ) : (
                      <Shield size={20} className="mx-auto mb-2 text-gray-400" />
                    )}
                    <p className="text-xs text-white/50">{selectedLanguage === 'es' ? 'Auto-aplicar' : 'Auto-apply'}</p>
                    <p className="text-sm font-semibold text-white">
                      {enableQuickStart 
                        ? (selectedLanguage === 'es' ? 'Activado' : 'On')
                        : (selectedLanguage === 'es' ? 'Desactivado' : 'Off')
                      }
                    </p>
                  </div>
                </motion.div>

                <div className="onboarding-nav mt-8">
                  <motion.button
                    whileHover={{ scale: 1.03 }}
                    whileTap={{ scale: 0.97 }}
                    onClick={handleComplete}
                    className="btn-onboarding-primary text-lg py-4"
                  >
                    <Rocket size={22} className="inline mr-2" />
                    {selectedLanguage === 'es' ? '¡Comenzar a Optimizar!' : 'Start Optimizing!'}
                  </motion.button>
                </div>

                {/* Confetti Particles */}
                <div className="absolute inset-0 overflow-hidden pointer-events-none rounded-[32px]">
                  {confettiParticles.map((particle) => (
                    <motion.div
                      key={particle.id}
                      className="confetti-particle"
                      style={{
                        left: `${particle.x}%`,
                        top: -20,
                        width: particle.size,
                        height: particle.size,
                        backgroundColor: particle.color,
                        borderRadius: particle.id % 2 === 0 ? '50%' : '2px',
                      }}
                      initial={{ y: -20, rotate: 0 }}
                      animate={{ 
                        y: 500, 
                        rotate: particle.id % 2 === 0 ? 360 : -360,
                        opacity: [1, 1, 0]
                      }}
                      transition={{ 
                        duration: particle.duration, 
                        delay: particle.delay,
                        repeat: Infinity,
                        repeatDelay: 2,
                        ease: "easeOut" as const
                      }}
                    />
                  ))}
                </div>
              </>
            )}
          </motion.div>
        </AnimatePresence>

        {/* Progress Dots */}
        {!isExiting && (
          <div className="onboarding-progress">
            {steps.map((step, idx) => (
              <motion.button
                key={step}
                className={`progress-dot ${
                  idx === currentStepIndex ? 'active' : idx < currentStepIndex ? 'completed' : ''
                }`}
                onClick={() => navigateToStep(step)}
                whileHover={{ scale: 1.3 }}
                whileTap={{ scale: 0.9 }}
              />
            ))}
          </div>
        )}
      </motion.div>
    </motion.div>
  );
}

export default OnboardingWizard;
