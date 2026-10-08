import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'
import en from './locales/en.json'
import es from './locales/es.json'

const stored = localStorage.getItem('ovsd.lang')
const browser = navigator.language.startsWith('es') ? 'es' : 'en'

void i18n.use(initReactI18next).init({
  resources: { es: { translation: es }, en: { translation: en } },
  lng: stored ?? browser,
  fallbackLng: 'en',
  interpolation: { escapeValue: false },
})

// Keep <html lang> in sync so browsers don't offer to translate the page.
document.documentElement.lang = i18n.language
i18n.on('languageChanged', (lng) => (document.documentElement.lang = lng))

export default i18n
