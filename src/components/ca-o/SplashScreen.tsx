'use client';

import { useEffect, useState, useCallback } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { useAppStore } from '@/store/useAppStore';
import { t } from '@/lib/i18n';
import { Zap } from 'lucide-react';

interface SplashScreenProps {
  onComplete: () => void;
}

export function SplashScreen({ onComplete }: SplashScreenProps) {
  const { settings, setCurrentView } = useAppStore();
  const [progress, setProgress] = useState(0);
  const [statusText, setStatusText] = useState('splashLoading');
  const [isExiting, setIsExiting] = useState(false);

  const simulateLoading = useCallback(() => {
    const stages = [
      { progress: 20, text: 'splashLoading' as const },
      { progress: 45, text: 'splashPreparing' as const },
      { progress: 70, text: 'splashPreparing' as const },
      { progress: 90, text: 'splashPreparing' as const },
      { progress: 100, text: 'splashReady' as const },
    ];

    let currentStage = 0;
    
    const interval = setInterval(() => {
      if (currentStage < stages.length) {
        const stage = stages[currentStage];
        setProgress(stage.progress);
        setStatusText(stage.text);
        currentStage++;
      } else {
        clearInterval(interval);
        
        // Wait a moment then start exit animation
        setTimeout(() => {
          setIsExiting(true);
          setTimeout(() => {
            onComplete();
            setCurrentView('dashboard');
          }, 500);
        }, 400);
      }
    }, 450);

    return interval;
  }, [onComplete, setCurrentView]);

  useEffect(() => {
    const interval = simulateLoading();
    return () => clearInterval(interval);
  }, [simulateLoading]);

  return (
    <AnimatePresence>
      {!isExiting && (
        <motion.div
          initial={{ opacity: 1 }}
          exit={{ opacity: 0 }}
          transition={{ duration: 0.5, ease: 'easeInOut' }}
          className="fixed inset-0 z-50 flex flex-col items-center justify-center overflow-hidden"
          style={{
            background: 'linear-gradient(135deg, #0f0f1a 0%, #1a1a2e 50%, #16213e 100%)',
          }}
        >
          {/* Animated Background Gradient Orbs */}
          <div className="absolute inset-0 overflow-hidden">
            <motion.div
              className="absolute w-[500px] h-[500px] rounded-full opacity-30"
              style={{
                background: 'radial-gradient(circle, rgba(255,107,53,0.4) 0%, transparent 70%)',
                top: '-10%',
                right: '-10%',
              }}
              animate={{
                scale: [1, 1.2, 1],
                x: [0, 30, 0],
                y: [0, -20, 0],
              }}
              transition={{
                duration: 8,
                repeat: Infinity,
                ease: 'easeInOut',
              }}
            />
            <motion.div
              className="absolute w-[400px] h-[400px] rounded-full opacity-20"
              style={{
                background: 'radial-gradient(circle, rgba(255,140,66,0.3) 0%, transparent 70%)',
                bottom: '-15%',
                left: '-10%',
              }}
              animate={{
                scale: [1.2, 1, 1.2],
                x: [0, -25, 0],
                y: [0, 30, 0],
              }}
              transition={{
                duration: 6,
                repeat: Infinity,
                ease: 'easeInOut',
              }}
            />
            <motion.div
              className="absolute w-[300px] h-[300px] rounded-full opacity-25"
              style={{
                background: 'radial-gradient(circle, rgba(255,107,53,0.35) 0%, transparent 70%)',
                top: '50%',
                left: '50%',
                transform: 'translate(-50%, -50%)',
              }}
              animate={{
                scale: [1, 1.4, 1],
                opacity: [0.2, 0.4, 0.2],
              }}
              transition={{
                duration: 4,
                repeat: Infinity,
                ease: 'easeInOut',
              }}
            />
          </div>

          {/* Grid Pattern Overlay */}
          <div 
            className="absolute inset-0 opacity-[0.03]"
            style={{
              backgroundImage: `
                linear-gradient(rgba(255,255,255,0.1) 1px, transparent 1px),
                linear-gradient(90deg, rgba(255,255,255,0.1) 1px, transparent 1px)
              `,
              backgroundSize: '50px 50px',
            }}
          />

          {/* Main Content */}
          <div className="relative z-10 flex flex-col items-center gap-8">
            {/* Logo Container with Glow Effect */}
            <motion.div
              initial={{ scale: 0, rotate: -180 }}
              animate={{ 
                scale: [1, 1.05, 1], 
                rotate: 0,
              }}
              transition={{
                scale: {
                  duration: 2,
                  repeat: Infinity,
                  ease: 'easeInOut',
                },
                rotate: {
                  duration: 0.8,
                  ease: 'easeOut',
                },
              }}
              className="relative"
            >
              {/* Outer Glow Ring */}
              <motion.div
                className="absolute inset-0 rounded-full"
                style={{
                  background: 'conic-gradient(from 0deg, transparent, #FF6B35, transparent)',
                  padding: '4px',
                  filter: 'blur(8px)',
                }}
                animate={{ rotate: 360 }}
                transition={{ duration: 3, repeat: Infinity, ease: 'linear' }}
              >
                <div className="w-full h-full rounded-full bg-[#0f0f1a]" />
              </motion.div>

              {/* Main Logo Circle */}
              <motion.div
                className="relative w-32 h-32 md:w-40 md:h-40 rounded-full flex items-center justify-center"
                style={{
                  background: 'linear-gradient(145deg, #1e1e32, #16162a)',
                  boxShadow: `
                    0 0 60px rgba(255, 107, 53, 0.4),
                    0 0 100px rgba(255, 107, 53, 0.2),
                    inset 0 2px 10px rgba(255, 255, 255, 0.05)
                  `,
                }}
                whileHover={{ scale: 1.02 }}
              >
                {/* Inner Gradient Ring */}
                <div 
                  className="absolute inset-2 rounded-full"
                  style={{
                    background: 'conic-gradient(from 180deg, #FF6B35, #ff8c42, #FF6B35)',
                    padding: '2px',
                    opacity: 0.6,
                  }}
                >
                  <div className="w-full h-full rounded-full bg-[#1a1a2e]" />
                </div>

                {/* Icon */}
                <Zap 
                  size={56} 
                  className="relative z-10 text-[#FF6B35]"
                  strokeWidth={1.5}
                  style={{
                    filter: 'drop-shadow(0 0 12px rgba(255, 107, 53, 0.7))',
                  }}
                />

                {/* Pulsing Dot */}
                <motion.div
                  className="absolute bottom-3 right-5 w-3 h-3 rounded-full bg-green-400"
                  animate={{
                    scale: [1, 1.5, 1],
                    opacity: [1, 0.7, 1],
                  }}
                  transition={{
                    duration: 1.5,
                    repeat: Infinity,
                    ease: 'easeInOut',
                  }}
                  style={{
                    boxShadow: '0 0 10px rgba(74, 222, 128, 0.6)',
                  }}
                />
              </motion.div>
            </motion.div>

            {/* App Name */}
            <motion.div
              initial={{ opacity: 0, y: 20 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ delay: 0.5, duration: 0.6 }}
              className="text-center"
            >
              <h1 
                className="text-5xl md:text-6xl font-bold tracking-tight mb-2"
                style={{
                  background: 'linear-gradient(135deg, #ffffff 0%, #FF6B35 50%, #ff8c42 100%)',
                  WebkitBackgroundClip: 'text',
                  WebkitTextFillColor: 'transparent',
                  backgroundClip: 'text',
                }}
              >
                CA-O
              </h1>
              <motion.p
                initial={{ opacity: 0 }}
                animate={{ opacity: 0.7 }}
                transition={{ delay: 0.8 }}
                className="text-sm md:text-base text-gray-400 tracking-widest uppercase"
              >
                {t('appTagline', settings.language)}
              </motion.p>
            </motion.div>

            {/* Status Text with Typing Effect */}
            <motion.div
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              transition={{ delay: 0.9 }}
              className="mt-4"
            >
              <AnimatePresence mode="wait">
                <motion.p
                  key={statusText}
                  initial={{ opacity: 0, y: 10 }}
                  animate={{ opacity: 1, y: 0 }}
                  exit={{ opacity: 0, y: -10 }}
                  transition={{ duration: 0.3 }}
                  className={`text-lg font-medium ${
                    statusText === 'splashReady' ? 'text-green-400' : 'text-gray-300'
                  }`}
                >
                  {t(statusText, settings.language)}
                  {(statusText === 'splashLoading' || statusText === 'splashPreparing') && (
                    <motion.span
                      animate={{ opacity: [1, 0] }}
                      transition={{ duration: 0.5, repeat: Infinity }}
                      className="inline-block ml-1"
                    >
                      _
                    </motion.span>
                  )}
                </motion.p>
              </AnimatePresence>
            </motion.div>

            {/* Progress Bar */}
            <motion.div
              initial={{ width: 0, opacity: 0 }}
              animate={{ width: 280, opacity: 1 }}
              transition={{ delay: 1, duration: 0.5 }}
              className="mt-6"
            >
              <div className="relative">
                {/* Background Track */}
                <div 
                  className="w-full h-2 rounded-full overflow-hidden"
                  style={{
                    background: 'rgba(255, 255, 255, 0.08)',
                    backdropFilter: 'blur(10px)',
                  }}
                >
                  {/* Progress Fill */}
                  <motion.div
                    className="h-full rounded-full relative"
                    style={{
                      width: `${progress}%`,
                      background: 'linear-gradient(90deg, #FF6B35, #ff8c42, #ffa94d)',
                      boxShadow: '0 0 20px rgba(255, 107, 53, 0.5)',
                    }}
                    transition={{ duration: 0.3, ease: 'easeOut' }}
                  >
                    {/* Shimmer Effect */}
                    <motion.div
                      className="absolute inset-0"
                      style={{
                        background: 'linear-gradient(90deg, transparent, rgba(255,255,255,0.3), transparent)',
                      }}
                      animate={{ x: ['-100%', '200%'] }}
                      transition={{ duration: 1.5, repeat: Infinity, ease: 'easeInOut' }}
                    />
                  </motion.div>
                </div>

                {/* Percentage Indicator */}
                <motion.p
                  initial={{ opacity: 0 }}
                  animate={{ opacity: 1 }}
                  className="text-center mt-3 text-xs text-gray-500 font-mono"
                >
                  {progress}%
                </motion.p>
              </div>
            </motion.div>
          </div>

          {/* Bottom Version Info */}
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 0.4 }}
            transition={{ delay: 1.5 }}
            className="absolute bottom-8 text-xs text-gray-600 font-mono tracking-wider"
          >
            v0.2.1 • CA-O Team © 2026
          </motion.div>
        </motion.div>
      )}
    </AnimatePresence>
  );
}

export default SplashScreen;
