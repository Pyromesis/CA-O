// CA-O Optimization Types

export enum Category {
  System = 'System',
  Network = 'Network',
  Input = 'Input',
  Tweaks = 'Tweaks',
  Powerful = 'Powerful',
  Privacy = 'Privacy'
}

export interface OptimizationItem {
  id: string;
  nameKey: string;
  descriptionKey: string;
  category: Category;
  icon: string;
  isEnabled: boolean;
  isApplied: boolean;
  requiresRestart: boolean;
  riskLevel: 'safe' | 'medium' | 'high';
  registryPath?: string;
  registryValue?: string;
  command?: string;
}

export interface OptimizationCategory {
  id: Category;
  nameKey: string;
  icon: string;
  color: string;
  descriptionKey: string;
  itemCount: number;
}

export interface TroubleshootItem {
  id: string;
  nameKey: string;
  descriptionKey: string;
  icon: string;
  action: string;
}

export interface SystemInfo {
  osVersion: string;
  totalMemory: number;
  freeMemory: number;
  cpuUsage: number;
  uptime: number;
}

export interface RestorePoint {
  id: string;
  name: string;
  createdAt: Date;
  description: string;
}

export type ThemeMode = 'light' | 'dark';
export type Language = 'es' | 'en';

export interface AppSettings {
  theme: ThemeMode;
  language: Language;
  autoApplySafeTweaks: boolean;
  confirmBeforeApply: boolean;
  showNotifications: boolean;
  soundEffectsEnabled: boolean;
}

export interface OptimizationProfile {
  id: string;
  nameKey: string;
  descriptionKey: string;
  icon: string;
  color: string;
  gradient: string;
  glowColor: string;
  optimizationIds: string[];
  category: string;
}
