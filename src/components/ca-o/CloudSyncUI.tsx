'use client';

import { useState, useEffect, useCallback, useSyncExternalStore } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { useAppStore } from '@/store/useAppStore';
import { t } from '@/lib/i18n';
import {
  Cloud,
  CloudOff,
  Upload,
  Download,
  RefreshCw,
  CheckCircle2,
  XCircle,
  AlertTriangle,
  Clock,
  Database,
  Shield,
  Wifi,
  HardDrive,
  Smartphone,
  Monitor,
  RefreshCw as Sync,
  Loader2,
  ChevronRight,
  Zap,
  History,
  Trash2,
  Settings,
} from 'lucide-react';

// Types
interface SyncStatus {
  isOnline: boolean;
  lastSync: number | null;
  syncInProgress: boolean;
  pendingChanges: number;
  storageUsed: number;
  storageLimit: number;
}

interface SyncHistoryEntry {
  id: string;
  type: 'upload' | 'download' | 'conflict_resolved';
  timestamp: number;
  itemsCount: number;
  success: boolean;
  errorMessage?: string;
}

interface ConnectedDevice {
  id: string;
  name: string;
  type: 'desktop' | 'mobile' | 'tablet';
  lastActive: number;
  isCurrentDevice: boolean;
}

interface CloudSyncUIProps {
  className?: string;
}

// SSR-safe mount detection
const emptySubscribe = () => () => {};
const getSnapshot = () => true;
const getServerSnapshot = () => false;

