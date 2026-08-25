'use client';

import { useState, useCallback, useMemo, useSyncExternalStore, useEffect } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { useAppStore } from '@/store/useAppStore';
import {
  Palette,
  Sun,
  Moon,
  Monitor,
  Sparkles,
  Check,
  CheckCircle2,
  RotateCcw,
  Eye,
  Type,
  Layout,
  Grid3X3,
  Ruler as BorderWidth,
  Droplets,
  Paintbrush,
  Layers,
  Zap,
  Heart,
  Star,
  Crown,
  Gem,
  Flame,
  Snowflake,
  Leaf,
  Waves,
  Infinity,
  Target,
  ChevronRight,
  Save,
  Undo2,
} from 'lucide-react';

// Types
interface ThemePreset {
  id: string;
  name: string;
  nameEs: string;
  primaryColor: string;
  secondaryColor: string;
  accentColor: string;
  background: string;
  surface: string;
  icon: React.ReactNode;
}

interface FontOption {
  id: string;
  name: string;
  family: string;
}

interface BorderRadiusOption {
  id: string;
  name: string;
  value: string;
  preview: string;
}

interface ThemeCustomizationProps {
  className?: string;
}

// SSR-safe
const emptySubscribe = () => () => {};
const getSnapshot = () => true;
const getServerSnapshot = () => false;

