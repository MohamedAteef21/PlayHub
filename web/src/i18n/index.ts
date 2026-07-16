import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import { ar, en } from './locales';

i18n.use(initReactI18next).init({
  resources: { en: { translation: en }, ar: { translation: ar } },
  lng: (() => {
    try {
      const stored = localStorage.getItem('playhub-ui');
      if (stored) return JSON.parse(stored).state?.language ?? 'en';
    } catch { /* ignore */ }
    return 'en';
  })(),
  fallbackLng: 'en',
  interpolation: { escapeValue: false },
});

export default i18n;
