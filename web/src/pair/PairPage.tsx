import { useTranslation } from 'react-i18next'
import { PairCode } from './PairCode'

/** Opened from the tray menu ("Connect device"). */
export function PairPage() {
  const { t } = useTranslation()
  return (
    <main className="pair-page">
      <div className="card">
        <h1>{t('pair.title')}</h1>
        <PairCode />
        <a href="/editor/devices">{t('pair.manageDevices')}</a>
      </div>
    </main>
  )
}
