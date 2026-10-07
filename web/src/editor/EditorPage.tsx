import { useTranslation } from 'react-i18next'
import { StatusBadge } from '../StatusBadge'

export function EditorPage() {
  const { t } = useTranslation()
  return (
    <main className="page">
      <h1>{t('editor.title')}</h1>
      <StatusBadge />
      <p className="muted">{t('editor.comingSoon')}</p>
    </main>
  )
}