export function ThemeCustomization({ className }: ThemeCustomizationProps) {
  const { settings, updateSettings } = useAppStore();
  const isMounted = useSyncExternalStore(emptySubscribe, getSnapshot, getServerSnapshot);
  
  const [activeTab, setActiveTab] = useState<'presets' | 'colors' | 'typography' | 'appearance'>('presets');
  const [selectedPreset, setSelectedPreset] = useState<string>('default-orange');
  const [customColors, setCustomColors] = useState({
    primary: '#FF6B35',
    secondary: '#ff8c42',
    accent: '#ffa94d',
  });
  const [selectedFont, setSelectedFont] = useState('system');
  const [borderRadius, setBorderRadius] = useState('medium');
  const [density, setDensity] = useState<'comfortable' | 'compact' | 'spacious'>('comfortable');
  const [showSavedToast, setShowSavedToast] = useState(false);
  
  // Theme presets
  const themePresets: ThemePreset[] = useMemo(() => [
    {
      id: 'default-orange',
      name: 'Sunset Orange',
      nameEs: 'Naranja Atardecer',
      primaryColor: '#FF6B35',
      secondaryColor: '#ff8c42',
      accentColor: '#ffa94d',
      background: '#0a0a14',
      surface: '#12121e',
      icon: <Flame className="w-5 h-5" />,
    },
    {
      id: 'ocean-blue',
      name: 'Ocean Blue',
      nameEs: 'Azul Océano',
      primaryColor: '#3B82F6',
      secondaryColor: '#60a5fa',
      accentColor: '#93c5fd',
      background: '#0a0f1a',
      surface: '#101828',
      icon: <Waves className="w-5 h-5" />,
    },
    {
      id: 'forest-green',
      name: 'Forest Green',
      nameEs: 'Verde Bosque',
      primaryColor: '#10B981',
      secondaryColor: '#34d399',
      accentColor: '#6ee7b7',
      background: '#0a120e',
      surface: '#121a16',
      icon: <Leaf className="w-5 h-5" />,
    },
    {
      id: 'royal-purple',
      name: 'Royal Purple',
      nameEs: 'Púrpura Real',
      primaryColor: '#A855F7',
      secondaryColor: '#c084fc',
      accentColor: '#d8b4fe',
      background: '#0f0a1a',
      surface: '#171227',
      icon: <Crown className="w-5 h-5" />,
    },
    {
      id: 'arctic-cyan',
      name: 'Arctic Cyan',
      nameEs: 'Cian Ártico',
      primaryColor: '#06b6d4',
      secondaryColor: '#22d3ee',
      accentColor: '#67e8f9',
      background: '#0a1418',
      surface: '#111e23',
      icon: <Snowflake className="w-5 h-5" />,
    },
    {
      id: 'rose-pink',
      name: 'Rose Pink',
      nameEs: 'Rosa Rosado',
      primaryColor: '#ec4899',
      secondaryColor: '#f472b6',
      accentColor: '#f9a8d4',
      background: '#140a10',
      surface: '#1c1218',
      icon: <Heart className="w-5 h-5" />,
    },
    {
      id: 'electric-yellow',
      name: 'Electric Yellow',
      nameEs: 'Amarillo Eléctrico',
      primaryColor: '#eab308',
      secondaryColor: '#facc15',
      accentColor: '#fde047',
      background: '#12100a',
      surface: '#1a1810',
      icon: <Zap className="w-5 h-5" />,
    },
    {
      id: 'ember-red',
      name: 'Ember Red',
      nameEs: 'Rojo Brasa',
      primaryColor: '#ef4444',
      secondaryColor: '#f87171',
      accentColor: '#fca5a5',
      background: '#140a0a',
      surface: '#1c1212',
      icon: <Gem className="w-5 h-5" />,
    },
  ], []);

  // Font options
  const fontOptions: FontOption[] = [
    { id: 'system', name: 'System Default', family: '-apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif' },
    { id: 'inter', name: 'Inter', family: '"Inter", sans-serif' },
    { id: 'roboto', name: 'Roboto', family: '"Roboto", sans-serif' },
    { id: 'poppins', name: 'Poppins', family: '"Poppins", sans-serif' },
    { id: 'jetbrains', name: 'JetBrains Mono', family: '"JetBrains Mono", monospace' },
    { id: 'space-grotesk', name: 'Space Grotesk', family: '"Space Grotesk", sans-serif' },
  ];

  // Border radius options
  const borderRadiusOptions: BorderRadiusOption[] = [
    { id: 'none', name: 'None', value: '0px', preview: '' },
    { id: 'small', name: 'Small', value: '4px', preview: 'rounded-sm' },
    { id: 'medium', name: 'Medium', value: '8px', preview: 'rounded-lg' },
    { id: 'large', name: 'Large', value: '16px', preview: 'rounded-xl' },
    { id: 'xlarge', name: 'X-Large', value: '24px', preview: 'rounded-2xl' },
    { id: 'pill', name: 'Pill', value: '9999px', preview: 'rounded-full' },
  ];

  useEffect(() => {
    const savedFont = localStorage.getItem('ca-o-custom-font');
    const savedRadius = localStorage.getItem('ca-o-border-radius');
    const savedDensity = localStorage.getItem('ca-o-density');
    const savedCustomColors = localStorage.getItem('ca-o-custom-colors');
    if (savedFont && fontOptions.some((option) => option.id === savedFont)) setSelectedFont(savedFont);
    if (savedRadius && borderRadiusOptions.some((option) => option.id === savedRadius)) setBorderRadius(savedRadius);
    if (savedDensity && ['comfortable', 'compact', 'spacious'].includes(savedDensity)) setDensity(savedDensity as typeof density);
    if (savedCustomColors) {
      try {
        const parsed = JSON.parse(savedCustomColors);
        if (parsed && typeof parsed.primary === 'string') {
          setCustomColors(parsed);
          document.documentElement.style.setProperty('--ca-o-primary', parsed.primary);
          document.documentElement.style.setProperty('--ca-o-secondary', parsed.secondary);
          document.documentElement.style.setProperty('--ca-o-accent', parsed.accent);
        }
      } catch { /* ignore malformed */ }
    } else {
      const savedPreset = localStorage.getItem('ca-o-theme-preset');
      if (savedPreset) {
        try {
          const preset = JSON.parse(savedPreset);
          if (preset?.primaryColor) {
            setCustomColors({ primary: preset.primaryColor, secondary: preset.secondaryColor, accent: preset.accentColor });
            document.documentElement.style.setProperty('--ca-o-primary', preset.primaryColor);
            document.documentElement.style.setProperty('--ca-o-secondary', preset.secondaryColor);
            document.documentElement.style.setProperty('--ca-o-accent', preset.accentColor);
          }
        } catch { /* ignore malformed */ }
      }
    }
  }, []);

  useEffect(() => {
    const font = fontOptions.find((option) => option.id === selectedFont)?.family;
    const radius = borderRadiusOptions.find((option) => option.id === borderRadius)?.value;
    if (font) document.documentElement.style.setProperty('--ca-o-font-family', font);
    if (radius) document.documentElement.style.setProperty('--ca-o-border-radius', radius);
    document.documentElement.dataset.caODensity = density;
  }, [selectedFont, borderRadius, density]);

  // Apply preset
  const applyPreset = useCallback((preset: ThemePreset) => {
    setSelectedPreset(preset.id);
    setCustomColors({
      primary: preset.primaryColor,
      secondary: preset.secondaryColor,
      accent: preset.accentColor,
    });
    
    // Apply CSS variables to document
    if (typeof window !== 'undefined') {
      document.documentElement.style.setProperty('--ca-o-primary', preset.primaryColor);
      document.documentElement.style.setProperty('--ca-o-secondary', preset.secondaryColor);
      document.documentElement.style.setProperty('--ca-o-accent', preset.accentColor);
      
      // Store preference
      localStorage.setItem('ca-o-theme-preset', JSON.stringify(preset));
    }
  }, [isMounted]);

  // Apply custom color
  const applyCustomColor = useCallback((colorType: keyof typeof customColors, color: string) => {
    setCustomColors(prev => {
      const next = { ...prev, [colorType]: color };
      if (typeof window !== 'undefined') {
        localStorage.setItem('ca-o-custom-colors', JSON.stringify(next));
        localStorage.removeItem('ca-o-theme-preset');
      }
      return next;
    });

    if (typeof window !== 'undefined') {
      document.documentElement.style.setProperty(`--ca-o-${colorType === 'primary' ? 'primary' : colorType === 'secondary' ? 'secondary' : 'accent'}`, color);
    }
  }, []);

  // Reset to defaults
  const resetToDefaults = useCallback(() => {
    const defaultPreset = themePresets.find(p => p.id === 'default-orange');
    if (defaultPreset) applyPreset(defaultPreset);
    setSelectedFont('system');
    setBorderRadius('medium');
    setDensity('comfortable');
    
    if (typeof window !== 'undefined') {
      localStorage.removeItem('ca-o-theme-preset');
      localStorage.removeItem('ca-o-custom-font');
    }
  }, [applyPreset, themePresets]);

  // Save preferences
  const savePreferences = useCallback(() => {
    if (typeof window !== 'undefined') {
      localStorage.setItem('ca-o-custom-font', selectedFont);
      localStorage.setItem('ca-o-border-radius', borderRadius);
      localStorage.setItem('ca-o-density', density);
    }
    
    setShowSavedToast(true);
    setTimeout(() => setShowSavedToast(false), 2000);
  }, [selectedFont, borderRadius, density]);

  // Tab configuration
  const tabs = [
    { id: 'presets' as const, label: settings.language === 'es' ? 'Temas' : 'Themes', icon: Palette },
    { id: 'colors' as const, label: settings.language === 'es' ? 'Colores' : 'Colors', icon: Droplets },
    { id: 'typography' as const, label: settings.language === 'es' ? 'Tipografía' : 'Typography', icon: Type },
    { id: 'appearance' as const, label: settings.language === 'es' ? 'Apariencia' : 'Appearance', icon: Layout },
  ];

  if (!isMounted) {
    return (
      <div className={`glass-premium rounded-2xl p-6 ${className || ''}`}>
        <div className="skeleton-modern h-80 w-full rounded-xl" />
      </div>
    );
  }

  return (
    <div className={`space-y-4 ${className || ''}`}>
      {/* Main Container */}
      <motion.div
        initial={{ opacity: 0, y: -10 }}
        animate={{ opacity: 1, y: 0 }}
        className="glass-ultra rounded-2xl overflow-hidden"
      >
        {/* Header */}
        <div className="p-5 border-b border-[rgba(255,255,255,0.06)]">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-3">
              <div className="w-11 h-11 rounded-xl bg-gradient-to-br from-purple-500/20 to-pink-500/20 flex items-center justify-center border border-purple-500/20">
                <Paintbrush className="w-5 h-5 text-purple-400" />
              </div>
              <div>
                <h3 className="font-bold text-white text-lg">
                  {settings.language === 'es' ? 'Personalización de Tema' : 'Theme Customization'}
                </h3>
                <p className="text-xs text-[rgba(255,255,255,0.4)]">
                  {settings.language === 'es'
                    ? 'Ajusta la apariencia a tu gusto'
                    : 'Customize the look and feel'}
                </p>
              </div>
            </div>

            <div className="flex items-center gap-2">
              <button
                onClick={resetToDefaults}
                className="p-2 rounded-lg hover:bg-[rgba(255,255,255,0.06)] transition-colors text-[rgba(255,255,255,0.4)] hover:text-[rgba(255,255,255,0.7)]"
                title={settings.language === 'es' ? 'Restaurar valores predeterminados' : 'Reset to defaults'}
              >
                <RotateCcw className="w-4 h-4" />
              </button>
              
              <button
                onClick={savePreferences}
                className="px-4 py-2 rounded-lg font-medium text-sm btn-neon flex items-center gap-1.5"
              >
                <Save className="w-4 h-4" />
                {settings.language === 'es' ? 'Guardar' : 'Save'}
              </button>
            </div>
          </div>
        </div>

        {/* Tabs */}
        <div className="px-5 pt-4 border-b border-[rgba(255,255,255,0.04)]">
          <div className="flex gap-1">
            {tabs.map(tab => (
              <button
                key={tab.id}
                onClick={() => setActiveTab(tab.id)}
                className={`flex items-center gap-2 px-4 py-2.5 rounded-lg text-xs font-medium transition-all press-depth ${
                  activeTab === tab.id
                    ? 'bg-white/10 text-white'
                    : 'text-[rgba(255,255,255,0.4)] hover:text-[rgba(255,255,255,0.7)] hover:bg-white/5'
                }`}
              >
                <tab.icon className="w-3.5 h-3.5" />
                {tab.label}
              </button>
            ))}
          </div>
        </div>

        {/* Tab Content */}
        <AnimatePresence mode="wait">
          <motion.div
            key={activeTab}
            initial={{ opacity: 0, y: 10 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0, y: -10 }}
            transition={{ duration: 0.2 }}
            className="p-5 space-y-5"
          >
            {/* Presets Tab */}
            {activeTab === 'presets' && (
              <div className="space-y-4">
                <div className="grid grid-cols-2 gap-3">
                  {themePresets.map(preset => (
                    <motion.button
                      key={preset.id}
                      whileHover={{ scale: 1.02 }}
                      whileTap={{ scale: 0.98 }}
                      onClick={() => applyPreset(preset)}
                      className={`relative p-4 rounded-xl text-left transition-all ${
                        selectedPreset === preset.id
                          ? 'ring-2 ring-offset-2 ring-offset-transparent ring-[#FF6B35]'
                          : 'hover:bg-white/5'
                      }`}
                    >
                      {/* Color Preview */}
                      <div 
                        className="h-12 rounded-lg mb-3 flex overflow-hidden"
                        style={{
                          background: `linear-gradient(135deg, ${preset.primaryColor}, ${preset.secondaryColor})`
                        }}
                      >
                        <div 
                          className="flex-1"
                          style={{ backgroundColor: preset.surface }}
                        />
                      </div>
                      
                      {/* Info */}
                      <div className="flex items-center gap-2">
                        <span className="text-white">
                          {preset.icon}
                        </span>
                        <div className="flex-1 min-w-0">
                          <p className="text-sm font-medium text-white truncate">
                            {settings.language === 'es' ? preset.nameEs : preset.name}
                          </p>
                          <p className="text-[10px] text-[rgba(255,255,255,0.3)]">
                            {preset.primaryColor}
                          </p>
                        </div>
                        
                        {selectedPreset === preset.id && (
                          <Check className="w-4 h-4 text-[#FF6B35]" />
                        )}
                        
                      </div>
                    </motion.button>
                  ))}
                </div>
                
                <p className="text-[10px] text-center text-[rgba(255,255,255,0.3)]">
                  {settings.language === 'es' ? 'Selecciona un tema predefinido o personaliza los colores manualmente en la pestaña "Colores"'
                    : 'Select a preset theme or customize colors manually in the "Colors" tab'}
                </p>
              </div>
            )}

            {/* Colors Tab */}
            {activeTab === 'colors' && (
              <div className="space-y-5">
                {[
                  { id: 'primary' as const, label: 'Primary Color', labelEs: 'Color Primario', description: 'Main action buttons and highlights' },
                  { id: 'secondary' as const, label: 'Secondary Color', labelEs: 'Color Secundario', description: 'Supporting elements and gradients' },
                  { id: 'accent' as const, label: 'Accent Color', labelEs: 'Color de Acento', description: 'Subtle details and decorations' },
                ].map(color => (
                  <div key={color.id} className="space-y-2">
                    <label className="text-xs font-medium text-[rgba(255,255,255,0.5)] uppercase tracking-wider">
                      {settings.language === 'es' ? color.labelEs : color.label}
                    </label>
                    
                    <div className="flex items-center gap-4">
                      {/* Color Input */}
                      <div className="relative">
                        <input
                          type="color"
                          value={customColors[color.id]}
                          onChange={(e) => applyCustomColor(color.id, e.target.value)}
                          className="w-14 h-14 rounded-xl cursor-pointer border-2 border-white/10"
                        />
                        <div 
                          className="absolute inset-0 rounded-xl pointer-events-none"
                          style={{ boxShadow: `inset 0 -8px 16px ${customColors[color.id]}30` }}
                        />
                      </div>
                      
                      {/* Hex Input */}
                      <input
                        type="text"
                        value={customColors[color.id]}
                        onChange={(e) => /^#[0-9A-Fa-f]{6}$/.test(e.target.value) && applyCustomColor(color.id, e.target.value)}
                        className="flex-1 px-4 py-3 rounded-xl bg-white/5 border border-white/10 text-white font-mono text-sm uppercase focus:outline-none focus:border-[#FF6B35]/50 transition-colors"
                        placeholder="#000000"
                      />
                      
                      {/* Preview Swatches */}
                      <div className="flex gap-1.5">
                        {[customColors.primary, customColors.secondary, customColors.accent].map((swatch, i) => (
                          <button
                            key={i}
                            onClick={() => applyCustomColor(color.id, swatch)}
                            className="w-7 h-7 rounded-md border border-white/10 hover:scale-110 transition-transform"
                            style={{ backgroundColor: swatch }}
                          />
                        ))}
                      </div>
                    </div>
                    
                    {/* Color Preview */}
                    <div className="flex items-center gap-3 p-3 rounded-lg bg-black/20">
                      <div 
                        className="w-10 h-10 rounded-lg"
                        style={{ backgroundColor: customColors[color.id] }}
                      />
                      <div className="flex-1">
                        <p className="text-xs text-white font-mono">{customColors[color.id]}</p>
                        <p className="text-[10px] text-[rgba(255,255,255,0.3)]">
                          {settings.language === 'es' ? color.description : color.description}
                        </p>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            )}

            {/* Typography Tab */}
            {activeTab === 'typography' && (
              <div className="space-y-5">
                {/* Font Selection */}
                <div className="space-y-2">
                  <label className="text-xs font-medium text-[rgba(255,255,255,0.5)] uppercase tracking-wider">
                    {settings.language === 'es' ? 'Fuente' : 'Font Family'}
                  </label>
                  
                  <div className="grid grid-cols-2 gap-2">
                    {fontOptions.map(font => (
                      <button
                        key={font.id}
                        onClick={() => { setSelectedFont(font.id); localStorage.setItem('ca-o-custom-font', font.id); }}
                        className={`p-3 rounded-xl text-left transition-all press-depth ${
                          selectedFont === font.id
                            ? 'bg-[#FF6B35]/10 border border-[#FF6B35]/25'
                            : 'bg-white/[0.03] border border-transparent hover:border-white/10'
                        }`}
                      >
                        <span 
                          className="block text-sm text-white mb-1"
                          style={{ fontFamily: font.family }}
                        >
                          {font.name}
                        </span>
                        <span 
                          className="text-[10px] text-[rgba(255,255,255,0.3)] block truncate"
                          style={{ fontFamily: font.family }}
                        >
                          AaBbCc 123
                        </span>
                        
                        {selectedFont === font.id && (
                          <Check className="w-4 h-4 text-[#FF6B35] mt-1" />
                        )}
                      </button>
                    ))}
                  </div>
                </div>

                {/* Size Preview */}
                <div className="space-y-2">
                  <label className="text-xs font-medium text-[rgba(255,255,255,0.5)] uppercase tracking-wider">
                    {settings.language === 'es' ? 'Vista Previa' : 'Preview'}
                  </label>
                  
                  <div className="p-4 rounded-xl glass-liquid space-y-2">
                    <p className="text-2xl font-bold text-white" style={{ fontFamily: fontOptions.find(f => f.id === selectedFont)?.family }}>
                      CA-O Optimización
                    </p>
                    <p className="text-base text-white/70" style={{ fontFamily: fontOptions.find(f => f.id === selectedFont)?.family }}>
                      {settings.language === 'es' 
                        ? 'La fuente seleccionada se aplicará a toda la interfaz de usuario.'
                        : 'The selected font will be applied throughout the user interface.'}
                    </p>
                    <p className="text-sm text-white/50 font-mono" style={{ fontFamily: fontOptions.find(f => f.id === selectedFont)?.family }}>
                      ABCDEFGHIJKLMNOPQRSTUVWXYZ abcdefghijklmnopqrstuvwxyz 1234567890
                    </p>
                  </div>
                </div>
              </div>
            )}

            {/* Appearance Tab */}
            {activeTab === 'appearance' && (
              <div className="space-y-5">
                {/* Border Radius */}
                <div className="space-y-2">
                  <label className="text-xs font-medium text-[rgba(255,255,255,0.5)] uppercase tracking-wider flex items-center gap-2">
                    <BorderWidth className="w-3.5 h-3.5" />
                    {settings.language === 'es' ? 'Radio de Borde' : 'Border Radius'}
                  </label>
                  
                  <div className="grid grid-cols-6 gap-2">
                    {borderRadiusOptions.map(option => (
                      <button
                        key={option.id}
                        onClick={() => { setBorderRadius(option.id); localStorage.setItem('ca-o-border-radius', option.id); }}
                        className={`flex flex-col items-center gap-1.5 p-2.5 rounded-lg transition-all press-depth ${
                          borderRadius === option.id
                            ? 'bg-[#FF6B35]/10 border border-[#FF6B35]/25'
                            : 'bg-white/[0.03] border border-transparent hover:border-white/10'
                        }`}
                      >
                        <div 
                          className={`w-6 h-6 ${option.preview} bg-gradient-to-br from-[#FF6B35] to-[#ff8c42]`}
                        />
                        <span className="text-[10px] text-[rgba(255,255,255,0.5)]">
                          {option.name}
                        </span>
                        
                        {borderRadius === option.id && (
                          <Check className="w-3 h-3 text-[#FF6B35]" />
                        )}
                      </button>
                    ))}
                  </div>
                </div>

                {/* Density */}
                <div className="space-y-2">
                  <label className="text-xs font-medium text-[rgba(255,255,255,0.5)] uppercase tracking-wider flex items-center gap-2">
                    <Grid3X3 className="w-3.5 h-3.5" />
                    {settings.language === 'es' ? 'Densidad' : 'Density'}
                  </label>
                  
                  <div className="grid grid-cols-3 gap-2">
                    {([
                      { id: 'compact', label: 'Compact', labelEs: 'Compacto' },
                      { id: 'comfortable', label: 'Comfortable', labelEs: 'Cómodo' },
                      { id: 'spacious', label: 'Spacious', labelEs: 'Espacioso' },
                    ] as const).map(option => (
                      <button
                        key={option.id}
                        onClick={() => { setDensity(option.id); localStorage.setItem('ca-o-density', option.id); }}
                        className={`p-3 rounded-xl text-center transition-all press-depth ${
                          density === option.id
                            ? 'bg-[#FF6B35]/10 border border-[#FF6B35]/25'
                            : 'bg-white/[0.03] border border-transparent hover:border-white/10'
                        }`}
                      >
                        <div className={`mx-auto w-8 h-8 rounded-lg mb-2 ${
                          option.id === 'compact' ? 'p-1.5' :
                          option.id === 'spacious' ? 'p-3.5' : 'p-2.5'
                        }`}>
                          <Layers className="w-full h-full text-[#FF6B35]" />
                        </div>
                        <span className={`text-xs font-medium ${
                          density === option.id ? 'text-white' : 'text-[rgba(255,255,255,0.5)]'
                        }`}>
                          {settings.language === 'es' ? option.labelEs : option.label}
                        </span>
                      </button>
                    ))}
                  </div>
                </div>

                {/* Quick Settings Row */}
                <div className="flex gap-3">
                  <button
                    onClick={() => updateSettings({ theme: settings.theme === 'dark' ? 'light' : 'dark' })}
                    className="flex-1 flex items-center justify-between p-3 rounded-xl glass-liquid hover-lift-glow cursor-pointer group"
                  >
                    <div className="flex items-center gap-2">
                      {settings.theme === 'dark' 
                        ? <Moon className="w-4 h-4 text-blue-400" /> 
                        : <Sun className="w-4 h-4 text-yellow-400" />}
                      <span className="text-xs text-white">
                        {settings.theme === 'dark' 
                          ? (settings.language === 'es' ? 'Modo Oscuro' : 'Dark Mode')
                          : (settings.language === 'es' ? 'Modo Claro' : 'Light Mode')}
                      </span>
                    </div>
                    <ChevronRight className="w-4 h-4 text-[rgba(255,255,255,0.3)] group-hover:translate-x-0.5 transition-transform" />
                  </button>
                </div>
              </div>
            )}
          </motion.div>
        </AnimatePresence>
      </motion.div>

      {/* Saved Toast */}
      <AnimatePresence>
        {showSavedToast && (
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0, y: 20 }}
            className="fixed bottom-24 left-1/2 -translate-x-1/2 px-6 py-3 rounded-xl glass-premium border border-green-500/30 flex items-center gap-2 z-50"
          >
            <CheckCircle2 className="w-5 h-5 text-green-400" />
            <span className="text-sm text-white font-medium">
              {settings.language === 'es' ? 'Preferencias guardadas!' : 'Preferences saved!'}
            </span>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}

export default ThemeCustomization;
