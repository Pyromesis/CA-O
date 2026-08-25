'use client';

import { useState, useCallback, useMemo, useSyncExternalStore, useRef } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { useAppStore } from '@/store/useAppStore';
import { t } from '@/lib/i18n';
import {
  FileDown,
  FileText,
  Image,
  BarChart3,
  PieChart,
  Download,
  Printer,
  Share2,
  Calendar,
  Clock,
  Filter,
  CheckCircle2,
  XCircle,
  AlertCircle,
  Loader2,
  ChevronRight,
  ChevronDown,
  Settings2,
  Zap,
  Shield,
  Activity,
  TrendingUp,
  Users,
  Cpu,
  MemoryStick,
  HardDrive,
  Wifi,
  Globe,
  Palette,
  FileJson,
  FileSpreadsheet,
  FileImage,
  Maximize2,
  Minimize2,
  Eye,
} from 'lucide-react';

// Types
interface ExportOptions {
  format: 'pdf' | 'csv' | 'json' | 'png' | 'html';
  dateRange: 'day' | 'week' | 'month' | 'all' | 'custom';
  includeCharts: boolean;
  includeRawData: boolean;
  includeMetadata: boolean;
  sections: ExportSection[];
}

type ExportSection = 
  | 'overview'
  | 'optimizations'
  | 'history'
  | 'statistics'
  | 'performance'
  | 'achievements';

interface ReportPreview {
  title: string;
  subtitle: string;
  generatedAt: Date;
  data: {
    totalOptimizations: number;
    appliedCount: number;
    pendingCount: number;
    successRate: number;
    sessionTime: string;
    topCategory: string;
    achievementsUnlocked: number;
  };
}

interface AdvancedExportProps {
  className?: string;
}

// SSR-safe
const emptySubscribe = () => () => {};
const getSnapshot = () => true;
const getServerSnapshot = () => false;