export function CloudSyncUI({ className }: CloudSyncUIProps) {
  const { settings, optimizations, history } = useAppStore();
  
  const [syncEnabled, setSyncEnabled] = useState(() => {
    if (typeof window !== 'undefined') {
      return localStorage.getItem('ca-o-sync-enabled') === 'true';
    }
    return false;
  });
  
  const [autoSync, setAutoSync] = useState(() => {
    if (typeof window !== 'undefined') {
      return localStorage.getItem('ca-o-auto-sync') === 'true';
    }
    return false;
  });

  const [syncInterval, setSyncInterval] = useState<'15min' | '30min' | '1hour' | 'manual'>(() => {
    if (typeof window !== 'undefined') {
      return (localStorage.getItem('ca-o-sync-interval') as any) || '30min';
    }
    return '30min';
  });

  const isMounted = useSyncExternalStore(emptySubscribe, getSnapshot, getServerSnapshot);
  
  const [syncStatus, setSyncStatus] = useState<SyncStatus>({
    isOnline: navigator.onLine,
    lastSync: null,
    syncInProgress: false,
    pendingChanges: 0,
    storageUsed: 0,
    storageLimit: 50,
  });

  const [syncHistory, setSyncHistory] = useState<SyncHistoryEntry[]>(() => {
    if (typeof window !== 'undefined') {
      const saved = localStorage.getItem('ca-o-sync-history');
      return saved ? JSON.parse(saved) : [];
    }
    return [];
  });

  const [connectedDevices, setConnectedDevices] = useState<ConnectedDevice[]>(() => {
    if (typeof window !== 'undefined') {
      const saved = localStorage.getItem('ca-o-devices');
      if (saved) return JSON.parse(saved);
      
      // Generate default current device
      const deviceId = `device-${crypto.randomUUID()}`;
      localStorage.setItem('ca-o-device-id', deviceId);
      
      return [{
        id: deviceId,
        name: `${typeof navigator !== 'undefined' ? navigator.platform : 'Device'} - CA-O`,
        type: 'desktop' as const,
        lastActive: Date.now(),
        isCurrentDevice: true,
      }];
    }
    return [];
  });

  const [showConfirmDialog, setShowConfirmDialog] = useState(false);
  const [confirmAction, setConfirmAction] = useState<'clear-data' | 'disconnect-all' | null>(null);

  useEffect(() => {
    if (!isMounted) return;

    const handleOnline = () => setSyncStatus(prev => ({ ...prev, isOnline: true }));
    const handleOffline = () => setSyncStatus(prev => ({ ...prev, isOnline: false }));

    window.addEventListener('online', handleOnline);
    window.addEventListener('offline', handleOffline);

    return () => {
      window.removeEventListener('online', handleOnline);
      window.removeEventListener('offline', handleOffline);
    };
  }, [isMounted]);

  // Persist settings
  useEffect(() => {
    if (!isMounted) return;
    localStorage.setItem('ca-o-sync-enabled', String(syncEnabled));
  }, [syncEnabled, isMounted]);

  useEffect(() => {
    if (!isMounted) return;
    localStorage.setItem('ca-o-auto-sync', String(autoSync));
  }, [autoSync, isMounted]);

  useEffect(() => {
    if (!isMounted) return;
    localStorage.setItem('ca-o-sync-interval', syncInterval);
  }, [syncInterval, isMounted]);

  // Manual sync action
  const handleManualSync = useCallback(async () => {
    if (syncStatus.syncInProgress || !syncStatus.isOnline) return;

    const newHistoryEntry: SyncHistoryEntry = {
      id: `sync-${Date.now()}`,
      type: 'upload',
      timestamp: Date.now(),
      itemsCount: 0,
      success: false,
      errorMessage: settings.language === 'es'
        ? 'La sincronización en la nube no está configurada.'
        : 'Cloud synchronization is not configured.',
    };

    setSyncHistory(prev => [newHistoryEntry, ...prev].slice(0, 20));
    
    if (typeof window !== 'undefined') {
      localStorage.setItem('ca-o-sync-history', JSON.stringify([newHistoryEntry, ...syncHistory].slice(0, 20)));
    }

    setSyncStatus(prev => ({ ...prev, syncInProgress: false }));
  }, [syncStatus.syncInProgress, syncStatus.isOnline, settings.language, syncHistory]);

  // Format relative time
  const formatRelativeTime = (timestamp: number): string => {
    const diff = Date.now() - timestamp;
    const seconds = Math.floor(diff / 1000);

    if (seconds < 60) return settings.language === 'es' ? 'Ahora' : 'Just now';
    
    const minutes = Math.floor(seconds / 60);
    if (minutes < 60) {
      return settings.language === 'es' 
        ? `Hace ${minutes} min`
        : `${minutes}m ago`;
    }

    const hours = Math.floor(minutes / 60);
    if (hours < 24) {
      return settings.language === 'es'
        ? `Hace ${hours}h`
        : `${hours}h ago`;
    }

    const days = Math.floor(hours / 24);
    return settings.language === 'es'
      ? `Hace ${days}d`
      : `${days}d ago`;
  };

  // Format bytes to readable size
  const formatStorageSize = (gb: number): string => {
    if (gb >= 1) return `${gb.toFixed(1)} GB`;
    return `${Math.round(gb * 1024)} MB`;
  };

  // Get device icon
  const getDeviceIcon = (type: ConnectedDevice['type']) => {
    switch (type) {
      case 'desktop': return <Monitor className="w-4 h-4" />;
      case 'mobile': return <Smartphone className="w-4 h-4" />;
      case 'tablet': return <Monitor className="w-4 h-4" />;
    }
  };

  // Confirm dialog handler
  const handleConfirmAction = useCallback(() => {
    if (confirmAction === 'clear-data') {
      setSyncHistory([]);
      if (typeof window !== 'undefined') {
        localStorage.removeItem('ca-o-sync-history');
      }
    } else if (confirmAction === 'disconnect-all') {
      const currentDevice = connectedDevices.find(d => d.isCurrentDevice);
      setConnectedDevices(currentDevice ? [currentDevice] : []);
    }
    setShowConfirmDialog(false);
    setConfirmAction(null);
  }, [confirmAction, connectedDevices]);

  if (!isMounted) {
    return (
      <div className={`glass-premium rounded-2xl p-6 ${className || ''}`}>
        <div className="skeleton-modern h-80 w-full rounded-xl" />
      </div>
    );
  }

  return (
    <div className={`space-y-4 ${className || ''}`}>
      {/* Main Card */}
      <motion.div
        initial={{ opacity: 0, y: -10 }}
        animate={{ opacity: 1, y: 0 }}
        className="glass-ultra rounded-2xl overflow-hidden"
      >
        {/* Header */}
        <div className="p-5 border-b border-[rgba(255,255,255,0.06)]">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-3">
              <div className={`w-11 h-11 rounded-xl flex items-center justify-center ${
                syncEnabled 
                  ? 'bg-gradient-to-br from-[#FF6B35] to-[#ff8c42]' 
                  : 'bg-[rgba(255,255,255,0.06)]'
              }`}>
                {syncEnabled ? (
                  <Cloud className="w-5 h-5 text-white" />
                ) : (
                  <CloudOff className="w-5 h-5 text-[rgba(255,255,255,0.3)]" />
                )}
              </div>
              <div>
                <h3 className="font-bold text-white text-lg">
                  {settings.language === 'es' ? 'Sincronización en la Nube' : 'Cloud Sync'}
                </h3>
                <p className="text-xs text-[rgba(255,255,255,0.4)] flex items-center gap-1.5">
                  <Wifi className={`w-3 h-3 ${syncStatus.isOnline ? 'text-green-400' : 'text-red-400'}`} />
                  {syncStatus.isOnline 
                    ? (settings.language === 'es' ? 'Conectado' : 'Connected')
                    : (settings.language === 'es' ? 'Sin conexión' : 'Offline')
                  }
                </p>
              </div>
            </div>

            {/* Enable/Disable Toggle */}
            <button
              onClick={() => setSyncEnabled(!syncEnabled)}
              className={`relative w-14 h-7 rounded-full transition-all duration-300 ${
                syncEnabled 
                  ? 'bg-gradient-to-r from-[#FF6B35] to-[#ff8c42]' 
                  : 'bg-[rgba(255,255,255,0.1)]'
              }`}
            >
              <motion.div
                className="absolute top-1 w-5 h-5 bg-white rounded-full shadow-md"
                animate={{ left: syncEnabled ? '32px' : '4px' }}
                transition={{ type: 'spring', stiffness: 500, damping: 30 }}
              />
            </button>
          </div>
        </div>

        <AnimatePresence mode="wait">
          {syncEnabled ? (
            <motion.div
              key="enabled"
              initial={{ opacity: 0, height: 0 }}
              animate={{ opacity: 1, height: 'auto' }}
              exit={{ opacity: 0, height: 0 }}
              transition={{ duration: 0.3 }}
              className="p-5 space-y-5"
            >
              {/* Status Cards Grid */}
              <div className="grid grid-cols-2 gap-3">
                {/* Storage Usage */}
                <div className="glass-liquid rounded-xl p-4 hover-lift-glow cursor-default">
                  <div className="flex items-center gap-2 mb-2">
                    <HardDrive className="w-4 h-4 text-[#FF6B35]" />
                    <span className="text-xs font-medium text-[rgba(255,255,255,0.5)] uppercase tracking-wider">
                      {settings.language === 'es' ? 'Almacenamiento' : 'Storage'}
                    </span>
                  </div>
                  <div className="text-lg font-bold text-white">
                    {formatStorageSize(syncStatus.storageUsed)}
                  </div>
                  <div className="text-xs text-[rgba(255,255,255,0.3)]">
                    / {formatStorageSize(syncStatus.storageLimit)}
                  </div>
                  {/* Progress Bar */}
                  <div className="mt-2 h-1.5 bg-[rgba(255,255,255,0.06)] rounded-full overflow-hidden">
                    <motion.div
                      className="h-full bg-gradient-to-r from-[#FF6B35] to-[#ff8c42] rounded-full"
                      initial={false}
                      animate={{ width: `${(syncStatus.storageUsed / syncStatus.storageLimit) * 100}%` }}
                      transition={{ duration: 0.8, ease: 'easeOut' }}
                    />
                  </div>
                </div>

                {/* Last Sync */}
                <div className="glass-liquid rounded-xl p-4 hover-lift-glow cursor-default">
                  <div className="flex items-center gap-2 mb-2">
                    <Clock className="w-4 h-4 text-green-400" />
                    <span className="text-xs font-medium text-[rgba(255,255,255,0.5)] uppercase tracking-wider">
                      {settings.language === 'es' ? 'Última Sinc.' : 'Last Sync'}
                    </span>
                  </div>
                  <div className="text-lg font-bold text-white">
                    {syncStatus.lastSync 
                      ? formatRelativeTime(syncStatus.lastSync)
                      : settings.language === 'es' ? 'Nunca' : 'Never'
                    }
                  </div>
                  <div className="flex items-center gap-1 mt-1">
                    <div className={`status-pulse-dot ${syncStatus.syncInProgress ? 'bg-yellow-400' : syncStatus.lastSync ? 'bg-green-400' : 'bg-[rgba(255,255,255,0.2)]'}`} />
                    <span className="text-[10px] text-[rgba(255,255,255,0.3)]">
                      {syncStatus.syncInProgress
                        ? (settings.language === 'es' ? 'Sincronizando...' : 'Syncing...')
                        : ''
                      }
                    </span>
                  </div>
                </div>
              </div>

              {/* Auto Sync Settings */}
              <div className="glass-liquid rounded-xl p-4 space-y-4">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <Sync className="w-4 h-4 text-purple-400" />
                    <span className="text-sm font-medium text-white">
                      {settings.language === 'es' ? 'Sincronización Automática' : 'Auto Sync'}
                </span>
              </div>
                  
                  <button
                    onClick={() => setAutoSync(!autoSync)}
                    className={`toggle-futuristic ${autoSync ? 'active' : ''}`}
                  />
                </div>

                {autoSync && (
                  <motion.div
                    initial={{ opacity: 0, height: 0 }}
                    animate={{ opacity: 1, height: 'auto' }}
                    exit={{ opacity: 0, height: 0 }}
                    className="space-y-3"
                  >
                    <p className="text-xs text-[rgba(255,255,255,0.4)]">
                      {settings.language === 'es' ? 'Intervalo de sincronización:' : 'Sync interval:'}
                    </p>
                    
                    <div className="grid grid-cols-4 gap-2">
                      {(['15min', '30min', '1hour', 'manual'] as const).map((interval) => (
                        <button
                          key={interval}
                          onClick={() => setSyncInterval(interval)}
                          className={`px-3 py-2 rounded-lg text-xs font-medium transition-all press-depth ${
                            syncInterval === interval
                              ? 'bg-[#FF6B35]/20 text-[#FF6B35] border border-[#FF6B35]/30'
                              : 'bg-[rgba(255,255,255,0.04)] text-[rgba(255,255,255,0.5)] border border-transparent hover:bg-[rgba(255,255,255,0.08)]'
                          }`}
                        >
                          {interval === '15min' && '15 min'}
                          {interval === '30min' && '30 min'}
                          {interval === '1hour' && '1 hour'}
                          {interval === 'manual' && (settings.language === 'es' ? 'Manual' : 'Manual')}
                        </button>
                      ))}
                    </div>
                  </motion.div>
                )}
              </div>

              {/* Sync Button */}
              <button
                onClick={handleManualSync}
                disabled={syncStatus.syncInProgress || !syncStatus.isOnline}
                className={`w-full py-3.5 rounded-xl font-semibold text-sm flex items-center justify-center gap-2 transition-all btn-neon ${
                  !syncStatus.isOnline ? 'opacity-50 cursor-not-allowed' : ''
                }`}
              >
                {syncStatus.syncInProgress ? (
                  <>
                    <Loader2 className="w-4 h-4 animate-spin" />
                    {settings.language === 'es' ? 'Sincronizando...' : 'Syncing...'}
                  </>
                ) : (
                  <>
                    <RefreshCw className="w-4 h-4" />
                    {settings.language === 'es' ? 'Sincronizar Ahora' : 'Sync Now'}
                  </>
                )}
              </button>

              {/* Connected Devices Section */}
              <div className="glass-liquid rounded-xl p-4 space-y-3">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <Smartphone className="w-4 h-4 text-blue-400" />
                    <span className="text-sm font-medium text-white">
                      {settings.language === 'es' ? 'Dispositivos Conectados' : 'Connected Devices'}
                    </span>
                  </div>
                  <span className="text-xs px-2 py-0.5 rounded-full bg-[rgba(59,130,246,0.15)] text-blue-400 font-medium">
                    {connectedDevices.length}
                  </span>
                </div>

                <div className="space-y-2">
                  {connectedDevices.map(device => (
                    <motion.div
                      key={device.id}
                      layout
                      initial={{ opacity: 0, x: -10 }}
                      animate={{ opacity: 1, x: 0 }}
                      className="flex items-center justify-between p-2.5 rounded-lg bg-[rgba(255,255,255,0.02)] hover:bg-[rgba(255,255,255,0.04)] transition-colors"
                    >
                      <div className="flex items-center gap-3">
                        <div className="w-8 h-8 rounded-lg bg-[rgba(255,255,255,0.05)] flex items-center justify-center text-[rgba(255,255,255,0.5)]">
                          {getDeviceIcon(device.type)}
                        </div>
                        <div>
                          <div className="flex items-center gap-2">
                            <span className="text-sm text-white font-medium truncate max-w-[140px]">
                              {device.name}
                            </span>
                            {device.isCurrentDevice && (
                              <span className="text-[10px] px-1.5 py-0.5 rounded bg-[#FF6B35]/20 text-[#FF6B35] font-medium">
                                {settings.language === 'es' ? 'Este dispositivo' : 'This device'}
                              </span>
                            )}
                          </div>
                          <span className="text-[10px] text-[rgba(255,255,255,0.3)]">
                            {formatRelativeTime(device.lastActive)}
                          </span>
                        </div>
                      </div>
                      
                      <ChevronRight className="w-4 h-4 opacity-30" />
                    </motion.div>
                  ))}
                </div>
              </div>

              {/* Sync History */}
              {syncHistory.length > 0 && (
                <div className="glass-liquid rounded-xl p-4 space-y-3">
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-2">
                      <History className="w-4 h-4 text-yellow-400" />
                      <span className="text-sm font-medium text-white">
                        {settings.language === 'es' ? 'Historial de Sincronización' : 'Sync History'}
                      </span>
                    </div>
                    <button
                      onClick={() => { setConfirmAction('clear-data'); setShowConfirmDialog(true); }}
                      className="text-[10px] text-red-400/70 hover:text-red-400 transition-colors flex items-center gap-1"
                    >
                      <Trash2 className="w-3 h-3" />
                      Clear
                    </button>
                  </div>

                  <div className="space-y-2 max-h-40 overflow-y-auto scrollbar-hide">
                    {syncHistory.slice(0, 5).map(entry => (
                      <div
                        key={entry.id}
                        className="flex items-center justify-between p-2 rounded-lg bg-[rgba(255,255,255,0.02)]"
                      >
                        <div className="flex items-center gap-2">
                          {entry.success ? (
                            <CheckCircle2 className="w-4 h-4 text-green-400 shrink-0" />
                          ) : (
                            <XCircle className="w-4 h-4 text-red-400 shrink-0" />
                          )}
                          <span className="text-xs text-white">
                            {entry.type === 'upload' 
                              ? (settings.language === 'es' ? 'Subida' : 'Upload')
                              : (settings.language === 'es' ? 'Descarga' : 'Download')
                            }
                          </span>
                          <span className="text-[10px] text-[rgba(255,255,255,0.3)]">
                            ({entry.itemsCount} {settings.language === 'es' ? 'elementos' : 'items'})
                          </span>
                        </div>
                        <span className="text-[10px] text-[rgba(255,255,255,0.3)]">
                          {formatRelativeTime(entry.timestamp)}
                        </span>
                      </div>
                    ))}
                  </div>
                </div>
              )}

              {/* Security Note */}
              <div className="flex items-start gap-3 p-3 rounded-xl bg-gradient-to-r from-[rgba(16,185,129,0.08)] to-transparent border border-[rgba(16,185,129,0.15)]">
                <Shield className="w-4 h-4 text-green-400 mt-0.5 shrink-0" />
                <div>
                  <p className="text-xs text-white font-medium">
                    {settings.language === 'es' ? 'Datos Seguros' : 'Secure Data'}
                  </p>
                  <p className="text-[10px] text-[rgba(255,255,255,0.4)] mt-0.5 leading-relaxed">
                    {settings.language === 'es'
                      ? 'Tus datos están cifrados con AES-256 durante la transferencia y almacenamiento.'
                      : 'Your data is encrypted with AES-256 during transfer and storage.'
                    }
                  </p>
                </div>
              </div>
            </motion.div>
          ) : (
            <motion.div
              key="disabled"
              initial={{ opacity: 0, height: 0 }}
              animate={{ opacity: 1, height: 'auto' }}
              exit={{ opacity: 0, height: 0 }}
              transition={{ duration: 0.3 }}
              className="p-8 text-center"
            >
              <div className="w-16 h-16 mx-auto mb-4 rounded-2xl bg-[rgba(255,255,255,0.03)] flex items-center justify-center">
                <CloudOff className="w-7 h-7 text-[rgba(255,255,255,0.2)]" />
              </div>
              <h4 className="text-white font-semibold text-lg mb-2">
                {settings.language === 'es' ? 'Sincronización Desactivada' : 'Cloud Sync Disabled'}
              </h4>
              <p className="text-sm text-[rgba(255,255,255,0.4)] max-w-xs mx-auto mb-4">
                {settings.language === 'es'
                  ? 'Activa la sincronización en la nube para respaldar tu configuración y sincronizarla entre dispositivos.'
                  : 'Enable cloud sync to backup your configuration and sync across devices.'
                }
              </p>
              
              <div className="grid grid-cols-3 gap-3 max-w-md mx-auto text-center">
                <div className="p-3 rounded-xl bg-[rgba(255,255,255,0.02)]">
                  <Database className="w-5 h-5 text-[#FF6B35] mx-auto mb-1" />
                  <p className="text-[10px] text-[rgba(255,255,255,0.4)]">
                    {settings.language === 'es' ? '50 GB' : '50 GB Free'}
                  </p>
                </div>
                <div className="p-3 rounded-xl bg-[rgba(255,255,255,0.02)]">
                  <Zap className="w-5 h-5 text-yellow-400 mx-auto mb-1" />
                  <p className="text-[10px] text-[rgba(255,255,255,0.4)]">
                    {settings.language === 'es' ? 'Instantáneo' : 'Instant'}
                  </p>
                </div>
                <div className="p-3 rounded-xl bg-[rgba(255,255,255,0.02)]">
                  <Shield className="w-5 h-5 text-green-400 mx-auto mb-1" />
                  <p className="text-[10px] text-[rgba(255,255,255,0.4)]">
                    {settings.language === 'es' ? 'AES-256' : 'Encrypted'}
                  </p>
                </div>
              </div>
            </motion.div>
          )}
        </AnimatePresence>
      </motion.div>

      {/* Confirmation Dialog */}
      <AnimatePresence>
        {showConfirmDialog && (
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="fixed inset-0 z-[200] bg-black/60 backdrop-blur-sm flex items-center justify-center p-4"
            onClick={() => setShowConfirmDialog(false)}
          >
            <motion.div
              initial={{ scale: 0.95, opacity: 0 }}
              animate={{ scale: 1, opacity: 1 }}
              exit={{ scale: 0.95, opacity: 0 }}
              onClick={(e) => e.stopPropagation()}
              className="glass-ultra rounded-2xl p-6 w-full max-w-sm"
            >
              <AlertTriangle className="w-12 h-12 text-yellow-400 mx-auto mb-4" />
              
              <h3 className="text-lg font-bold text-white text-center mb-2">
                {confirmAction === 'clear-data'
                  ? (settings.language === 'es' ? 'Limpiar Historial' : 'Clear History')
                  : (settings.language === 'es' ? 'Desconectar Dispositivos' : 'Disconnect Devices')
                }
              </h3>
              
              <p className="text-sm text-[rgba(255,255,255,0.5)] text-center mb-6">
                {confirmAction === 'clear-data'
                  ? (settings.language === 'es' 
                    ? '¿Estás seguro de que deseas eliminar todo el historial de sincronización? Esta acción no se puede deshacer.'
                    : 'Are you sure you want to clear all sync history? This action cannot be undone.')
                  : (settings.language === 'es'
                    ? '¿Estás seguro de que deseas desconectar todos los dispositivos excepto el actual?'
                    : 'Are you sure you want to disconnect all devices except this one?')
                }
              </p>
              
              <div className="flex gap-3">
                <button
                  onClick={() => setShowConfirmDialog(false)}
                  className="flex-1 py-2.5 rounded-xl font-medium text-sm bg-[rgba(255,255,255,0.06)] text-white hover:bg-[rgba(255,255,255,0.1)] transition-colors press-depth"
                >
                  {settings.language === 'es' ? 'Cancelar' : 'Cancel'}
                </button>
                <button
                  onClick={handleConfirmAction}
                  className="flex-1 py-2.5 rounded-xl font-medium text-sm bg-red-500/20 text-red-400 hover:bg-red-500/30 transition-colors press-depth"
                >
                  {settings.language === 'es' ? 'Confirmar' : 'Confirm'}
                </button>
              </div>
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}

export default CloudSyncUI;
