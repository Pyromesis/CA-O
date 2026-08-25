'use client';

import { useEffect, useCallback } from 'react';
import { useAppStore } from '@/store/useAppStore';

// List of input element tags where keyboard shortcuts should be disabled
const INPUT_TAGS = ['INPUT', 'TEXTAREA', 'SELECT'];
const CONTENT_EDITABLE_ATTR = 'contentEditable';

/**
 * Custom hook for keyboard shortcuts in CA-O app
 * 
 * Features:
 * - Press 1-4 to navigate between views (Dashboard, Optimization, Troubleshooting, Settings)
 * - Press Escape to go back to dashboard
 * - Press ? to show help modal
 * - Prevents triggering when typing in inputs
 */
export function useKeyboardShortcuts() {
  const { 
    currentView, 
    setCurrentView, 
    showHelpModal,
    setShowHelpModal 
  } = useAppStore();

  // Check if the active element is an input field
  const isInputFocused = useCallback((): boolean => {
    const activeElement = document.activeElement;
    
    if (!activeElement) return false;
    
    const tagName = (activeElement as HTMLElement).tagName;
    const contentEditable = (activeElement as HTMLElement).getAttribute(CONTENT_EDITABLE_ATTR);
    
    // Check if it's an input-like element
    if (INPUT_TAGS.includes(tagName)) return true;
    
    // Check if it's a contenteditable element
    if (contentEditable === 'true' || contentEditable === '') return true;
    
    // Check if it's a specific role that indicates input
    const role = (activeElement as HTMLElement).getAttribute('role');
    if (role === 'textbox' || role === 'combobox') return true;
    
    return false;
  }, []);

  const handleKeyDown = useCallback((event: KeyboardEvent) => {
    // Don't handle shortcuts when typing in inputs
    if (isInputFocused()) return;

    // Don't handle when modifier keys are pressed (except for specific shortcuts)
    const { key, metaKey, ctrlKey, altKey } = event;

    // Handle ? key for help modal (works with or without Shift)
    if (key === '?' || (key === '/' && shiftKeyPressed(event))) {
      event.preventDefault();
      setShowHelpModal(!showHelpModal);
      return;
    }

    // Don't process other shortcuts if modifier keys are pressed
    if (metaKey || ctrlKey || altKey) return;

    switch (key) {
      case '1':
        event.preventDefault();
        setCurrentView('dashboard');
        break;
      
      case '2':
        event.preventDefault();
        setCurrentView('optimization');
        break;
      
      case '3':
        event.preventDefault();
        setCurrentView('troubleshooting');
        break;
      
      case '4':
        event.preventDefault();
        setCurrentView('settings');
        break;
      
      case 'Escape':
        event.preventDefault();
        if (showHelpModal) {
          setShowHelpModal(false);
        } else if (currentView !== 'dashboard') {
          setCurrentView('dashboard');
        }
        break;
      
      default:
        break;
    }
  }, [currentView, showHelpModal, isInputFocused, setCurrentView, setShowHelpModal]);

  useEffect(() => {
    window.addEventListener('keydown', handleKeyDown);
    
    return () => {
      window.removeEventListener('keydown', handleKeyDown);
    };
  }, [handleKeyDown]);

  return {
    /** Current view for reference */
    currentView,
    /** Whether help modal is showing */
    showHelpModal
  };
}

/**
 * Helper function to detect if Shift key was pressed
 */
function shiftKeyPressed(event: KeyboardEvent): boolean {
  // The key property already includes shift state for some characters
  // But we can also check the event.shiftKey property
  return event.shiftKey;
}

export default useKeyboardShortcuts;
