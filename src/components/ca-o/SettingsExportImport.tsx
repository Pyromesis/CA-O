'use client';

import { useState, useCallback } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { useAppStore } from '@/store/useAppStore';
import { t } from '@/lib/i18n';
import {
  Download,
  Upload,
  FileJson,
  CheckCircle2,
  XCircle,
  AlertTriangle,
  Copy,
  ClipboardCheck,
  Trash2,
  FileUp,
  FileDown,
  Shield,
  Settings,
  History,
  Zap,
} from 'lucide-react';
import { cn } from '@/lib/utils';

interface SettingsExportImportProps {
  className?: string;
}

// Export data structure
export interface ExportData {
  version: string;
  exportDate: string;
  settings: {
    theme: string;
    language: string;
    autoApplySafeTweaks: boolean;
    confirmBeforeApply: boolean;
    showNotifications: boolean;
  };
  optimizations: Array<{
    id: string;
    isEnabled: boolean;
    isApplied: boolean;
  }>;
  history: Array<{
    id: string;
    nameKey: string;
    action: string;
    timestamp: number;
  }>;
}

export function SettingsExportImport({ className }: SettingsExportImportProps) {
  const { settings, optimizations, history, updateSettings, revertAll, applyOptimization, clearHistory } = useAppStore();
  
  const [exportStatus, setExportStatus] = useState<'idle' | 'success' | 'error'>('idle');
  const [importStatus, setImportStatus] = useState<'idle' | 'success' | 'error' | 'confirming'>('idle');
  const [importError, setImportError] = useState<string>('');
  const [showPreview, setShowPreview] = useState(false);
  const [previewData, setPreviewData] = useState<ExportData | null>(null);
  const [copiedStatus, setCopiedStatus] = useState<'idle' | 'copied'>('idle');

  // Generate export data
  const generateExportData = useCallback((): ExportData => {
    return {
      version: '1.0.0',
      exportDate: new Date().toISOString(),
      settings: { ...settings },
      optimizations: optimizations.map(opt => ({
        id: opt.id,
        isEnabled: opt.isEnabled,
        isApplied: opt.isApplied,
      })),
      history: history.slice(0, 50), // Limit history in export
    };
  }, [settings, optimizations, history]);

  // Export to JSON file
  const handleExportFile = useCallback(() => {
    try {
      const data = generateExportData();
      const jsonString = JSON.stringify(data, null, 2);
      const blob = new Blob([jsonString], { type: 'application/json' });
      const url = URL.createObjectURL(blob);
      
      const link = document.createElement('a');
      link.href = url;
      link.download = `ca-o-settings-${new Date().toISOString().split('T')[0]}.json`;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      URL.revokeObjectURL(url);
      
      setExportStatus('success');
      setTimeout(() => setExportStatus('idle'), 3000);
    } catch (error) {
      console.error('Export error:', error);
      setExportStatus('error');
      setTimeout(() => setExportStatus('idle'), 3000);
    }
  }, [generateExportData]);

  // Export to clipboard
  const handleExportClipboard = useCallback(async () => {
    try {
      const data = generateExportData();
      const jsonString = JSON.stringify(data, null, 2);
      
      await navigator.clipboard.writeText(jsonString);
      setCopiedStatus('copied');
      setTimeout(() => setCopiedStatus('idle'), 3000);
    } catch (error) {
      console.error('Clipboard error:', error);
    }
  }, [generateExportData]);

  // Handle file import
  const handleFileImport = useCallback((event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (!file) return;

    const reader = new FileReader();
    reader.onload = (e) => {
      try {
        const content = e.target?.result as string;
        const data: ExportData = JSON.parse(content);
        
        // Validate structure
        if (!data.version || !data.settings || !Array.isArray(data.optimizations)) {
          throw new Error('Invalid file format');
        }
        
        setPreviewData(data);
        setShowPreview(true);
        setImportStatus('confirming');
      } catch (error) {
        setImportError(settings.language === 'es' 
          ? 'Archivo inválido. Por favor verifica el formato JSON.'
          : 'Invalid file. Please check the JSON format.');
        setImportStatus('error');
        setTimeout(() => setImportStatus('idle'), 4000);
      }
    };
    
    reader.readAsText(file);
    // Reset input
    event.target.value = '';
  }, [settings.language]);

  // Confirm import
  const handleConfirmImport = useCallback(async () => {
    if (!previewData) return;

    try {
      // Apply settings
      updateSettings(previewData.settings as any);
      
      // Revert all current optimizations first
      await revertAll();
      
      // Apply imported optimization states
      for (const optData of previewData.optimizations) {
        if (optData.isApplied) {
          await applyOptimization(optData.id);
          await new Promise(resolve => setTimeout(resolve, 50));
        }
      }
      
      setImportStatus('success');
      setShowPreview(false);
      setPreviewData(null);
      setTimeout(() => setImportStatus('idle'), 3000);
    } catch (error) {
      console.error('Import error:', error);
      setImportError(settings.language === 'es'
        ? 'Error al importar configuración'
        : 'Error importing configuration');
      setImportStatus('error');
      setTimeout(() => setImportStatus('idle'), 4000);
    }
  }, [previewData, updateSettings, revertAll, applyOptimization, settings.language]);

  // Cancel import
  const handleCancelImport = () => {
    setShowPreview(false);
    setPreviewData(null);
    setImportStatus('idle');
  };

  const isDark = settings.theme === 'dark';

  return (
    <div className={cn('space-y-6', className)}>
      {/* Header */}
      <motion.div
        initial={{ opacity: 0, y: -10 }}
        animate={{ opacity: 1, y: 0 }}
        className="flex items-center gap-3 mb-6"
      >
        <div 
          className="w-10 h-10 rounded-xl flex items-center justify-center"
          style={{ background: 'rgba(255, 107, 53, 0.15)' }}
        >
          <FileJson size={20} className="text-[#FF6B35]" />
        </div>
        <div>
          <h3 
            className="text-lg font-semibold"
            style={{ color: isDark ? '#fff' : '#1a1a2e' }}
          >
            {t('exportImportTitle', settings.language)}
          </h3>
          <p 
            className="text-xs"
            style={{ color: isDark ? 'rgba(255,255,255,0.5)' : 'rgba(0,0,0,0.5)' }}
          >
            {t('exportImportDesc', settings.language)}
          </p>
        </div>
      </motion.div>

      {/* Export Section */}
      <div 
        className="rounded-2xl p-5 space-y-4"
        style={{
          background: isDark ? 'rgba(30, 30, 50, 0.6)' : 'rgba(255, 255, 255, 0.8)',
          border: `1px solid ${isDark ? 'rgba(255, 255, 255, 0.08)' : 'rgba(0, 0, 0, 0.08)'}`,
        }}
      >
        <div className="flex items-center gap-2 mb-4">
          <FileDown size={18} style={{ color: '#10B981' }} />
          <span 
            className="font-medium text-sm"
            style={{ color: isDark ? '#fff' : '#1a1a2e' }}
          >
            {t('exportSettings', settings.language)}
          </span>
        </div>

        <p 
          className="text-sm mb-4"
          style={{ color: isDark ? 'rgba(255,255,255,0.5)' : 'rgba(0,0,0,0.5)' }}
        >
          {t('exportSettingsDesc', settings.language)}
        </p>

        <div className="flex flex-wrap gap-3">
          {/* Export to File */}
          <motion.button
            onClick={handleExportFile}
            whileHover={{ scale: 1.02 }}
            whileTap={{ scale: 0.98 }}
            className="flex items-center gap-2 px-4 py-2.5 rounded-xl text-sm font-medium text-white transition-all"
            style={{
              background: 'linear-gradient(135deg, #10B981, #059669)',
              boxShadow: '0 4px 15px rgba(16, 185, 129, 0.25)',
            }}
          >
            <Download size={16} />
            {t('downloadJson', settings.language)}
            
            {exportStatus === 'success' && (
              <motion.span
                initial={{ scale: 0 }}
                animate={{ scale: 1 }}
                className="ml-1"
              >
                <CheckCircle2 size={14} />
              </motion.span>
            )}
          </motion.button>

          {/* Export to Clipboard */}
          <motion.button
            onClick={handleExportClipboard}
            whileHover={{ scale: 1.02 }}
            whileTap={{ scale: 0.98 }}
            className="flex items-center gap-2 px-4 py-2.5 rounded-xl text-sm font-medium transition-all"
            style={{
              background: isDark ? 'rgba(255, 255, 255, 0.08)' : 'rgba(0, 0, 0, 0.05)',
              border: `1px solid ${isDark ? 'rgba(255, 255, 255, 0.1)' : 'rgba(0, 0, 0, 0.1)'}`,
              color: isDark ? 'rgba(255,255,255,0.8)' : 'rgba(0,0,0,0.8)',
            }}
          >
            {copiedStatus === 'copied' ? (
              <>
                <ClipboardCheck size={16} className="text-emerald-500" />
                <span className="text-emerald-500">{t('copied', settings.language)}</span>
              </>
            ) : (
              <>
                <Copy size={16} />
                {t('copyToClipboard', settings.language)}
              </>
            )}
          </motion.button>
        </div>
        
        {/* Error state */}
        <AnimatePresence>
          {exportStatus === 'error' && (
            <motion.div
              initial={{ opacity: 0, height: 0 }}
              animate={{ opacity: 1, height: 'auto' }}
              exit={{ opacity: 0, height: 0 }}
              className="flex items-center gap-2 px-3 py-2 rounded-lg text-sm"
              style={{ background: 'rgba(239, 68, 68, 0.15)', color: '#EF4444' }}
            >
              <XCircle size={16} />
              <span>{settings.language === 'es' ? 'Error al exportar' : 'Export failed'}</span>
            </motion.div>
          )}
        </AnimatePresence>
      </div>

      {/* Import Section */}
      <div 
        className="rounded-2xl p-5 space-y-4"
        style={{
          background: isDark ? 'rgba(30, 30, 50, 0.6)' : 'rgba(255, 255, 255, 0.8)',
          border: `1px solid ${isDark ? 'rgba(255, 255, 255, 0.08)' : 'rgba(0, 0, 0, 0.08)'}`,
        }}
      >
        <div className="flex items-center gap-2 mb-4">
          <FileUp size={18} style={{ color: '#3B82F6' }} />
          <span 
            className="font-medium text-sm"
            style={{ color: isDark ? '#fff' : '#1a1a2e' }}
          >
            {t('importSettings', settings.language)}
          </span>
        </div>

        <p 
          className="text-sm mb-4"
          style={{ color: isDark ? 'rgba(255,255,255,0.5)' : 'rgba(0,0,0,0.5)' }}
        >
          {t('importSettingsDesc', settings.language)}
        </p>

        {/* File Upload Area */}
        <label 
          className="flex flex-col items-center justify-center w-full h-32 rounded-xl cursor-pointer transition-all hover:border-[#FF6B35]/50"
          style={{
            background: isDark ? 'rgba(255, 255, 255, 0.03)' : 'rgba(0, 0, 0, 0.03)',
            border: `2px dashed ${isDark ? 'rgba(255, 255, 255, 0.15)' : 'rgba(0, 0, 0, 0.15)'}`,
          }}
        >
          <input
            type="file"
            accept=".json"
            onChange={handleFileImport}
            className="hidden"
          />
          
          <Upload size={28} className="mb-2" style={{ color: isDark ? 'rgba(255,255,255,0.4)' : 'rgba(0,0,0,0.4)' }} />
          
          <p 
            className="text-sm font-medium"
            style={{ color: isDark ? 'rgba(255,255,255,0.7)' : 'rgba(0,0,0,0.7)' }}
          >
            {settings.language === 'es' ? 'Haz clic para seleccionar archivo' : 'Click to select file'}
          </p>
          
          <p 
            className="text-xs mt-1"
            style={{ color: isDark ? 'rgba(255,255,255,0.4)' : 'rgba(0,0,0,0.4)' }}
          >
            .JSON
          </p>
        </label>

        {/* Import Status Messages */}
        <AnimatePresence>
          {importStatus === 'error' && (
            <motion.div
              initial={{ opacity: 0, x: -10 }}
              animate={{ opacity: 1, x: 0 }}
              exit={{ opacity: 0, x: -10 }}
              className="flex items-center gap-2 px-3 py-2 rounded-lg text-sm"
              style={{ background: 'rgba(239, 68, 68, 0.15)', color: '#EF4444' }}
            >
              <XCircle size={16} />
              <span>{importError}</span>
            </motion.div>
          )}
          
          {importStatus === 'success' && (
            <motion.div
              initial={{ opacity: 0, scale: 0.95 }}
              animate={{ opacity: 1, scale: 1 }}
              className="flex items-center gap-2 px-3 py-2 rounded-lg text-sm"
              style={{ background: 'rgba(16, 185, 129, 0.15)', color: '#10B981' }}
            >
              <CheckCircle2 size={16} />
              <span>{settings.language === 'es' ? '¡Configuración importada correctamente!' : 'Settings imported successfully!'}</span>
            </motion.div>
          )}
        </AnimatePresence>
      </div>

      {/* Import Preview Modal */}
      <AnimatePresence>
        {showPreview && previewData && (
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="fixed inset-0 z-50 flex items-center justify-center p-4"
            style={{ background: 'rgba(0, 0, 0, 0.7)', backdropFilter: 'blur(8px)' }}
            onClick={handleCancelImport}
          >
            <motion.div
              initial={{ scale: 0.9, opacity: 0 }}
              animate={{ scale: 1, opacity: 1 }}
              exit={{ scale: 0.9, opacity: 0 }}
              onClick={(e) => e.stopPropagation()}
              className="w-full max-w-md rounded-2xl p-6 max-h-[80vh] overflow-y-auto"
              style={{
                background: isDark ? '#1a1a2e' : '#fff',
                border: `1px solid ${isDark ? 'rgba(255, 255, 255, 0.1)' : 'rgba(0, 0, 0, 0.1)'}`,
              }}
            >
              {/* Header */}
              <div className="flex items-center gap-3 mb-4">
                <div 
                  className="w-10 h-10 rounded-xl flex items-center justify-center"
                  style={{ background: 'rgba(59, 130, 246, 0.15)' }}
                >
                  <Shield size={20} className="text-blue-500" />
                </div>
                <div>
                  <h3 
                    className="font-semibold"
                    style={{ color: isDark ? '#fff' : '#1a1a2e' }}
                  >
                    {t('confirmImport', settings.language)}
                  </h3>
                  <p 
                    className="text-xs"
                    style={{ color: isDark ? 'rgba(255,255,255,0.5)' : 'rgba(0,0,0,0.5)' }}
                  >
                    {t('reviewImportData', settings.language)}
                  </p>
                </div>
              </div>

              {/* Preview Content */}
              <div className="space-y-4 mb-6">
                {/* Settings Summary */}
                <div 
                  className="rounded-xl p-4"
                  style={{ background: isDark ? 'rgba(255, 255, 255, 0.05)' : 'rgba(0, 0, 0, 0.03)' }}
                >
                  <div className="flex items-center gap-2 mb-3">
                    <Settings size={14} style={{ color: '#FF6B35' }} />
                    <span className="text-xs font-medium uppercase tracking-wider" style={{ color: isDark ? 'rgba(255,255,255,0.5)' : 'rgba(0,0,0,0.5)' }}>
                      {t('settings', settings.language)}
                    </span>
                  </div>
                  
                  <div className="grid grid-cols-2 gap-2 text-sm">
                    <div className="flex items-center gap-2">
                      <span style={{ color: isDark ? 'rgba(255,255,255,0.5)' : 'rgba(0,0,0,0.5)' }}>Theme:</span>
                      <span className="font-medium capitalize" style={{ color: isDark ? '#fff' : '#1a1a2e' }}>{previewData.settings.theme}</span>
                    </div>
                    <div className="flex items-center gap-2">
                      <span style={{ color: isDark ? 'rgba(255,255,255,0.5)' : 'rgba(0,0,0,0.5)' }}>Language:</span>
                      <span className="font-medium uppercase" style={{ color: isDark ? '#fff' : '#1a1a2e' }}>{previewData.settings.language}</span>
                    </div>
                    <div className="flex items-center gap-2">
                      <span style={{ color: isDark ? 'rgba(255,255,255,0.5)' : 'rgba(0,0,0,0.5)' }}>Auto-apply:</span>
                      <span className="font-medium" style={{ color: previewData.settings.autoApplySafeTweaks ? '#10B981' : '#EF4444' }}>
                        {previewData.settings.autoApplySafeTweaks ? 'ON' : 'OFF'}
                      </span>
                    </div>
                    <div className="flex items-center gap-2">
                      <span style={{ color: isDark ? 'rgba(255,255,255,0.5)' : 'rgba(0,0,0,0.5)' }}>Confirm:</span>
                      <span className="font-medium" style={{ color: previewData.settings.confirmBeforeApply ? '#10B981' : '#EF4444' }}>
                        {previewData.settings.confirmBeforeApply ? 'ON' : 'OFF'}
                      </span>
                    </div>
                  </div>
                </div>

                {/* Optimizations Summary */}
                <div 
                  className="rounded-xl p-4"
                  style={{ background: isDark ? 'rgba(255, 255, 255, 0.05)' : 'rgba(0, 0, 0, 0.03)' }}
                >
                  <div className="flex items-center gap-2 mb-3">
                    <Zap size={14} style={{ color: '#FF6B35' }} />
                    <span className="text-xs font-medium uppercase tracking-wider" style={{ color: isDark ? 'rgba(255,255,255,0.5)' : 'rgba(0,0,0,0.5)' }}>
                      {t('optimization', settings.language)}
                    </span>
                  </div>
                  
                  <div className="grid grid-cols-2 gap-2 text-sm">
                    <div className="flex items-center gap-2">
                      <span style={{ color: isDark ? 'rgba(255,255,255,0.5)' : 'rgba(0,0,0,0.5)' }}>Total:</span>
                      <span className="font-medium" style={{ color: isDark ? '#fff' : '#1a1a2e' }}>{previewData.optimizations.length}</span>
                    </div>
                    <div className="flex items-center gap-2">
                      <span style={{ color: isDark ? 'rgba(255,255,255,0.5)' : 'rgba(0,0,0,0.5)' }}>Applied:</span>
                      <span className="font-medium text-emerald-500">{previewData.optimizations.filter(o => o.isApplied).length}</span>
                    </div>
                  </div>
                  
                  {/* Progress Bar */}
                  <div className="mt-3 h-2 rounded-full overflow-hidden" style={{ background: isDark ? 'rgba(255, 255, 255, 0.1)' : 'rgba(0, 0, 0, 0.1)' }}>
                    <motion.div
                      initial={{ width: 0 }}
                      animate={{ width: `${(previewData.optimizations.filter(o => o.isApplied).length / previewData.optimizations.length) * 100}%` }}
                      transition={{ duration: 0.5 }}
                      className="h-full rounded-full"
                      style={{ background: 'linear-gradient(90deg, #10B981, #34D399)' }}
                    />
                  </div>
                </div>

                {/* Warning */}
                <div 
                  className="flex items-start gap-2 p-3 rounded-xl"
                  style={{ background: 'rgba(245, 158, 11, 0.1)' }}
                >
                  <AlertTriangle size={16} className="shrink-0 mt-0.5" style={{ color: '#F59E0B' }} />
                  <p className="text-xs" style={{ color: '#F59E0B' }}>
                    {t('importWarning', settings.language)}
                  </p>
                </div>
              </div>

              {/* Actions */}
              <div className="flex gap-3">
                <motion.button
                  onClick={handleCancelImport}
                  whileHover={{ scale: 1.02 }}
                  whileTap={{ scale: 0.98 }}
                  className="flex-1 py-2.5 rounded-xl text-sm font-medium"
                  style={{
                    background: isDark ? 'rgba(255, 255, 255, 0.08)' : 'rgba(0, 0, 0, 0.05)',
                    color: isDark ? 'rgba(255,255,255,0.8)' : 'rgba(0,0,0,0.8)',
                  }}
                >
                  {t('cancel', settings.language)}
                </motion.button>
                
                <motion.button
                  onClick={handleConfirmImport}
                  whileHover={{ scale: 1.02 }}
                  whileTap={{ scale: 0.98 }}
                  className="flex-1 py-2.5 rounded-xl text-sm font-semibold text-white"
                  style={{
                    background: 'linear-gradient(135deg, #3B82F6, #2563EB)',
                    boxShadow: '0 4px 15px rgba(59, 130, 246, 0.3)',
                  }}
                >
                  <div className="flex items-center justify-center gap-2">
                    <Upload size={16} />
                    {t('confirmImportAction', settings.language)}
                  </div>
                </motion.button>
              </div>
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>

      {/* Advanced Options */}
      <div 
        className="rounded-2xl p-5"
        style={{
          background: isDark ? 'rgba(30, 30, 50, 0.6)' : 'rgba(255, 255, 255, 0.8)',
          border: `1px solid ${isDark ? 'rgba(255, 255, 255, 0.08)' : 'rgba(0, 0, 0, 0.08)'}`,
        }}
      >
        <div className="flex items-center gap-2 mb-4">
          <AlertTriangle size={18} style={{ color: '#F59E0B' }} />
          <span 
            className="font-medium text-sm"
            style={{ color: isDark ? '#fff' : '#1a1a2e' }}
          >
            {settings.language === 'es' ? 'Opciones Avanzadas' : 'Advanced Options'}
          </span>
        </div>

        <div className="flex flex-wrap gap-3">
          {/* Clear All Data */}
          <motion.button
            onClick={() => {
              if (confirm(settings.language === 'es' 
                ? '¿Estás seguro? Esto eliminará toda la configuración.' 
                : 'Are you sure? This will erase all settings.')) {
                localStorage.removeItem('ca-o-storage');
                window.location.reload();
              }
            }}
            whileHover={{ scale: 1.02 }}
            whileTap={{ scale: 0.98 }}
            className="flex items-center gap-2 px-4 py-2.5 rounded-xl text-sm font-medium transition-all"
            style={{
              background: 'rgba(239, 68, 68, 0.1)',
              border: '1px solid rgba(239, 68, 68, 0.2)',
              color: '#EF4444',
            }}
          >
            <Trash2 size={16} />
            {settings.language === 'es' ? 'Restablecer Todo' : 'Reset Everything'}
          </motion.button>
        </div>
      </div>
    </div>
  );
}

export default SettingsExportImport;
