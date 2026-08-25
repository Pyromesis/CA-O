import { es } from './es';
import { en } from './en';
import { Language } from '@/types/optimization';

const translations = { es, en };

function toHumanReadableLabel(key: string): string {
  const normalized = key.replace(/Desc$/, '').replace(/^tweak/, '');
  return normalized
    .replace(/-/g, ' ')
    .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
    .replace(/\s+/g, ' ')
    .trim()
    .replace(/^./, (char) => char.toUpperCase());
}

export function t(key: string, lang: Language): string {
  const translation = translations[lang];
  if (key in translation) {
    return (translation as Record<string, string>)[key];
  }

  const fallback = toHumanReadableLabel(key);
  console.warn(`Translation missing for key: ${key} in language: ${lang}. Falling back to '${fallback}'.`);
  return fallback;
}

export { es, en };
export type { Language };