export function AdvancedExport({ className }: AdvancedExportProps) {
  const { settings, optimizations, history } = useAppStore();
  const isMounted = useSyncExternalStore(emptySubscribe, getSnapshot, getServerSnapshot);
  
  const [options, setOptions] = useState<ExportOptions>({
    format: 'pdf',
    dateRange: 'week',
    includeCharts: true,
    includeRawData: false,
    includeMetadata: true,
    sections: ['overview', 'optimizations', 'statistics'],
  });

  const [isExpanded, setIsExpanded] = useState(true);
  const [isGenerating, setIsGenerating] = useState(false);
  const [showPreview, setShowPreview] = useState(false);
  const [previewData, setPreviewData] = useState<ReportPreview | null>(null);
  const [exportHistory, setExportHistory] = useState<Array<{
    id: string;
    format: string;
    timestamp: number;
    size: string;
  }>>(() => {
    if (typeof window !== 'undefined') {
      const saved = localStorage.getItem('ca-o-export-history');
      return saved ? JSON.parse(saved) : [];
    }
    return [];
  });

  // Generate preview data
  const generatePreview = useCallback((): ReportPreview => {
    const appliedCount = optimizations.filter(o => o.isApplied).length;
    const pendingCount = optimizations.length - appliedCount;
    
    return {
      title: settings.language === 'es' ? 'Informe de Optimización CA-O' : 'CA-O Optimization Report',
      subtitle: new Date().toLocaleDateString(settings.language === 'es' ? 'es-ES' : 'en-US', {
        year: 'numeric', month: 'long', day: 'numeric'
      }),
      generatedAt: new Date(),
      data: {
        totalOptimizations: optimizations.length,
        appliedCount,
        pendingCount,
        successRate: Math.round((appliedCount / optimizations.length) * 100) || 0,
        sessionTime: `${Math.floor(history.length * 0.5)}min`,
        topCategory: settings.language === 'es' ? 'Sistema' : 'System',
        achievementsUnlocked: parseInt(localStorage.getItem('ca-o-unlocked-achievements') || '0') || 0,
      },
    };
  }, [optimizations, history, settings.language]);

  // Handle export generation
  const handleGenerateReport = useCallback(async () => {
    setIsGenerating(true);
    const preview = generatePreview();
    setPreviewData(preview);
    setShowPreview(true);
    setIsGenerating(false);

    // Only local report generation is supported at this time.
    const newEntry = {
      id: `export-${Date.now()}`,
      format: options.format.toUpperCase(),
      timestamp: Date.now(),
      size: 'pending download',
    };

    setExportHistory(prev => [newEntry, ...prev].slice(0, 10));
    if (typeof window !== 'undefined') {
      localStorage.setItem('ca-o-export-history', JSON.stringify([newEntry, ...exportHistory].slice(0, 10)));
    }
  }, [generatePreview, options.format, exportHistory]);

  const handleDownload = useCallback((format: string) => {
    if (format !== 'json' && format !== 'csv') return;

    let content = '';
    let mimeType = '';
    let extension = '';

    switch (format) {
      case 'json':
        content = JSON.stringify({
          report: previewData,
          generatedAt: new Date().toISOString(),
          version: '1.0.0'
        }, null, 2);
        mimeType = 'application/json';
        extension = 'json';
        break;
      case 'csv':
        content = [
          ['Metric', 'Value'].join(','),
          ['Total Optimizations', String(previewData?.data.totalOptimizations || 0)].join(','),
          ['Applied', String(previewData?.data.appliedCount || 0)].join(','),
          ['Pending', String(previewData?.data.pendingCount || 0)].join(','),
          ['Success Rate', `${previewData?.data.successRate || 0}%`].join(','),
          ['', ''].join(','),
          ...optimizations.map(opt => [
            t(opt.nameKey, settings.language),
            opt.isApplied ? 'Applied' : 'Pending'
          ].join(','))
        ].join('\n');
        mimeType = 'text/csv';
        extension = 'csv';
        break;
    }

    if (typeof window !== 'undefined') {
      const blob = new Blob([content], { type: mimeType });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `CA-O-Report-${Date.now()}.${extension}`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);
    }
  }, [previewData, optimizations, settings.language]);

  // Format relative time
  const formatRelativeTime = (timestamp: number): string => {
    const diff = Date.now() - timestamp;
    const minutes = Math.floor(diff / 60000);
    if (minutes < 1) return settings.language === 'es' ? 'Ahora' : 'Just now';
    if (minutes < 60) return `${minutes}m`;
    const hours = Math.floor(minutes / 60);
    if (hours < 24) return `${hours}h`;
    return `${Math.floor(hours / 24)}d`;
  };

  // Format icons for sections
  const sectionIcons: Record<ExportSection, React.ReactNode> = {
    overview: <BarChart3 className="w-4 h-4" />,
    optimizations: <Zap className="w-4 h-4" />,
    history: <Clock className="w-4 h-4" />,
    statistics: <TrendingUp className="w-4 h-4" />,
    performance: <Activity className="w-4 h-4" />,
    achievements: <Shield className="w-4 h-4" />,
  };

  const sectionLabels: Record<ExportSection, string> = {
    overview: settings.language === 'es' ? 'Resumen General' : 'Overview',
    optimizations: settings.language === 'es' ? 'Optimizaciones' : 'Optimizations',
    history: settings.language === 'es' ? 'Historial' : 'History',
    statistics: settings.language === 'es' ? 'Estadísticas' : 'Statistics',
    performance: settings.language === 'es' ? 'Rendimiento' : 'Performance',
    achievements: settings.language === 'es' ? 'Logros' : 'Achievements',
  };

  // Toggle section selection
  const toggleSection = (section: ExportSection) => {
    setOptions(prev => ({
      ...prev,
      sections: prev.sections.includes(section)
        ? prev.sections.filter(s => s !== section)
        : [...prev.sections, section]
    }));
  };

  if (!isMounted) {
    return (
      <div className={`glass-premium rounded-2xl p-6 ${className || ''}`}>
        <div className="skeleton-modern h-64 w-full rounded-xl" />
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
              <div className="w-11 h-11 rounded-xl bg-gradient-to-br from-blue-500/20 to-cyan-500/20 flex items-center justify-center border border-blue-500/20">
                <FileDown className="w-5 h-5 text-blue-400" />
              </div>
              <div>
                <h3 className="font-bold text-white text-lg">
                  {settings.language === 'es' ? 'Informes y Exportación' : 'Reports & Export'}
                </h3>
                <p className="text-xs text-[rgba(255,255,255,0.4)]">
                  {settings.language === 'es' 
                    ? 'Genera informes detallados de tu sistema'
                    : 'Generate detailed reports of your system'}
                </p>
              </div>
            </div>

            <button
              onClick={() => setIsExpanded(!isExpanded)}
              className="p-2 rounded-lg hover:bg-[rgba(255,255,255,0.06)] transition-colors text-[rgba(255,255,255,0.5)]"
            >
              {isExpanded ? <Minimize2 className="w-4 h-4" /> : <Maximize2 className="w-4 h-4" />}
            </button>
          </div>
        </div>

        <AnimatePresence mode="wait">
          {isExpanded && (
            <motion.div
              key="expanded"
              initial={{ opacity: 0, height: 0 }}
              animate={{ opacity: 1, height: 'auto' }}
              exit={{ opacity: 0, height: 0 }}
              transition={{ duration: 0.25 }}
              className="p-5 space-y-5"
            >
              {/* Format Selection */}
              <div className="space-y-3">
                <label className="text-xs font-medium text-[rgba(255,255,255,0.5)] uppercase tracking-wider flex items-center gap-2">
                  <FileText className="w-3.5 h-3.5" />
                  {settings.language === 'es' ? 'Formato de Exportación' : 'Export Format'}
                </label>
                
                <div className="grid grid-cols-5 gap-2">
                  {([
                    { id: 'pdf', icon: FileText, label: 'PDF', color: '#EF4444' },
                    { id: 'csv', icon: FileSpreadsheet, label: 'CSV', color: '#10B981' },
                    { id: 'json', icon: FileJson, label: 'JSON', color: '#F59E0B' },
                    { id: 'png', icon: FileImage, label: 'PNG', color: '#8B5CF6' },
                    { id: 'html', icon: Globe, label: 'HTML', color: '#3B82F6' },
                  ] as const).map(({ id, icon: Icon, label, color }) => (
                    <button
                      key={id}
                      onClick={() => setOptions(prev => ({ ...prev, format: id as any }))}
                      className={`flex flex-col items-center gap-2 p-3 rounded-xl transition-all press-depth ${
                        options.format === id
                          ? 'bg-[rgba(255,255,255,0.08)] border border-white/20'
                          : 'bg-[rgba(255,255,255,0.02)] border border-transparent hover:bg-[rgba(255,255,255,0.04)]'
                      }`}
                    >
                      <Icon 
                        className={`w-5 h-5 transition-colors`}
                        style={{ color: options.format === id ? color : 'rgba(255,255,255,0.3)' }} 
                      />
                      <span className={`text-[11px] font-medium ${
                        options.format === id ? 'text-white' : 'text-[rgba(255,255,255,0.4)]'
                      }`}>
                        {label}
                      </span>
                    </button>
                  ))}
                </div>
              </div>

              {/* Date Range Selection */}
              <div className="space-y-3">
                <label className="text-xs font-medium text-[rgba(255,255,255,0.5)] uppercase tracking-wider flex items-center gap-2">
                  <Calendar className="w-3.5 h-3.5" />
                  {settings.language === 'es' ? 'Rango de Fechas' : 'Date Range'}
                </label>
                
                <div className="grid grid-cols-5 gap-2">
                  {(['day', 'week', 'month', 'all'] as const).map(range => (
                    <button
                      key={range}
                      onClick={() => setOptions(prev => ({ ...prev, dateRange: range }))}
                      className={`px-3 py-2 rounded-lg text-xs font-medium transition-all press-depth ${
                        options.dateRange === range
                          ? 'bg-[#FF6B35]/15 text-[#FF6B35] border border-[#FF6B35]/25'
                          : 'bg-[rgba(255,255,255,0.03)] text-[rgba(255,255,255,0.5)] border border-transparent hover:border-[rgba(255,255,255,0.1)]'
                      }`}
                    >
                      {range === 'day' && (settings.language === 'es' ? 'Hoy' : 'Today')}
                      {range === 'week' && (settings.language === 'es' ? 'Semana' : 'Week')}
                      {range === 'month' && (settings.language === 'es' ? 'Mes' : 'Month')}
                      {range === 'all' && (settings.language === 'es' ? 'Todo' : 'All')}
                    </button>
                  ))}
                </div>
              </div>

              {/* Sections to Include */}
              <div className="space-y-3">
                <label className="text-xs font-medium text-[rgba(255,255,255,0.5)] uppercase tracking-wider flex items-center gap-2">
                  <Filter className="w-3.5 h-3.5" />
                  {settings.language === 'es' ? 'Secciones a Incluir' : 'Sections to Include'}
                </label>
                
                <div className="grid grid-cols-3 gap-2">
                  {(Object.keys(sectionLabels) as ExportSection[]).map(section => (
                    <button
                      key={section}
                      onClick={() => toggleSection(section)}
                      className={`flex items-center gap-2 px-3 py-2.5 rounded-lg text-xs font-medium transition-all press-depth ${
                        options.sections.includes(section)
                          ? 'bg-[rgba(16,185,129,0.1)] text-green-400 border border-green-500/20'
                          : 'bg-[rgba(255,255,255,0.02)] text-[rgba(255,255,255,0.4)] border border-transparent hover:border-[rgba(255,255,255,0.1)]'
                      }`}
                    >
                      {sectionIcons[section]}
                      <span>{sectionLabels[section]}</span>
                      
                      {options.sections.includes(section) && (
                        <CheckCircle2 className="w-3.5 h-3.5 ml-auto" />
                      )}
                    </button>
                  ))}
                </div>
              </div>

              {/* Additional Options */}
              <div className="space-y-3">
                <label className="text-xs font-medium text-[rgba(255,255,255,0.5)] uppercase tracking-wider flex items-center gap-2">
                  <Settings2 className="w-3.5 h-3.5" />
                  {settings.language === 'es' ? 'Opciones Adicionales' : 'Additional Options'}
                </label>
                
                <div className="grid grid-cols-3 gap-3">
                  <label className="flex items-center gap-2 cursor-pointer p-3 rounded-lg bg-[rgba(255,255,255,0.02)] hover:bg-[rgba(255,255,255,0.04)] transition-colors">
                    <input
                      type="checkbox"
                      checked={options.includeCharts}
                      onChange={(e) => setOptions(prev => ({ ...prev, includeCharts: e.target.checked }))}
                      className="sr-only"
                    />
                    <div className={`w-4 h-4 rounded flex items-center justify-center transition-colors ${
                      options.includeCharts ? 'bg-[#FF6B35]' : 'bg-[rgba(255,255,255,0.1)]'
                    }`}>
                      {options.includeCharts && <CheckCircle2 className="w-3 h-3 text-white" strokeWidth={3} />}
                    </div>
                    <span className="text-xs text-[rgba(255,255,255,0.7)]">
                      {settings.language === 'es' ? 'Gráficos' : 'Charts'}
                    </span>
                  </label>
                  
                  <label className="flex items-center gap-2 cursor-pointer p-3 rounded-lg bg-[rgba(255,255,255,0.02)] hover:bg-[rgba(255,255,255,0.04)] transition-colors">
                    <input
                      type="checkbox"
                      checked={options.includeRawData}
                      onChange={(e) => setOptions(prev => ({ ...prev, includeRawData: e.target.checked }))}
                      className="sr-only"
                    />
                    <div className={`w-4 h-4 rounded flex items-center justify-center transition-colors ${
                      options.includeRawData ? 'bg-[#FF6B35]' : 'bg-[rgba(255,255,255,0.1)]'
                    }`}>
                      {options.includeRawData && <CheckCircle2 className="w-3 h-3 text-white" strokeWidth={3} />}
                    </div>
                    <span className="text-xs text-[rgba(255,255,255,0.7)]">
                      {settings.language === 'es' ? 'Datos Crudos' : 'Raw Data'}
                    </span>
                  </label>
                  
                  <label className="flex items-center gap-2 cursor-pointer p-3 rounded-lg bg-[rgba(255,255,255,0.02)] hover:bg-[rgba(255,255,255,0.04)] transition-colors">
                    <input
                      type="checkbox"
                      checked={options.includeMetadata}
                      onChange={(e) => setOptions(prev => ({ ...prev, includeMetadata: e.target.checked }))}
                      className="sr-only"
                    />
                    <div className={`w-4 h-4 rounded flex items-center justify-center transition-colors ${
                      options.includeMetadata ? 'bg-[#FF6B35]' : 'bg-[rgba(255,255,255,0.1)]'
                    }`}>
                      {options.includeMetadata && <CheckCircle2 className="w-3 h-3 text-white" strokeWidth={3} />}
                    </div>
                    <span className="text-xs text-[rgba(255,255,255,0.7)]">
                      {settings.language === 'es' ? 'Metadatos' : 'Metadata'}
                    </span>
                  </label>
                </div>
              </div>

              {/* Generate Button */}
              <button
                onClick={handleGenerateReport}
                disabled={isGenerating}
                className={`w-full py-4 rounded-xl font-semibold text-sm flex items-center justify-center gap-2 transition-all btn-neon ${
                  isGenerating ? 'opacity-70 cursor-wait' : ''
                }`}
              >
                {isGenerating ? (
                  <>
                    <Loader2 className="w-4 h-4 animate-spin" />
                    {settings.language === 'es' ? 'Generando Informe...' : 'Generating Report...'}
                  </>
                ) : (
                  <>
                    <FileText className="w-4 h-4" />
                    {settings.language === 'es' ? 'Generar Informe' : 'Generate Report'}
                  </>
                )}
              </button>

              {/* Preview Modal */}
              <AnimatePresence>
                {showPreview && previewData && (
                  <motion.div
                    initial={{ opacity: 0, y: 20 }}
                    animate={{ opacity: 1, y: 0 }}
                    exit={{ opacity: 0, y: 20 }}
                    className="glass-holo rounded-2xl p-5 space-y-4"
                  >
                    <div className="flex items-start justify-between">
                      <div>
                        <h4 className="font-bold text-white text-lg">{previewData.title}</h4>
                        <p className="text-sm text-[rgba(255,255,255,0.4)]">{previewData.subtitle}</p>
                      </div>
                      <button
                        onClick={() => setShowPreview(false)}
                        className="p-1.5 rounded-lg hover:bg-[rgba(255,255,255,0.1)] transition-colors text-[rgba(255,255,255,0.4)]"
                      >
                        <XCircle className="w-5 h-5" />
                      </button>
                    </div>

                    {/* Stats Grid in Preview */}
                    <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
                      {[
                        { label: settings.language === 'es' ? 'Total' : 'Total', value: previewData.data.totalOptimizations, icon: Zap, color: '#FF6B35' },
                        { label: settings.language === 'es' ? 'Aplicadas' : 'Applied', value: previewData.data.appliedCount, icon: CheckCircle2, color: '#10B981' },
                        { label: settings.language === 'es' ? 'Éxito' : 'Success Rate', value: `${previewData.data.successRate}%`, icon: TrendingUp, color: '#3B82F6' },
                        { label: settings.language === 'es' ? 'Logros' : 'Achievements', value: previewData.data.achievementsUnlocked, icon: Shield, color: '#A855F7' },
                      ].map(({ label, value, icon: Icon, color }) => (
                        <div key={label} className="glass-liquid rounded-lg p-3 text-center">
                          <Icon className="w-4 h-4 mx-auto mb-1" style={{ color }} />
                          <div className="text-lg font-bold text-white">{value}</div>
                          <div className="text-[10px] text-[rgba(255,255,255,0.4)]">{label}</div>
                        </div>
                      ))}
                    </div>

                    {/* Download Buttons */}
                    <div className="flex gap-3">
                      <button
                        onClick={() => handleDownload(options.format)}
                        className="flex-1 py-3 rounded-xl font-semibold text-sm btn-neon flex items-center justify-center gap-2"
                      >
                        <Download className="w-4 h-4" />
                        {options.format.toUpperCase()}
                      </button>
                      
                      <button
                        onClick={() => {
                          navigator.clipboard?.writeText(JSON.stringify(previewData, null, 2));
                        }}
                        className="px-4 py-3 rounded-xl font-medium text-sm bg-[rgba(255,255,255,0.06)] text-white hover:bg-[rgba(255,255,255,0.1)] transition-colors flex items-center gap-2"
                      >
                        <Share2 className="w-4 h-4" />
                      </button>
                      
                      <button
                        onClick={() => window.print()}
                        className="px-4 py-3 rounded-xl font-medium text-sm bg-[rgba(255,255,255,0.06)] text-white hover:bg-[rgba(255,255,255,0.1)] transition-colors flex items-center gap-2"
                      >
                        <Printer className="w-4 h-4" />
                      </button>
                    </div>
                  </motion.div>
                )}
              </AnimatePresence>

              {/* Export History */}
              {exportHistory.length > 0 && (
                <div className="space-y-3">
                  <div className="flex items-center justify-between">
                    <label className="text-xs font-medium text-[rgba(255,255,255,0.5)] uppercase tracking-wider flex items-center gap-2">
                      <Clock className="w-3.5 h-3.5" />
                      {settings.language === 'es' ? 'Historial de Exportaciones' : 'Export History'}
                    </label>
                    <span className="text-[10px] px-2 py-0.5 rounded-full bg-[rgba(255,255,255,0.05)] text-[rgba(255,255,255,0.3)]">
                      {exportHistory.length} {settings.language === 'es' ? 'archivos' : 'files'}
                    </span>
                  </div>
                  
                  <div className="space-y-2 max-h-32 overflow-y-auto scrollbar-custom">
                    {exportHistory.map(entry => (
                      <motion.div
                        key={entry.id}
                        initial={{ opacity: 0, x: -10 }}
                        animate={{ opacity: 1, x: 0 }}
                        className="flex items-center justify-between p-2.5 rounded-lg bg-[rgba(255,255,255,0.02)] hover:bg-[rgba(255,255,255,0.04)] transition-colors"
                      >
                        <div className="flex items-center gap-3">
                          <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-blue-500/20 to-cyan-500/20 flex items-center justify-center">
                            <FileDown className="w-4 h-4 text-blue-400" />
                          </div>
                          <div>
                            <span className="text-xs font-medium text-white">CA-O Report</span>
                            <span className="text-[10px] text-[#FF6B35] ml-1.5">.{entry.format.toLowerCase()}</span>
                          </div>
                        </div>
                        
                        <div className="flex items-center gap-3">
                          <span className="text-[10px] text-[rgba(255,255,255,0.3)]">{entry.size}</span>
                          <span className="text-[10px] text-[rgba(255,255,255,0.4)]">
                            {formatRelativeTime(entry.timestamp)}
                          </span>
                        </div>
                      </motion.div>
                    ))}
                  </div>
                </div>
              )}
            </motion.div>
          )}
        </AnimatePresence>
      </motion.div>
    </div>
  );
}

export default AdvancedExport;
