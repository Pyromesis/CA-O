'use client';

import { createContext, useContext, useCallback, useState, useEffect, ReactNode } from 'react';
import { useAppStore } from '@/store/useAppStore';
import { Volume2, VolumeX, Music } from 'lucide-react';
import { motion, AnimatePresence } from 'framer-motion';

// Sound effect types
type SoundType = 
  | 'click'        // UI click
  | 'toggle'       // Toggle switch
  | 'apply'        // Apply optimization (success)
  | 'revert'       // Revert optimization
  | 'error'        // Error sound
  | 'success'      // Success notification
  | 'notification' // Notification popup
  | 'hover'        // Hover feedback
  | 'screenshot'   // Screenshot/capture
  | 'delete'       // Delete action
  | 'refresh';     // Refresh action

// Sound context type
interface SoundContextType {
  playSound: (sound: SoundType) => void;
  isEnabled: boolean;
  setEnabled: (enabled: boolean) => void;
  volume: number;
  setVolume: (volume: number) => void;
}

const SoundContext = createContext<SoundContextType>({
  playSound: () => {},
  isEnabled: false,
  setEnabled: () => {},
  volume: 0.5,
  setVolume: () => {},
});

// Audio context cache
let audioContext: AudioContext | null = null;

const getAudioContext = (): AudioContext => {
  if (!audioContext) {
    audioContext = new (window.AudioContext || (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext)();
  }
  return audioContext;
};

// Sound configurations with Web Audio API synthesis
const soundConfigs: Record<SoundType, {
  frequency: number | number[];
  duration: number;
  type: OscillatorType;
  attack: number;
  decay: number;
  sustain: number;
  release: number;
}> = {
  click: {
    frequency: 800,
    duration: 0.08,
    type: 'sine',
    attack: 0.001,
    decay: 0.05,
    sustain: 0.1,
    release: 0.02,
  },
  toggle: {
    frequency: [600, 900],
    duration: 0.1,
    type: 'sine',
    attack: 0.001,
    decay: 0.06,
    sustain: 0.2,
    release: 0.03,
  },
  apply: {
    frequency: [523, 659, 784], // C5, E5, G5 chord
    duration: 0.3,
    type: 'sine',
    attack: 0.01,
    decay: 0.15,
    sustain: 0.3,
    release: 0.1,
  },
  revert: {
    frequency: [784, 659, 523], // Descending chord
    duration: 0.25,
    type: 'triangle',
    attack: 0.01,
    decay: 0.12,
    sustain: 0.2,
    release: 0.08,
  },
  error: {
    frequency: [200, 180],
    duration: 0.25,
    type: 'square',
    attack: 0.01,
    decay: 0.15,
    sustain: 0.3,
    release: 0.05,
  },
  success: {
    frequency: [523, 659, 784, 1047], // Ascending arpeggio
    duration: 0.4,
    type: 'sine',
    attack: 0.01,
    decay: 0.2,
    sustain: 0.4,
    release: 0.15,
  },
  notification: {
    frequency: [880, 1100],
    duration: 0.15,
    type: 'sine',
    attack: 0.005,
    decay: 0.08,
    sustain: 0.3,
    release: 0.05,
  },
  hover: {
    frequency: 1200,
    duration: 0.04,
    type: 'sine',
    attack: 0.001,
    decay: 0.03,
    sustain: 0.1,
    release: 0.005,
  },
  screenshot: {
    frequency: [400, 600, 800],
    duration: 0.15,
    type: 'sawtooth',
    attack: 0.001,
    decay: 0.1,
    sustain: 0.2,
    release: 0.04,
  },
  delete: {
    frequency: [300, 200],
    duration: 0.2,
    type: 'square',
    attack: 0.01,
    decay: 0.12,
    sustain: 0.15,
    release: 0.06,
  },
  refresh: {
    frequency: [500, 700, 900, 1100],
    duration: 0.25,
    type: 'sine',
    attack: 0.005,
    decay: 0.12,
    sustain: 0.3,
    release: 0.08,
  },
};

// Synthesize and play a sound using Web Audio API
function synthesizeSound(
  soundType: SoundType,
  volume: number = 0.5
): Promise<void> {
  return new Promise((resolve) => {
    try {
      const ctx = getAudioContext();
      const config = soundConfigs[soundType];
      
      // Create gain node for envelope
      const gainNode = ctx.createGain();
      gainNode.connect(ctx.destination);
      gainNode.gain.value = 0;
      
      const now = ctx.currentTime;
      const frequencies = Array.isArray(config.frequency) ? config.frequency : [config.frequency];
      
      frequencies.forEach((freq, index) => {
        // Create oscillator
        const osc = ctx.createOscillator();
        osc.type = config.type;
        osc.frequency.value = freq;
        
        osc.connect(gainNode);
        
        // Calculate timing for this note
        const noteStart = now + (index * config.duration * 0.35);
        const noteDuration = config.duration * 0.6;
        
        // Apply ADSR envelope
        gainNode.gain.setValueAtTime(0, noteStart);
        gainNode.gain.linearRampToValueAtTime(volume * 0.3, noteStart + config.attack);
        gainNode.gain.exponentialRampToValueAtTime(volume * 0.2 * config.sustain, noteStart + config.attack + config.decay);
        gainNode.gain.exponentialRampToValueAtTime(0.001, noteStart + noteDuration);
        
        osc.start(noteStart);
        osc.stop(noteStart + noteDuration + config.release);
      });
      
      // Resolve after total duration
      const totalTime = (config.duration * frequencies.length * 0.4) + config.release + 0.05;
      setTimeout(resolve, totalTime * 1000);
      
    } catch (error) {
      console.warn('Sound playback failed:', error);
      resolve();
    }
  });
}

// Hook to use sounds
export function useSounds() {
  return useContext(SoundContext);
}

// Individual sound trigger hooks
export function useClickSound() {
  const { playSound, isEnabled } = useSounds();
  return useCallback(() => { if (isEnabled) playSound('click'); }, [isEnabled, playSound]);
}

export function useToggleSound() {
  const { playSound, isEnabled } = useSounds();
  return useCallback(() => { if (isEnabled) playSound('toggle'); }, [isEnabled, playSound]);
}

export function useApplySound() {
  const { playSound, isEnabled } = useSounds();
  return useCallback(() => { if (isEnabled) playSound('apply'); }, [isEnabled, playSound]);
}

export function useRevertSound() {
  const { playSound, isEnabled } = useSounds();
  return useCallback(() => { if (isEnabled) playSound('revert'); }, [isEnabled, playSound]);
}

export function useSuccessSound() {
  const { playSound, isEnabled } = useSounds();
  return useCallback(() => { if (isEnabled) playSound('success'); }, [isEnabled, playSound]);
}

export function useErrorSound() {
  const { playSound, isEnabled } = useSounds();
  return useCallback(() => { if (isEnabled) playSound('error'); }, [isEnabled, playSound]);
}

interface SoundProviderProps {
  children: ReactNode;
}

export function SoundProvider({ children }: SoundProviderProps) {
  const { updateSettings } = useAppStore();
  
  // Use state for values that are rendered (not refs)
  // Lazy initialization from localStorage
  const [volume, setVolumeState] = useState(() => {
    if (typeof window !== 'undefined') {
      const savedVolume = localStorage.getItem('ca-o-sound-volume');
      return savedVolume ? parseFloat(savedVolume) : 0.5;
    }
    return 0.5;
  });
  
  const [isEnabled, setIsEnabled] = useState(() => {
    if (typeof window !== 'undefined') {
      const savedEnabled = localStorage.getItem('ca-o-sound-enabled');
      return savedEnabled === null ? true : savedEnabled === 'true';
    }
    return true;
  });

  const playSound = useCallback(async (soundType: SoundType) => {
    if (!isEnabled) return;
    
    await synthesizeSound(soundType, volume);
  }, [isEnabled, volume]);

  const setEnabled = useCallback((enabled: boolean) => {
    setIsEnabled(enabled);
    localStorage.setItem('ca-o-sound-enabled', String(enabled));
    updateSettings({ soundEffectsEnabled: enabled });
  }, [updateSettings]);

  const setVolume = useCallback((newVolume: number) => {
    const clampedVolume = Math.max(0, Math.min(1, newVolume));
    setVolumeState(clampedVolume);
    localStorage.setItem('ca-o-sound-volume', String(clampedVolume));
  }, []);

  return (
    <SoundContext.Provider value={{
      playSound,
      isEnabled,
      setEnabled,
      volume,
      setVolume,
    }}>
      {children}
    </SoundContext.Provider>
  );
}

// Sound Settings Panel Component
interface SoundSettingsProps {
  language?: 'es' | 'en';
}

export function SoundSettings({ language }: SoundSettingsProps) {
  const { isEnabled, setEnabled, volume, setVolume, playSound } = useSounds();

  const handleTestSound = useCallback(() => {
    playSound('success');
  }, [playSound]);

  return (
    <div className="space-y-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <div className={`w-10 h-10 rounded-xl flex items-center justify-center ${isEnabled ? 'bg-[rgba(255,107,53,0.15)]' : 'bg-[rgba(255,255,255,0.05)]'}`}>
            {isEnabled ? (
              <Volume2 className="w-5 h-5 text-[#FF6B35]" />
            ) : (
              <VolumeX className="w-5 h-5 text-[rgba(255,255,255,0.4)]" />
            )}
          </div>
          <div>
            <h3 className="font-semibold text-white text-sm">
              {language === 'en' ? 'Sound Effects' : 'Efectos de Sonido'}
            </h3>
            <p className="text-xs text-[rgba(255,255,255,0.5)]">
              {language === 'en' ? 'Audio feedback for actions' : 'Retroalimentación de audio'}
            </p>
          </div>
        </div>

        {/* Toggle */}
        <button
          onClick={() => setEnabled(!isEnabled)}
          className={`toggle-premium ${isEnabled ? 'active' : ''}`}
        >
          <span className="toggle-premium-knob" />
        </button>
      </div>

      {/* Volume Slider - Only show when enabled */}
      <AnimatePresence>
        {isEnabled && (
          <motion.div
            initial={{ opacity: 0, height: 0 }}
            animate={{ opacity: 1, height: 'auto' }}
            exit={{ opacity: 0, height: 0 }}
            transition={{ duration: 0.2 }}
            className="overflow-hidden"
          >
            <div className="pt-3 space-y-3">
              <div className="flex items-center gap-3">
                <Music className="w-4 h-4 text-[rgba(255,255,255,0.5)]" />
                <span className="text-sm text-[rgba(255,255,255,0.7)]">
                  {language === 'en' ? 'Volume' : 'Volumen'}
                </span>
                <span className="ml-auto text-xs text-[#FF6B35] font-medium">
                  {Math.round(volume * 100)}%
                </span>
              </div>
              
              <input
                type="range"
                min="0"
                max="100"
                value={Math.round(volume * 100)}
                onChange={(e) => setVolume(parseInt(e.target.value) / 100)}
                className="w-full h-2 rounded-full appearance-none cursor-pointer"
                style={{
                  background: `linear-gradient(to right, #FF6B35 0%, #FF6B35 ${volume * 100}%, rgba(255,255,255,0.1) ${volume * 100}%, rgba(255,255,255,0.1) 100%)`,
                }}
              />

              {/* Test Button */}
              <button
                onClick={handleTestSound}
                className="btn-ghost w-full text-xs"
              >
                <Volume2 className="w-3 h-3" />
                {language === 'en' ? 'Test Sound' : 'Probar Sonido'}
              </button>
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}

export default SoundProvider;
