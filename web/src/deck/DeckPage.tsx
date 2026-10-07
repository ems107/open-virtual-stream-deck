import { useTranslation } from 'react-i18next'
import { StatusBadge } from '../StatusBadge'

export function DeckPage() {
  const { t } = useTranslation()
  return (
    <main className="page deck-page">
      <StatusBadge />
      <p className="muted">{t('deck.empty')}</p>
      <button onClick={() => void document.documentElement.requestFullscreen?.()}>{t('deck.fullscreen')}</button>
    </main>
  )
}
